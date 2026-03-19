using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.ProviderBackedCompliance;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="ProviderBackedComplianceController"/> covering HTTP response codes,
    /// request routing, actor ID propagation, correlation ID handling, and schema contract.
    ///
    /// Uses a lightweight stub implementation of <see cref="IProviderBackedComplianceExecutionService"/>
    /// to isolate controller behaviour from service logic (service logic is tested separately in
    /// <c>ProviderBackedComplianceExecutionTests.cs</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProviderBackedComplianceControllerTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Stub service — configurable per test via delegates
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class StubService : IProviderBackedComplianceExecutionService
        {
            public Func<string, ExecuteProviderBackedDecisionRequest, string, Task<ExecuteProviderBackedDecisionResponse>>?
                ExecuteDecisionImpl;
            public Func<string, string, string?, Task<GetProviderBackedExecutionStatusResponse>>?
                GetStatusImpl;
            public Func<string, BuildProviderBackedSignOffEvidenceRequest, string, Task<BuildProviderBackedSignOffEvidenceResponse>>?
                BuildSignOffImpl;

            public Task<ExecuteProviderBackedDecisionResponse> ExecuteDecisionAsync(
                string caseId, ExecuteProviderBackedDecisionRequest request, string actorId)
                => ExecuteDecisionImpl != null
                    ? ExecuteDecisionImpl(caseId, request, actorId)
                    : Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true });

            public Task<GetProviderBackedExecutionStatusResponse> GetExecutionStatusAsync(
                string caseId, string actorId, string? correlationId)
                => GetStatusImpl != null
                    ? GetStatusImpl(caseId, actorId, correlationId)
                    : Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });

            public Task<BuildProviderBackedSignOffEvidenceResponse> BuildSignOffEvidenceAsync(
                string caseId, BuildProviderBackedSignOffEvidenceRequest request, string actorId)
                => BuildSignOffImpl != null
                    ? BuildSignOffImpl(caseId, request, actorId)
                    : Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Factory helper
        // ═══════════════════════════════════════════════════════════════════════

        private static ProviderBackedComplianceController BuildController(
            IProviderBackedComplianceExecutionService svc,
            string actorId = "test-actor",
            string? correlationId = null)
        {
            var controller = new ProviderBackedComplianceController(
                svc, NullLogger<ProviderBackedComplianceController>.Instance);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, actorId) }, "test");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            if (correlationId != null)
                httpContext.Request.Headers["X-Correlation-Id"] = correlationId;

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static ExecuteProviderBackedDecisionRequest DefaultExecuteRequest(
            ProviderBackedCaseDecisionKind kind = ProviderBackedCaseDecisionKind.Approve)
            => new()
            {
                DecisionKind  = kind,
                ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
            };

        // ═══════════════════════════════════════════════════════════════════════
        // POST /{caseId}/execute — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_ServiceSuccess_Returns200()
        {
            var stub = new StubService { ExecuteDecisionImpl = (_, _, _) =>
                Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.ExecuteDecision("case-1", DefaultExecuteRequest());

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task ExecuteDecision_ServiceFailure_Returns400()
        {
            var stub = new StubService { ExecuteDecisionImpl = (_, _, _) =>
                Task.FromResult(new ExecuteProviderBackedDecisionResponse
                    { Success = false, ErrorCode = "CONFIG_MISSING" }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.ExecuteDecision("case-1", DefaultExecuteRequest(ProviderBackedCaseDecisionKind.Reject));

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ExecuteDecision_CaseNotFound_Returns404()
        {
            var stub = new StubService { ExecuteDecisionImpl = (_, _, _) =>
                Task.FromResult(new ExecuteProviderBackedDecisionResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.ExecuteDecision("unknown", DefaultExecuteRequest());

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task ExecuteDecision_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { ExecuteDecisionImpl = (cid, _, _) =>
            {
                captured = cid;
                return Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true });
            }};
            await BuildController(stub).ExecuteDecision("case-42", DefaultExecuteRequest());

            Assert.That(captured, Is.EqualTo("case-42"));
        }

        [Test]
        public async Task ExecuteDecision_PropagatesActorIdToService()
        {
            string? captured = null;
            var stub = new StubService { ExecuteDecisionImpl = (_, _, actor) =>
            {
                captured = actor;
                return Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true });
            }};
            await BuildController(stub, "operator-99").ExecuteDecision("case-1", DefaultExecuteRequest());

            Assert.That(captured, Is.EqualTo("operator-99"));
        }

        [Test]
        public async Task ExecuteDecision_FallsBackToAnonymousWhenNoClaim()
        {
            string? captured = null;
            var stub = new StubService { ExecuteDecisionImpl = (_, _, actor) =>
            {
                captured = actor;
                return Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true });
            }};
            // Controller without identity claims
            var ctrl = new ProviderBackedComplianceController(stub, NullLogger<ProviderBackedComplianceController>.Instance);
            ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            await ctrl.ExecuteDecision("case-1", DefaultExecuteRequest());

            Assert.That(captured, Is.EqualTo("anonymous"));
        }

        [Test]
        public async Task ExecuteDecision_ResponseBody_IsReturnedInOkResult()
        {
            var response = new ExecuteProviderBackedDecisionResponse { Success = true, ExecutionId = "exec-777" };
            var stub = new StubService { ExecuteDecisionImpl = (_, _, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).ExecuteDecision("case-1", DefaultExecuteRequest());

            Assert.That(((ExecuteProviderBackedDecisionResponse)result.Value!).ExecutionId, Is.EqualTo("exec-777"));
        }

        [TestCase(ProviderBackedCaseDecisionKind.Approve)]
        [TestCase(ProviderBackedCaseDecisionKind.Reject)]
        [TestCase(ProviderBackedCaseDecisionKind.ReturnForInformation)]
        [TestCase(ProviderBackedCaseDecisionKind.SanctionsReview)]
        [TestCase(ProviderBackedCaseDecisionKind.Escalate)]
        public async Task ExecuteDecision_AllDecisionKinds_PropagatedToService(ProviderBackedCaseDecisionKind kind)
        {
            ProviderBackedCaseDecisionKind? captured = null;
            var stub = new StubService { ExecuteDecisionImpl = (_, req, _) =>
            {
                captured = req.DecisionKind;
                return Task.FromResult(new ExecuteProviderBackedDecisionResponse { Success = true });
            }};
            await BuildController(stub).ExecuteDecision("case-1", DefaultExecuteRequest(kind));

            Assert.That(captured, Is.EqualTo(kind));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GET /{caseId}/status — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetExecutionStatus_ServiceSuccess_Returns200()
        {
            var stub = new StubService { GetStatusImpl = (_, _, _) =>
                Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true }) };

            var result = await BuildController(stub).GetExecutionStatus("case-1");

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetExecutionStatus_ServiceFailure_Returns404()
        {
            var stub = new StubService { GetStatusImpl = (_, _, _) =>
                Task.FromResult(new GetProviderBackedExecutionStatusResponse
                    { Success = false, ErrorMessage = "not found" }) };

            var result = await BuildController(stub).GetExecutionStatus("missing-case");

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetExecutionStatus_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (cid, _, _) =>
            {
                captured = cid;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            await BuildController(stub).GetExecutionStatus("case-status-99");

            Assert.That(captured, Is.EqualTo("case-status-99"));
        }

        [Test]
        public async Task GetExecutionStatus_PropagatesActorIdToService()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (_, actor, _) =>
            {
                captured = actor;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            await BuildController(stub, "status-actor").GetExecutionStatus("case-1");

            Assert.That(captured, Is.EqualTo("status-actor"));
        }

        [Test]
        public async Task GetExecutionStatus_PropagatesCorrelationIdFromHeader()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (_, _, corr) =>
            {
                captured = corr;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            await BuildController(stub, correlationId: "trace-xyz").GetExecutionStatus("case-1");

            Assert.That(captured, Is.EqualTo("trace-xyz"));
        }

        [Test]
        public async Task GetExecutionStatus_NoCorrelationHeader_GeneratesGuid()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (_, _, corr) =>
            {
                captured = corr;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            await BuildController(stub).GetExecutionStatus("case-1");

            Assert.That(captured, Is.Not.Null.And.Not.Empty);
            Assert.That(Guid.TryParse(captured, out _), Is.True, $"Expected GUID, got: {captured}");
        }

        [Test]
        public async Task GetExecutionStatus_ResponseBody_IsReturnedInOkResult()
        {
            var response = new GetProviderBackedExecutionStatusResponse
            {
                Success = true,
                CaseId  = "case-body-test",
                Status  = ProviderBackedCaseExecutionStatus.Completed
            };
            var stub = new StubService { GetStatusImpl = (_, _, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).GetExecutionStatus("case-body-test");

            var body = (GetProviderBackedExecutionStatusResponse)result.Value!;
            Assert.That(body.CaseId, Is.EqualTo("case-body-test"));
            Assert.That(body.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.Completed));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST /{caseId}/sign-off — HTTP status code tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task BuildSignOffEvidence_ServiceSuccess_Returns200()
        {
            var stub = new StubService { BuildSignOffImpl = (_, _, _) =>
                Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse { Success = true }) };

            var result = await BuildController(stub).BuildSignOffEvidence("case-1", new BuildProviderBackedSignOffEvidenceRequest());

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task BuildSignOffEvidence_ServiceFailure_Returns400()
        {
            var stub = new StubService { BuildSignOffImpl = (_, _, _) =>
                Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse
                    { Success = false, ErrorCode = "SIMULATED_EVIDENCE_NOT_ALLOWED" }) };

            var result = await BuildController(stub).BuildSignOffEvidence("case-1",
                new BuildProviderBackedSignOffEvidenceRequest { RequireProviderBackedEvidence = true });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task BuildSignOffEvidence_CaseNotFound_Returns404()
        {
            var stub = new StubService { BuildSignOffImpl = (_, _, _) =>
                Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };

            var result = await BuildController(stub).BuildSignOffEvidence("unknown", new BuildProviderBackedSignOffEvidenceRequest());

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task BuildSignOffEvidence_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { BuildSignOffImpl = (cid, _, _) =>
            {
                captured = cid;
                return Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse { Success = true });
            }};
            await BuildController(stub).BuildSignOffEvidence("signoff-99", new BuildProviderBackedSignOffEvidenceRequest());

            Assert.That(captured, Is.EqualTo("signoff-99"));
        }

        [Test]
        public async Task BuildSignOffEvidence_PropagatesActorIdToService()
        {
            string? captured = null;
            var stub = new StubService { BuildSignOffImpl = (_, _, actor) =>
            {
                captured = actor;
                return Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse { Success = true });
            }};
            await BuildController(stub, "signoff-actor").BuildSignOffEvidence("case-1", new BuildProviderBackedSignOffEvidenceRequest());

            Assert.That(captured, Is.EqualTo("signoff-actor"));
        }

        [Test]
        public async Task BuildSignOffEvidence_RequireProviderBackedFlag_PropagatedToService()
        {
            bool? capturedFlag = null;
            var stub = new StubService { BuildSignOffImpl = (_, req, _) =>
            {
                capturedFlag = req.RequireProviderBackedEvidence;
                return Task.FromResult(new BuildProviderBackedSignOffEvidenceResponse { Success = true });
            }};
            await BuildController(stub).BuildSignOffEvidence("case-1",
                new BuildProviderBackedSignOffEvidenceRequest { RequireProviderBackedEvidence = true });

            Assert.That(capturedFlag, Is.True);
        }

        [Test]
        public async Task BuildSignOffEvidence_ResponseBody_IsReturnedInOkResult()
        {
            var response = new BuildProviderBackedSignOffEvidenceResponse
            {
                Success = true,
                Bundle = new ProviderBackedCaseSignOffEvidenceBundle
                {
                    IsReleaseGrade = true,
                    ContentHash    = "sha256-xyz"
                }
            };
            var stub = new StubService { BuildSignOffImpl = (_, _, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).BuildSignOffEvidence("case-1", new BuildProviderBackedSignOffEvidenceRequest());

            var body = (BuildProviderBackedSignOffEvidenceResponse)result.Value!;
            Assert.That(body.Bundle!.IsReleaseGrade, Is.True);
            Assert.That(body.Bundle.ContentHash, Is.EqualTo("sha256-xyz"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Actor ID fallback chain
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetExecutionStatus_ActorId_FallsBackToNameidClaim()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (_, actor, _) =>
            {
                captured = actor;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            var ctrl = new ProviderBackedComplianceController(stub, NullLogger<ProviderBackedComplianceController>.Instance);
            var identity = new ClaimsIdentity(new[] { new Claim("nameid", "nameid-actor") }, "test");
            ctrl.ControllerContext = new ControllerContext
                { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            await ctrl.GetExecutionStatus("case-1");

            Assert.That(captured, Is.EqualTo("nameid-actor"));
        }

        [Test]
        public async Task GetExecutionStatus_ActorId_FallsBackToSubClaim()
        {
            string? captured = null;
            var stub = new StubService { GetStatusImpl = (_, actor, _) =>
            {
                captured = actor;
                return Task.FromResult(new GetProviderBackedExecutionStatusResponse { Success = true });
            }};
            var ctrl = new ProviderBackedComplianceController(stub, NullLogger<ProviderBackedComplianceController>.Instance);
            var identity = new ClaimsIdentity(new[] { new Claim("sub", "sub-actor") }, "test");
            ctrl.ControllerContext = new ControllerContext
                { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            await ctrl.GetExecutionStatus("case-1");

            Assert.That(captured, Is.EqualTo("sub-actor"));
        }
    }
}
