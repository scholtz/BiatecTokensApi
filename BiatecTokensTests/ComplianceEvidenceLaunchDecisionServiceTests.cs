using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive unit tests for ComplianceEvidenceLaunchDecisionService covering:
    ///
    /// AC1  – Authenticated API for compliance evidence-bundle summary
    /// AC2  – Canonical evidence model (strict sign-off status, release-grade, policy snapshot,
    ///         attestation metadata, audit trail references, evidence freshness, remediation guidance)
    /// AC3  – Downloadable machine-readable artifacts: JSON required, CSV preferred
    /// AC4  – Explicit release-grade vs permissive/incomplete evidence distinction
    /// AC5  – Fail-closed responses with actionable diagnostics for missing/stale/inconsistent evidence
    /// AC6  – Authorization (integration tests cover HTTP layer; service tests cover input validation)
    /// AC7  – Logging and observability (service logs outcomes without secrets)
    /// AC9  – Tests cover bundle assembly, release-grade classification, fail-closed behavior,
    ///         export generation (JSON + CSV), and authorization validation
    /// </summary>
    [TestFixture]
    public class ComplianceEvidenceLaunchDecisionServiceTests
    {
        private static ComplianceEvidenceLaunchDecisionService CreateService()
            => new(NullLogger<ComplianceEvidenceLaunchDecisionService>.Instance);

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static LaunchDecisionRequest MakeRequest(
            string ownerId = "owner-001",
            string tokenStandard = "ASA",
            string network = "testnet",
            string? idempotencyKey = null,
            string? policyVersion = null,
            bool forceRefresh = false) => new()
        {
            OwnerId = ownerId,
            TokenStandard = tokenStandard,
            Network = network,
            IdempotencyKey = idempotencyKey,
            PolicyVersion = policyVersion,
            ForceRefresh = forceRefresh
        };

        private static EvidenceBundleRequest BundleRequest(string ownerId, string? decisionId = null) => new()
        {
            OwnerId = ownerId,
            DecisionId = decisionId
        };

        private static EvidenceExportRequest ExportRequest(string ownerId, int limit = 100) => new()
        {
            OwnerId = ownerId,
            Limit = limit
        };

        // ═══════════════════════════════════════════════════════════════════════
        // AC5/AC9 – Input validation (fail-closed for bad inputs)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateLaunchDecision_MissingOwnerId_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: ""));
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
            Assert.That(resp.ErrorMessage, Does.Contain("OwnerId"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_MissingTokenStandard_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(tokenStandard: ""));
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_InvalidTokenStandard_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(tokenStandard: "INVALID"));
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
            Assert.That(resp.ErrorMessage, Does.Contain("INVALID"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_MissingNetwork_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(network: ""));
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_InvalidNetwork_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(network: "mynet"));
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_NETWORK"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC2/AC9 – Bundle assembly
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnetASA_ReturnsSuccessDecision()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest());

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.PolicyVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(resp.EvidenceSummary, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_AllKnownTokenStandards_Succeed()
        {
            var svc = CreateService();
            foreach (var standard in new[] { "ASA", "ARC3", "ARC200", "ERC20" })
            {
                var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(tokenStandard: standard));
                Assert.That(resp.Success, Is.True, $"Expected success for standard={standard}");
            }
        }

        [Test]
        public async Task EvaluateLaunchDecision_AllKnownNetworks_Succeed()
        {
            var svc = CreateService();
            foreach (var net in new[] { "testnet", "betanet", "base-testnet" })
            {
                var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(network: net));
                Assert.That(resp.Success, Is.True, $"Expected success for network={net}");
            }
        }

        [Test]
        public async Task EvaluateLaunchDecision_DecisionContainsEvidenceSummary()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest());

            Assert.That(resp.EvidenceSummary, Has.Count.GreaterThan(0));
            foreach (var item in resp.EvidenceSummary)
            {
                Assert.That(item.EvidenceId, Is.Not.Null.And.Not.Empty);
                Assert.That(item.Rationale, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public async Task EvaluateLaunchDecision_DecisionTimestampIsRecent()
        {
            var before = DateTime.UtcNow.AddSeconds(-5);
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest());
            Assert.That(resp.DecidedAt, Is.GreaterThan(before));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC4/AC9 – Release-grade classification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateLaunchDecision_TestnetASA_IsNotReleaseGradeWhenBlockers()
        {
            // ARC1400 requires Premium – generates a blocker → not release-grade
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(tokenStandard: "ARC1400", network: "testnet"));

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.IsReleaseGradeEvidence, Is.False);
            Assert.That(resp.ReleaseGradeNote, Does.Contain("Not release-grade").Or.Contain("not release-grade").IgnoreCase);
        }

        [Test]
        public async Task EvaluateLaunchDecision_TestnetASA_HasReleaseGradeFields()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest());

            // Fields must be present regardless of value
            Assert.That(resp.ReleaseGradeNote, Is.Not.Null);
        }

        [Test]
        public async Task EvaluateLaunchDecision_MainnetLaunch_HasWarningAndReleaseGradeFields()
        {
            var svc = CreateService();
            // Mainnet launch adds KYC advisory warning – still may be release-grade depending on status
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(network: "mainnet"));

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Warnings, Has.Count.GreaterThan(0));
            Assert.That(resp.ReleaseGradeNote, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_StalePolicy_IsNotReleaseGrade()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest(policyVersion: "2024.01.01.0"));

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.IsReleaseGradeEvidence, Is.False,
                "A stale policy version should prevent release-grade status.");
            Assert.That(resp.ReleaseGradeNote, Does.Contain("Not release-grade").Or.Contain("not release-grade").IgnoreCase);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ReleaseGradeNoteIsDescriptive()
        {
            var svc = CreateService();
            var blocked = await svc.EvaluateLaunchDecisionAsync(MakeRequest(tokenStandard: "ARC1400"));
            // Verify the note explains the specific failure, not just "validation failed"
            Assert.That(blocked.ReleaseGradeNote.Length, Is.GreaterThan(20),
                "ReleaseGradeNote must be descriptive, not a generic placeholder.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC5/AC9 – Fail-closed behavior
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEvidenceBundle_MissingOwnerId_ReturnsFail()
        {
            var svc = CreateService();
            var resp = await svc.GetEvidenceBundleAsync(BundleRequest(""));

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task GetEvidenceBundle_InvalidLimit_ReturnsFail()
        {
            var svc = CreateService();
            var resp = await svc.GetEvidenceBundleAsync(new EvidenceBundleRequest
            {
                OwnerId = "owner-001",
                Limit = 0
            });
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task GetEvidenceBundle_NoEvidence_IsNotReleaseGrade()
        {
            var svc = CreateService();
            var resp = await svc.GetEvidenceBundleAsync(BundleRequest("unknown-owner-xyz"));

            // An empty bundle must not be labeled as release-grade
            Assert.That(resp.Success, Is.True);
            Assert.That(resp.IsReleaseGradeEvidence, Is.False,
                "An empty evidence bundle must never be labeled release-grade.");
            Assert.That(resp.RemediationGuidance, Is.Not.Empty,
                "An empty bundle must include remediation guidance.");
        }

        [Test]
        public async Task GetEvidenceBundle_NoEvidence_HasActionableDiagnostics()
        {
            var svc = CreateService();
            var resp = await svc.GetEvidenceBundleAsync(BundleRequest("no-evidence-owner"));

            Assert.That(resp.IsReleaseGradeEvidence, Is.False);
            // Must provide actionable explanation, not just "validation failed"
            Assert.That(resp.ReleaseGradeNote, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.RemediationGuidance, Is.Not.Null);
            Assert.That(resp.RemediationGuidance.Any(r => r.Length > 10), Is.True,
                "Remediation guidance must contain actionable steps.");
        }

        [Test]
        public async Task GetDecisionTrace_MissingDecisionId_ReturnsFail()
        {
            var svc = CreateService();
            var resp = await svc.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = "" });

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_DECISION_ID"));
        }

        [Test]
        public async Task GetDecisionTrace_UnknownDecisionId_ReturnsFail()
        {
            var svc = CreateService();
            var resp = await svc.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = "nonexistent-id" });

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("DECISION_NOT_FOUND"));
        }

        [Test]
        public async Task GetDecision_UnknownId_ReturnsNull()
        {
            var svc = CreateService();
            var resp = await svc.GetDecisionAsync("nonexistent-id");
            Assert.That(resp, Is.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC2/AC9 – Bundle fields: policy snapshot, attestation, audit trail
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEvidenceBundle_AfterDecision_IncludesPolicySnapshot()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-snap"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-snap"));

            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.PolicySnapshot, Is.Not.Null);
            Assert.That(bundle.PolicySnapshot!.PolicyVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(bundle.PolicySnapshot.IsCurrent, Is.True);
        }

        [Test]
        public async Task GetEvidenceBundle_AfterDecision_IncludesAuditTrailReferences()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-audit"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-audit"));

            Assert.That(bundle.AuditTrailReferences, Is.Not.Null);
            if (bundle.AuditTrailReferences.Any())
            {
                var firstRef = bundle.AuditTrailReferences[0];
                Assert.That(firstRef.AuditId, Is.Not.Null.And.Not.Empty);
                Assert.That(firstRef.EventType, Is.Not.Null.And.Not.Empty);
                Assert.That(firstRef.OccurredAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)));
            }
        }

        [Test]
        public async Task GetEvidenceBundle_AfterDecision_IncludesExportManifest()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-mfst"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-mfst"));

            Assert.That(bundle.ExportManifest, Is.Not.Null);
            Assert.That(bundle.ExportManifest!.ExportId, Is.Not.Null.And.Not.Empty);
            Assert.That(bundle.ExportManifest.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(bundle.ExportManifest.ActivePolicyVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(bundle.ExportManifest.GeneratedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
        }

        [Test]
        public async Task GetEvidenceBundle_AfterDecision_FreshnessIsFresh()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-fresh"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-fresh"));

            // Just-generated evidence should be Fresh
            Assert.That(bundle.FreshnessStatus, Is.EqualTo(EvidenceFreshnessStatus.Fresh));
        }

        [Test]
        public async Task GetEvidenceBundle_ManifestItemCountMatchesEvidence()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-count"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-count"));

            Assert.That(bundle.ExportManifest, Is.Not.Null);
            Assert.That(bundle.ExportManifest!.EvidenceItemCount, Is.EqualTo(bundle.Items.Count));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC3/AC9 – JSON export generation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExportJson_MissingOwnerId_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest(""));
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task ExportJson_NoEvidence_ReturnsSuccessWithEmptyBundle()
        {
            // No evaluation run for this owner – export should succeed with empty evidence
            var svc = CreateService();
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-owner-nodata"));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Content, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ContentType, Is.EqualTo("application/json"));
            Assert.That(result.FileName, Does.EndWith(".json"));
        }

        [Test]
        public async Task ExportJson_AfterDecision_ReturnsPopulatedJson()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-owner-1"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-owner-1"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Content, Is.Not.Null);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            // Validate required JSON keys are present
            Assert.That(json, Does.Contain("schemaVersion"));
            Assert.That(json, Does.Contain("isReleaseGradeEvidence"));
            Assert.That(json, Does.Contain("releaseGradeNote"));
            Assert.That(json, Does.Contain("freshnessStatus"));
            Assert.That(json, Does.Contain("policySnapshot"));
            Assert.That(json, Does.Contain("evidenceItems"));
            Assert.That(json, Does.Contain("exportManifest"));
        }

        [Test]
        public async Task ExportJson_ContainsAttestationRecords()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-owner-att"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-owner-att"));

            Assert.That(result.Success, Is.True);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            Assert.That(json, Does.Contain("attestationRecords"));
        }

        [Test]
        public async Task ExportJson_ContainsAuditTrailReferences()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-owner-aud"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-owner-aud"));

            Assert.That(result.Success, Is.True);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            Assert.That(json, Does.Contain("auditTrailReferences"));
        }

        [Test]
        public async Task ExportJson_ManifestIncludesPayloadHash()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-owner-hash"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-owner-hash"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Manifest, Is.Not.Null);
            Assert.That(result.Manifest!.PayloadHash, Is.Not.Null.And.Not.Empty,
                "Export manifest must include a payload hash for integrity verification.");
        }

        [Test]
        public async Task ExportJson_SchemaVersionInPayload()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-schema-owner"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-schema-owner"));

            Assert.That(result.Success, Is.True);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            Assert.That(json, Does.Contain("\"schemaVersion\""),
                "JSON export must be versioned for downstream schema evolution.");
        }

        [Test]
        public async Task ExportJson_IsReleaseGradeEvidenceField_IsPresentInPayload()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "export-grade-owner"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("export-grade-owner"));

            Assert.That(result.Success, Is.True);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            // Must explicitly include the release-grade field
            Assert.That(json, Does.Contain("\"isReleaseGradeEvidence\""),
                "JSON export must include isReleaseGradeEvidence field (AC4 contract).");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC3/AC9 – CSV export generation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExportCsv_MissingOwnerId_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest(""));
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task ExportCsv_AfterDecision_ReturnsCsv()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "csv-owner-1"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("csv-owner-1"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Content, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ContentType, Is.EqualTo("text/csv"));
            Assert.That(result.FileName, Does.EndWith(".csv"));
        }

        [Test]
        public async Task ExportCsv_ContainsExpectedHeaders()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "csv-header-owner"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("csv-header-owner"));

            Assert.That(result.Success, Is.True);
            var csv = System.Text.Encoding.UTF8.GetString(result.Content!);
            // Required column headers for auditor consumption
            Assert.That(csv, Does.Contain("EvidenceId"));
            Assert.That(csv, Does.Contain("Category"));
            Assert.That(csv, Does.Contain("Source"));
            Assert.That(csv, Does.Contain("Timestamp"));
            Assert.That(csv, Does.Contain("ValidationStatus"));
            Assert.That(csv, Does.Contain("Rationale"));
        }

        [Test]
        public async Task ExportCsv_ContainsBundleMetadataInHeader()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "csv-meta-owner"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("csv-meta-owner"));

            Assert.That(result.Success, Is.True);
            var csv = System.Text.Encoding.UTF8.GetString(result.Content!);
            // Bundle header section
            Assert.That(csv, Does.Contain("Release Grade"));
            Assert.That(csv, Does.Contain("Policy Version"));
        }

        [Test]
        public async Task ExportCsv_RemediationSection_IncludedWhenBundleNotReleaseGrade()
        {
            var svc = CreateService();
            // ARC1400 generates a blocker → bundle is not release-grade → remediation must be in CSV
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "csv-remediation-owner", tokenStandard: "ARC1400"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("csv-remediation-owner"));

            Assert.That(result.Success, Is.True);
            var csv = System.Text.Encoding.UTF8.GetString(result.Content!);
            Assert.That(csv, Does.Contain("Remediation"),
                "CSV export must include a remediation section when bundle is not release-grade.");
        }

        [Test]
        public async Task ExportCsv_ManifestIncludesPayloadHash()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "csv-hash-owner"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("csv-hash-owner"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Manifest?.PayloadHash, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC9 – Idempotency
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateLaunchDecision_SameIdempotencyKey_ReturnsCachedResult()
        {
            var svc = CreateService();
            var key = Guid.NewGuid().ToString();
            var r1 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(idempotencyKey: key));
            var r2 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(idempotencyKey: key));

            Assert.That(r2.DecisionId, Is.EqualTo(r1.DecisionId),
                "Same idempotency key must return the same DecisionId.");
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ForceRefresh_BypassesIdempotencyCache()
        {
            var svc = CreateService();
            var key = Guid.NewGuid().ToString();
            var r1 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(idempotencyKey: key));
            var r2 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(idempotencyKey: key, forceRefresh: true));

            Assert.That(r2.DecisionId, Is.Not.EqualTo(r1.DecisionId),
                "ForceRefresh must produce a new DecisionId.");
            Assert.That(r2.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task EvaluateLaunchDecision_SameInputsThreeTimes_AllIdenticalWhenIdempotent()
        {
            var svc = CreateService();
            var key = $"idempotency-3x-{Guid.NewGuid()}";
            var r1 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-idem", idempotencyKey: key));
            var r2 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-idem", idempotencyKey: key));
            var r3 = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-idem", idempotencyKey: key));

            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
            Assert.That(r2.DecisionId, Is.EqualTo(r3.DecisionId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC9 – Decision retrieval and listing
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetDecision_AfterEvaluate_ReturnsStoredDecision()
        {
            var svc = CreateService();
            var r = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-retrieve"));
            var fetched = await svc.GetDecisionAsync(r.DecisionId);

            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched!.DecisionId, Is.EqualTo(r.DecisionId));
            Assert.That(fetched.Success, Is.True);
        }

        [Test]
        public async Task GetDecisionTrace_AfterEvaluate_ReturnsTrace()
        {
            var svc = CreateService();
            var r = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-trace"));
            var trace = await svc.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = r.DecisionId });

            Assert.That(trace.Success, Is.True);
            Assert.That(trace.Rules, Is.Not.Empty);
            Assert.That(trace.DecisionId, Is.EqualTo(r.DecisionId));
        }

        [Test]
        public async Task GetDecisionTrace_RulesAreOrdered()
        {
            var svc = CreateService();
            var r = await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-trace-order"));
            var trace = await svc.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = r.DecisionId });

            var orders = trace.Rules.Select(r => r.EvaluationOrder).ToList();
            Assert.That(orders, Is.EqualTo(orders.OrderBy(o => o).ToList()),
                "Rule evaluation order must be deterministic and ascending.");
        }

        [Test]
        public async Task ListDecisions_AfterMultipleEvaluations_ReturnsAllDecisions()
        {
            var svc = CreateService();
            var owner = $"list-owner-{Guid.NewGuid():N}";
            for (int i = 0; i < 3; i++)
                await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: owner));

            var list = await svc.ListDecisionsAsync(owner, limit: 10);
            Assert.That(list, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task ListDecisions_LimitRespected()
        {
            var svc = CreateService();
            var owner = $"limit-owner-{Guid.NewGuid():N}";
            for (int i = 0; i < 5; i++)
                await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: owner));

            var list = await svc.ListDecisionsAsync(owner, limit: 2);
            Assert.That(list, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ListDecisions_EmptyOwner_ReturnsEmptyList()
        {
            var svc = CreateService();
            var list = await svc.ListDecisionsAsync("");
            Assert.That(list, Is.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC4/AC9 – Release-grade never applied to permissive/incomplete evidence
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEvidenceBundle_EmptyOwner_NeverLabeledReleaseGrade()
        {
            var svc = CreateService();
            // Evaluate for one owner, retrieve for a different owner (no evidence)
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "owner-a"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("owner-b-no-evidence"));

            Assert.That(bundle.IsReleaseGradeEvidence, Is.False,
                "A bundle with no evidence must never be labeled release-grade (AC4).");
        }

        [Test]
        public async Task ExportJson_BlockedDecision_IsReleaseGradeFalseInExport()
        {
            var svc = CreateService();
            // ARC1400 on testnet → entitlement blocker → not release-grade
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "blocked-export-owner", tokenStandard: "ARC1400"));
            var result = await svc.ExportEvidenceBundleAsJsonAsync(ExportRequest("blocked-export-owner"));

            Assert.That(result.Success, Is.True);
            var json = System.Text.Encoding.UTF8.GetString(result.Content!);
            // The JSON must contain isReleaseGradeEvidence: false (not true)
            Assert.That(json, Does.Contain("\"isReleaseGradeEvidence\": false"),
                "A blocked decision must never be exported with isReleaseGradeEvidence=true.");
        }

        [Test]
        public async Task ExportCsv_BlockedDecision_ReleaseGradeFalseInHeader()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "blocked-csv-owner", tokenStandard: "ARC1400"));
            var result = await svc.ExportEvidenceBundleAsCsvAsync(ExportRequest("blocked-csv-owner"));

            Assert.That(result.Success, Is.True);
            var csv = System.Text.Encoding.UTF8.GetString(result.Content!);
            Assert.That(csv, Does.Contain("False"),
                "CSV header must show Release Grade: False for blocked decisions.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC9 – CorrelationId propagation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateLaunchDecision_CorrelationIdPropagated()
        {
            var svc = CreateService();
            var corrId = $"corr-{Guid.NewGuid()}";
            var req = MakeRequest();
            req.CorrelationId = corrId;
            var resp = await svc.EvaluateLaunchDecisionAsync(req);
            Assert.That(resp.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task GetEvidenceBundle_CorrelationIdPropagated()
        {
            var svc = CreateService();
            var corrId = $"corr-bundle-{Guid.NewGuid()}";
            var resp = await svc.GetEvidenceBundleAsync(new EvidenceBundleRequest
            {
                OwnerId = "owner-corr",
                CorrelationId = corrId
            });
            Assert.That(resp.CorrelationId, Is.EqualTo(corrId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AC9 – Schema contract stability
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task LaunchDecisionResponse_SchemaVersion_Is100()
        {
            var svc = CreateService();
            var resp = await svc.EvaluateLaunchDecisionAsync(MakeRequest());
            Assert.That(resp.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task EvidenceBundleResponse_SchemaVersion_Is100()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "schema-owner"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("schema-owner"));
            Assert.That(bundle.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task ExportManifest_SchemaVersion_Is100()
        {
            var svc = CreateService();
            await svc.EvaluateLaunchDecisionAsync(MakeRequest(ownerId: "manifest-schema-owner"));
            var bundle = await svc.GetEvidenceBundleAsync(BundleRequest("manifest-schema-owner"));
            Assert.That(bundle.ExportManifest?.SchemaVersion, Is.EqualTo("1.0.0"));
        }
    }
}
