using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IComplianceAuditExportService"/>.
    /// </summary>
    /// <remarks>
    /// Assembles scenario-specific compliance audit export packages by composing
    /// KYC/AML decisions, onboarding case records, compliance blockers, sign-off
    /// evidence, and approval-workflow history into durable, provenance-backed packages.
    ///
    /// Readiness semantics are fail-closed (priority order):
    /// <list type="number">
    ///   <item><description>Provider unreachable → DegradedProviderUnavailable</description></item>
    ///   <item><description>Unresolved critical blocker → Blocked</description></item>
    ///   <item><description>Missing required evidence → Incomplete</description></item>
    ///   <item><description>Required evidence stale → Stale</description></item>
    ///   <item><description>Approval review pending → RequiresReview</description></item>
    ///   <item><description>All checks pass → Ready</description></item>
    /// </list>
    ///
    /// In a production implementation the deterministic in-memory seed behaviour
    /// would be replaced by calls to KycAmlDecisionIngestionService,
    /// ComplianceCaseManagementService, ProtectedSignOffEvidencePersistenceService,
    /// and ApprovalWorkflowService.
    /// </remarks>
    public class ComplianceAuditExportService : IComplianceAuditExportService
    {
        private readonly ILogger<ComplianceAuditExportService> _logger;
        private readonly TimeProvider _timeProvider;

        // In-memory stores: exportId → package
        private readonly Dictionary<string, ComplianceAuditExportPackage> _packages = new();

        // Idempotency index: idempotencyKey → exportId
        private readonly Dictionary<string, string> _idempotencyIndex = new();

        // Per-subject, per-scenario index: "{subjectId}|{scenario}" → ordered exportIds (newest first)
        private readonly Dictionary<string, List<string>> _subjectScenarioIndex = new();

        private readonly object _lock = new();

        private const string SchemaVersion = "1.0.0";
        private const string PolicyVersion = "2026.03.24.1";

        // Freshness windows
        private static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(90);
        private static readonly TimeSpan NearExpiryWindow = TimeSpan.FromDays(7);

        /// <summary>Initializes a new instance of <see cref="ComplianceAuditExportService"/>.</summary>
        public ComplianceAuditExportService(
            ILogger<ComplianceAuditExportService> logger,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── AssembleReleaseReadinessExportAsync ───────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceAuditExportResponse> AssembleReleaseReadinessExportAsync(
            ReleaseReadinessExportRequest request)
        {
            request.CorrelationId ??= Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to assemble a release-readiness export.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                if (TryGetIdempotentReplay(request.IdempotencyKey, request.ForceRegenerate,
                    out var cached))
                {
                    _logger.LogInformation(
                        "Idempotent replay for release-readiness key={Key} ExportId={Id}",
                        LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                        cached!.ExportId);
                    return Task.FromResult(new ComplianceAuditExportResponse
                    {
                        Success = true,
                        Package = cached,
                        IsIdempotentReplay = true
                    });
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var exportId = Guid.NewGuid().ToString();
                var history = GetTrackerHistory(request.SubjectId, AuditScenario.ReleaseReadinessSignOff);

                var pkg = BuildReleaseReadinessPackage(exportId, request, history, now);
                StorePackage(pkg, request.IdempotencyKey);

                _logger.LogInformation(
                    "Assembled release-readiness export {ExportId} for subject {SubjectId} → {Readiness}",
                    exportId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    pkg.Readiness);

                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = true,
                    Package = pkg,
                    IsIdempotentReplay = false
                });
            }
        }

        // ── AssembleOnboardingCaseReviewExportAsync ───────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceAuditExportResponse> AssembleOnboardingCaseReviewExportAsync(
            OnboardingCaseReviewExportRequest request)
        {
            request.CorrelationId ??= Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to assemble an onboarding case review export.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                if (TryGetIdempotentReplay(request.IdempotencyKey, request.ForceRegenerate,
                    out var cached))
                {
                    return Task.FromResult(new ComplianceAuditExportResponse
                    {
                        Success = true,
                        Package = cached,
                        IsIdempotentReplay = true
                    });
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var exportId = Guid.NewGuid().ToString();
                var history = GetTrackerHistory(request.SubjectId, AuditScenario.OnboardingCaseReview);

                var pkg = BuildOnboardingCasePackage(exportId, request, history, now);
                StorePackage(pkg, request.IdempotencyKey);

                _logger.LogInformation(
                    "Assembled onboarding-case-review export {ExportId} for subject {SubjectId} → {Readiness}",
                    exportId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    pkg.Readiness);

                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = true,
                    Package = pkg,
                    IsIdempotentReplay = false
                });
            }
        }

        // ── AssembleBlockerReviewExportAsync ──────────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceAuditExportResponse> AssembleBlockerReviewExportAsync(
            ComplianceBlockerReviewExportRequest request)
        {
            request.CorrelationId ??= Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to assemble a compliance blocker review export.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                if (TryGetIdempotentReplay(request.IdempotencyKey, request.ForceRegenerate,
                    out var cached))
                {
                    return Task.FromResult(new ComplianceAuditExportResponse
                    {
                        Success = true,
                        Package = cached,
                        IsIdempotentReplay = true
                    });
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var exportId = Guid.NewGuid().ToString();
                var history = GetTrackerHistory(request.SubjectId, AuditScenario.ComplianceBlockerReview);

                var pkg = BuildBlockerReviewPackage(exportId, request, history, now);
                StorePackage(pkg, request.IdempotencyKey);

                _logger.LogInformation(
                    "Assembled blocker-review export {ExportId} for subject {SubjectId} → {Readiness}",
                    exportId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    pkg.Readiness);

                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = true,
                    Package = pkg,
                    IsIdempotentReplay = false
                });
            }
        }

        // ── AssembleApprovalHistoryExportAsync ────────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceAuditExportResponse> AssembleApprovalHistoryExportAsync(
            ApprovalHistoryExportRequest request)
        {
            request.CorrelationId ??= Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to assemble an approval-history export.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                if (TryGetIdempotentReplay(request.IdempotencyKey, request.ForceRegenerate,
                    out var cached))
                {
                    return Task.FromResult(new ComplianceAuditExportResponse
                    {
                        Success = true,
                        Package = cached,
                        IsIdempotentReplay = true
                    });
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var exportId = Guid.NewGuid().ToString();
                var history = GetTrackerHistory(request.SubjectId, AuditScenario.ApprovalHistoryExport);
                var decisionLimit = Math.Clamp(request.DecisionLimit, 1, 200);

                var pkg = BuildApprovalHistoryPackage(exportId, request, history, decisionLimit, now);
                StorePackage(pkg, request.IdempotencyKey);

                _logger.LogInformation(
                    "Assembled approval-history export {ExportId} for subject {SubjectId} → {Readiness}",
                    exportId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    pkg.Readiness);

                return Task.FromResult(new ComplianceAuditExportResponse
                {
                    Success = true,
                    Package = pkg,
                    IsIdempotentReplay = false
                });
            }
        }

        // ── GetExportAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceAuditExportResponse> GetExportAsync(
            string exportId, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(exportId))
            {
                return Task.FromResult(new GetComplianceAuditExportResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_EXPORT_ID",
                    ErrorMessage = "ExportId is required."
                });
            }

            lock (_lock)
            {
                if (!_packages.TryGetValue(exportId, out var pkg))
                {
                    return Task.FromResult(new GetComplianceAuditExportResponse
                    {
                        Success = false,
                        ErrorCode = "NOT_FOUND",
                        ErrorMessage = $"Compliance audit export '{LoggingHelper.SanitizeLogInput(exportId)}' was not found."
                    });
                }

                var copy = ShallowCopyPackage(pkg);
                copy.CorrelationId = correlationId ?? copy.CorrelationId;

                return Task.FromResult(new GetComplianceAuditExportResponse
                {
                    Success = true,
                    Package = copy
                });
            }
        }

        // ── ListExportsAsync ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListComplianceAuditExportsResponse> ListExportsAsync(
            string subjectId,
            AuditScenario? scenario = null,
            int limit = 20,
            string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return Task.FromResult(new ListComplianceAuditExportsResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required."
                });
            }

            limit = Math.Clamp(limit, 1, 100);

            lock (_lock)
            {
                IEnumerable<string> ids;

                if (scenario.HasValue)
                {
                    var key = SubjectScenarioKey(subjectId, scenario.Value);
                    ids = _subjectScenarioIndex.TryGetValue(key, out var scenarioIds)
                        ? (IEnumerable<string>)scenarioIds
                        : Array.Empty<string>();
                }
                else
                {
                    // Merge all scenarios for this subject
                    ids = Enum.GetValues<AuditScenario>()
                        .SelectMany(s =>
                        {
                            var key = SubjectScenarioKey(subjectId, s);
                            return _subjectScenarioIndex.TryGetValue(key, out var l) ? l : Enumerable.Empty<string>();
                        })
                        .Distinct()
                        .OrderByDescending(id => _packages.TryGetValue(id, out var p) ? p.AssembledAt : DateTime.MinValue);
                }

                var idList = ids.ToList();
                var total = idList.Count;

                var summaries = idList
                    .Take(limit)
                    .Where(id => _packages.ContainsKey(id))
                    .Select(id => BuildSummary(_packages[id]))
                    .ToList();

                return Task.FromResult(new ListComplianceAuditExportsResponse
                {
                    Success = true,
                    Exports = summaries,
                    TotalCount = total
                });
            }
        }

        // ── Package builders ──────────────────────────────────────────────────

        private ComplianceAuditExportPackage BuildReleaseReadinessPackage(
            string exportId,
            ReleaseReadinessExportRequest request,
            List<string> history,
            DateTime now)
        {
            var provenance = BuildReleaseReadinessProvenance(request.SubjectId, now,
                request.EvidenceFromTimestamp);

            var (blockers, releaseSection) = BuildReleaseReadinessSectionAndBlockers(
                request.SubjectId, provenance, request.HeadRef, request.EnvironmentLabel, now);

            var readiness = DetermineReadiness(provenance, blockers);

            var (headline, detail) = BuildReadinessText(readiness, AuditScenario.ReleaseReadinessSignOff,
                provenance, blockers);

            var contentHash = ComputeContentHash(exportId, request.SubjectId,
                AuditScenario.ReleaseReadinessSignOff, provenance, blockers, now);

            return new ComplianceAuditExportPackage
            {
                ExportId = exportId,
                SubjectId = request.SubjectId,
                Scenario = AuditScenario.ReleaseReadinessSignOff,
                AudienceProfile = request.AudienceProfile,
                Readiness = readiness,
                ReadinessHeadline = headline,
                ReadinessDetail = detail,
                AssembledAt = now,
                ExpiresAt = now.Add(FreshnessWindow),
                EnvironmentLabel = request.EnvironmentLabel,
                HeadReference = request.HeadRef,
                SchemaVersion = SchemaVersion,
                PolicyVersion = PolicyVersion,
                ProvenanceRecords = provenance,
                Blockers = blockers,
                CorrelationId = request.CorrelationId,
                RequestorNotes = request.RequestorNotes,
                ContentHash = contentHash,
                TrackerHistory = history,
                ReleaseReadiness = releaseSection
            };
        }

        private ComplianceAuditExportPackage BuildOnboardingCasePackage(
            string exportId,
            OnboardingCaseReviewExportRequest request,
            List<string> history,
            DateTime now)
        {
            var provenance = BuildOnboardingProvenance(request.SubjectId, now,
                request.EvidenceFromTimestamp);

            var (blockers, caseSection) = BuildOnboardingCaseSectionAndBlockers(
                request.SubjectId, provenance, request.CaseId, now);

            var readiness = DetermineReadiness(provenance, blockers);

            var (headline, detail) = BuildReadinessText(readiness, AuditScenario.OnboardingCaseReview,
                provenance, blockers);

            var contentHash = ComputeContentHash(exportId, request.SubjectId,
                AuditScenario.OnboardingCaseReview, provenance, blockers, now);

            return new ComplianceAuditExportPackage
            {
                ExportId = exportId,
                SubjectId = request.SubjectId,
                Scenario = AuditScenario.OnboardingCaseReview,
                AudienceProfile = request.AudienceProfile,
                Readiness = readiness,
                ReadinessHeadline = headline,
                ReadinessDetail = detail,
                AssembledAt = now,
                ExpiresAt = now.Add(FreshnessWindow),
                EnvironmentLabel = request.EnvironmentLabel,
                SchemaVersion = SchemaVersion,
                PolicyVersion = PolicyVersion,
                ProvenanceRecords = provenance,
                Blockers = blockers,
                CorrelationId = request.CorrelationId,
                RequestorNotes = request.RequestorNotes,
                ContentHash = contentHash,
                TrackerHistory = history,
                OnboardingCase = caseSection
            };
        }

        private ComplianceAuditExportPackage BuildBlockerReviewPackage(
            string exportId,
            ComplianceBlockerReviewExportRequest request,
            List<string> history,
            DateTime now)
        {
            var provenance = BuildBlockerReviewProvenance(request.SubjectId, now,
                request.EvidenceFromTimestamp);

            var (blockers, blockerSection) = BuildBlockerReviewSectionAndBlockers(
                request.SubjectId, provenance, request.IncludeResolvedBlockers, now);

            var readiness = DetermineReadiness(provenance, blockers);

            var (headline, detail) = BuildReadinessText(readiness, AuditScenario.ComplianceBlockerReview,
                provenance, blockers);

            var contentHash = ComputeContentHash(exportId, request.SubjectId,
                AuditScenario.ComplianceBlockerReview, provenance, blockers, now);

            return new ComplianceAuditExportPackage
            {
                ExportId = exportId,
                SubjectId = request.SubjectId,
                Scenario = AuditScenario.ComplianceBlockerReview,
                AudienceProfile = request.AudienceProfile,
                Readiness = readiness,
                ReadinessHeadline = headline,
                ReadinessDetail = detail,
                AssembledAt = now,
                ExpiresAt = now.Add(FreshnessWindow),
                EnvironmentLabel = request.EnvironmentLabel,
                SchemaVersion = SchemaVersion,
                PolicyVersion = PolicyVersion,
                ProvenanceRecords = provenance,
                Blockers = blockers,
                CorrelationId = request.CorrelationId,
                RequestorNotes = request.RequestorNotes,
                ContentHash = contentHash,
                TrackerHistory = history,
                BlockerReview = blockerSection
            };
        }

        private ComplianceAuditExportPackage BuildApprovalHistoryPackage(
            string exportId,
            ApprovalHistoryExportRequest request,
            List<string> history,
            int decisionLimit,
            DateTime now)
        {
            var provenance = BuildApprovalHistoryProvenance(request.SubjectId, now,
                request.EvidenceFromTimestamp);

            var (blockers, approvalSection) = BuildApprovalHistorySectionAndBlockers(
                request.SubjectId, provenance, decisionLimit, now);

            var readiness = DetermineReadiness(provenance, blockers);

            var (headline, detail) = BuildReadinessText(readiness, AuditScenario.ApprovalHistoryExport,
                provenance, blockers);

            var contentHash = ComputeContentHash(exportId, request.SubjectId,
                AuditScenario.ApprovalHistoryExport, provenance, blockers, now);

            return new ComplianceAuditExportPackage
            {
                ExportId = exportId,
                SubjectId = request.SubjectId,
                Scenario = AuditScenario.ApprovalHistoryExport,
                AudienceProfile = request.AudienceProfile,
                Readiness = readiness,
                ReadinessHeadline = headline,
                ReadinessDetail = detail,
                AssembledAt = now,
                ExpiresAt = now.Add(FreshnessWindow),
                EnvironmentLabel = request.EnvironmentLabel,
                SchemaVersion = SchemaVersion,
                PolicyVersion = PolicyVersion,
                ProvenanceRecords = provenance,
                Blockers = blockers,
                CorrelationId = request.CorrelationId,
                RequestorNotes = request.RequestorNotes,
                ContentHash = contentHash,
                TrackerHistory = history,
                ApprovalHistory = approvalSection
            };
        }

        // ── Provenance builders ───────────────────────────────────────────────

        private List<AuditEvidenceProvenance> BuildReleaseReadinessProvenance(
            string subjectId, DateTime now, DateTime? fromTimestamp)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);
            var list = new List<AuditEvidenceProvenance>();

            // KYC decision
            var kycAge = TimeSpan.FromDays(rng.Next(1, 200));
            var kycCaptured = now - kycAge;
            if (fromTimestamp == null || kycCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-kyc-{subjectId}", "StripeIdentity",
                    "KYC Identity Verification", kycCaptured, kycCaptured.Add(FreshnessWindow),
                    isRequired: true, now,
                    $"Identity verification decision from Stripe Identity provider.",
                    $"kyc-ref-{subjectId}"));
            }

            // AML decision
            var amlAge = TimeSpan.FromDays(rng.Next(1, 180));
            var amlCaptured = now - amlAge;
            if (fromTimestamp == null || amlCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-aml-{subjectId}", "ComplyAdvantage",
                    "AML Sanctions Screening", amlCaptured, amlCaptured.Add(FreshnessWindow),
                    isRequired: true, now,
                    "AML and sanctions screening decision from ComplyAdvantage.",
                    $"aml-ref-{subjectId}"));
            }

            // Sign-off evidence (required for release readiness)
            var signOffAge = TimeSpan.FromDays(rng.Next(1, 45));
            var signOffCaptured = now - signOffAge;
            list.Add(MakeProvenance($"prov-signoff-{subjectId}", "ProtectedSignOffEvidencePersistence",
                "Release Sign-Off Evidence", signOffCaptured, signOffCaptured.Add(FreshnessWindow),
                isRequired: true, now,
                "Protected sign-off evidence record for this release.",
                $"signoff-ref-{subjectId}"));

            // Launch decision
            var launchAge = TimeSpan.FromDays(rng.Next(1, 45));
            var launchCaptured = now - launchAge;
            list.Add(MakeProvenance($"prov-launch-{subjectId}", "ComplianceEvidenceLaunchDecision",
                "Launch Readiness Decision", launchCaptured, launchCaptured.Add(FreshnessWindow),
                isRequired: true, now,
                "Launch readiness evaluation for the token issuance.",
                $"launch-ref-{subjectId}"));

            return list;
        }

        private List<AuditEvidenceProvenance> BuildOnboardingProvenance(
            string subjectId, DateTime now, DateTime? fromTimestamp)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);
            var list = new List<AuditEvidenceProvenance>();

            // Onboarding case record (required)
            var caseAge = TimeSpan.FromDays(rng.Next(1, 60));
            var caseCaptured = now - caseAge;
            if (fromTimestamp == null || caseCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-case-{subjectId}", "KycAmlOnboardingCaseService",
                    "KYC/AML Onboarding Case", caseCaptured, null,
                    isRequired: true, now,
                    "Active KYC/AML onboarding case record.",
                    $"case-ref-{subjectId}"));
            }

            // KYC provider check
            var kycAge = TimeSpan.FromDays(rng.Next(1, 200));
            var kycCaptured = now - kycAge;
            if (fromTimestamp == null || kycCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-kyc-{subjectId}", "StripeIdentity",
                    "KYC Identity Verification", kycCaptured, kycCaptured.Add(FreshnessWindow),
                    isRequired: true, now,
                    "Identity verification result from the onboarding provider check.",
                    $"kyc-onboard-{subjectId}"));
            }

            // AML provider check
            var amlAge = TimeSpan.FromDays(rng.Next(1, 180));
            var amlCaptured = now - amlAge;
            if (fromTimestamp == null || amlCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-aml-{subjectId}", "ComplyAdvantage",
                    "AML Sanctions Screening", amlCaptured, amlCaptured.Add(FreshnessWindow),
                    isRequired: true, now,
                    "AML screening result from the onboarding provider check.",
                    $"aml-onboard-{subjectId}"));
            }

            // Reviewer action record (optional)
            var reviewAge = TimeSpan.FromDays(rng.Next(1, 30));
            var reviewCaptured = now - reviewAge;
            if (fromTimestamp == null || reviewCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-review-{subjectId}", "ComplianceCaseManagement",
                    "Reviewer Action", reviewCaptured, null,
                    isRequired: false, now,
                    "Most recent reviewer action recorded on this case.",
                    $"review-ref-{subjectId}"));
            }

            return list;
        }

        private List<AuditEvidenceProvenance> BuildBlockerReviewProvenance(
            string subjectId, DateTime now, DateTime? fromTimestamp)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);
            var list = new List<AuditEvidenceProvenance>();

            // Compliance case blockers (required)
            var caseAge = TimeSpan.FromDays(rng.Next(1, 60));
            var caseCaptured = now - caseAge;
            if (fromTimestamp == null || caseCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-case-{subjectId}", "ComplianceCaseManagementService",
                    "Compliance Case Blockers", caseCaptured, null,
                    isRequired: true, now,
                    "Compliance case blockers and resolution status.",
                    $"case-ref-{subjectId}"));
            }

            // KYC/AML decision (required)
            var kycAge = TimeSpan.FromDays(rng.Next(1, 200));
            var kycCaptured = now - kycAge;
            if (fromTimestamp == null || kycCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-kyc-{subjectId}", "StripeIdentity",
                    "KYC Identity Verification", kycCaptured, kycCaptured.Add(FreshnessWindow),
                    isRequired: true, now,
                    "Identity verification decision contributing to blocker posture.",
                    $"kyc-ref-{subjectId}"));
            }

            // Ongoing monitoring tasks (optional)
            var monitorAge = TimeSpan.FromDays(rng.Next(1, 30));
            var monitorCaptured = now - monitorAge;
            if (fromTimestamp == null || monitorCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-monitor-{subjectId}", "OngoingMonitoringService",
                    "Ongoing Monitoring Tasks", monitorCaptured, null,
                    isRequired: false, now,
                    "Ongoing monitoring task records contributing to blocker posture.",
                    $"monitor-ref-{subjectId}"));
            }

            return list;
        }

        private List<AuditEvidenceProvenance> BuildApprovalHistoryProvenance(
            string subjectId, DateTime now, DateTime? fromTimestamp)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);
            var list = new List<AuditEvidenceProvenance>();

            // Approval workflow history (required)
            var approvalAge = TimeSpan.FromDays(rng.Next(1, 30));
            var approvalCaptured = now - approvalAge;
            if (fromTimestamp == null || approvalCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-approval-{subjectId}", "ApprovalWorkflowService",
                    "Approval Workflow History", approvalCaptured, null,
                    isRequired: true, now,
                    "Complete approval workflow decision history.",
                    $"approval-ref-{subjectId}"));
            }

            // Compliance decision (optional supplementary)
            var decisionAge = TimeSpan.FromDays(rng.Next(1, 60));
            var decisionCaptured = now - decisionAge;
            if (fromTimestamp == null || decisionCaptured >= fromTimestamp.Value)
            {
                list.Add(MakeProvenance($"prov-decision-{subjectId}", "ComplianceDecisionService",
                    "Compliance Decision Record", decisionCaptured, null,
                    isRequired: false, now,
                    "Most recent compliance decision record for this subject.",
                    $"decision-ref-{subjectId}"));
            }

            return list;
        }

        // ── Section + blocker builders ────────────────────────────────────────

        private (List<AuditExportBlocker> blockers, AuditReleaseReadinessSection section)
            BuildReleaseReadinessSectionAndBlockers(
                string subjectId,
                List<AuditEvidenceProvenance> provenance,
                string? headRef,
                string? envLabel,
                DateTime now)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);

            var signOffProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "Release Sign-Off Evidence");
            var kycProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "KYC Identity Verification");
            var amlProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "AML Sanctions Screening");

            var isSignOffFresh = signOffProvenance?.FreshnessState == AuditEvidenceFreshness.Fresh ||
                                 signOffProvenance?.FreshnessState == AuditEvidenceFreshness.NearingExpiry;
            var isKycPassed = kycProvenance?.FreshnessState == AuditEvidenceFreshness.Fresh ||
                              kycProvenance?.FreshnessState == AuditEvidenceFreshness.NearingExpiry;
            var isAmlPassed = amlProvenance?.FreshnessState == AuditEvidenceFreshness.Fresh ||
                              amlProvenance?.FreshnessState == AuditEvidenceFreshness.NearingExpiry;
            var kycAmlPassed = isKycPassed && isAmlPassed;

            // Deterministic: ~80% of subjects get release-grade evidence
            var isReleaseGrade = (rng.Next(0, 5) != 0) && isSignOffFresh;

            var blockers = new List<AuditExportBlocker>();
            var releaseBlockers = new List<string>();
            var approvalWebhooks = new List<string>();

            if (!isSignOffFresh)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-signoff-stale-{subjectId}",
                    Title = "Sign-off evidence is stale or missing",
                    Description = "Protected sign-off evidence is either absent or has exceeded the " +
                                  "90-day freshness window. Release-grade classification requires fresh sign-off.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = signOffProvenance?.FreshnessState == AuditEvidenceFreshness.Missing
                        ? "MissingEvidence"
                        : "StaleEvidence",
                    RelatedProvenanceIds = signOffProvenance != null
                        ? new List<string> { signOffProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Re-run the protected sign-off evidence collection process.",
                        "Ensure the sign-off record passes IsReleaseGrade validation.",
                        "Regenerate the export after fresh sign-off evidence is available."
                    },
                    OwnerTeam = "operations",
                    IsResolved = false
                };
                blockers.Add(b);
                releaseBlockers.Add(b.Title);
            }
            else if (!isReleaseGrade)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-signoff-notgrade-{subjectId}",
                    Title = "Sign-off evidence is not release-grade",
                    Description = "Sign-off evidence was found but is classified as non-release-grade " +
                                  "(e.g., test artifact or non-grade record). Only release-grade evidence " +
                                  "qualifies for production sign-off.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "NonReleaseGradeEvidence",
                    RelatedProvenanceIds = signOffProvenance != null
                        ? new List<string> { signOffProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Ensure sign-off evidence is submitted with IsReleaseGrade=true.",
                        "Remove any test-mode evidence from the release evidence set.",
                        "Regenerate the export after release-grade evidence is confirmed."
                    },
                    OwnerTeam = "operations",
                    IsResolved = false
                };
                blockers.Add(b);
                releaseBlockers.Add(b.Title);
            }
            else
            {
                // Sign-off is release-grade — add approval webhook reference
                approvalWebhooks.Add($"webhook-approval-{subjectId}-release");
            }

            if (!kycAmlPassed)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-kycaml-{subjectId}",
                    Title = "KYC/AML checks have not passed",
                    Description = $"KYC status: {(isKycPassed ? "Passed" : "Not passed")}. " +
                                  $"AML status: {(isAmlPassed ? "Cleared" : "Not cleared")}. " +
                                  "All identity and sanctions checks must pass before release-grade " +
                                  "sign-off can be granted.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "ComplianceCheckFailed",
                    RelatedProvenanceIds = new List<string>
                    {
                        kycProvenance?.ProvenanceId ?? $"prov-kyc-{subjectId}",
                        amlProvenance?.ProvenanceId ?? $"prov-aml-{subjectId}"
                    },
                    RemediationHints = new List<string>
                    {
                        "Re-run KYC/AML provider checks for this subject.",
                        "Ensure provider results are ingested through the KYC/AML decision endpoint.",
                        "Regenerate the export after all checks pass."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                };
                blockers.Add(b);
                releaseBlockers.Add(b.Title);
            }

            string? operatorGuidance = releaseBlockers.Any()
                ? $"Release sign-off is blocked by {releaseBlockers.Count} issue(s). " +
                  "Resolve all blockers and regenerate the export before proceeding."
                : null;

            var section = new AuditReleaseReadinessSection
            {
                HeadRef = headRef,
                EnvironmentLabel = envLabel,
                HasReleaseGradeSignOff = isReleaseGrade && blockers.Count == 0,
                SignOffCapturedAt = signOffProvenance?.CapturedAt,
                SignOffStatus = isSignOffFresh ? (isReleaseGrade ? "Ready" : "NotReleaseGrade") : "Stale",
                IsSignOffFresh = isSignOffFresh,
                IsReleaseGradeEvidence = isReleaseGrade,
                ReleaseBlockers = releaseBlockers,
                ApprovalWebhookEvents = approvalWebhooks,
                OperatorGuidance = operatorGuidance,
                KycAmlPostureSummary = kycAmlPassed
                    ? "KYC identity verified and AML sanctions screening cleared."
                    : $"Compliance posture incomplete: KYC={( isKycPassed ? "Passed" : "Pending")}, " +
                      $"AML={( isAmlPassed ? "Cleared" : "Pending")}.",
                KycAmlChecksPassed = kycAmlPassed
            };

            return (blockers, section);
        }

        private (List<AuditExportBlocker> blockers, AuditOnboardingCaseSection section)
            BuildOnboardingCaseSectionAndBlockers(
                string subjectId,
                List<AuditEvidenceProvenance> provenance,
                string? requestedCaseId,
                DateTime now)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);

            var caseProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "KYC/AML Onboarding Case");
            var kycProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "KYC Identity Verification");
            var amlProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "AML Sanctions Screening");

            // Deterministic state based on seed
            var stateOptions = new[] { "Approved", "UnderReview", "ProviderUnavailable",
                "Initiated", "Escalated", "Rejected" };
            var caseState = stateOptions[rng.Next(0, stateOptions.Length)];
            var caseId = requestedCaseId ?? $"case-{subjectId}-001";

            var isTerminal = caseState is "Approved" or "Rejected" or "Expired";
            var supportsPositive = caseState == "Approved";
            var providerAvailable = caseState != "ProviderUnavailable";

            var blockers = new List<AuditExportBlocker>();

            if (caseProvenance == null ||
                caseProvenance.FreshnessState == AuditEvidenceFreshness.Missing)
            {
                blockers.Add(new AuditExportBlocker
                {
                    BlockerId = $"blocker-case-missing-{subjectId}",
                    Title = "Onboarding case record not found",
                    Description = "No onboarding case record was found for this subject. " +
                                  "A case must be initiated and completed before a case review export " +
                                  "can be assembled.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "MissingEvidence",
                    RelatedProvenanceIds = new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Initiate a new KYC/AML onboarding case via POST /api/v1/kyc-aml-onboarding/cases.",
                        "Complete provider checks and reviewer actions.",
                        "Regenerate the export after the case is in a reviewable state."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                });
            }
            else if (!isTerminal && caseState != "UnderReview")
            {
                blockers.Add(new AuditExportBlocker
                {
                    BlockerId = $"blocker-case-incomplete-{subjectId}",
                    Title = $"Onboarding case is not yet in a reviewable state ({caseState})",
                    Description = $"The onboarding case is currently in state '{caseState}'. " +
                                  "A case review export requires the case to be approved, under review, " +
                                  "or in another reviewable terminal state.",
                    Severity = AuditBlockerSeverity.Warning,
                    Category = "CaseNotReviewable",
                    RelatedProvenanceIds = caseProvenance != null
                        ? new List<string> { caseProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Complete the provider check initiation step.",
                        "Ensure a reviewer has taken action on the case.",
                        "Regenerate the export once the case is approved or under review."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                });
            }

            if (!providerAvailable)
            {
                blockers.Add(new AuditExportBlocker
                {
                    BlockerId = $"blocker-provider-unavail-{subjectId}",
                    Title = "KYC/AML provider is unavailable",
                    Description = "The onboarding provider was unreachable at the time the case was " +
                                  "last evaluated. Evidence may be incomplete.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "ProviderUnavailable",
                    RelatedProvenanceIds = caseProvenance != null
                        ? new List<string> { caseProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Re-initiate provider checks once the provider is available.",
                        "Check provider configuration and credentials.",
                        "Regenerate the export after checks complete."
                    },
                    OwnerTeam = "operations",
                    IsResolved = false
                });
            }

            var evidenceSummaries = new List<string>();
            if (kycProvenance != null)
                evidenceSummaries.Add(
                    $"KYC Identity Verification ({kycProvenance.FreshnessState}): " +
                    $"captured {kycProvenance.CapturedAt:yyyy-MM-dd}");
            if (amlProvenance != null)
                evidenceSummaries.Add(
                    $"AML Sanctions Screening ({amlProvenance.FreshnessState}): " +
                    $"captured {amlProvenance.CapturedAt:yyyy-MM-dd}");

            var reviewerActions = new List<string>();
            if (caseState == "Approved")
                reviewerActions.Add($"Approved by compliance-reviewer-system on {now.AddDays(-rng.Next(1, 10)):yyyy-MM-dd}");
            else if (caseState == "UnderReview")
                reviewerActions.Add($"Assigned to compliance-reviewer-{rng.Next(1, 10):000} for review.");
            else if (caseState == "Escalated")
                reviewerActions.Add("Escalated for senior compliance review.");

            var section = new AuditOnboardingCaseSection
            {
                CaseId = caseId,
                CaseState = caseState,
                CaseInitiatedAt = caseProvenance?.CapturedAt,
                LastTransitionAt = caseProvenance?.CapturedAt.AddDays(rng.Next(0, 5)),
                ProviderChecksCompleted = isTerminal || caseState == "UnderReview",
                ProviderAvailabilityStatus = providerAvailable ? "Available" : "Unavailable",
                EvidenceSummaries = evidenceSummaries,
                IsInTerminalState = isTerminal,
                SupportsPositiveDetermination = supportsPositive,
                CaseSummary = supportsPositive
                    ? "Case approved: all KYC/AML checks completed and passed. " +
                      "Subject is cleared for onboarding."
                    : $"Case status: {caseState}. " +
                      (blockers.Any() ? "Blockers remain." : "No critical blockers."),
                ReviewerActions = reviewerActions
            };

            return (blockers, section);
        }

        private (List<AuditExportBlocker> blockers, AuditBlockerReviewSection section)
            BuildBlockerReviewSectionAndBlockers(
                string subjectId,
                List<AuditEvidenceProvenance> provenance,
                bool includeResolved,
                DateTime now)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);

            var caseProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "Compliance Case Blockers");
            var kycProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "KYC Identity Verification");

            // Deterministic number of open blockers based on seed
            var openCriticalCount = rng.Next(0, 3);
            var openWarningCount = rng.Next(0, 4);
            var openAdvisoryCount = rng.Next(0, 3);
            var resolvedCount = rng.Next(0, 5);

            var packageBlockers = new List<AuditExportBlocker>();
            var openBlockers = new List<AuditExportBlocker>();
            var resolvedBlockers = new List<AuditExportBlocker>();

            for (int i = 0; i < openCriticalCount; i++)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-critical-{subjectId}-{i + 1:000}",
                    Title = i == 0 ? "Missing required KYC evidence"
                                  : "AML screening result contradicts prior decision",
                    Description = i == 0
                        ? "Required KYC identity verification evidence is absent or invalid."
                        : "AML screening result contradicts a previous cleared status. Manual review required.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = i == 0 ? "MissingEvidence" : "UnresolvedContradiction",
                    RelatedProvenanceIds = kycProvenance != null
                        ? new List<string> { kycProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        i == 0 ? "Re-initiate KYC check and ingest the result."
                               : "Review conflicting AML decisions and submit a resolved outcome.",
                        "Regenerate the export after resolution."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                };
                openBlockers.Add(b);
                packageBlockers.Add(b);
            }

            for (int i = 0; i < openWarningCount; i++)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-warning-{subjectId}-{i + 1:000}",
                    Title = $"Evidence approaching expiry: {(i % 2 == 0 ? "AML decision" : "Onboarding case record")}",
                    Description = $"The {(i % 2 == 0 ? "AML decision" : "onboarding case record")} is within " +
                                  "the 7-day near-expiry window. Refresh before the next review cycle.",
                    Severity = AuditBlockerSeverity.Warning,
                    Category = "NearingExpiry",
                    RelatedProvenanceIds = caseProvenance != null
                        ? new List<string> { caseProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Schedule a re-run of the expiring check before its expiry date.",
                        "Submit the refreshed result through the ingestion endpoint."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                };
                openBlockers.Add(b);
                packageBlockers.Add(b);
            }

            for (int i = 0; i < openAdvisoryCount; i++)
            {
                var b = new AuditExportBlocker
                {
                    BlockerId = $"blocker-advisory-{subjectId}-{i + 1:000}",
                    Title = "Optional evidence source not yet collected",
                    Description = "An optional evidence source (external document reference) " +
                                  "has not yet been submitted. This does not affect readiness " +
                                  "but is recommended for regulator-grade completeness.",
                    Severity = AuditBlockerSeverity.Advisory,
                    Category = "OptionalMissing",
                    RelatedProvenanceIds = new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Submit the external reference document via the evidence ingestion endpoint."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                };
                openBlockers.Add(b);
                packageBlockers.Add(b);
            }

            if (includeResolved)
            {
                for (int i = 0; i < resolvedCount; i++)
                {
                    var b = new AuditExportBlocker
                    {
                        BlockerId = $"blocker-resolved-{subjectId}-{i + 1:000}",
                        Title = i == 0 ? "KYC decision renewed" : "Stale AML record refreshed",
                        Description = i == 0
                            ? "Previously stale KYC decision was renewed by re-running the check."
                            : "AML record has been refreshed within the freshness window.",
                        Severity = i == 0 ? AuditBlockerSeverity.Critical : AuditBlockerSeverity.Warning,
                        Category = i == 0 ? "StaleEvidence" : "StaleEvidence",
                        RelatedProvenanceIds = kycProvenance != null
                            ? new List<string> { kycProvenance.ProvenanceId }
                            : new List<string>(),
                        RemediationHints = i == 0
                                ? new List<string> { "KYC check was renewed — no further action required." }
                                : new List<string> { "AML record was refreshed — monitor for future staleness." },
                        OwnerTeam = "compliance",
                        IsResolved = true,
                        ResolvedAt = now.AddDays(-rng.Next(1, 30))
                    };
                    resolvedBlockers.Add(b);
                    packageBlockers.Add(b);
                }
            }

            var byCategory = openBlockers
                .GroupBy(b => b.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            var section = new AuditBlockerReviewSection
            {
                OpenBlockerCount = openBlockers.Count,
                ResolvedBlockerCount = resolvedBlockers.Count,
                CriticalOpenCount = openBlockers.Count(b => b.Severity == AuditBlockerSeverity.Critical),
                WarningOpenCount = openBlockers.Count(b => b.Severity == AuditBlockerSeverity.Warning),
                AdvisoryOpenCount = openBlockers.Count(b => b.Severity == AuditBlockerSeverity.Advisory),
                BlockersByCategory = byCategory,
                OpenBlockers = openBlockers,
                RecentlyResolvedBlockers = resolvedBlockers,
                HasUnresolvedCriticalBlockers = openBlockers.Any(b => b.Severity == AuditBlockerSeverity.Critical),
                BlockerPostureSummary = openBlockers.Count == 0
                    ? "No open blockers. Compliance posture is clear."
                    : $"{openCriticalCount} critical, {openWarningCount} warning, {openAdvisoryCount} advisory " +
                      $"blockers are open. Resolve all critical blockers to achieve regulator-ready status."
            };

            return (packageBlockers, section);
        }

        private (List<AuditExportBlocker> blockers, AuditApprovalHistorySection section)
            BuildApprovalHistorySectionAndBlockers(
                string subjectId,
                List<AuditEvidenceProvenance> provenance,
                int decisionLimit,
                DateTime now)
        {
            var seed = Math.Abs(subjectId.GetHashCode());
            var rng = new Random(seed);

            var approvalProvenance = provenance.FirstOrDefault(
                p => p.EvidenceCategory == "Approval Workflow History");

            var blockers = new List<AuditExportBlocker>();

            if (approvalProvenance == null ||
                approvalProvenance.FreshnessState == AuditEvidenceFreshness.Missing)
            {
                blockers.Add(new AuditExportBlocker
                {
                    BlockerId = $"blocker-approval-missing-{subjectId}",
                    Title = "Approval workflow history not found",
                    Description = "No approval workflow history was found for this subject. " +
                                  "At least one approval decision is required for a valid " +
                                  "approval-history export.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "MissingEvidence",
                    RelatedProvenanceIds = new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Initiate the approval workflow via the compliance or issuance approval endpoints.",
                        "Ensure at least one approval decision has been recorded.",
                        "Regenerate the export after the workflow history is available."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                });
            }

            // Build deterministic approval history
            var decisions = new List<AuditApprovalDecisionEntry>();
            var stageCount = 1 + rng.Next(0, 2);

            for (int i = 0; i < stageCount; i++)
            {
                var stage = i == 0 ? "ComplianceReview" : "ExecutiveSignOff";
                var outcome = (i == 0 && rng.Next(0, 4) == 0) ? "NeedsMoreEvidence" : "Approved";
                decisions.Add(new AuditApprovalDecisionEntry
                {
                    EntryId = $"decision-{subjectId}-{i + 1:000}",
                    Stage = stage,
                    Decision = outcome,
                    DecidedBy = outcome == "Approved"
                        ? $"compliance-reviewer-{i + 1:000}"
                        : "compliance-reviewer-001",
                    DecidedAt = now.AddDays(-((stageCount - i) * rng.Next(1, 15))),
                    Rationale = outcome == "Approved"
                        ? "All required evidence reviewed and found compliant."
                        : "Additional evidence required before approval can proceed."
                });
            }

            decisions.Sort((a, b) => a.DecidedAt.CompareTo(b.DecidedAt));
            decisions = decisions.Take(decisionLimit).ToList();

            var latestDecision = decisions.LastOrDefault();
            var hasPending = latestDecision?.Decision == "NeedsMoreEvidence";

            if (hasPending)
            {
                blockers.Add(new AuditExportBlocker
                {
                    BlockerId = $"blocker-approval-pending-{subjectId}",
                    Title = "Approval workflow has a pending review stage",
                    Description = "The most recent approval stage decision is 'NeedsMoreEvidence'. " +
                                  "The approval workflow cannot be considered complete until this stage " +
                                  "is resolved.",
                    Severity = AuditBlockerSeverity.Critical,
                    Category = "ApprovalPending",
                    RelatedProvenanceIds = approvalProvenance != null
                        ? new List<string> { approvalProvenance.ProvenanceId }
                        : new List<string>(),
                    RemediationHints = new List<string>
                    {
                        "Provide the additional evidence requested by the reviewer.",
                        "Resubmit the approval decision once evidence is complete.",
                        "Regenerate the export after the approval stage is resolved."
                    },
                    OwnerTeam = "compliance",
                    IsResolved = false
                });
            }

            // Build stage summary
            var stages = decisions
                .GroupBy(d => d.Stage)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(d => d.DecidedAt).First();
                    return new AuditApprovalStageEntry
                    {
                        StageName = g.Key,
                        Outcome = latest.Decision,
                        IsLatest = true,
                        LastDecisionAt = latest.DecidedAt,
                        LastDecisionBy = latest.DecidedBy
                    };
                })
                .ToList();

            var workflowCompleted = !hasPending && decisions.Any() &&
                                    decisions.Any(d => d.Decision == "Approved");

            var section = new AuditApprovalHistorySection
            {
                TotalDecisionCount = decisions.Count,
                LatestDecisionAt = latestDecision?.DecidedAt,
                LatestDecisionOutcome = latestDecision?.Decision,
                LatestDecisionBy = latestDecision?.DecidedBy,
                IsWorkflowCompleted = workflowCompleted,
                HasPendingReviewStage = hasPending,
                Stages = stages,
                DecisionHistory = decisions,
                WorkflowSummary = workflowCompleted
                    ? "Approval workflow is complete. All required stages have been approved."
                    : hasPending
                        ? "Approval workflow is pending: the most recent stage requires additional evidence."
                        : decisions.Any()
                            ? "Approval workflow is in progress."
                            : "No approval decisions have been recorded."
            };

            return (blockers, section);
        }

        // ── Readiness determination (fail-closed) ─────────────────────────────

        private static AuditExportReadiness DetermineReadiness(
            List<AuditEvidenceProvenance> provenance,
            List<AuditExportBlocker> blockers)
        {
            // Priority 1: Provider unreachable
            if (provenance.Any(p => p.FreshnessState == AuditEvidenceFreshness.ProviderUnavailable))
                return AuditExportReadiness.DegradedProviderUnavailable;

            // Priority 2: Unresolved critical blockers
            if (blockers.Any(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical))
                return AuditExportReadiness.Blocked;

            // Priority 3: Required evidence missing
            if (provenance.Any(p => p.IsRequired && p.FreshnessState == AuditEvidenceFreshness.Missing))
                return AuditExportReadiness.Incomplete;

            // Priority 4: Required evidence stale
            if (provenance.Any(p => p.IsRequired && p.FreshnessState == AuditEvidenceFreshness.Stale))
                return AuditExportReadiness.Stale;

            // Priority 5: Non-required evidence partially unavailable
            if (provenance.Any(p => !p.IsRequired &&
                (p.FreshnessState == AuditEvidenceFreshness.Missing ||
                 p.FreshnessState == AuditEvidenceFreshness.Invalid)))
                return AuditExportReadiness.PartiallyAvailable;

            // Priority 6: Manual review required (warning blockers remain)
            if (blockers.Any(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Warning))
                return AuditExportReadiness.RequiresReview;

            // All checks pass
            return AuditExportReadiness.Ready;
        }

        // ── Readiness text builder ────────────────────────────────────────────

        private static (string headline, string detail) BuildReadinessText(
            AuditExportReadiness readiness,
            AuditScenario scenario,
            List<AuditEvidenceProvenance> provenance,
            List<AuditExportBlocker> blockers)
        {
            var scenarioLabel = scenario switch
            {
                AuditScenario.ReleaseReadinessSignOff => "release-readiness sign-off",
                AuditScenario.OnboardingCaseReview => "onboarding case review",
                AuditScenario.ComplianceBlockerReview => "compliance blocker review",
                AuditScenario.ApprovalHistoryExport => "approval-history export",
                _ => "compliance audit"
            };

            return readiness switch
            {
                AuditExportReadiness.Ready => (
                    $"Package is regulator-ready: all required evidence is present, current, and validated.",
                    $"All required evidence for the {scenarioLabel} is available and within the 90-day " +
                    "freshness window. No unresolved critical blockers. Package is suitable for " +
                    "regulator-grade submission."),

                AuditExportReadiness.Blocked => (
                    $"Package is blocked: {blockers.Count(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical)} critical blocker(s) prevent regulator-ready classification.",
                    $"Critical blockers must be resolved before this {scenarioLabel} package can be " +
                    "considered regulator-ready. Review the Blockers section for remediation hints. " +
                    "Never submit a blocked package for regulator review."),

                AuditExportReadiness.Incomplete => (
                    $"Package is incomplete: one or more required evidence sources are missing.",
                    $"Required evidence sources for the {scenarioLabel} could not be located. " +
                    "Readiness cannot be established until all required records are present. " +
                    "Review the ProvenanceRecords section for missing sources."),

                AuditExportReadiness.Stale => (
                    $"Package readiness is downgraded: required evidence has exceeded the 90-day freshness window.",
                    $"One or more required evidence sources for the {scenarioLabel} are stale. " +
                    "Stale records are not acceptable for regulator submission without renewal. " +
                    "Re-run the relevant checks to restore readiness."),

                AuditExportReadiness.PartiallyAvailable => (
                    "Package is partially available: some optional evidence sources are missing.",
                    $"All required evidence for the {scenarioLabel} is present, but one or more " +
                    "optional sources are unavailable. The package may be suitable for internal review " +
                    "but lacks the completeness required for regulator-grade submission."),

                AuditExportReadiness.RequiresReview => (
                    "Package requires manual review before regulator-ready classification.",
                    $"Evidence for the {scenarioLabel} is complete and current, but one or more " +
                    "warning-level blockers indicate that human review is recommended before the " +
                    "package is submitted for regulator review."),

                AuditExportReadiness.DegradedProviderUnavailable => (
                    "Package assembly was degraded: one or more evidence providers were unreachable.",
                    $"A provider or data source was unavailable during {scenarioLabel} assembly. " +
                    "Evidence may be incomplete. Do not use this package for regulator submission " +
                    "until providers are available and the package is regenerated."),

                _ => ("Readiness status is indeterminate.", "Package readiness could not be determined.")
            };
        }

        // ── Provenance helper ─────────────────────────────────────────────────

        private AuditEvidenceProvenance MakeProvenance(
            string id,
            string sourceSystem,
            string category,
            DateTime capturedAt,
            DateTime? expiresAt,
            bool isRequired,
            DateTime now,
            string description,
            string? externalRef = null)
        {
            var freshness = ClassifyFreshness(capturedAt, expiresAt, now);
            return new AuditEvidenceProvenance
            {
                ProvenanceId = id,
                SourceSystem = sourceSystem,
                EvidenceCategory = category,
                CapturedAt = capturedAt,
                ExpiresAt = expiresAt,
                FreshnessState = freshness,
                IsRequired = isRequired,
                IntegrityHash = ComputeHash($"{id}:{capturedAt:O}"),
                ExternalReferenceId = externalRef,
                Description = description
            };
        }

        private AuditEvidenceFreshness ClassifyFreshness(
            DateTime capturedAt, DateTime? expiresAt, DateTime now)
        {
            if (expiresAt.HasValue)
            {
                if (now > expiresAt.Value)
                    return AuditEvidenceFreshness.Stale;
                if (now > expiresAt.Value - NearExpiryWindow)
                    return AuditEvidenceFreshness.NearingExpiry;
            }
            else
            {
                // No expiry: stale if older than freshness window
                if (capturedAt.Add(FreshnessWindow) < now)
                    return AuditEvidenceFreshness.Stale;
                if (capturedAt.Add(FreshnessWindow - NearExpiryWindow) < now)
                    return AuditEvidenceFreshness.NearingExpiry;
            }
            return AuditEvidenceFreshness.Fresh;
        }

        // ── Content hash ──────────────────────────────────────────────────────

        private static string ComputeContentHash(
            string exportId,
            string subjectId,
            AuditScenario scenario,
            List<AuditEvidenceProvenance> provenance,
            List<AuditExportBlocker> blockers,
            DateTime assembledAt)
        {
            var payload = JsonSerializer.Serialize(new
            {
                exportId,
                subjectId,
                scenario = scenario.ToString(),
                assembledAt = assembledAt.ToString("O"),
                provenanceIds = provenance.Select(p => p.ProvenanceId).OrderBy(x => x),
                blockerIds = blockers.Select(b => b.BlockerId).OrderBy(x => x)
            });
            return ComputeHash(payload);
        }

        private static string ComputeHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // ── Summary builder ───────────────────────────────────────────────────

        private static ComplianceAuditExportSummary BuildSummary(ComplianceAuditExportPackage pkg)
        {
            var openBlockers = pkg.Blockers.Where(b => !b.IsResolved).ToList();
            return new ComplianceAuditExportSummary
            {
                ExportId = pkg.ExportId,
                SubjectId = pkg.SubjectId,
                Scenario = pkg.Scenario,
                AudienceProfile = pkg.AudienceProfile,
                Readiness = pkg.Readiness,
                ReadinessHeadline = pkg.ReadinessHeadline,
                AssembledAt = pkg.AssembledAt,
                ExpiresAt = pkg.ExpiresAt,
                CriticalBlockerCount = openBlockers.Count(b => b.Severity == AuditBlockerSeverity.Critical),
                TotalOpenBlockerCount = openBlockers.Count,
                ProvenanceRecordCount = pkg.ProvenanceRecords.Count,
                IsRegulatorReady = pkg.IsRegulatorReady,
                ContentHash = pkg.ContentHash,
                CorrelationId = pkg.CorrelationId
            };
        }

        // ── Tracker history ───────────────────────────────────────────────────

        private List<string> GetTrackerHistory(string subjectId, AuditScenario scenario)
        {
            var key = SubjectScenarioKey(subjectId, scenario);
            if (!_subjectScenarioIndex.TryGetValue(key, out var ids))
                return new List<string>();
            // Return previous IDs (before the new one is stored), most recent first
            return new List<string>(ids);
        }

        // ── Storage helpers ───────────────────────────────────────────────────

        private void StorePackage(ComplianceAuditExportPackage pkg, string? idempotencyKey)
        {
            _packages[pkg.ExportId] = pkg;

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                _idempotencyIndex[idempotencyKey] = pkg.ExportId;

            var key = SubjectScenarioKey(pkg.SubjectId, pkg.Scenario);
            if (!_subjectScenarioIndex.ContainsKey(key))
                _subjectScenarioIndex[key] = new List<string>();
            _subjectScenarioIndex[key].Insert(0, pkg.ExportId);
        }

        private bool TryGetIdempotentReplay(
            string? idempotencyKey,
            bool forceRegenerate,
            out ComplianceAuditExportPackage? cached)
        {
            cached = null;
            if (forceRegenerate || string.IsNullOrWhiteSpace(idempotencyKey))
                return false;

            if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId) &&
                _packages.TryGetValue(existingId, out cached))
                return true;

            return false;
        }

        private static ComplianceAuditExportPackage ShallowCopyPackage(
            ComplianceAuditExportPackage src)
        {
            return new ComplianceAuditExportPackage
            {
                ExportId = src.ExportId,
                SubjectId = src.SubjectId,
                Scenario = src.Scenario,
                AudienceProfile = src.AudienceProfile,
                Readiness = src.Readiness,
                ReadinessHeadline = src.ReadinessHeadline,
                ReadinessDetail = src.ReadinessDetail,
                AssembledAt = src.AssembledAt,
                ExpiresAt = src.ExpiresAt,
                EnvironmentLabel = src.EnvironmentLabel,
                HeadReference = src.HeadReference,
                SchemaVersion = src.SchemaVersion,
                PolicyVersion = src.PolicyVersion,
                ProvenanceRecords = new List<AuditEvidenceProvenance>(src.ProvenanceRecords),
                Blockers = new List<AuditExportBlocker>(src.Blockers),
                CorrelationId = src.CorrelationId,
                RequestorNotes = src.RequestorNotes,
                ContentHash = src.ContentHash,
                TrackerHistory = new List<string>(src.TrackerHistory),
                ReleaseReadiness = src.ReleaseReadiness,
                OnboardingCase = src.OnboardingCase,
                BlockerReview = src.BlockerReview,
                ApprovalHistory = src.ApprovalHistory
            };
        }

        private static string SubjectScenarioKey(string subjectId, AuditScenario scenario)
            => $"{subjectId}|{scenario}";
    }
}
