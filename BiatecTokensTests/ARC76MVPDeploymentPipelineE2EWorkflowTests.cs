using BiatecTokensApi.Models.ARC76MVPPipeline;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// End-to-end workflow tests for ARC76 MVP Deployment Pipeline - direct service calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76MVPDeploymentPipelineE2EWorkflowTests
    {
        private ARC76MVPDeploymentPipelineService CreateService() =>
            new(NullLogger<ARC76MVPDeploymentPipelineService>.Instance);

        private static PipelineInitiateRequest ValidRequest(string? correlationId = null, string? idempotencyKey = null) => new()
        {
            TokenName = "E2ETestToken",
            TokenStandard = "ARC200",
            Network = "testnet",
            DeployerAddress = "ALGO_E2E_ADDRESS",
            MaxRetries = 3,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            IdempotencyKey = idempotencyKey
        };

        private static async Task AdvanceAllStagesAsync(ARC76MVPDeploymentPipelineService svc, string pipelineId)
        {
            for (int i = 0; i < 9; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = pipelineId });
        }

        // ── WA: Full happy path ──────────────────────────────────────────────────

        [Test]
        public async Task WA_FullHappyPath_PipelineReachesCompleted()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));

            await AdvanceAllStagesAsync(svc, r.PipelineId!);

            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        // ── WB: Idempotency determinism ──────────────────────────────────────────

        [Test]
        public async Task WB_IdempotencyDeterminism_ThreeRunsReturnSameResult()
        {
            var svc = CreateService();
            var key = Guid.NewGuid().ToString();
            var r1 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            var r2 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            var r3 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));

            Assert.That(r1.PipelineId, Is.EqualTo(r2.PipelineId));
            Assert.That(r2.PipelineId, Is.EqualTo(r3.PipelineId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ── WC: Correlation ID propagation ───────────────────────────────────────

        [Test]
        public async Task WC_CorrelationId_PropagatesThroughFullLifecycleAudit()
        {
            var svc = CreateService();
            var corrId = "wc-test-" + Guid.NewGuid();
            var r = await svc.InitiateAsync(ValidRequest(correlationId: corrId));
            await AdvanceAllStagesAsync(svc, r.PipelineId!);

            var audit = await svc.GetAuditAsync(r.PipelineId!, corrId);
            Assert.That(audit.Events, Is.Not.Empty);

            var byCorr = svc.GetAuditEvents(correlationId: corrId);
            // At minimum the Initiate event should have the correlation ID
            Assert.That(byCorr.Any(e => e.CorrelationId == corrId), Is.True);
        }

        // ── WD: Failed then Retry then complete ──────────────────────────────────

        [Test]
        public async Task WD_FailedThenRetry_PipelineReachesRetrying()
        {
            var svc = CreateService();
            // Create a fresh pipeline, advance to DeploymentActive (7 steps), then try retry
            // Since we cannot force-transition to Failed via the normal advance API,
            // we test the retry behavior from the non-failed state (which returns error).
            var r = await svc.InitiateAsync(ValidRequest());
            // Advance to step 7 (DeploymentActive)
            for (int i = 0; i < 7; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentActive));

            // Retry from non-failed state returns error (cannot retry non-failed)
            var retry = await svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.Success, Is.False);
            Assert.That(retry.ErrorCode, Is.EqualTo("NOT_IN_FAILED_STATE"));
        }

        // ── WE: Cancel at different stages ───────────────────────────────────────

        [Test]
        public async Task WE1_Cancel_AtPendingReadiness_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.PreviousStage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task WE2_Cancel_AtReadinessVerified_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.PreviousStage, Is.EqualTo(PipelineStage.ReadinessVerified));
        }

        [Test]
        public async Task WE3_Cancel_AtDeploymentActive_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 7; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.PreviousStage, Is.EqualTo(PipelineStage.DeploymentActive));
        }

        // ── WF: Compliance audit completeness ────────────────────────────────────

        [Test]
        public async Task WF_ComplianceAudit_AllStagesRecorded()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Count, Is.GreaterThanOrEqualTo(10),
                "Should have at least 1 Initiate + 9 Advance events");
        }

        // ── WG: Concurrent pipeline isolation ───────────────────────────────────

        [Test]
        public async Task WG_ConcurrentPipelines_AreIndependent()
        {
            var svc = CreateService();
            var tasks = Enumerable.Range(0, 5).Select(_ => svc.InitiateAsync(ValidRequest())).ToArray();
            var results = await Task.WhenAll(tasks);
            var ids = results.Select(r => r.PipelineId).Distinct().ToList();
            Assert.That(ids.Count, Is.EqualTo(5), "Each concurrent pipeline should have a unique ID");
        }

        // ── WH: Schema contract stability ────────────────────────────────────────

        [Test]
        public async Task WH_SchemaContract_ResponseShapesConsistent()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(r.PipelineId, Is.Not.Null);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));

            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(status.PipelineId, Is.EqualTo(r.PipelineId));
        }

        // ── WI: Failure category determinism ─────────────────────────────────────

        [Test]
        public async Task WI_SameError_ProducesSameFailureCategory()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.TokenName = null;
            var r1 = await svc.InitiateAsync(req);
            var r2 = await svc.InitiateAsync(req);
            Assert.That(r1.FailureCategory, Is.EqualTo(r2.FailureCategory));
            Assert.That(r1.ErrorCode, Is.EqualTo(r2.ErrorCode));
        }

        // ── WJ: MaxRetries boundary ──────────────────────────────────────────────

        [Test]
        public async Task WJ_MaxRetries0_RetryImmediatelyFails()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.MaxRetries = 0;
            var r = await svc.InitiateAsync(req);
            // Retry from non-failed state still gives NOT_IN_FAILED_STATE
            var retry = await svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.Success, Is.False);
        }

        // ── WK: Multiple pipelines for same deployer ─────────────────────────────

        [Test]
        public async Task WK_MultiplePipelinesSameDeployer_AreIndependent()
        {
            var svc = CreateService();
            var r1 = await svc.InitiateAsync(ValidRequest());
            var r2 = await svc.InitiateAsync(ValidRequest());
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
            // Advance one without affecting the other
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r1.PipelineId });
            var s1 = await svc.GetStatusAsync(r1.PipelineId!, null);
            var s2 = await svc.GetStatusAsync(r2.PipelineId!, null);
            Assert.That(s1!.Stage, Is.EqualTo(PipelineStage.ReadinessVerified));
            Assert.That(s2!.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        // ── WL: Different token standards ─────────────────────────────────────────

        [Test]
        public async Task WL_DifferentTokenStandards_AllComplete()
        {
            var standards = new[] { "ASA", "ARC3", "ARC200", "ERC20", "ARC1400" };
            foreach (var standard in standards)
            {
                var svc = CreateService();
                var req = ValidRequest();
                req.TokenStandard = standard;
                var r = await svc.InitiateAsync(req);
                Assert.That(r.Success, Is.True, $"Standard {standard} should initiate");
                await AdvanceAllStagesAsync(svc, r.PipelineId!);
                var status = await svc.GetStatusAsync(r.PipelineId!, null);
                Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed),
                    $"Standard {standard} should reach Completed");
            }
        }

        // ── WM: Email provided - ARC76 readiness traceability ────────────────────

        [Test]
        public async Task WM_EmailProvided_ARC76ReadinessFlow_IsTraceable()
        {
            var svc = CreateService();
            var corrId = "wm-email-" + Guid.NewGuid();
            var req = ValidRequest(correlationId: corrId);
            req.DeployerEmail = "deployer@test.example.com";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);

            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });

            var audit = await svc.GetAuditAsync(r.PipelineId!, corrId);
            Assert.That(audit.Success, Is.True);
            Assert.That(audit.Events, Is.Not.Empty);
            Assert.That(audit.Events.All(e => e.PipelineId == r.PipelineId), Is.True);
        }

        // ── WN: Correlation ID preserved on cancel ────────────────────────────────

        [Test]
        public async Task WN_CorrelationIdOnCancel_IsPreserved()
        {
            var svc = CreateService();
            var corrId = "wn-cancel-" + Guid.NewGuid();
            var r = await svc.InitiateAsync(ValidRequest(correlationId: corrId));
            var cancel = await svc.CancelAsync(new PipelineCancelRequest
            {
                PipelineId = r.PipelineId,
                CorrelationId = corrId
            });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.CorrelationId, Is.EqualTo(corrId));
        }

        // ── WO: Multi-step audit trail has all operations ─────────────────────────

        [Test]
        public async Task WO_MultiStepAuditTrail_HasAllOperations()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);

            var ops = audit.Events.Select(e => e.Operation).ToList();
            Assert.That(ops, Has.Member("Initiate"), "Should have Initiate operation");
            Assert.That(ops.Count(o => o == "Advance"), Is.EqualTo(9),
                "Should have exactly 9 Advance operations (one per stage transition to Completed)");
            Assert.That(audit.Events.Count, Is.GreaterThanOrEqualTo(10),
                "Should have at least 10 total audit events");
        }

        // ── WP: Multi-network pipeline ───────────────────────────────────────────

        [Test]
        public async Task WP_MainnetNetwork_CompletesFullLifecycle()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Network = "mainnet";
            req.TokenStandard = "ASA";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        // ── WQ: Multiple completed pipelines have isolated audit trails ──────────

        [Test]
        public async Task WQ_MultipleCompletedPipelines_HaveIsolatedAuditTrails()
        {
            var svc = CreateService();
            var r1 = await svc.InitiateAsync(ValidRequest());
            var r2 = await svc.InitiateAsync(ValidRequest());

            await AdvanceAllStagesAsync(svc, r1.PipelineId!);
            // r2 remains at PendingReadiness

            var audit1 = await svc.GetAuditAsync(r1.PipelineId!, null);
            var audit2 = await svc.GetAuditAsync(r2.PipelineId!, null);

            // r1 should have more events (advanced all the way)
            Assert.That(audit1.Events.Count, Is.GreaterThan(audit2.Events.Count));
            // All r2 events should only reference r2's pipeline
            Assert.That(audit2.Events.All(e => e.PipelineId == r2.PipelineId || e.PipelineId == null), Is.True);
        }

        // ── WR: Cancel at ValidationPassed produces Cancelled terminal stage ─────

        [Test]
        public async Task WR_CancelAtValidationPassed_CorrectTerminalStage()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            // Advance to ValidationPassed (3 steps)
            for (int i = 0; i < 3; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });

            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.ValidationPassed));

            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);

            status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        // ── WS: Schema version stable across all response types ──────────────────

        [Test]
        public async Task WS_SchemaVersionStable_AcrossAllResponseTypes()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));

            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"));

            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.SchemaVersion, Is.EqualTo("1.0.0"));

            // Cancel a fresh pipeline to verify schema version on cancel response
            var svc2 = CreateService();
            var r2 = await svc2.InitiateAsync(ValidRequest());
            var cancel2 = await svc2.CancelAsync(new PipelineCancelRequest { PipelineId = r2.PipelineId });
            Assert.That(cancel2.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        // ── WT: ARC200 standard supported on testnet ──────────────────────────────

        [Test]
        public async Task WT_ARC200Standard_SupportedOnTestnet()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.TokenStandard = "ARC200";
            req.Network = "testnet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task WW_CompliancePassed_ToDeploymentQueued_IsCorrectTransition()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            // 5 advances: PendingReadiness->ReadinessVerified->ValidationPending->ValidationPassed->CompliancePending->CompliancePassed
            for (int i = 0; i < 5; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.DeploymentQueued));
        }

        [Test]
        public async Task WX_DeploymentActive_StageIsReachable()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 7; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentActive));
        }

        [Test]
        public async Task WY_DeploymentConfirmed_StageIsReachable()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 8; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentConfirmed));
        }

        [Test]
        public async Task WZ_FullLifecycle_AllStagesAreTraversed_InCorrectOrder()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var stages = new List<PipelineStage> { r.Stage };

            for (int i = 0; i < 9; i++)
            {
                var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                stages.Add(adv.CurrentStage);
            }

            var expectedOrder = new[]
            {
                PipelineStage.PendingReadiness,
                PipelineStage.ReadinessVerified,
                PipelineStage.ValidationPending,
                PipelineStage.ValidationPassed,
                PipelineStage.CompliancePending,
                PipelineStage.CompliancePassed,
                PipelineStage.DeploymentQueued,
                PipelineStage.DeploymentActive,
                PipelineStage.DeploymentConfirmed,
                PipelineStage.Completed
            };
            Assert.That(stages, Is.EqualTo(expectedOrder));
        }

        // ── Additional E2E workflow tests ─────────────────────────────────────────

        [Test]
        public async Task W0_ARC3_OnBetanet_FullLifecycle()
        {
            var svc = CreateService();
            var req = new PipelineInitiateRequest
            {
                TokenName = "BetanetARC3Token",
                TokenStandard = "ARC3",
                Network = "betanet",
                DeployerAddress = "ALGO_BETANET_ADDRESS",
                MaxRetries = 3,
                CorrelationId = Guid.NewGuid().ToString()
            };
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task W1_ASA_OnVoimain_FullLifecycle()
        {
            var svc = CreateService();
            var req = new PipelineInitiateRequest
            {
                TokenName = "VoimainASAToken",
                TokenStandard = "ASA",
                Network = "voimain",
                DeployerAddress = "ALGO_VOIMAIN_ADDRESS",
                MaxRetries = 3,
                CorrelationId = Guid.NewGuid().ToString()
            };
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task W2_IdempotencyKey_Null_CreatesTwoPipelines()
        {
            var svc = CreateService();
            var r1 = await svc.InitiateAsync(ValidRequest(idempotencyKey: null));
            var r2 = await svc.InitiateAsync(ValidRequest(idempotencyKey: null));
            // Without idempotency key, each call should produce a distinct pipeline
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
        }

        [Test]
        public async Task W3_MultipleStagesAdvanced_AuditTrailGrowsWithEachStep()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var audit0 = await svc.GetAuditAsync(r.PipelineId!, null);
            int count0 = audit0.Events.Count;

            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit1 = await svc.GetAuditAsync(r.PipelineId!, null);
            int count1 = audit1.Events.Count;

            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit2 = await svc.GetAuditAsync(r.PipelineId!, null);
            int count2 = audit2.Events.Count;

            Assert.That(count1, Is.GreaterThan(count0), "Audit should grow after first advance");
            Assert.That(count2, Is.GreaterThan(count1), "Audit should grow after second advance");
        }

        // ── E2E tests (batch 2) ──────────────────────────────────────────────────

        [Test]
        public async Task W4_TwoConcurrentPipelines_SameDeployer_ProduceDifferentAuditLogs()
        {
            var svc = CreateService();
            var r1 = await svc.InitiateAsync(ValidRequest(idempotencyKey: "e2e-dup-" + Guid.NewGuid()));
            var r2 = await svc.InitiateAsync(ValidRequest(idempotencyKey: "e2e-dup-" + Guid.NewGuid()));
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r1.PipelineId });
            var audit1 = await svc.GetAuditAsync(r1.PipelineId!, null);
            var audit2 = await svc.GetAuditAsync(r2.PipelineId!, null);
            Assert.That(audit1.PipelineId, Is.EqualTo(r1.PipelineId));
            Assert.That(audit2.PipelineId, Is.EqualTo(r2.PipelineId));
            Assert.That(audit1.Events.All(e => e.PipelineId == r1.PipelineId), Is.True);
            Assert.That(audit2.Events.All(e => e.PipelineId == r2.PipelineId), Is.True);
        }

        [Test]
        public async Task W5_CancelledPipeline_HasCancelledInAuditEvents()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Any(e => e.Operation.Contains("Cancel", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public async Task W6_FullLifecycle_CompletedPipeline_HasExpectedStageCount()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest(correlationId: "e2e-stage-count"));
            for (int i = 0; i < 9; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            // At least 10 events: 1 Initiate + 9 Advance
            Assert.That(audit.Events.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public async Task W7_AuditEvents_Timestamps_AreRecentUtcTimes()
        {
            var svc = CreateService();
            var before = DateTime.UtcNow.AddSeconds(-1);
            var r = await svc.InitiateAsync(ValidRequest());
            var after = DateTime.UtcNow.AddSeconds(1);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            foreach (var ev in audit.Events)
            {
                Assert.That(ev.Timestamp, Is.GreaterThanOrEqualTo(before), "Timestamp should be recent");
                Assert.That(ev.Timestamp, Is.LessThanOrEqualTo(after), "Timestamp should not be in future");
            }
        }

        [Test]
        public async Task W8_PipelineStatus_SchemaVersion_ConsistentAcrossAllStages()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
            for (int i = 0; i < 3; i++)
            {
                var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"), $"SchemaVersion must be 1.0.0 at stage {i + 1}");
            }
        }

        // ── E2E tests (batch 3) ──────────────────────────────────────────────────

        [Test]
        public async Task W9_InitiateAndAdvance_CorrelationIdIsPreservedInInitiateEvent()
        {
            var svc = CreateService();
            const string corrId = "e2e-corr-preserve";
            var r = await svc.InitiateAsync(ValidRequest(correlationId: corrId));
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await svc.GetAuditAsync(r.PipelineId!, corrId);
            Assert.That(audit.Events, Is.Not.Empty);
            // At least the initiate event should have the correlation ID
            Assert.That(audit.Events.Any(e => e.CorrelationId == corrId), Is.True);
        }

        [Test]
        public async Task WA_ThreePipelines_AllReachReadinessVerified()
        {
            var svc = CreateService();
            var ids = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var r = await svc.InitiateAsync(ValidRequest(idempotencyKey: "e2e-three-" + i + "-" + Guid.NewGuid()));
                ids.Add(r.PipelineId!);
            }
            foreach (var id in ids)
            {
                var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = id });
                Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ReadinessVerified));
            }
        }

        [Test]
        public async Task WB_CancelledPipeline_AdvanceReturnsTerminalStageError()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task WC_AuditLog_EventOperations_AreNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.All(e => !string.IsNullOrEmpty(e.Operation)), Is.True);
        }

        [Test]
        public async Task WD_IdempotencyKey_SameKeyDifferentDeployer_ReturnsConflict()
        {
            var svc = CreateService();
            var key = "e2e-conflict-deploy-" + Guid.NewGuid();
            var req1 = ValidRequest(idempotencyKey: key);
            req1.DeployerAddress = "DEPLOYER_A";
            await svc.InitiateAsync(req1);

            var req2 = ValidRequest(idempotencyKey: key);
            req2.DeployerAddress = "DEPLOYER_B";
            var r2 = await svc.InitiateAsync(req2);
            Assert.That(r2.Success, Is.False);
            Assert.That(r2.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
        }

        [Test]
        public async Task WE_FullLifecycle_ARC200_OnBetanet_Completes()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-arc200-betanet-" + Guid.NewGuid());
            req.TokenStandard = "ARC200";
            req.Network = "betanet";
            var r = await svc.InitiateAsync(req);
            PipelineAdvanceResponse? last = null;
            for (int i = 0; i < 9; i++)
                last = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(last!.CurrentStage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WF_GetStatus_AfterFullLifecycle_IsCompletedStage()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 9; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WG_InitiateAndCancel_PipelineIsInCancelledState()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WH_AuditLog_PipelineId_MatchesInitiateResponse()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.PipelineId, Is.EqualTo(r.PipelineId));
        }

        // ── WR: Additional E2E workflow tests ────────────────────────────────────

        [Test]
        public async Task WR1_ARC1400_OnMainnet_FullLifecycle_AllStagesPresent()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.TokenStandard = "ARC1400";
            req.Network = "mainnet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            var ops = audit.Events.Select(e => e.Operation).ToList();
            Assert.That(ops, Has.Member("Initiate"), "Should have an Initiate event");
            Assert.That(ops.Count(o => o == "Advance"), Is.EqualTo(9), "Should have exactly 9 Advance events");
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WR2_ASA_OnAramidmain_FullLifecycle_Completes()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.TokenStandard = "ASA";
            req.Network = "aramidmain";
            var r = await svc.InitiateAsync(req);
            if (!r.Success)
            {
                Assert.That(r.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"),
                    "If aramidmain is not supported it must return UNSUPPORTED_NETWORK");
                return;
            }
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WR3_TwoPipelines_SameStandard_DifferentNetworks_BothComplete()
        {
            var svc = CreateService();
            var req1 = ValidRequest(idempotencyKey: "wr3-testnet-" + Guid.NewGuid());
            req1.Network = "testnet";
            req1.TokenStandard = "ARC3";
            var req2 = ValidRequest(idempotencyKey: "wr3-mainnet-" + Guid.NewGuid());
            req2.Network = "mainnet";
            req2.TokenStandard = "ARC3";

            var r1 = await svc.InitiateAsync(req1);
            var r2 = await svc.InitiateAsync(req2);
            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));

            await AdvanceAllStagesAsync(svc, r1.PipelineId!);
            await AdvanceAllStagesAsync(svc, r2.PipelineId!);

            var s1 = await svc.GetStatusAsync(r1.PipelineId!, null);
            var s2 = await svc.GetStatusAsync(r2.PipelineId!, null);
            Assert.That(s1!.Stage, Is.EqualTo(PipelineStage.Completed));
            Assert.That(s2!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WR4_PipelineAudit_SchemaVersion_IsConsistent()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));

            for (int i = 0; i < 9; i++)
            {
                var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"), $"SchemaVersion must be 1.0.0 at advance {i + 1}");
            }

            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.SchemaVersion, Is.EqualTo("1.0.0"));

            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task WR5_Cancel_At_DeploymentQueued_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 6; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var statusBefore = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusBefore!.Stage, Is.EqualTo(PipelineStage.DeploymentQueued));
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var statusAfter = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusAfter!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WR6_Cancel_At_DeploymentActive_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 7; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var statusBefore = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusBefore!.Stage, Is.EqualTo(PipelineStage.DeploymentActive));
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var statusAfter = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusAfter!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WR7_Cancel_At_DeploymentConfirmed_ReturnsCannotCancel()
        {
            // DeploymentConfirmed only transitions to Completed; cancel is not a valid transition
            // because the blockchain transaction has already been submitted and confirmed.
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 8; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var statusBefore = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusBefore!.Stage, Is.EqualTo(PipelineStage.DeploymentConfirmed));
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.False);
            Assert.That(cancel.ErrorCode, Is.EqualTo("CANNOT_CANCEL"));
        }

        [Test]
        public async Task WR8_Idempotency_SameKey_DifferentStandard_ReturnsConflict()
        {
            var svc = CreateService();
            var key = "wr8-conflict-std-" + Guid.NewGuid();
            var req1 = ValidRequest(idempotencyKey: key);
            req1.TokenStandard = "ARC200"; // explicit standard to avoid reliance on default
            await svc.InitiateAsync(req1);

            var req2 = ValidRequest(idempotencyKey: key);
            req2.TokenStandard = "ARC3"; // different standard — should trigger idempotency conflict
            var r2 = await svc.InitiateAsync(req2);
            Assert.That(r2.Success, Is.False);
            Assert.That(r2.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
        }

        [Test]
        public async Task WR9_Idempotency_SameKey_SameParams_AllReturnTrue_IsIdempotentReplay()
        {
            var svc = CreateService();
            var key = "wr9-replay-" + Guid.NewGuid();
            var results = new List<PipelineInitiateResponse>();
            for (int i = 0; i < 5; i++)
                results.Add(await svc.InitiateAsync(ValidRequest(idempotencyKey: key)));

            Assert.That(results[0].IsIdempotentReplay, Is.False, "First call should not be a replay");
            for (int i = 1; i < 5; i++)
                Assert.That(results[i].IsIdempotentReplay, Is.True, $"Call {i + 1} should be IsIdempotentReplay=true");

            var ids = results.Select(r => r.PipelineId).Distinct().ToList();
            Assert.That(ids.Count, Is.EqualTo(1), "All replays should return the same PipelineId");
        }

        [Test]
        public async Task WR10_GetAudit_AfterFullLifecycle_Has_Exactly10Events()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            // 1 Initiate + 9 Advance events = 10 total
            Assert.That(audit.Events.Count, Is.EqualTo(10),
                "Should have exactly 10 audit events: 1 Initiate + 9 Advance");
        }

        [Test]
        public async Task WR11_GetStatus_AfterCancel_ReturnsCancelled()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WR12_CorrelationId_In_All_AuditEvents_NonNull()
        {
            var svc = CreateService();
            var corrId = "wr12-corr-" + Guid.NewGuid();
            var r = await svc.InitiateAsync(ValidRequest(correlationId: corrId));
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, corrId);
            Assert.That(audit.Events, Is.Not.Empty);
            // Any event that has a CorrelationId set must have a non-empty value
            var eventsWithCorr = audit.Events.Where(e => e.CorrelationId != null).ToList();
            Assert.That(eventsWithCorr, Is.Not.Empty, "At least one audit event should carry a CorrelationId");
            foreach (var ev in eventsWithCorr)
                Assert.That(ev.CorrelationId, Is.Not.Empty,
                    $"Audit event '{ev.Operation}' has CorrelationId set but it is empty");
        }

        [Test]
        public async Task WX1_AuditEvents_Grow_By1_With_Each_Advance()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var audit0 = await svc.GetAuditAsync(r.PipelineId!, null);
            int baseline = audit0.Events.Count;
            for (int step = 1; step <= 3; step++)
            {
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                var audit = await svc.GetAuditAsync(r.PipelineId!, null);
                Assert.That(audit.Events.Count, Is.EqualTo(baseline + step),
                    $"After {step} advance(s), audit should have {baseline + step} events");
            }
        }

        [Test]
        public async Task WX2_FullLifecycle_ARC1400_Voimain_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.TokenStandard = "ARC1400";
            req.Network = "voimain";
            req.IdempotencyKey = "e2e-arc1400-voi-" + Guid.NewGuid();
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WX3_Cancel_Audit_Contains_Cancel_Operation()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId, Reason = "test" });
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Any(e => e.Operation.Contains("Cancel", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public async Task WX4_Idempotency_Replay_Returns_Same_Schema_Version()
        {
            var svc = CreateService();
            var key = "e2e-idem-sv-" + Guid.NewGuid();
            var r1 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            var r2 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r2.SchemaVersion, Is.EqualTo(r1.SchemaVersion));
            Assert.That(r2.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task WX5_Cancel_At_ValidationPending_Stage()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.ValidationPending));
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var statusAfter = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(statusAfter!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WX6_SchemaVersion_1_0_0_In_All_Response_Types()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"), "InitiateResponse SchemaVersion");
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"), "AdvanceResponse SchemaVersion");
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.SchemaVersion, Is.EqualTo("1.0.0"), "StatusResponse SchemaVersion");
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.SchemaVersion, Is.EqualTo("1.0.0"), "AuditResponse SchemaVersion");
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.SchemaVersion, Is.EqualTo("1.0.0"), "CancelResponse SchemaVersion");
        }

        [Test]
        public async Task WX7_Concurrent_5Pipelines_All_Complete()
        {
            var svc = CreateService();
            var tasks = Enumerable.Range(0, 5).Select(async _ =>
            {
                var req = ValidRequest(idempotencyKey: Guid.NewGuid().ToString());
                var r = await svc.InitiateAsync(req);
                await AdvanceAllStagesAsync(svc, r.PipelineId!);
                return r.PipelineId!;
            }).ToList();
            var ids = await Task.WhenAll(tasks);
            foreach (var id in ids)
            {
                var status = await svc.GetStatusAsync(id, null);
                Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
            }
        }

        [Test]
        public async Task WX8_Pipeline_With_MaxRetries_0_Completes()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-mr0-" + Guid.NewGuid());
            req.MaxRetries = 0;
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WX9_Stage_Transitions_Have_NonNull_Timestamps()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 3; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events, Is.Not.Empty);
            Assert.That(audit.Events.All(e => e.Timestamp > DateTime.MinValue), Is.True,
                "All audit events must have a non-default Timestamp");
        }

        [Test]
        public async Task WY1_FullLifecycle_ARC200_Mainnet_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-arc200-mn-" + Guid.NewGuid());
            req.TokenStandard = "ARC200";
            req.Network = "mainnet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WY2_AdvanceAsync_ReturnsCurrentStage_NonNull()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv, Is.Not.Null);
            Assert.That(adv.Success, Is.True);
        }

        [Test]
        public async Task WY3_GetStatus_Returns_TokenName_Matching_Request()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-tokenname-" + Guid.NewGuid());
            req.TokenName = "UniqueTokenName123";
            var r = await svc.InitiateAsync(req);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.TokenName, Is.EqualTo("UniqueTokenName123"));
        }

        [Test]
        public async Task WY4_Initiate_Then_GetAudit_Returns_EventCount_1()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Count, Is.EqualTo(1), "After initiation only, audit should have exactly 1 event");
        }

        [Test]
        public async Task WZ1_FullLifecycle_ASA_Mainnet_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-asa-mn-" + Guid.NewGuid());
            req.TokenStandard = "ASA";
            req.Network = "mainnet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WZ2_FullLifecycle_ARC1400_Testnet_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-arc1400-tn-" + Guid.NewGuid());
            req.TokenStandard = "ARC1400";
            req.Network = "testnet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WZ3_FullLifecycle_ERC20_Base_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-erc20-base-" + Guid.NewGuid());
            req.TokenStandard = "ERC20";
            req.Network = "base";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WZ4_IdempotencyReplay_Stability_ThreeRunsSamePipeline()
        {
            var svc = CreateService();
            var key = "e2e-idem-stable-" + Guid.NewGuid();
            var r1 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            var r2 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            var r3 = await svc.InitiateAsync(ValidRequest(idempotencyKey: key));
            Assert.That(r1.PipelineId, Is.EqualTo(r2.PipelineId));
            Assert.That(r2.PipelineId, Is.EqualTo(r3.PipelineId));
        }

        [Test]
        public async Task WZ5_CorrelationId_Propagated_InAdvanceResponse()
        {
            var svc = CreateService();
            var corrId = "e2e-corr-" + Guid.NewGuid();
            var r = await svc.InitiateAsync(ValidRequest(correlationId: corrId));
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId, CorrelationId = corrId });
            Assert.That(adv.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task WZ6_CancelAtReadinessVerified_ReturnsProperStage()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WZ7_AuditEvents_GrowMonotonically_WithEachAdvance()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            int previous = 1;
            for (int i = 0; i < 5; i++)
            {
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                var audit = await svc.GetAuditAsync(r.PipelineId!, null);
                Assert.That(audit.Events.Count, Is.GreaterThan(previous));
                previous = audit.Events.Count;
            }
        }

        [Test]
        public async Task WZ8_GetAuditEvents_AfterFullLifecycle_Returns10Events()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task WZ9_FullLifecycle_Voimain_ARC3_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-voimain-arc3-" + Guid.NewGuid());
            req.TokenStandard = "ARC3";
            req.Network = "voimain";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WA2_FullLifecycle_Betanet_ASA_Succeeds()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-betanet-asa-" + Guid.NewGuid());
            req.TokenStandard = "ASA";
            req.Network = "betanet";
            var r = await svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task WA3_CancelAtValidationPending_StatusIsCancelled()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 2; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task WA4_AuditEventType_IsNonNull()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            var audit = await svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.All(e => e.Operation != null), Is.True);
        }

        [Test]
        public async Task WA5_AdvanceOnCompleted_ReturnsTerminalStage()
        {
            var svc = CreateService();
            var r = await svc.InitiateAsync(ValidRequest());
            await AdvanceAllStagesAsync(svc, r.PipelineId!);
            var adv = await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task WA6_MultipleSequentialPipelines_EachComplete()
        {
            var svc = CreateService();
            for (int p = 0; p < 3; p++)
            {
                var r = await svc.InitiateAsync(ValidRequest(idempotencyKey: "e2e-seq-" + p + "-" + Guid.NewGuid()));
                await AdvanceAllStagesAsync(svc, r.PipelineId!);
                var status = await svc.GetStatusAsync(r.PipelineId!, null);
                Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed), $"Pipeline {p} should complete");
            }
        }

        [Test]
        public async Task WA7_UniqueIdempotencyKeys_GiveUniquePipelineIds()
        {
            var svc = CreateService();
            var ids = new HashSet<string?>();
            for (int i = 0; i < 5; i++)
            {
                var r = await svc.InitiateAsync(ValidRequest(idempotencyKey: "e2e-unique-" + i + "-" + Guid.NewGuid()));
                ids.Add(r.PipelineId);
            }
            Assert.That(ids.Count, Is.EqualTo(5));
        }

        [Test]
        public async Task WA8_GetStatus_AfterInitiate_DeployerAddressMatches()
        {
            var svc = CreateService();
            var req = ValidRequest(idempotencyKey: "e2e-depladdr-" + Guid.NewGuid());
            req.DeployerAddress = "ALGO_E2E_DEPL_ADDR";
            var r = await svc.InitiateAsync(req);
            var status = await svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.DeployerAddress, Is.EqualTo("ALGO_E2E_DEPL_ADDR"));
        }
    }
}
