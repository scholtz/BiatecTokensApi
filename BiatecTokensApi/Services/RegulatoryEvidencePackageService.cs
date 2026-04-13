using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IRegulatoryEvidencePackageService"/>.
    /// </summary>
    /// <remarks>
    /// Assembles regulator-facing evidence packages by composing simulated (or future real)
    /// KYC/AML decisions, compliance case records, approval workflow history, and readiness
    /// posture transitions into a deterministic, manifest-backed package.
    ///
    /// Readiness is fail-closed:
    ///   - Any missing required source → Incomplete (downgrade)
    ///   - Any stale required source  → Stale (downgrade)
    ///   - Any unresolved contradiction → Blocked (downgrade, highest priority)
    ///   - RequiresReview approval stage pending → RequiresReview
    ///   - All sources available and current → Ready
    ///
    /// Audience profiles affect framing and applied rules only.
    /// The canonical detail payload is never redacted even for ExecutiveSignOff.
    /// </remarks>
    public class RegulatoryEvidencePackageService : IRegulatoryEvidencePackageService
    {
        private readonly ILogger<RegulatoryEvidencePackageService> _logger;
        private readonly TimeProvider _timeProvider;

        // In-memory stores: package ID → summary/detail
        private readonly Dictionary<string, RegulatoryEvidencePackageSummary> _summaries = new();
        private readonly Dictionary<string, RegulatoryEvidencePackageDetail> _details = new();

        // Idempotency index: idempotencyKey → packageId
        private readonly Dictionary<string, string> _idempotencyIndex = new();

        // Per-subject index: subjectId → ordered list of packageIds (newest first)
        private readonly Dictionary<string, List<string>> _subjectIndex = new();

        private readonly object _lock = new();

        private const string SchemaVersion = "1.0.0";
        private const string CurrentPolicyVersion = "2026.03.07.1";

        // Evidence freshness window: records older than this are considered stale.
        private static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(90);
        // Near-expiry warning threshold.
        private static readonly TimeSpan NearExpiryWindow = TimeSpan.FromDays(7);

        /// <summary>Initializes a new instance of <see cref="RegulatoryEvidencePackageService"/>.</summary>
        public RegulatoryEvidencePackageService(
            ILogger<RegulatoryEvidencePackageService> logger,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── CreatePackageAsync ────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CreateRegulatoryEvidencePackageResponse> CreatePackageAsync(
            CreateRegulatoryEvidencePackageRequest request)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new CreateRegulatoryEvidencePackageResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to assemble an evidence package."
                });
            }

            lock (_lock)
            {
                // Idempotency: return cached package if same key exists and ForceRegenerate is false
                if (!request.ForceRegenerate &&
                    !string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    _idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId) &&
                    _summaries.TryGetValue(existingId, out var cachedSummary))
                {
                    _logger.LogInformation(
                        "Idempotent replay for key={Key}, PackageId={Id}",
                        LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                        cachedSummary.PackageId);

                    return Task.FromResult(new CreateRegulatoryEvidencePackageResponse
                    {
                        Success = true,
                        Package = cachedSummary,
                        IsIdempotentReplay = true
                    });
                }

                // Assemble new package
                var packageId = Guid.NewGuid().ToString();
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                var detail = AssemblePackageDetail(
                    packageId, request.SubjectId, request.AudienceProfile,
                    request.RequestorNotes, request.EvidenceFromTimestamp, correlationId, now);

                var summary = BuildSummary(detail, correlationId);

                // Store
                _summaries[packageId] = summary;
                _details[packageId] = detail;

                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    _idempotencyIndex[request.IdempotencyKey] = packageId;

                if (!_subjectIndex.ContainsKey(request.SubjectId))
                    _subjectIndex[request.SubjectId] = new List<string>();
                _subjectIndex[request.SubjectId].Insert(0, packageId);

                _logger.LogInformation(
                    "Assembled evidence package {PackageId} for subject {SubjectId} with audience {Audience} → {ReadinessStatus}",
                    packageId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    request.AudienceProfile,
                    detail.ReadinessStatus);

                return Task.FromResult(new CreateRegulatoryEvidencePackageResponse
                {
                    Success = true,
                    Package = summary,
                    IsIdempotentReplay = false
                });
            }
        }

        // ── GetPackageSummaryAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetPackageSummaryResponse> GetPackageSummaryAsync(
            string packageId, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Task.FromResult(new GetPackageSummaryResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_PACKAGE_ID",
                    ErrorMessage = "PackageId is required."
                });
            }

            lock (_lock)
            {
                if (!_summaries.TryGetValue(packageId, out var summary))
                {
                    return Task.FromResult(new GetPackageSummaryResponse
                    {
                        Success = false,
                        ErrorCode = "NOT_FOUND",
                        ErrorMessage = $"Evidence package '{LoggingHelper.SanitizeLogInput(packageId)}' was not found."
                    });
                }

                var result = ShallowCopySummary(summary);
                result.CorrelationId = correlationId ?? result.CorrelationId;

                return Task.FromResult(new GetPackageSummaryResponse
                {
                    Success = true,
                    Package = result
                });
            }
        }

        // ── GetPackageDetailAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetPackageDetailResponse> GetPackageDetailAsync(
            string packageId, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Task.FromResult(new GetPackageDetailResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_PACKAGE_ID",
                    ErrorMessage = "PackageId is required."
                });
            }

            lock (_lock)
            {
                if (!_details.TryGetValue(packageId, out var detail))
                {
                    return Task.FromResult(new GetPackageDetailResponse
                    {
                        Success = false,
                        ErrorCode = "NOT_FOUND",
                        ErrorMessage = $"Evidence package '{LoggingHelper.SanitizeLogInput(packageId)}' was not found."
                    });
                }

                var result = DeepCopyDetail(detail);
                result.CorrelationId = correlationId ?? result.CorrelationId;

                return Task.FromResult(new GetPackageDetailResponse
                {
                    Success = true,
                    Package = result
                });
            }
        }

        // ── ListPackagesAsync ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListEvidencePackagesResponse> ListPackagesAsync(
            string subjectId, int limit = 20, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return Task.FromResult(new ListEvidencePackagesResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required."
                });
            }

            limit = Math.Clamp(limit, 1, 100);

            lock (_lock)
            {
                if (!_subjectIndex.TryGetValue(subjectId, out var ids))
                {
                    return Task.FromResult(new ListEvidencePackagesResponse
                    {
                        Success = true,
                        Packages = new List<RegulatoryEvidencePackageSummary>(),
                        TotalCount = 0
                    });
                }

                var totalCount = ids.Count;
                var packages = ids
                    .Take(limit)
                    .Where(id => _summaries.ContainsKey(id))
                    .Select(id => ShallowCopySummary(_summaries[id]))
                    .ToList();

                return Task.FromResult(new ListEvidencePackagesResponse
                {
                    Success = true,
                    Packages = packages,
                    TotalCount = totalCount
                });
            }
        }

        // ── Package assembly ──────────────────────────────────────────────────

        /// <summary>
        /// Assembles the full canonical package detail including manifest, KYC/AML summary,
        /// contradictions, remediation items, approval history, posture transitions, and rationale.
        /// </summary>
        private RegulatoryEvidencePackageDetail AssemblePackageDetail(
            string packageId,
            string subjectId,
            RegulatoryAudienceProfile audience,
            string? requestorNotes,
            DateTime? evidenceFromTimestamp,
            string correlationId,
            DateTime now)
        {
            // Build manifest sources
            var sources = BuildManifestSources(subjectId, now, evidenceFromTimestamp);

            // Evaluate source statistics
            var missingRequired = sources.Where(s => s.IsRequired && s.Availability == RegSourceAvailability.Missing).ToList();
            var stale = sources.Where(s => s.Availability == RegSourceAvailability.Stale).ToList();
            var unavailable = sources.Where(s => s.Availability == RegSourceAvailability.ProviderUnavailable).ToList();

            // Build KYC/AML summary
            var kycAmlSummary = BuildKycAmlSummary(subjectId, sources, now);

            // Build contradictions (fail-closed: any active contradiction blocks readiness)
            var contradictions = BuildContradictions(subjectId, sources, now);
            var openContradictions = contradictions.Where(c => !c.IsResolved).ToList();

            // Build remediation items
            var remediationItems = BuildRemediationItems(missingRequired, stale, openContradictions, kycAmlSummary);

            // Build approval history
            var approvalHistory = BuildApprovalHistory(subjectId, now);

            // Determine readiness status (fail-closed priority order)
            var readinessStatus = DetermineReadiness(
                missingRequired, stale, openContradictions, approvalHistory, kycAmlSummary);

            // Build posture transitions log
            var postureTransitions = BuildPostureTransitions(readinessStatus, now, missingRequired, stale, openContradictions);

            // Build readiness rationale
            var rationale = BuildReadinessRationale(
                readinessStatus, missingRequired, stale, openContradictions, remediationItems, kycAmlSummary);

            // Build audience rules list
            var audienceRules = BuildAudienceRules(audience);

            // Compute payload hash (canonical, audience-independent)
            var payloadHash = ComputePayloadHash(subjectId, sources, now);

            var manifest = new RegPackageManifest
            {
                TotalSourceRecords = sources.Count,
                AvailableSourceCount = sources.Count(s =>
                    s.Availability == RegSourceAvailability.Available ||
                    s.Availability == RegSourceAvailability.NearingExpiry),
                MissingRequiredCount = missingRequired.Count,
                StaleSourceCount = stale.Count,
                UnavailableSourceCount = unavailable.Count,
                Sources = sources,
                AudienceProfile = audience,
                AppliedAudienceRules = audienceRules,
                SchemaVersion = SchemaVersion,
                AssembledAt = now,
                PayloadHash = payloadHash
            };

            return new RegulatoryEvidencePackageDetail
            {
                PackageId = packageId,
                SubjectId = subjectId,
                AudienceProfile = audience,
                PackageStatus = RegPackageStatus.Assembled,
                ReadinessStatus = readinessStatus,
                ReadinessRationale = rationale,
                GeneratedAt = now,
                ExpiresAt = now.Add(FreshnessWindow),
                Manifest = manifest,
                KycAmlSummary = kycAmlSummary,
                Contradictions = contradictions,
                RemediationItems = remediationItems,
                ApprovalHistory = approvalHistory,
                PostureTransitions = postureTransitions,
                RequestorNotes = requestorNotes,
                SchemaVersion = SchemaVersion,
                CorrelationId = correlationId
            };
        }

        // ── Source builder ────────────────────────────────────────────────────

        private List<RegEvidenceSourceEntry> BuildManifestSources(
            string subjectId, DateTime now, DateTime? fromTimestamp)
        {
            // In this in-memory implementation we produce deterministic simulated sources
            // based on the subjectId hash to ensure stable test output.
            // In a production implementation these would be fetched from KycAmlDecisionIngestionService,
            // ComplianceCaseManagementService, and ApprovalWorkflowService.

            var seed = subjectId.GetHashCode();
            var rng = new Random(Math.Abs(seed));

            var sources = new List<RegEvidenceSourceEntry>();

            // KYC decision
            var kycAge = TimeSpan.FromDays(rng.Next(1, 200));
            var kycCollected = now - kycAge;
            sources.Add(new RegEvidenceSourceEntry
            {
                SourceId = $"kyc-{subjectId}-001",
                Kind = RegEvidenceSourceKind.KycDecision,
                DisplayName = "KYC Identity Verification",
                OriginSystem = "StripeIdentity",
                CollectedAt = kycCollected,
                ExpiresAt = kycCollected.Add(FreshnessWindow),
                Availability = ClassifyAvailability(kycCollected, kycCollected.Add(FreshnessWindow), now),
                IsRequired = true,
                Description = "Identity verification decision from Stripe Identity KYC provider.",
                DataHash = ComputeHash($"kyc:{subjectId}:{kycCollected:O}")
            });

            // AML decision
            var amlAge = TimeSpan.FromDays(rng.Next(1, 180));
            var amlCollected = now - amlAge;
            sources.Add(new RegEvidenceSourceEntry
            {
                SourceId = $"aml-{subjectId}-001",
                Kind = RegEvidenceSourceKind.AmlDecision,
                DisplayName = "AML Sanctions Screening",
                OriginSystem = "ComplyAdvantage",
                CollectedAt = amlCollected,
                ExpiresAt = amlCollected.Add(FreshnessWindow),
                Availability = ClassifyAvailability(amlCollected, amlCollected.Add(FreshnessWindow), now),
                IsRequired = true,
                Description = "AML and sanctions screening decision from ComplyAdvantage.",
                DataHash = ComputeHash($"aml:{subjectId}:{amlCollected:O}")
            });

            // Compliance case (required for RegulatorReview, optional otherwise)
            var caseCollected = now - TimeSpan.FromDays(rng.Next(1, 60));
            sources.Add(new RegEvidenceSourceEntry
            {
                SourceId = $"case-{subjectId}-001",
                Kind = RegEvidenceSourceKind.ComplianceCase,
                DisplayName = "Compliance Case Record",
                OriginSystem = "ComplianceCaseManagement",
                CollectedAt = caseCollected,
                ExpiresAt = null,
                Availability = RegSourceAvailability.Available,
                IsRequired = false,
                Description = "Active compliance case tracking evidence and escalations for this subject.",
                DataHash = ComputeHash($"case:{subjectId}:{caseCollected:O}")
            });

            // Approval workflow record
            var approvalCollected = now - TimeSpan.FromDays(rng.Next(1, 30));
            sources.Add(new RegEvidenceSourceEntry
            {
                SourceId = $"approval-{subjectId}-001",
                Kind = RegEvidenceSourceKind.ApprovalWorkflow,
                DisplayName = "Approval Workflow Stage Record",
                OriginSystem = "ApprovalWorkflow",
                CollectedAt = approvalCollected,
                ExpiresAt = null,
                Availability = RegSourceAvailability.Available,
                IsRequired = false,
                Description = "Most recent approval stage decision for this subject.",
                DataHash = ComputeHash($"approval:{subjectId}:{approvalCollected:O}")
            });

            // Launch decision record (required)
            var launchCollected = now - TimeSpan.FromDays(rng.Next(1, 45));
            sources.Add(new RegEvidenceSourceEntry
            {
                SourceId = $"launch-{subjectId}-001",
                Kind = RegEvidenceSourceKind.LaunchDecision,
                DisplayName = "Launch Readiness Decision",
                OriginSystem = "ComplianceEvidenceLaunchDecision",
                CollectedAt = launchCollected,
                ExpiresAt = launchCollected.Add(FreshnessWindow),
                Availability = ClassifyAvailability(launchCollected, launchCollected.Add(FreshnessWindow), now),
                IsRequired = true,
                Description = "Launch readiness evaluation for the token issuance.",
                DataHash = ComputeHash($"launch:{subjectId}:{launchCollected:O}")
            });

            return sources;
        }

        // ── KYC/AML summary builder ───────────────────────────────────────────

        private RegKycAmlSummary BuildKycAmlSummary(
            string subjectId, List<RegEvidenceSourceEntry> sources, DateTime now)
        {
            var kycSource = sources.FirstOrDefault(s => s.Kind == RegEvidenceSourceKind.KycDecision);
            var amlSource = sources.FirstOrDefault(s => s.Kind == RegEvidenceSourceKind.AmlDecision);

            var kycStatus = kycSource?.Availability == RegSourceAvailability.Available ? "Approved"
                          : kycSource?.Availability == RegSourceAvailability.Stale ? "Expired"
                          : kycSource == null ? "Missing"
                          : "Pending";

            var amlStatus = amlSource?.Availability == RegSourceAvailability.Available ? "Cleared"
                          : amlSource?.Availability == RegSourceAvailability.Stale ? "Expired"
                          : amlSource == null ? "Missing"
                          : "Pending";

            var passedAll = kycStatus == "Approved" && amlStatus == "Cleared";

            var seed = subjectId.GetHashCode();
            var rng = new Random(Math.Abs(seed));
            var amlScore = passedAll ? (double)(85 + rng.Next(0, 15)) : (double)rng.Next(20, 70);

            return new RegKycAmlSummary
            {
                SubjectId = subjectId,
                KycStatus = kycStatus,
                KycDecidedAt = kycSource?.CollectedAt,
                KycProvider = kycSource?.OriginSystem,
                KycExpiresAt = kycSource?.ExpiresAt,
                AmlStatus = amlStatus,
                AmlDecidedAt = amlSource?.CollectedAt,
                AmlProvider = amlSource?.OriginSystem,
                AmlExpiresAt = amlSource?.ExpiresAt,
                PassedAllChecks = passedAll,
                PostureSummary = passedAll
                    ? "KYC identity verified and AML sanctions screening cleared."
                    : $"Compliance posture incomplete: KYC={kycStatus}, AML={amlStatus}.",
                AmlConfidenceScore = amlScore,
                MatchedWatchlistCategories = new List<string>()
            };
        }

        // ── Contradiction builder ─────────────────────────────────────────────

        private List<RegContradictionItem> BuildContradictions(
            string subjectId, List<RegEvidenceSourceEntry> sources, DateTime now)
        {
            // In a production implementation, contradictions would be queried from
            // KycAmlDecisionIngestionService where NormalizedIngestionStatus == Contradiction.
            // For this in-memory implementation we produce deterministic results.
            var seed = subjectId.GetHashCode();
            var rng = new Random(Math.Abs(seed));

            // ~20% of subjects have an active contradiction (deterministic from seed)
            if (rng.Next(0, 5) != 0)
                return new List<RegContradictionItem>();

            var kycSource = sources.FirstOrDefault(s => s.Kind == RegEvidenceSourceKind.KycDecision);
            var amlSource = sources.FirstOrDefault(s => s.Kind == RegEvidenceSourceKind.AmlDecision);

            return new List<RegContradictionItem>
            {
                new RegContradictionItem
                {
                    ContradictionId = $"contradiction-{subjectId}-001",
                    CheckType = "IdentityKyc",
                    ConflictingSourceIds = new List<string>
                    {
                        kycSource?.SourceId ?? $"kyc-{subjectId}-001",
                        $"kyc-{subjectId}-prev"
                    },
                    Description = "Two KYC decisions for this subject produced conflicting outcomes. Manual review required.",
                    DetectedAt = now.AddDays(-5),
                    IsResolved = false
                }
            };
        }

        // ── Remediation item builder ──────────────────────────────────────────

        private List<RegRemediationItem> BuildRemediationItems(
            List<RegEvidenceSourceEntry> missingRequired,
            List<RegEvidenceSourceEntry> stale,
            List<RegContradictionItem> openContradictions,
            RegKycAmlSummary? kycAmlSummary = null)
        {
            var items = new List<RegRemediationItem>();

            foreach (var source in missingRequired)
            {
                items.Add(new RegRemediationItem
                {
                    RemediationId = $"rem-missing-{source.SourceId}",
                    Title = $"Missing required evidence: {source.DisplayName}",
                    Description = $"The required source '{source.DisplayName}' from '{source.OriginSystem}' " +
                                  "was not found. Package readiness cannot be established until this record is present.",
                    Severity = RegMissingDataSeverity.Blocker,
                    RelatedSourceIds = new List<string> { source.SourceId },
                    RemediationSteps = new List<string>
                    {
                        $"Initiate a new {source.Kind} check for this subject.",
                        $"Ensure the result is submitted to the {source.OriginSystem} ingestion endpoint.",
                        "Regenerate the evidence package after the record has been ingested."
                    },
                    OwnerHint = "compliance",
                    IsResolved = false
                });
            }

            foreach (var source in stale)
            {
                items.Add(new RegRemediationItem
                {
                    RemediationId = $"rem-stale-{source.SourceId}",
                    Title = $"Stale evidence: {source.DisplayName}",
                    Description = $"The source '{source.DisplayName}' from '{source.OriginSystem}' " +
                                  $"was collected on {source.CollectedAt:yyyy-MM-dd} and has exceeded " +
                                  "the 90-day freshness window.",
                    Severity = source.IsRequired ? RegMissingDataSeverity.Blocker : RegMissingDataSeverity.Warning,
                    RelatedSourceIds = new List<string> { source.SourceId },
                    RemediationSteps = new List<string>
                    {
                        $"Re-run the {source.Kind} check to obtain a fresh result.",
                        "Submit the updated result to the ingestion endpoint.",
                        "Regenerate the evidence package."
                    },
                    OwnerHint = "compliance",
                    IsResolved = false
                });
            }

            foreach (var contradiction in openContradictions)
            {
                items.Add(new RegRemediationItem
                {
                    RemediationId = $"rem-contradiction-{contradiction.ContradictionId}",
                    Title = $"Unresolved contradiction: {contradiction.CheckType}",
                    Description = $"A contradiction was detected between {contradiction.ConflictingSourceIds.Count} " +
                                  $"records for check type '{contradiction.CheckType}'. " +
                                  "Unresolved contradictions block package readiness.",
                    Severity = RegMissingDataSeverity.Blocker,
                    RelatedSourceIds = contradiction.ConflictingSourceIds,
                    RemediationSteps = new List<string>
                    {
                        "Review each conflicting source record and determine the authoritative outcome.",
                        "Submit a resolved decision through the KYC/AML ingestion API.",
                        "Close the contradiction record and regenerate the evidence package."
                    },
                    OwnerHint = "compliance",
                    IsResolved = false
                });
            }

            // Add KYC/AML remediation items when checks haven't passed
            if (kycAmlSummary != null && !kycAmlSummary.PassedAllChecks)
            {
                if (kycAmlSummary.KycStatus != "Approved")
                {
                    items.Add(new RegRemediationItem
                    {
                        RemediationId = $"rem-kyc-{kycAmlSummary.SubjectId}",
                        Title = $"KYC check not approved: current status is '{kycAmlSummary.KycStatus}'",
                        Description = $"The KYC (Know Your Customer) identity verification check for subject '{kycAmlSummary.SubjectId}' " +
                                      $"has not been approved. Current KYC status: {kycAmlSummary.KycStatus}. " +
                                      "Package readiness is blocked until KYC approval is obtained.",
                        Severity = RegMissingDataSeverity.Blocker,
                        RelatedSourceIds = new List<string>(),
                        RemediationSteps = new List<string>
                        {
                            "Initiate or re-run a KYC identity verification check for this subject.",
                            "Ensure the KYC decision record is submitted to the ingestion endpoint.",
                            "Regenerate the evidence package after KYC approval is confirmed."
                        },
                        OwnerHint = "compliance",
                        IsResolved = false
                    });
                }

                if (kycAmlSummary.AmlStatus != "Cleared")
                {
                    items.Add(new RegRemediationItem
                    {
                        RemediationId = $"rem-aml-{kycAmlSummary.SubjectId}",
                        Title = $"AML screening not cleared: current status is '{kycAmlSummary.AmlStatus}'",
                        Description = $"The AML (Anti-Money Laundering) sanctions screening for subject '{kycAmlSummary.SubjectId}' " +
                                      $"has not been cleared. Current AML status: {kycAmlSummary.AmlStatus}. " +
                                      "Package readiness is blocked until AML screening is cleared.",
                        Severity = RegMissingDataSeverity.Blocker,
                        RelatedSourceIds = new List<string>(),
                        RemediationSteps = new List<string>
                        {
                            "Initiate or re-run an AML sanctions screening check for this subject.",
                            "Ensure the AML decision record is submitted to the ingestion endpoint.",
                            "Regenerate the evidence package after AML screening is cleared."
                        },
                        OwnerHint = "compliance",
                        IsResolved = false
                    });
                }
            }

            return items;
        }

        // ── Approval history builder ──────────────────────────────────────────

        private List<RegApprovalHistoryEntry> BuildApprovalHistory(string subjectId, DateTime now)
        {
            // In a production implementation this would be queried from ApprovalWorkflowService.
            // For the in-memory implementation we produce deterministic history.
            var seed = subjectId.GetHashCode();
            var rng = new Random(Math.Abs(seed));

            var history = new List<RegApprovalHistoryEntry>();

            // All subjects have a ComplianceReview entry
            var reviewDecision = rng.Next(0, 3) == 0 ? "NeedsMoreEvidence" : "Approved";
            history.Add(new RegApprovalHistoryEntry
            {
                EntryId = $"approval-entry-{subjectId}-001",
                Stage = "ComplianceReview",
                Decision = reviewDecision,
                DecidedBy = reviewDecision == "Approved" ? "compliance-reviewer-system" : "compliance-reviewer-001",
                DecidedAt = now - TimeSpan.FromDays(rng.Next(1, 30)),
                Rationale = reviewDecision == "Approved"
                    ? "All required evidence reviewed and found compliant. Approved for next stage."
                    : "Additional evidence required before approval can be granted.",
                IsLatestForStage = true
            });

            // ~70% have an ExecutiveSignOff entry
            if (rng.Next(0, 10) < 7 && reviewDecision == "Approved")
            {
                history.Add(new RegApprovalHistoryEntry
                {
                    EntryId = $"approval-entry-{subjectId}-002",
                    Stage = "ExecutiveSignOff",
                    Decision = "Approved",
                    DecidedBy = "exec-compliance-officer",
                    DecidedAt = now - TimeSpan.FromDays(rng.Next(1, 10)),
                    Rationale = "Reviewed compliance package; satisfied with evidence quality. Approved for issuance.",
                    IsLatestForStage = true
                });
            }

            // Sort oldest first
            history.Sort((a, b) => a.DecidedAt.CompareTo(b.DecidedAt));
            return history;
        }

        // ── Readiness determination ───────────────────────────────────────────

        private static RegPackageReadinessStatus DetermineReadiness(
            List<RegEvidenceSourceEntry> missingRequired,
            List<RegEvidenceSourceEntry> stale,
            List<RegContradictionItem> openContradictions,
            List<RegApprovalHistoryEntry> approvalHistory,
            RegKycAmlSummary kycAmlSummary)
        {
            // Priority 1: Unresolved contradictions → Blocked
            if (openContradictions.Any())
                return RegPackageReadinessStatus.Blocked;

            // Priority 2: Missing required sources → Incomplete
            if (missingRequired.Any())
                return RegPackageReadinessStatus.Incomplete;

            // Priority 3: Required stale sources → Stale
            var requiredStale = stale.Where(s => s.IsRequired).ToList();
            if (requiredStale.Any())
                return RegPackageReadinessStatus.Stale;

            // Priority 4: KYC/AML checks not passed → Blocked
            if (!kycAmlSummary.PassedAllChecks)
                return RegPackageReadinessStatus.Blocked;

            // Priority 5: Approval stage pending review → RequiresReview
            var latestApproval = approvalHistory.LastOrDefault();
            if (latestApproval != null && latestApproval.Decision == "NeedsMoreEvidence")
                return RegPackageReadinessStatus.RequiresReview;

            // All checks passed → Ready
            return RegPackageReadinessStatus.Ready;
        }

        // ── Posture transition builder ────────────────────────────────────────

        private List<RegReadinessPostureTransition> BuildPostureTransitions(
            RegPackageReadinessStatus finalStatus,
            DateTime now,
            List<RegEvidenceSourceEntry> missingRequired,
            List<RegEvidenceSourceEntry> stale,
            List<RegContradictionItem> openContradictions)
        {
            var transitions = new List<RegReadinessPostureTransition>();

            // Record an initial "Incomplete" state when package was created
            if (finalStatus != RegPackageReadinessStatus.Incomplete)
            {
                transitions.Add(new RegReadinessPostureTransition
                {
                    FromStatus = RegPackageReadinessStatus.Incomplete,
                    ToStatus = finalStatus == RegPackageReadinessStatus.Ready
                        ? RegPackageReadinessStatus.RequiresReview
                        : finalStatus,
                    TransitionedAt = now - TimeSpan.FromDays(60),
                    Reason = "Initial evidence collection completed.",
                    TriggerEvent = "EVIDENCE_COLLECTED"
                });
            }

            if (finalStatus == RegPackageReadinessStatus.Ready)
            {
                transitions.Add(new RegReadinessPostureTransition
                {
                    FromStatus = RegPackageReadinessStatus.RequiresReview,
                    ToStatus = RegPackageReadinessStatus.Ready,
                    TransitionedAt = now - TimeSpan.FromDays(5),
                    Reason = "All required evidence verified, KYC/AML cleared, and approval workflow completed.",
                    TriggerEvent = "APPROVAL_COMPLETED"
                });
            }

            return transitions;
        }

        // ── Readiness rationale builder ───────────────────────────────────────

        private static RegReadinessRationale BuildReadinessRationale(
            RegPackageReadinessStatus status,
            List<RegEvidenceSourceEntry> missingRequired,
            List<RegEvidenceSourceEntry> stale,
            List<RegContradictionItem> openContradictions,
            List<RegRemediationItem> remediationItems,
            RegKycAmlSummary kycAmlSummary)
        {
            return status switch
            {
                RegPackageReadinessStatus.Ready => new RegReadinessRationale
                {
                    Headline = "Package is regulator-ready: all required evidence is present, current, and validated.",
                    Detail = "All mandatory source records are available and within the 90-day freshness window. " +
                             "KYC identity verification and AML sanctions screening both passed. " +
                             "No unresolved contradictions. Approval workflow completed.",
                    BlockingSourceIds = new List<string>(),
                    MissingRequiredSourceIds = new List<string>(),
                    StaleSourceIds = new List<string>(),
                    UnresolvedContradictionIds = new List<string>(),
                    RecommendedNextSteps = new List<string> { "Package is ready. No action required." }
                },

                RegPackageReadinessStatus.Blocked => new RegReadinessRationale
                {
                    Headline = openContradictions.Any()
                        ? $"Package is blocked: {openContradictions.Count} unresolved contradiction(s) detected."
                        : "Package is blocked: KYC or AML checks have not passed.",
                    Detail = openContradictions.Any()
                        ? "Unresolved contradictions prevent package readiness. " +
                          "Each contradiction must be reviewed and resolved before this package can be considered regulator-ready."
                        : $"KYC status: {kycAmlSummary.KycStatus}. AML status: {kycAmlSummary.AmlStatus}. " +
                          "All compliance checks must pass before the package can be marked ready.",
                    BlockingSourceIds = openContradictions.SelectMany(c => c.ConflictingSourceIds).Distinct().ToList(),
                    MissingRequiredSourceIds = missingRequired.Select(s => s.SourceId).ToList(),
                    StaleSourceIds = stale.Select(s => s.SourceId).ToList(),
                    UnresolvedContradictionIds = openContradictions.Select(c => c.ContradictionId).ToList(),
                    RecommendedNextSteps = remediationItems
                        .Where(r => !r.IsResolved && r.Severity == RegMissingDataSeverity.Blocker)
                        .SelectMany(r => r.RemediationSteps)
                        .Take(5)
                        .ToList()
                },

                RegPackageReadinessStatus.Incomplete => new RegReadinessRationale
                {
                    Headline = $"Package is incomplete: {missingRequired.Count} required source(s) are missing.",
                    Detail = "The following required evidence source records were not found during package assembly. " +
                             "Readiness cannot be established until all required records are present: " +
                             string.Join(", ", missingRequired.Select(s => s.DisplayName)) + ".",
                    BlockingSourceIds = missingRequired.Select(s => s.SourceId).ToList(),
                    MissingRequiredSourceIds = missingRequired.Select(s => s.SourceId).ToList(),
                    StaleSourceIds = new List<string>(),
                    UnresolvedContradictionIds = new List<string>(),
                    RecommendedNextSteps = new List<string>
                    {
                        "Ensure all required compliance checks have been completed and ingested.",
                        "Re-submit missing source records through the appropriate ingestion APIs.",
                        "Regenerate the evidence package after all required sources are available."
                    }
                },

                RegPackageReadinessStatus.Stale => new RegReadinessRationale
                {
                    Headline = $"Package readiness is downgraded: {stale.Count(s => s.IsRequired)} required source(s) are stale.",
                    Detail = "One or more required evidence sources have exceeded the 90-day freshness window. " +
                             "Stale records are not acceptable for regulator submission without renewal.",
                    BlockingSourceIds = stale.Where(s => s.IsRequired).Select(s => s.SourceId).ToList(),
                    MissingRequiredSourceIds = new List<string>(),
                    StaleSourceIds = stale.Select(s => s.SourceId).ToList(),
                    UnresolvedContradictionIds = new List<string>(),
                    RecommendedNextSteps = new List<string>
                    {
                        "Re-run expired compliance checks to produce fresh source records.",
                        "Submit fresh results through the ingestion APIs.",
                        "Regenerate the evidence package to restore Ready status."
                    }
                },

                RegPackageReadinessStatus.RequiresReview => new RegReadinessRationale
                {
                    Headline = "Package requires manual review before it can be considered regulator-ready.",
                    Detail = "The approval workflow has one or more stages pending manual review. " +
                             "Evidence is complete and current, but human sign-off is required before final readiness can be established.",
                    BlockingSourceIds = new List<string>(),
                    MissingRequiredSourceIds = new List<string>(),
                    StaleSourceIds = new List<string>(),
                    UnresolvedContradictionIds = new List<string>(),
                    RecommendedNextSteps = new List<string>
                    {
                        "Complete the pending approval workflow stage.",
                        "Ensure all reviewers have acknowledged the evidence package.",
                        "Regenerate the package after approval is confirmed."
                    }
                },

                _ => new RegReadinessRationale
                {
                    Headline = "Readiness status is unknown.",
                    Detail = "Package readiness could not be determined.",
                    RecommendedNextSteps = new List<string> { "Regenerate the package." }
                }
            };
        }

        // ── Audience rules builder ────────────────────────────────────────────

        private static List<string> BuildAudienceRules(RegulatoryAudienceProfile audience)
        {
            return audience switch
            {
                RegulatoryAudienceProfile.InternalCompliance => new List<string>
                {
                    "InternalNotes=Included",
                    "RemediationSteps=Included",
                    "TechnicalDetail=Included",
                    "ReviewerNotes=Included"
                },
                RegulatoryAudienceProfile.ExecutiveSignOff => new List<string>
                {
                    "InternalNotes=Redacted",
                    "RemediationSteps=Summarized",
                    "TechnicalDetail=Excluded",
                    "ReviewerNotes=Redacted"
                },
                RegulatoryAudienceProfile.ExternalAuditor => new List<string>
                {
                    "InternalNotes=Excluded",
                    "RemediationSteps=Included",
                    "TechnicalDetail=Included",
                    "ReviewerNotes=Excluded"
                },
                RegulatoryAudienceProfile.RegulatorReview => new List<string>
                {
                    "InternalNotes=Included",
                    "RemediationSteps=Included",
                    "TechnicalDetail=Included",
                    "ReviewerNotes=Included",
                    "CanonicalRecord=NoRedactions",
                    "IntegrityHash=Required"
                },
                _ => new List<string>()
            };
        }

        // ── Summary builder ───────────────────────────────────────────────────

        private static RegulatoryEvidencePackageSummary BuildSummary(
            RegulatoryEvidencePackageDetail detail, string correlationId)
        {
            return new RegulatoryEvidencePackageSummary
            {
                PackageId = detail.PackageId,
                SubjectId = detail.SubjectId,
                AudienceProfile = detail.AudienceProfile,
                PackageStatus = detail.PackageStatus,
                ReadinessStatus = detail.ReadinessStatus,
                ReadinessHeadline = detail.ReadinessRationale.Headline,
                GeneratedAt = detail.GeneratedAt,
                ExpiresAt = detail.ExpiresAt,
                TotalSourceRecords = detail.Manifest.TotalSourceRecords,
                MissingRequiredCount = detail.Manifest.MissingRequiredCount,
                StaleSourceCount = detail.Manifest.StaleSourceCount,
                OpenContradictionCount = detail.Contradictions.Count(c => !c.IsResolved),
                OpenRemediationCount = detail.RemediationItems.Count(r => !r.IsResolved),
                HasApprovalHistory = detail.ApprovalHistory.Any(),
                SchemaVersion = detail.SchemaVersion,
                CorrelationId = correlationId
            };
        }

        // ── Availability classifier ───────────────────────────────────────────

        private static RegSourceAvailability ClassifyAvailability(
            DateTime collectedAt, DateTime? expiresAt, DateTime now)
        {
            if (expiresAt.HasValue && now > expiresAt.Value)
                return RegSourceAvailability.Stale;

            if (expiresAt.HasValue && now > expiresAt.Value - NearExpiryWindow)
                return RegSourceAvailability.NearingExpiry;

            return RegSourceAvailability.Available;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string ComputeHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string ComputePayloadHash(
            string subjectId,
            List<RegEvidenceSourceEntry> sources,
            DateTime now)
        {
            var payload = JsonSerializer.Serialize(new
            {
                subjectId,
                sourceIds = sources.Select(s => s.SourceId).OrderBy(x => x).ToList(),
                generatedAt = now.ToString("O")
            });
            return ComputeHash(payload);
        }

        private static RegulatoryEvidencePackageSummary ShallowCopySummary(
            RegulatoryEvidencePackageSummary src)
        {
            return new RegulatoryEvidencePackageSummary
            {
                PackageId = src.PackageId,
                SubjectId = src.SubjectId,
                AudienceProfile = src.AudienceProfile,
                PackageStatus = src.PackageStatus,
                ReadinessStatus = src.ReadinessStatus,
                ReadinessHeadline = src.ReadinessHeadline,
                GeneratedAt = src.GeneratedAt,
                ExpiresAt = src.ExpiresAt,
                TotalSourceRecords = src.TotalSourceRecords,
                MissingRequiredCount = src.MissingRequiredCount,
                StaleSourceCount = src.StaleSourceCount,
                OpenContradictionCount = src.OpenContradictionCount,
                OpenRemediationCount = src.OpenRemediationCount,
                HasApprovalHistory = src.HasApprovalHistory,
                SchemaVersion = src.SchemaVersion,
                CorrelationId = src.CorrelationId
            };
        }

        private static RegulatoryEvidencePackageDetail DeepCopyDetail(
            RegulatoryEvidencePackageDetail src)
        {
            // Use JSON serialization for a proper deep copy without introducing
            // a third-party deep-clone library.
            var json = JsonSerializer.Serialize(src);
            return JsonSerializer.Deserialize<RegulatoryEvidencePackageDetail>(json)!;
        }
    }
}
