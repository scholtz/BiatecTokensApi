using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Controller-level tests for <see cref="KycAmlOnboardingController"/> covering HTTP
    /// response codes, request routing, actor ID propagation, correlation ID handling,
    /// and schema contracts.
    ///
    /// Uses a lightweight stub implementation of <see cref="IKycAmlOnboardingCaseService"/>
    /// to isolate controller behaviour from service logic.
    ///
    /// Coverage:
    /// CT01–CT06: CreateCase HTTP codes and propagation
    /// CT07–CT10: GetCase HTTP codes and propagation
    /// CT11–CT16: InitiateProviderChecks HTTP codes and propagation
    /// CT17–CT23: RecordReviewerAction HTTP codes and propagation
    /// CT24–CT27: GetEvidenceSummary HTTP codes and propagation
    /// CT28–CT30: ListCases and remaining scenarios
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlOnboardingControllerTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Stub service — configurable per test via delegates
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class StubService : IKycAmlOnboardingCaseService
        {
            public Func<CreateOnboardingCaseRequest, string, Task<CreateOnboardingCaseResponse>>?
                CreateCaseImpl;
            public Func<string, Task<GetOnboardingCaseResponse>>?
                GetCaseImpl;
            public Func<string, InitiateProviderChecksRequest, string, Task<InitiateProviderChecksResponse>>?
                InitiateChecksImpl;
            public Func<string, RecordReviewerActionRequest, string, Task<RecordReviewerActionResponse>>?
                ReviewerActionImpl;
            public Func<string, Task<GetOnboardingEvidenceSummaryResponse>>?
                GetEvidenceImpl;
            public Func<ListOnboardingCasesRequest?, Task<ListOnboardingCasesResponse>>?
                ListCasesImpl;

            public Task<CreateOnboardingCaseResponse> CreateCaseAsync(
                CreateOnboardingCaseRequest request, string actorId)
                => CreateCaseImpl != null
                    ? CreateCaseImpl(request, actorId)
                    : Task.FromResult(new CreateOnboardingCaseResponse { Success = true });

            public Task<GetOnboardingCaseResponse> GetCaseAsync(string caseId)
                => GetCaseImpl != null
                    ? GetCaseImpl(caseId)
                    : Task.FromResult(new GetOnboardingCaseResponse { Success = true });

            public Task<InitiateProviderChecksResponse> InitiateProviderChecksAsync(
                string caseId, InitiateProviderChecksRequest request, string actorId)
                => InitiateChecksImpl != null
                    ? InitiateChecksImpl(caseId, request, actorId)
                    : Task.FromResult(new InitiateProviderChecksResponse { Success = true });

            public Task<RecordReviewerActionResponse> RecordReviewerActionAsync(
                string caseId, RecordReviewerActionRequest request, string actorId)
                => ReviewerActionImpl != null
                    ? ReviewerActionImpl(caseId, request, actorId)
                    : Task.FromResult(new RecordReviewerActionResponse { Success = true });

            public Task<GetOnboardingEvidenceSummaryResponse> GetEvidenceSummaryAsync(string caseId)
                => GetEvidenceImpl != null
                    ? GetEvidenceImpl(caseId)
                    : Task.FromResult(new GetOnboardingEvidenceSummaryResponse { Success = true });

            public Task<ListOnboardingCasesResponse> ListCasesAsync(ListOnboardingCasesRequest? request = null)
                => ListCasesImpl != null
                    ? ListCasesImpl(request)
                    : Task.FromResult(new ListOnboardingCasesResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Factory helper
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a controller wired with an authenticated HttpContext.
        /// Uses <see cref="ClaimTypes.Name"/> because <c>GetActorId()</c> reads
        /// <c>User.Identity.Name</c>, which is backed by the Name claim type.
        /// </summary>
        private static KycAmlOnboardingController BuildController(
            IKycAmlOnboardingCaseService svc,
            string actorId = "test-actor",
            string? correlationIdHeader = null)
        {
            var controller = new KycAmlOnboardingController(
                svc, NullLogger<KycAmlOnboardingController>.Instance);

            // ClaimTypes.Name drives User.Identity.Name
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, actorId) }, "test");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            if (correlationIdHeader != null)
                httpContext.Request.Headers["X-Correlation-Id"] = correlationIdHeader;

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT01–CT06: POST /cases — CreateCase
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT01: Service success → 200 OK.</summary>
        [Test]
        public async Task CT01_CreateCase_ServiceSuccess_Returns200()
        {
            var stub = new StubService { CreateCaseImpl = (_, _) =>
                Task.FromResult(new CreateOnboardingCaseResponse { Success = true }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.CreateCase(new CreateOnboardingCaseRequest { SubjectId = "sub-1" });

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT01: success must return 200");
        }

        /// <summary>CT02: Service failure → 400 BadRequest.</summary>
        [Test]
        public async Task CT02_CreateCase_ServiceFailure_Returns400()
        {
            var stub = new StubService { CreateCaseImpl = (_, _) =>
                Task.FromResult(new CreateOnboardingCaseResponse
                    { Success = false, ErrorCode = "INVALID_SUBJECT_ID" }) };
            var ctrl = BuildController(stub);

            var result = await ctrl.CreateCase(new CreateOnboardingCaseRequest { SubjectId = "" });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>(), "CT02: failure must return 400");
        }

        /// <summary>CT03: Actor ID from JWT Name claim propagated to service.</summary>
        [Test]
        public async Task CT03_CreateCase_PropagatesActorIdToService()
        {
            string? captured = null;
            var stub = new StubService { CreateCaseImpl = (_, actor) =>
            {
                captured = actor;
                return Task.FromResult(new CreateOnboardingCaseResponse { Success = true });
            }};
            await BuildController(stub, "operator-abc")
                .CreateCase(new CreateOnboardingCaseRequest { SubjectId = "sub-1" });

            Assert.That(captured, Is.EqualTo("operator-abc"), "CT03: actor must be propagated");
        }

        /// <summary>CT04: SubjectId propagated to service.</summary>
        [Test]
        public async Task CT04_CreateCase_PropagatesSubjectIdToService()
        {
            string? capturedSubject = null;
            var stub = new StubService { CreateCaseImpl = (req, _) =>
            {
                capturedSubject = req.SubjectId;
                return Task.FromResult(new CreateOnboardingCaseResponse { Success = true });
            }};
            await BuildController(stub).CreateCase(new CreateOnboardingCaseRequest { SubjectId = "subject-999" });

            Assert.That(capturedSubject, Is.EqualTo("subject-999"), "CT04: SubjectId must be propagated");
        }

        /// <summary>CT05: Falls back to "anonymous" when no identity is present.</summary>
        [Test]
        public async Task CT05_CreateCase_FallsBackToAnonymousWhenNoIdentity()
        {
            string? captured = null;
            var stub = new StubService { CreateCaseImpl = (_, actor) =>
            {
                captured = actor;
                return Task.FromResult(new CreateOnboardingCaseResponse { Success = true });
            }};
            var ctrl = new KycAmlOnboardingController(stub, NullLogger<KycAmlOnboardingController>.Instance);
            ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            await ctrl.CreateCase(new CreateOnboardingCaseRequest { SubjectId = "s" });

            Assert.That(captured, Is.EqualTo("anonymous"), "CT05: must fall back to anonymous");
        }

        /// <summary>CT06: Response body returned in OkObjectResult.</summary>
        [Test]
        public async Task CT06_CreateCase_ResponseBody_IsReturnedInOkResult()
        {
            var caseRecord = new KycAmlOnboardingCase
            {
                CaseId    = "case-ct06",
                SubjectId = "sub-ct06",
                State     = KycAmlOnboardingCaseState.Initiated
            };
            var response = new CreateOnboardingCaseResponse { Success = true, Case = caseRecord };
            var stub = new StubService { CreateCaseImpl = (_, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub)
                .CreateCase(new CreateOnboardingCaseRequest { SubjectId = "sub-ct06" });

            var body = (CreateOnboardingCaseResponse)result.Value!;
            Assert.That(body.Case!.CaseId, Is.EqualTo("case-ct06"), "CT06: CaseId must be in body");
            Assert.That(body.Case.SubjectId, Is.EqualTo("sub-ct06"), "CT06: SubjectId must be in body");
            Assert.That(body.Case.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated),
                "CT06: State must be Initiated");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT07–CT10: GET /cases/{caseId} — GetCase
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT07: Service success → 200 OK.</summary>
        [Test]
        public async Task CT07_GetCase_ServiceSuccess_Returns200()
        {
            var stub = new StubService { GetCaseImpl = _ =>
                Task.FromResult(new GetOnboardingCaseResponse { Success = true }) };

            var result = await BuildController(stub).GetCase("case-1");

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT07: success must return 200");
        }

        /// <summary>CT08: Service failure → 404 NotFound.</summary>
        [Test]
        public async Task CT08_GetCase_ServiceFailure_Returns404()
        {
            var stub = new StubService { GetCaseImpl = _ =>
                Task.FromResult(new GetOnboardingCaseResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };

            var result = await BuildController(stub).GetCase("unknown");

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>(), "CT08: not found must return 404");
        }

        /// <summary>CT09: Case ID propagated to service.</summary>
        [Test]
        public async Task CT09_GetCase_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { GetCaseImpl = cid =>
            {
                captured = cid;
                return Task.FromResult(new GetOnboardingCaseResponse { Success = true });
            }};
            await BuildController(stub).GetCase("case-xyz");

            Assert.That(captured, Is.EqualTo("case-xyz"), "CT09: CaseId must be propagated");
        }

        /// <summary>CT10: Response body returned in OkObjectResult.</summary>
        [Test]
        public async Task CT10_GetCase_ResponseBody_IsReturnedInOkResult()
        {
            var caseRecord = new KycAmlOnboardingCase
            {
                CaseId    = "case-ct10",
                SubjectId = "sub-ct10",
                State     = KycAmlOnboardingCaseState.PendingReview
            };
            var response = new GetOnboardingCaseResponse { Success = true, Case = caseRecord };
            var stub = new StubService { GetCaseImpl = _ => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).GetCase("case-ct10");

            var body = (GetOnboardingCaseResponse)result.Value!;
            Assert.That(body.Case!.CaseId, Is.EqualTo("case-ct10"), "CT10: CaseId in body");
            Assert.That(body.Case.State, Is.EqualTo(KycAmlOnboardingCaseState.PendingReview),
                "CT10: State in body");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT11–CT16: POST /cases/{caseId}/initiate-checks — InitiateProviderChecks
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT11: Service success → 200 OK.</summary>
        [Test]
        public async Task CT11_InitiateProviderChecks_ServiceSuccess_Returns200()
        {
            var stub = new StubService { InitiateChecksImpl = (_, _, _) =>
                Task.FromResult(new InitiateProviderChecksResponse { Success = true }) };

            var result = await BuildController(stub).InitiateProviderChecks(
                "case-1", new InitiateProviderChecksRequest());

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT11: success must return 200");
        }

        /// <summary>CT12: Service failure with CASE_NOT_FOUND → 404 NotFound.</summary>
        [Test]
        public async Task CT12_InitiateProviderChecks_CaseNotFound_Returns404()
        {
            var stub = new StubService { InitiateChecksImpl = (_, _, _) =>
                Task.FromResult(new InitiateProviderChecksResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };

            var result = await BuildController(stub).InitiateProviderChecks(
                "unknown", new InitiateProviderChecksRequest());

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>(), "CT12: CASE_NOT_FOUND must return 404");
        }

        /// <summary>CT13: Service failure with other error code → 400 BadRequest.</summary>
        [Test]
        public async Task CT13_InitiateProviderChecks_OtherFailure_Returns400()
        {
            var stub = new StubService { InitiateChecksImpl = (_, _, _) =>
                Task.FromResult(new InitiateProviderChecksResponse
                    { Success = false, ErrorCode = "INVALID_STATE" }) };

            var result = await BuildController(stub).InitiateProviderChecks(
                "case-1", new InitiateProviderChecksRequest());

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>(), "CT13: other failure must return 400");
        }

        /// <summary>CT14: Case ID and actor ID propagated to service.</summary>
        [Test]
        public async Task CT14_InitiateProviderChecks_PropagatesCaseIdAndActorId()
        {
            string? capturedCase = null;
            string? capturedActor = null;
            var stub = new StubService { InitiateChecksImpl = (cid, _, actor) =>
            {
                capturedCase = cid;
                capturedActor = actor;
                return Task.FromResult(new InitiateProviderChecksResponse { Success = true });
            }};
            await BuildController(stub, "actor-initiate").InitiateProviderChecks(
                "case-initiate-42", new InitiateProviderChecksRequest());

            Assert.That(capturedCase, Is.EqualTo("case-initiate-42"), "CT14: CaseId propagated");
            Assert.That(capturedActor, Is.EqualTo("actor-initiate"), "CT14: ActorId propagated");
        }

        /// <summary>CT15: ExecutionMode propagated to service.</summary>
        [Test]
        public async Task CT15_InitiateProviderChecks_ExecutionModePropagatedToService()
        {
            KycAmlOnboardingExecutionMode? captured = null;
            var stub = new StubService { InitiateChecksImpl = (_, req, _) =>
            {
                captured = req.ExecutionMode;
                return Task.FromResult(new InitiateProviderChecksResponse { Success = true });
            }};
            await BuildController(stub).InitiateProviderChecks("case-1",
                new InitiateProviderChecksRequest
                    { ExecutionMode = KycAmlOnboardingExecutionMode.Simulated });

            Assert.That(captured, Is.EqualTo(KycAmlOnboardingExecutionMode.Simulated),
                "CT15: ExecutionMode must be propagated");
        }

        /// <summary>CT16: Response body returned in OkObjectResult.</summary>
        [Test]
        public async Task CT16_InitiateProviderChecks_ResponseBody_IsReturnedInOkResult()
        {
            var caseRecord = new KycAmlOnboardingCase
            {
                CaseId = "case-ct16",
                State  = KycAmlOnboardingCaseState.ProviderChecksStarted
            };
            var response = new InitiateProviderChecksResponse
            {
                Success             = true,
                Case                = caseRecord,
                VerificationJourneyId = "journey-ct16"
            };
            var stub = new StubService { InitiateChecksImpl = (_, _, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).InitiateProviderChecks(
                "case-ct16", new InitiateProviderChecksRequest());

            var body = (InitiateProviderChecksResponse)result.Value!;
            Assert.That(body.VerificationJourneyId, Is.EqualTo("journey-ct16"),
                "CT16: VerificationJourneyId in body");
            Assert.That(body.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted),
                "CT16: Case state in body");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT17–CT23: POST /cases/{caseId}/reviewer-actions — RecordReviewerAction
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT17: Service success → 200 OK.</summary>
        [Test]
        public async Task CT17_RecordReviewerAction_ServiceSuccess_Returns200()
        {
            var stub = new StubService { ReviewerActionImpl = (_, _, _) =>
                Task.FromResult(new RecordReviewerActionResponse { Success = true }) };

            var result = await BuildController(stub).RecordReviewerAction(
                "case-1", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote });

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT17: success must return 200");
        }

        /// <summary>CT18: Service failure with CASE_NOT_FOUND → 404 NotFound.</summary>
        [Test]
        public async Task CT18_RecordReviewerAction_CaseNotFound_Returns404()
        {
            var stub = new StubService { ReviewerActionImpl = (_, _, _) =>
                Task.FromResult(new RecordReviewerActionResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };

            var result = await BuildController(stub).RecordReviewerAction(
                "unknown", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve });

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>(), "CT18: CASE_NOT_FOUND must return 404");
        }

        /// <summary>CT19: Service failure with INVALID_STATE_TRANSITION → 400 BadRequest.</summary>
        [Test]
        public async Task CT19_RecordReviewerAction_InvalidStateTransition_Returns400()
        {
            var stub = new StubService { ReviewerActionImpl = (_, _, _) =>
                Task.FromResult(new RecordReviewerActionResponse
                    { Success = false, ErrorCode = "INVALID_STATE_TRANSITION" }) };

            var result = await BuildController(stub).RecordReviewerAction(
                "case-1", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve });

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>(),
                "CT19: INVALID_STATE_TRANSITION must return 400");
        }

        /// <summary>CT20: Case ID propagated to service.</summary>
        [Test]
        public async Task CT20_RecordReviewerAction_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { ReviewerActionImpl = (cid, _, _) =>
            {
                captured = cid;
                return Task.FromResult(new RecordReviewerActionResponse { Success = true });
            }};
            await BuildController(stub).RecordReviewerAction(
                "case-action-77", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote });

            Assert.That(captured, Is.EqualTo("case-action-77"), "CT20: CaseId propagated");
        }

        /// <summary>CT21: Actor ID from JWT Name claim propagated to service.</summary>
        [Test]
        public async Task CT21_RecordReviewerAction_PropagatesActorIdToService()
        {
            string? captured = null;
            var stub = new StubService { ReviewerActionImpl = (_, _, actor) =>
            {
                captured = actor;
                return Task.FromResult(new RecordReviewerActionResponse { Success = true });
            }};
            await BuildController(stub, "reviewer-007").RecordReviewerAction(
                "case-1", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate });

            Assert.That(captured, Is.EqualTo("reviewer-007"), "CT21: ActorId propagated");
        }

        /// <summary>CT22: Action kind propagated for all valid kinds.</summary>
        [TestCase(KycAmlOnboardingActionKind.Approve)]
        [TestCase(KycAmlOnboardingActionKind.Reject)]
        [TestCase(KycAmlOnboardingActionKind.Escalate)]
        [TestCase(KycAmlOnboardingActionKind.RequestAdditionalInfo)]
        [TestCase(KycAmlOnboardingActionKind.AddNote)]
        public async Task CT22_RecordReviewerAction_AllActionKinds_PropagatedToService(
            KycAmlOnboardingActionKind kind)
        {
            KycAmlOnboardingActionKind? captured = null;
            var stub = new StubService { ReviewerActionImpl = (_, req, _) =>
            {
                captured = req.Kind;
                return Task.FromResult(new RecordReviewerActionResponse { Success = true });
            }};
            await BuildController(stub).RecordReviewerAction(
                "case-1", new RecordReviewerActionRequest { Kind = kind });

            Assert.That(captured, Is.EqualTo(kind), $"CT22: Kind {kind} must be propagated");
        }

        /// <summary>CT23: Response body returned in OkObjectResult.</summary>
        [Test]
        public async Task CT23_RecordReviewerAction_ResponseBody_IsReturnedInOkResult()
        {
            var action = new KycAmlOnboardingActorAction
            {
                ActionId  = "action-ct23",
                Kind      = KycAmlOnboardingActionKind.Approve,
                ActorId   = "actor-ct23",
                Timestamp = DateTimeOffset.UtcNow,
                Rationale = "looks good"
            };
            var response = new RecordReviewerActionResponse
            {
                Success = true,
                Action  = action
            };
            var stub = new StubService { ReviewerActionImpl = (_, _, _) => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).RecordReviewerAction(
                "case-1", new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve });

            var body = (RecordReviewerActionResponse)result.Value!;
            Assert.That(body.Action!.ActionId, Is.EqualTo("action-ct23"), "CT23: ActionId in body");
            Assert.That(body.Action.Kind, Is.EqualTo(KycAmlOnboardingActionKind.Approve),
                "CT23: Action kind in body");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT24–CT27: GET /cases/{caseId}/evidence — GetEvidenceSummary
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT24: Service success → 200 OK.</summary>
        [Test]
        public async Task CT24_GetEvidenceSummary_ServiceSuccess_Returns200()
        {
            var stub = new StubService { GetEvidenceImpl = _ =>
                Task.FromResult(new GetOnboardingEvidenceSummaryResponse { Success = true }) };

            var result = await BuildController(stub).GetEvidenceSummary("case-1");

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT24: success must return 200");
        }

        /// <summary>CT25: Service failure → 404 NotFound.</summary>
        [Test]
        public async Task CT25_GetEvidenceSummary_ServiceFailure_Returns404()
        {
            var stub = new StubService { GetEvidenceImpl = _ =>
                Task.FromResult(new GetOnboardingEvidenceSummaryResponse
                    { Success = false, ErrorCode = "CASE_NOT_FOUND" }) };

            var result = await BuildController(stub).GetEvidenceSummary("missing");

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>(), "CT25: not found must return 404");
        }

        /// <summary>CT26: Case ID propagated to service.</summary>
        [Test]
        public async Task CT26_GetEvidenceSummary_PropagatesCaseIdToService()
        {
            string? captured = null;
            var stub = new StubService { GetEvidenceImpl = cid =>
            {
                captured = cid;
                return Task.FromResult(new GetOnboardingEvidenceSummaryResponse { Success = true });
            }};
            await BuildController(stub).GetEvidenceSummary("evidence-case-88");

            Assert.That(captured, Is.EqualTo("evidence-case-88"), "CT26: CaseId propagated");
        }

        /// <summary>CT27: Response body including Summary returned in OkObjectResult.</summary>
        [Test]
        public async Task CT27_GetEvidenceSummary_ResponseBody_IsReturnedInOkResult()
        {
            var summary = new KycAmlOnboardingEvidenceSummary
            {
                CaseId             = "case-ct27",
                EvidenceState      = KycAmlOnboardingEvidenceState.AuthoritativeProviderBacked,
                IsProviderBacked   = true,
                IsReleaseGrade     = true,
                IsProviderConfigured = true
            };
            var response = new GetOnboardingEvidenceSummaryResponse
            {
                Success = true,
                Summary = summary
            };
            var stub = new StubService { GetEvidenceImpl = _ => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).GetEvidenceSummary("case-ct27");

            var body = (GetOnboardingEvidenceSummaryResponse)result.Value!;
            Assert.That(body.Summary!.CaseId, Is.EqualTo("case-ct27"), "CT27: CaseId in summary");
            Assert.That(body.Summary.IsProviderBacked, Is.True, "CT27: IsProviderBacked in summary");
            Assert.That(body.Summary.IsReleaseGrade, Is.True, "CT27: IsReleaseGrade in summary");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CT28–CT30: GET /cases — ListCases and remaining scenarios
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>CT28: ListCases always returns 200 OK regardless of filter.</summary>
        [Test]
        public async Task CT28_ListCases_AlwaysReturns200()
        {
            var stub = new StubService { ListCasesImpl = _ =>
                Task.FromResult(new ListOnboardingCasesResponse { Success = true, Cases = new() }) };

            var result = await BuildController(stub).ListCases(null);

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), "CT28: list must always return 200");
        }

        /// <summary>CT29: ListCases response body contains the cases list and count.</summary>
        [Test]
        public async Task CT29_ListCases_ResponseBody_ContainsCasesAndCount()
        {
            var cases = new List<KycAmlOnboardingCase>
            {
                new() { CaseId = "c1", SubjectId = "s1", State = KycAmlOnboardingCaseState.Initiated },
                new() { CaseId = "c2", SubjectId = "s2", State = KycAmlOnboardingCaseState.Approved }
            };
            var response = new ListOnboardingCasesResponse
            {
                Success    = true,
                Cases      = cases,
                TotalCount = 2
            };
            var stub = new StubService { ListCasesImpl = _ => Task.FromResult(response) };

            var result = (OkObjectResult)await BuildController(stub).ListCases(null);

            var body = (ListOnboardingCasesResponse)result.Value!;
            Assert.That(body.Cases, Has.Count.EqualTo(2), "CT29: must contain 2 cases");
            Assert.That(body.TotalCount, Is.EqualTo(2), "CT29: TotalCount must be 2");
        }

        /// <summary>CT30: IdempotencyKey propagated to service on CreateCase.</summary>
        [Test]
        public async Task CT30_CreateCase_IdempotencyKey_PropagatedToService()
        {
            string? capturedKey = null;
            var stub = new StubService { CreateCaseImpl = (req, _) =>
            {
                capturedKey = req.IdempotencyKey;
                return Task.FromResult(new CreateOnboardingCaseResponse { Success = true });
            }};
            await BuildController(stub).CreateCase(new CreateOnboardingCaseRequest
            {
                SubjectId      = "sub-idem",
                IdempotencyKey = "idem-key-999"
            });

            Assert.That(capturedKey, Is.EqualTo("idem-key-999"),
                "CT30: IdempotencyKey must be propagated to service");
        }
    }
}
