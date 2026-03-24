using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="ProtectedSignOffEvidencePersistenceController"/> covering HTTP
    /// response codes, request routing, actor ID propagation, and schema contract.
    ///
    /// Uses a lightweight stub implementation of
    /// <see cref="IProtectedSignOffEvidencePersistenceService"/> to isolate controller
    /// behaviour from service logic (service logic is tested separately in
    /// <c>ProtectedSignOffEvidencePersistenceTests.cs</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceControllerTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Stub service — configurable per test via delegates
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class StubService : IProtectedSignOffEvidencePersistenceService
        {
            public Func<RecordApprovalWebhookRequest, string, Task<RecordApprovalWebhookResponse>>?
                RecordImpl;
            public Func<PersistSignOffEvidenceRequest, string, Task<PersistSignOffEvidenceResponse>>?
                PersistImpl;
            public Func<GetSignOffReleaseReadinessRequest, Task<GetSignOffReleaseReadinessResponse>>?
                ReadinessImpl;
            public Func<GetApprovalWebhookHistoryRequest, Task<GetApprovalWebhookHistoryResponse>>?
                WebhookHistoryImpl;
            public Func<GetEvidencePackHistoryRequest, Task<GetEvidencePackHistoryResponse>>?
                EvidenceHistoryImpl;

            public Task<RecordApprovalWebhookResponse> RecordApprovalWebhookAsync(
                RecordApprovalWebhookRequest request, string actorId)
                => RecordImpl != null
                    ? RecordImpl(request, actorId)
                    : Task.FromResult(new RecordApprovalWebhookResponse { Success = true });

            public Task<PersistSignOffEvidenceResponse> PersistSignOffEvidenceAsync(
                PersistSignOffEvidenceRequest request, string actorId)
                => PersistImpl != null
                    ? PersistImpl(request, actorId)
                    : Task.FromResult(new PersistSignOffEvidenceResponse { Success = true });

            public Task<GetSignOffReleaseReadinessResponse> GetReleaseReadinessAsync(
                GetSignOffReleaseReadinessRequest request)
                => ReadinessImpl != null
                    ? ReadinessImpl(request)
                    : Task.FromResult(new GetSignOffReleaseReadinessResponse { Success = true });

            public Task<GetApprovalWebhookHistoryResponse> GetApprovalWebhookHistoryAsync(
                GetApprovalWebhookHistoryRequest request)
                => WebhookHistoryImpl != null
                    ? WebhookHistoryImpl(request)
                    : Task.FromResult(new GetApprovalWebhookHistoryResponse { Success = true });

            public Task<GetEvidencePackHistoryResponse> GetEvidencePackHistoryAsync(
                GetEvidencePackHistoryRequest request)
                => EvidenceHistoryImpl != null
                    ? EvidenceHistoryImpl(request)
                    : Task.FromResult(new GetEvidencePackHistoryResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Factory helper
        // ═══════════════════════════════════════════════════════════════════════

        private static ProtectedSignOffEvidencePersistenceController BuildController(
            IProtectedSignOffEvidencePersistenceService svc,
            string actorId = "test-actor")
        {
            var controller = new ProtectedSignOffEvidencePersistenceController(
                svc, NullLogger<ProtectedSignOffEvidencePersistenceController>.Instance);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, actorId) }, "test");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST /webhooks/approval — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordApprovalWebhook_ServiceSuccess_Returns200()
        {
            var stub = new StubService { RecordImpl = (_, _) =>
                Task.FromResult(new RecordApprovalWebhookResponse { Success = true }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.RecordApprovalWebhook(new RecordApprovalWebhookRequest
            {
                CaseId = "case-1", HeadRef = "sha-abc", Outcome = ApprovalWebhookOutcome.Approved
            });

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task RecordApprovalWebhook_ServiceFailure_Returns400()
        {
            var stub = new StubService { RecordImpl = (_, _) =>
                Task.FromResult(new RecordApprovalWebhookResponse
                    { Success = false, ErrorCode = "MISSING_CASE_ID" }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.RecordApprovalWebhook(new RecordApprovalWebhookRequest
                { CaseId = "", HeadRef = "sha-abc", Outcome = ApprovalWebhookOutcome.Approved });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task RecordApprovalWebhook_PropagatesActorId()
        {
            string? capturedActor = null;
            var stub = new StubService { RecordImpl = (_, actor) =>
            {
                capturedActor = actor;
                return Task.FromResult(new RecordApprovalWebhookResponse { Success = true });
            }};
            var ctrl = BuildController(stub, actorId: "reviewer-001");

            await ctrl.RecordApprovalWebhook(new RecordApprovalWebhookRequest
                { CaseId = "c1", HeadRef = "h1", Outcome = ApprovalWebhookOutcome.Approved });

            Assert.That(capturedActor, Is.EqualTo("reviewer-001"));
        }

        [Test]
        public async Task RecordApprovalWebhook_MalformedOutcome_ServiceReceivesCorrectOutcome()
        {
            ApprovalWebhookOutcome? capturedOutcome = null;
            var stub = new StubService { RecordImpl = (req, _) =>
            {
                capturedOutcome = req.Outcome;
                return Task.FromResult(new RecordApprovalWebhookResponse { Success = true });
            }};
            var ctrl = BuildController(stub);

            await ctrl.RecordApprovalWebhook(new RecordApprovalWebhookRequest
                { CaseId = "c1", HeadRef = "h1", Outcome = ApprovalWebhookOutcome.Malformed });

            Assert.That(capturedOutcome, Is.EqualTo(ApprovalWebhookOutcome.Malformed));
        }

        [Test]
        public async Task RecordApprovalWebhook_ResponseBodyContainsRecord()
        {
            var expectedRecord = new ApprovalWebhookRecord { RecordId = "rec-xyz", CaseId = "c1", HeadRef = "h1" };
            var stub = new StubService { RecordImpl = (_, _) =>
                Task.FromResult(new RecordApprovalWebhookResponse { Success = true, Record = expectedRecord }) };
            var ctrl = BuildController(stub);

            var actionResult = await ctrl.RecordApprovalWebhook(new RecordApprovalWebhookRequest
                { CaseId = "c1", HeadRef = "h1", Outcome = ApprovalWebhookOutcome.Approved });

            var ok = actionResult as OkObjectResult;
            Assert.That(ok, Is.Not.Null);
            var response = ok!.Value as RecordApprovalWebhookResponse;
            Assert.That(response!.Record!.RecordId, Is.EqualTo("rec-xyz"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST /evidence — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PersistSignOffEvidence_ServiceSuccess_Returns200()
        {
            var stub = new StubService();
            var ctrl = BuildController(stub);

            var result = await ctrl.PersistSignOffEvidence(new PersistSignOffEvidenceRequest
                { HeadRef = "sha-abc" });

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task PersistSignOffEvidence_ServiceFailure_Returns400()
        {
            var stub = new StubService { PersistImpl = (_, _) =>
                Task.FromResult(new PersistSignOffEvidenceResponse
                    { Success = false, ErrorCode = "MISSING_HEAD_REF" }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.PersistSignOffEvidence(new PersistSignOffEvidenceRequest
                { HeadRef = "" });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task PersistSignOffEvidence_PropagatesActorId()
        {
            string? capturedActor = null;
            var stub = new StubService { PersistImpl = (_, actor) =>
            {
                capturedActor = actor;
                return Task.FromResult(new PersistSignOffEvidenceResponse { Success = true });
            }};
            var ctrl = BuildController(stub, actorId: "ops-operator");

            await ctrl.PersistSignOffEvidence(new PersistSignOffEvidenceRequest { HeadRef = "h1" });

            Assert.That(capturedActor, Is.EqualTo("ops-operator"));
        }

        [Test]
        public async Task PersistSignOffEvidence_RequireReleaseGrade_PropagatedToService()
        {
            bool? capturedFlag = null;
            var stub = new StubService { PersistImpl = (req, _) =>
            {
                capturedFlag = req.RequireReleaseGrade;
                return Task.FromResult(new PersistSignOffEvidenceResponse { Success = true });
            }};
            var ctrl = BuildController(stub);

            await ctrl.PersistSignOffEvidence(new PersistSignOffEvidenceRequest
                { HeadRef = "h1", RequireReleaseGrade = true });

            Assert.That(capturedFlag, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST /release-readiness — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReleaseReadiness_Ready_Returns200()
        {
            var stub = new StubService { ReadinessImpl = _ =>
                Task.FromResult(new GetSignOffReleaseReadinessResponse
                    { Success = true, Status = SignOffReleaseReadinessStatus.Ready }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetReleaseReadiness(new GetSignOffReleaseReadinessRequest
                { HeadRef = "sha-abc" });

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetReleaseReadiness_Blocked_Returns400()
        {
            var stub = new StubService { ReadinessImpl = _ =>
                Task.FromResult(new GetSignOffReleaseReadinessResponse
                {
                    Success = false,
                    Status = SignOffReleaseReadinessStatus.Blocked,
                    Blockers = new List<SignOffReleaseBlocker>
                    {
                        new SignOffReleaseBlocker
                        {
                            Category = SignOffReleaseBlockerCategory.MissingApproval,
                            Code = "APPROVAL_MISSING",
                            Description = "No approval webhook received.",
                            RemediationHint = "Trigger approval workflow.",
                            IsCritical = true
                        }
                    }
                }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetReleaseReadiness(new GetSignOffReleaseReadinessRequest
                { HeadRef = "sha-abc" });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetReleaseReadiness_PropagatesHeadRefToService()
        {
            string? capturedHeadRef = null;
            var stub = new StubService { ReadinessImpl = req =>
            {
                capturedHeadRef = req.HeadRef;
                return Task.FromResult(new GetSignOffReleaseReadinessResponse { Success = true });
            }};
            var ctrl = BuildController(stub);

            await ctrl.GetReleaseReadiness(new GetSignOffReleaseReadinessRequest
                { HeadRef = "sha-specific-head" });

            Assert.That(capturedHeadRef, Is.EqualTo("sha-specific-head"));
        }

        [Test]
        public async Task GetReleaseReadiness_ResponseBodyContainsBlockers()
        {
            var blocker = new SignOffReleaseBlocker
            {
                Category = SignOffReleaseBlockerCategory.MissingEvidence,
                Code = "EVIDENCE_UNAVAILABLE",
                Description = "No evidence for this head.",
                RemediationHint = "Run sign-off workflow.",
                IsCritical = true
            };
            var stub = new StubService { ReadinessImpl = _ =>
                Task.FromResult(new GetSignOffReleaseReadinessResponse
                {
                    Success = false,
                    Status = SignOffReleaseReadinessStatus.Blocked,
                    Blockers = new List<SignOffReleaseBlocker> { blocker }
                }) };
            var ctrl = BuildController(stub);

            var actionResult = await ctrl.GetReleaseReadiness(new GetSignOffReleaseReadinessRequest
                { HeadRef = "h1" });

            var bad = actionResult as BadRequestObjectResult;
            var response = bad!.Value as GetSignOffReleaseReadinessResponse;
            Assert.That(response!.Blockers, Is.Not.Empty);
            Assert.That(response.Blockers[0].Code, Is.EqualTo("EVIDENCE_UNAVAILABLE"));
            Assert.That(response.Blockers[0].RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetReleaseReadiness_Stale_Returns400WithStaleStatus()
        {
            var stub = new StubService { ReadinessImpl = _ =>
                Task.FromResult(new GetSignOffReleaseReadinessResponse
                {
                    Success = false,
                    Status = SignOffReleaseReadinessStatus.Stale,
                    EvidenceFreshness = SignOffEvidenceFreshnessStatus.Stale
                }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetReleaseReadiness(new GetSignOffReleaseReadinessRequest
                { HeadRef = "sha-stale" });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var bad = result as BadRequestObjectResult;
            var response = bad!.Value as GetSignOffReleaseReadinessResponse;
            Assert.That(response!.Status, Is.EqualTo(SignOffReleaseReadinessStatus.DegradedStaleEvidence));
            Assert.That(response.EvidenceFreshness, Is.EqualTo(SignOffEvidenceFreshnessStatus.Stale));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GET /webhooks/approval/history — tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetApprovalWebhookHistory_ReturnsOkWithRecords()
        {
            var records = new List<ApprovalWebhookRecord>
            {
                new ApprovalWebhookRecord { RecordId = "r1", CaseId = "c1", HeadRef = "h1" }
            };
            var stub = new StubService { WebhookHistoryImpl = _ =>
                Task.FromResult(new GetApprovalWebhookHistoryResponse
                    { Success = true, Records = records, TotalCount = 1 }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetApprovalWebhookHistory("c1", "h1");

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as GetApprovalWebhookHistoryResponse;
            Assert.That(response!.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetApprovalWebhookHistory_PropagatesFiltersToService()
        {
            string? capturedCaseId = null;
            string? capturedHeadRef = null;
            int? capturedMax = null;
            var stub = new StubService { WebhookHistoryImpl = req =>
            {
                capturedCaseId = req.CaseId;
                capturedHeadRef = req.HeadRef;
                capturedMax = req.MaxRecords;
                return Task.FromResult(new GetApprovalWebhookHistoryResponse { Success = true });
            }};
            var ctrl = BuildController(stub);

            await ctrl.GetApprovalWebhookHistory("case-xyz", "sha-filter", maxRecords: 25);

            Assert.That(capturedCaseId, Is.EqualTo("case-xyz"));
            Assert.That(capturedHeadRef, Is.EqualTo("sha-filter"));
            Assert.That(capturedMax, Is.EqualTo(25));
        }

        [Test]
        public async Task GetApprovalWebhookHistory_NoFilter_ReturnsAllRecords()
        {
            var stub = new StubService { WebhookHistoryImpl = _ =>
                Task.FromResult(new GetApprovalWebhookHistoryResponse
                    { Success = true, Records = new(), TotalCount = 0 }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetApprovalWebhookHistory(null, null);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GET /evidence/history — tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEvidencePackHistory_ReturnsOkWithPacks()
        {
            var packs = new List<ProtectedSignOffEvidencePack>
            {
                new ProtectedSignOffEvidencePack { PackId = "p1", HeadRef = "h1" }
            };
            var stub = new StubService { EvidenceHistoryImpl = _ =>
                Task.FromResult(new GetEvidencePackHistoryResponse
                    { Success = true, Packs = packs, TotalCount = 1 }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetEvidencePackHistory("h1", null);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as GetEvidencePackHistoryResponse;
            Assert.That(response!.Packs.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetEvidencePackHistory_PropagatesFiltersToService()
        {
            string? capturedHead = null;
            string? capturedCase = null;
            int? capturedMax = null;
            var stub = new StubService { EvidenceHistoryImpl = req =>
            {
                capturedHead = req.HeadRef;
                capturedCase = req.CaseId;
                capturedMax = req.MaxRecords;
                return Task.FromResult(new GetEvidencePackHistoryResponse { Success = true });
            }};
            var ctrl = BuildController(stub);

            await ctrl.GetEvidencePackHistory("sha-999", "case-filter", maxRecords: 10);

            Assert.That(capturedHead, Is.EqualTo("sha-999"));
            Assert.That(capturedCase, Is.EqualTo("case-filter"));
            Assert.That(capturedMax, Is.EqualTo(10));
        }

        [Test]
        public async Task GetEvidencePackHistory_EmptyPacks_ReturnsOkWithEmptyList()
        {
            var stub = new StubService { EvidenceHistoryImpl = _ =>
                Task.FromResult(new GetEvidencePackHistoryResponse
                    { Success = true, Packs = new(), TotalCount = 0 }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.GetEvidencePackHistory(null, null);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as GetEvidencePackHistoryResponse;
            Assert.That(response!.Packs, Is.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Anonymous actor fallback
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordApprovalWebhook_NoClaimsUser_FallsBackToAnonymous()
        {
            string? capturedActor = null;
            var stub = new StubService { RecordImpl = (_, actor) =>
            {
                capturedActor = actor;
                return Task.FromResult(new RecordApprovalWebhookResponse { Success = true });
            }};

            // Build controller without claims
            var controller = new ProtectedSignOffEvidencePersistenceController(
                stub, NullLogger<ProtectedSignOffEvidencePersistenceController>.Instance);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                    { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            await controller.RecordApprovalWebhook(new RecordApprovalWebhookRequest
                { CaseId = "c1", HeadRef = "h1", Outcome = ApprovalWebhookOutcome.Approved });

            Assert.That(capturedActor, Is.EqualTo("anonymous"));
        }
    }
}
