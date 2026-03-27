using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEventBackboneTests
    {
        [Test]
        public async Task GetEvents_NotConfiguredReleaseReadiness_RemainsFailClosed()
        {
            var repo = new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance);
            var onboarding = new FakeOnboardingCaseService
            {
                Cases =
                [
                    new KycAmlOnboardingCase
                    {
                        CaseId = "onboarding-case-1",
                        SubjectId = "subject-1",
                        CreatedAt = DateTimeOffset.Parse("2026-03-27T10:00:00Z"),
                        UpdatedAt = DateTimeOffset.Parse("2026-03-27T10:05:00Z"),
                        State = KycAmlOnboardingCaseState.ConfigurationMissing,
                        EvidenceState = KycAmlOnboardingEvidenceState.MissingConfiguration,
                        Timeline =
                        [
                            new KycAmlOnboardingTimelineEvent
                            {
                                EventId = "onb-tl-1",
                                EventType = KycAmlOnboardingTimelineEventType.ProviderConfigurationMissing,
                                OccurredAt = DateTimeOffset.Parse("2026-03-27T10:05:00Z"),
                                ActorId = "system",
                                Summary = "Provider checks could not start because configuration is missing.",
                                ToState = KycAmlOnboardingCaseState.ConfigurationMissing
                            }
                        ]
                    }
                ]
            };

            var protectedSignOff = new FakeProtectedSignOffEvidencePersistenceService
            {
                ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                {
                    Success = true,
                    HeadRef = "head-1",
                    Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                    EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                    Mode = StrictArtifactMode.NotConfigured,
                    EvaluatedAt = DateTimeOffset.Parse("2026-03-27T10:06:00Z"),
                    OperatorGuidance = "Configure the protected sign-off environment before claiming release readiness.",
                    LatestApprovalWebhook = new ApprovalWebhookRecord
                    {
                        RecordId = "webhook-1",
                        CaseId = "onboarding-case-1",
                        HeadRef = "head-1",
                        ReceivedAt = DateTimeOffset.Parse("2026-03-27T10:04:00Z")
                    }
                }
            };

            var service = new ComplianceEventBackboneService(
                repo,
                onboarding,
                protectedSignOff,
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "onboarding-case-1",
                HeadRef = "head-1"
            }, "operator-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Events.Any(evt => evt.EventType == ComplianceEventType.ReleaseReadinessEvaluated), Is.True);
            Assert.That(result.CurrentState.ReleaseReadiness, Is.Not.Null);
            Assert.That(result.CurrentState.ReleaseReadiness!.Mode, Is.EqualTo(StrictArtifactMode.NotConfigured));
            Assert.That(result.CurrentState.CurrentFreshness, Is.EqualTo(ComplianceEventFreshness.NotConfigured));
            Assert.That(result.CurrentState.CurrentDeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.NotConfigured));
            Assert.That(result.CurrentState.HasNotConfigured, Is.True);
        }

        [Test]
        public async Task GetEvents_FailedDeliveryFilter_ReturnsOnlyFailedDeliveryEvents()
        {
            var repo = new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance);
            await repo.SaveCaseAsync(new ComplianceCase
            {
                CaseId = "compliance-case-1",
                SubjectId = "subject-2",
                IssuerId = "issuer-2",
                State = ComplianceCaseState.UnderReview,
                CreatedAt = DateTimeOffset.Parse("2026-03-27T10:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-03-27T10:03:00Z"),
                Timeline =
                [
                    new CaseTimelineEntry
                    {
                        EntryId = "case-timeline-1",
                        CaseId = "compliance-case-1",
                        EventType = CaseTimelineEventType.CaseCreated,
                        OccurredAt = DateTimeOffset.Parse("2026-03-27T10:00:00Z"),
                        ActorId = "operator",
                        Description = "Case created."
                    }
                ],
                DeliveryRecords =
                [
                    new CaseDeliveryRecord
                    {
                        DeliveryId = "delivery-success",
                        CaseId = "compliance-case-1",
                        EventId = "evt-1",
                        EventType = BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceCaseCreated,
                        Outcome = CaseDeliveryOutcome.Success,
                        AttemptedAt = DateTimeOffset.Parse("2026-03-27T10:01:00Z"),
                        AttemptCount = 1
                    },
                    new CaseDeliveryRecord
                    {
                        DeliveryId = "delivery-failure",
                        CaseId = "compliance-case-1",
                        EventId = "evt-2",
                        EventType = BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceCaseStateTransitioned,
                        Outcome = CaseDeliveryOutcome.RetryExhausted,
                        AttemptedAt = DateTimeOffset.Parse("2026-03-27T10:02:00Z"),
                        AttemptCount = 3,
                        LastErrorSummary = "Connection refused",
                        RecommendedAction = "Verify webhook endpoint configuration."
                    }
                ]
            });

            var service = new ComplianceEventBackboneService(
                repo,
                new FakeOnboardingCaseService(),
                new FakeProtectedSignOffEvidencePersistenceService(),
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "compliance-case-1",
                DeliveryStatus = ComplianceEventDeliveryStatus.Failed,
                Page = 1,
                PageSize = 1
            }, "operator-2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].EventType, Is.EqualTo(ComplianceEventType.NotificationDeliveryUpdated));
            Assert.That(result.Events[0].DeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.Failed));
            Assert.That(result.CurrentState.CurrentDeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.Failed));
        }

        [Test]
        public async Task GetEvents_SubjectFilter_IncludesComplianceAuditExportEvents()
        {
            var service = new ComplianceEventBackboneService(
                new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance),
                new FakeOnboardingCaseService(),
                new FakeProtectedSignOffEvidencePersistenceService(),
                new FakeComplianceAuditExportService
                {
                    Exports =
                    [
                        new ComplianceAuditExportSummary
                        {
                            ExportId = "export-1",
                            SubjectId = "subject-3",
                            Scenario = AuditScenario.OnboardingCaseReview,
                            Readiness = AuditExportReadiness.RequiresReview,
                            AssembledAt = DateTime.Parse("2026-03-27T10:03:00Z"),
                            ContentHash = "hash-1"
                        }
                    ]
                },
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                SubjectId = "subject-3"
            }, "operator-3");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Events.Any(evt => evt.EventType == ComplianceEventType.ComplianceAuditExportGenerated), Is.True);
        }

        [Test]
        public async Task GetEvents_CaseAndHeadRefFilter_IncludesReleaseReadinessEventWhenEvidenceIsMissing()
        {
            var service = new ComplianceEventBackboneService(
                new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance),
                new FakeOnboardingCaseService(),
                new FakeProtectedSignOffEvidencePersistenceService
                {
                    ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                    {
                        Success = true,
                        HeadRef = "head-filtered-1",
                        Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                        EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                        Mode = StrictArtifactMode.NotConfigured,
                        EvaluatedAt = DateTimeOffset.Parse("2026-03-27T10:07:00Z"),
                        OperatorGuidance = "Provision protected sign-off configuration before release."
                    }
                },
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "case-filtered-1",
                HeadRef = "head-filtered-1"
            }, "operator-filter");

            var releaseEvent = result.Events.Single(evt => evt.EventType == ComplianceEventType.ReleaseReadinessEvaluated);
            Assert.That(result.Success, Is.True);
            Assert.That(releaseEvent.CaseId, Is.EqualTo("case-filtered-1"));
            Assert.That(releaseEvent.HeadRef, Is.EqualTo("head-filtered-1"));
            Assert.That(releaseEvent.Payload["status"], Is.EqualTo(SignOffReleaseReadinessStatus.BlockedMissingConfiguration.ToString()));
            Assert.That(releaseEvent.Payload["operatorGuidance"], Does.Contain("Provision protected sign-off configuration"));
            Assert.That(releaseEvent.RecommendedAction, Does.Contain("Provision protected sign-off configuration"));
        }

        [Test]
        public async Task GetEvents_CaseAndHeadRefWithFreshnessAndDeliveryFilters_IncludesReleaseReadinessEvent()
        {
            var service = new ComplianceEventBackboneService(
                new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance),
                new FakeOnboardingCaseService(),
                new FakeProtectedSignOffEvidencePersistenceService
                {
                    ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                    {
                        Success = true,
                        HeadRef = "head-filtered-2",
                        Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                        EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                        Mode = StrictArtifactMode.NotConfigured,
                        EvaluatedAt = DateTimeOffset.Parse("2026-03-27T10:08:00Z"),
                        OperatorGuidance = "Provide protected environment configuration before release."
                    }
                },
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "case-filtered-2",
                HeadRef = "head-filtered-2",
                Freshness = ComplianceEventFreshness.NotConfigured,
                DeliveryStatus = ComplianceEventDeliveryStatus.NotConfigured
            }, "operator-filter");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].EventType, Is.EqualTo(ComplianceEventType.ReleaseReadinessEvaluated));
            Assert.That(result.Events[0].CaseId, Is.EqualTo("case-filtered-2"));
            Assert.That(result.Events[0].Freshness, Is.EqualTo(ComplianceEventFreshness.NotConfigured));
            Assert.That(result.Events[0].DeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.NotConfigured));
        }

        [Test]
        public async Task GetEvents_CaseAndHeadRefPagination_OrdersProtectedEventsNewestFirst()
        {
            var protectedSignOff = new FakeProtectedSignOffEvidencePersistenceService
            {
                ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                {
                    Success = true,
                    HeadRef = "head-filtered-3",
                    Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                    EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                    Mode = StrictArtifactMode.NotConfigured,
                    EvaluatedAt = DateTimeOffset.Parse("2026-03-27T10:09:00Z"),
                    OperatorGuidance = "Provide protected environment configuration before release."
                }
            };
            protectedSignOff.EvidencePacks.Add(new ProtectedSignOffEvidencePack
            {
                PackId = "pack-filtered-3",
                CaseId = "case-filtered-3",
                HeadRef = "head-filtered-3",
                CreatedAt = DateTimeOffset.Parse("2026-03-27T10:08:00Z"),
                CreatedBy = "operator-pack",
                FreshnessStatus = SignOffEvidenceFreshnessStatus.Partial,
                IsReleaseGrade = false,
                IsProviderBacked = false
            });
            protectedSignOff.ApprovalWebhookHistory.Add(new ApprovalWebhookRecord
            {
                RecordId = "webhook-filtered-3",
                CaseId = "case-filtered-3",
                HeadRef = "head-filtered-3",
                ReceivedAt = DateTimeOffset.Parse("2026-03-27T10:07:00Z"),
                ActorId = "operator-webhook",
                Outcome = ApprovalWebhookOutcome.Approved,
                IsValid = true
            });

            var service = new ComplianceEventBackboneService(
                new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance),
                new FakeOnboardingCaseService(),
                protectedSignOff,
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse firstPage = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "case-filtered-3",
                HeadRef = "head-filtered-3",
                Page = 1,
                PageSize = 1
            }, "operator-filter");
            ComplianceEventListResponse secondPage = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "case-filtered-3",
                HeadRef = "head-filtered-3",
                Page = 2,
                PageSize = 1
            }, "operator-filter");
            ComplianceEventListResponse thirdPage = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = "case-filtered-3",
                HeadRef = "head-filtered-3",
                Page = 3,
                PageSize = 1
            }, "operator-filter");

            Assert.That(firstPage.TotalCount, Is.EqualTo(3));
            Assert.That(firstPage.Events[0].EventType, Is.EqualTo(ComplianceEventType.ReleaseReadinessEvaluated));
            Assert.That(secondPage.Events[0].EventType, Is.EqualTo(ComplianceEventType.ProtectedSignOffEvidenceCaptured));
            Assert.That(thirdPage.Events[0].EventType, Is.EqualTo(ComplianceEventType.ProtectedSignOffApprovalWebhookRecorded));
            Assert.That(firstPage.CurrentState.TotalEvents, Is.EqualTo(3));
        }

        [Test]
        public async Task GetEvents_FreshnessFilter_AwaitingProviderCallback_ReturnsOnboardingPendingSignal()
        {
            var service = new ComplianceEventBackboneService(
                new ComplianceCaseRepository(NullLogger<ComplianceCaseRepository>.Instance),
                new FakeOnboardingCaseService
                {
                    Cases =
                    [
                        new KycAmlOnboardingCase
                        {
                            CaseId = "onboarding-pending-1",
                            SubjectId = "subject-pending-1",
                            State = KycAmlOnboardingCaseState.ProviderChecksStarted,
                            EvidenceState = KycAmlOnboardingEvidenceState.PendingVerification,
                            CreatedAt = DateTimeOffset.Parse("2026-03-27T10:00:00Z"),
                            UpdatedAt = DateTimeOffset.Parse("2026-03-27T10:04:00Z"),
                            Timeline =
                            [
                                new KycAmlOnboardingTimelineEvent
                                {
                                    EventId = "pending-event-1",
                                    EventType = KycAmlOnboardingTimelineEventType.ProviderChecksInitiated,
                                    OccurredAt = DateTimeOffset.Parse("2026-03-27T10:04:00Z"),
                                    ActorId = "operator-pending",
                                    Summary = "Provider checks initiated for subject subject-pending-1.",
                                    ToState = KycAmlOnboardingCaseState.ProviderChecksStarted
                                }
                            ]
                        }
                    ]
                },
                new FakeProtectedSignOffEvidencePersistenceService(),
                new FakeComplianceAuditExportService(),
                NullLogger<ComplianceEventBackboneService>.Instance);

            ComplianceEventListResponse result = await service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                SubjectId = "subject-pending-1",
                Freshness = ComplianceEventFreshness.AwaitingProviderCallback
            }, "operator-pending");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].EventType, Is.EqualTo(ComplianceEventType.OnboardingProviderChecksInitiated));
            Assert.That(result.Events[0].Payload["subjectId"], Is.EqualTo("subject-pending-1"));
            Assert.That(result.Events[0].Payload["evidenceState"], Is.EqualTo(KycAmlOnboardingEvidenceState.PendingVerification.ToString()));
            Assert.That(result.Events[0].Payload["toState"], Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted.ToString()));
            Assert.That(result.CurrentState.CurrentFreshness, Is.EqualTo(ComplianceEventFreshness.AwaitingProviderCallback));
        }

        [Test]
        public async Task GetEventsApi_Unauthenticated_ReturnsUnauthorized()
        {
            await using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/api/v1/compliance-events");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetCaseTimelineApi_ReturnsPaginatedTypedEvents()
        {
            await using var factory = new CustomWebApplicationFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            var createResponse = await client.PostAsJsonAsync("/api/v1/compliance-cases", new CreateComplianceCaseRequest
            {
                IssuerId = "issuer-events",
                SubjectId = "subject-events",
                Type = CaseType.InvestorEligibility,
                Priority = CasePriority.High,
                Jurisdiction = "US"
            });
            createResponse.EnsureSuccessStatusCode();
            var createBody = await createResponse.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            var caseId = createBody!.Case!.CaseId;

            var evidenceResponse = await client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/evidence", new AddEvidenceRequest
            {
                EvidenceType = "KYC_DOCUMENT",
                Status = CaseEvidenceStatus.Valid,
                Summary = "Passport verified",
                CapturedAt = DateTimeOffset.UtcNow
            });
            evidenceResponse.EnsureSuccessStatusCode();

            HttpResponseMessage timelineResponse = await client.GetAsync($"/api/v1/compliance-events/cases/{caseId}/timeline?page=1&pageSize=1");
            timelineResponse.EnsureSuccessStatusCode();
            ComplianceEventListResponse? timeline = await timelineResponse.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            Assert.That(timeline, Is.Not.Null);
            Assert.That(timeline!.Success, Is.True);
            Assert.That(timeline.TotalCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(timeline.Events, Has.Count.EqualTo(1));
            Assert.That(timeline.CurrentState.TotalEvents, Is.GreaterThanOrEqualTo(2));
            Assert.That(timeline.Events[0].EventType, Is.AnyOf(
                ComplianceEventType.ComplianceCaseEvidenceUpdated,
                ComplianceEventType.ComplianceCaseCreated));
        }

        [Test]
        public async Task GetEventsApi_SubjectFilter_ReturnsAuditExportMilestone()
        {
            await using var factory = new CustomWebApplicationFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            string subjectId = $"subject-export-{Guid.NewGuid():N}";
            var onboardingCreate = await client.PostAsJsonAsync("/api/v1/kyc-aml-onboarding/cases", new CreateOnboardingCaseRequest
            {
                SubjectId = subjectId,
                SubjectKind = KycAmlOnboardingSubjectKind.Individual
            });
            onboardingCreate.EnsureSuccessStatusCode();
            var onboardingBody = await onboardingCreate.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();

            var exportAssembly = await client.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review", new OnboardingCaseReviewExportRequest
            {
                SubjectId = subjectId,
                CaseId = onboardingBody!.Case!.CaseId,
                AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
            });
            exportAssembly.EnsureSuccessStatusCode();

            HttpResponseMessage eventsResponse = await client.GetAsync($"/api/v1/compliance-events?subjectId={Uri.EscapeDataString(subjectId)}&page=1&pageSize=100");
            eventsResponse.EnsureSuccessStatusCode();
            ComplianceEventListResponse? events = await eventsResponse.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            Assert.That(events, Is.Not.Null);
            Assert.That(events!.Events.Any(evt => evt.EventType == ComplianceEventType.ComplianceAuditExportGenerated), Is.True);
        }

        [Test]
        public async Task GetEventsApi_HeadRefFilter_ExposesNotConfiguredReleaseState()
        {
            await using var factory = new NotConfiguredReleaseReadinessFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            var createResponse = await client.PostAsJsonAsync("/api/v1/compliance-cases", new CreateComplianceCaseRequest
            {
                IssuerId = "issuer-release",
                SubjectId = "subject-release",
                Type = CaseType.LaunchPackage,
                Priority = CasePriority.Critical,
                Jurisdiction = "US"
            });
            createResponse.EnsureSuccessStatusCode();
            var createBody = await createResponse.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            var caseId = createBody!.Case!.CaseId;

            HttpResponseMessage response = await client.GetAsync("/api/v1/compliance-events?headRef=head-release-1&page=1&pageSize=50");
            response.EnsureSuccessStatusCode();
            ComplianceEventListResponse? result = await response.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.CurrentState.ReleaseReadiness, Is.Not.Null);
            Assert.That(result.CurrentState.ReleaseReadiness!.Mode, Is.EqualTo(StrictArtifactMode.NotConfigured));
            Assert.That(result.CurrentState.CurrentFreshness, Is.EqualTo(ComplianceEventFreshness.NotConfigured));
            Assert.That(result.Events.Any(evt => evt.EventType == ComplianceEventType.ReleaseReadinessEvaluated), Is.True);
        }

        [Test]
        public async Task GetCaseTimelineApi_WithHeadRef_IncludesReleaseReadinessEventForRequestedCase()
        {
            await using var factory = new NotConfiguredReleaseReadinessFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            var createResponse = await client.PostAsJsonAsync("/api/v1/compliance-cases", new CreateComplianceCaseRequest
            {
                IssuerId = "issuer-release-case",
                SubjectId = "subject-release-case",
                Type = CaseType.LaunchPackage,
                Priority = CasePriority.Critical,
                Jurisdiction = "US"
            });
            createResponse.EnsureSuccessStatusCode();
            var createBody = await createResponse.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            var caseId = createBody!.Case!.CaseId;

            HttpResponseMessage response = await client.GetAsync($"/api/v1/compliance-events/cases/{caseId}/timeline?headRef=head-release-1&page=1&pageSize=100");
            response.EnsureSuccessStatusCode();
            ComplianceEventListResponse? result = await response.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            var releaseEvent = result!.Events.Single(evt => evt.EventType == ComplianceEventType.ReleaseReadinessEvaluated);
            Assert.That(result, Is.Not.Null);
            Assert.That(releaseEvent.CaseId, Is.EqualTo(caseId));
            Assert.That(releaseEvent.HeadRef, Is.EqualTo("head-release-1"));
            Assert.That(releaseEvent.Payload["status"], Is.EqualTo(SignOffReleaseReadinessStatus.BlockedMissingConfiguration.ToString()));
            Assert.That(releaseEvent.Payload["evidenceFreshness"], Is.EqualTo(SignOffEvidenceFreshnessStatus.Unavailable.ToString()));
            Assert.That(releaseEvent.Payload["mode"], Is.EqualTo(StrictArtifactMode.NotConfigured.ToString()));
            Assert.That(releaseEvent.Payload["operatorGuidance"], Does.Contain("Provide protected environment configuration"));
            Assert.That(result.CurrentState.ReleaseReadiness!.Mode, Is.EqualTo(StrictArtifactMode.NotConfigured));
        }

        [Test]
        public async Task GetCaseTimelineApi_WithHeadRefFreshnessAndDeliveryFilters_ReturnsReleaseReadinessEventForRequestedCase()
        {
            await using var factory = new NotConfiguredReleaseReadinessFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            var createResponse = await client.PostAsJsonAsync("/api/v1/compliance-cases", new CreateComplianceCaseRequest
            {
                IssuerId = "issuer-release-case-filtered",
                SubjectId = "subject-release-case-filtered",
                Type = CaseType.LaunchPackage,
                Priority = CasePriority.Critical,
                Jurisdiction = "US"
            });
            createResponse.EnsureSuccessStatusCode();
            var createBody = await createResponse.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            var caseId = createBody!.Case!.CaseId;

            HttpResponseMessage response = await client.GetAsync(
                $"/api/v1/compliance-events/cases/{caseId}/timeline?headRef=head-release-1&freshness=NotConfigured&deliveryStatus=NotConfigured&page=1&pageSize=100");
            response.EnsureSuccessStatusCode();
            ComplianceEventListResponse? result = await response.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TotalCount, Is.EqualTo(1));
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].EventType, Is.EqualTo(ComplianceEventType.ReleaseReadinessEvaluated));
            Assert.That(result.Events[0].CaseId, Is.EqualTo(caseId));
            Assert.That(result.Events[0].HeadRef, Is.EqualTo("head-release-1"));
            Assert.That(result.Events[0].Freshness, Is.EqualTo(ComplianceEventFreshness.NotConfigured));
            Assert.That(result.Events[0].DeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.NotConfigured));
        }

        [Test]
        public async Task GetCaseTimelineApi_WithHeadRefPagination_OrdersProtectedEventsNewestFirst()
        {
            await using var factory = new OrderedProtectedEventsFactory();
            using HttpClient client = await CreateAuthenticatedClientAsync(factory);

            HttpResponseMessage firstPageResponse = await client.GetAsync(
                "/api/v1/compliance-events/cases/case-ordered-1/timeline?headRef=head-ordered-1&page=1&pageSize=1");
            firstPageResponse.EnsureSuccessStatusCode();
            ComplianceEventListResponse? firstPage = await firstPageResponse.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            HttpResponseMessage secondPageResponse = await client.GetAsync(
                "/api/v1/compliance-events/cases/case-ordered-1/timeline?headRef=head-ordered-1&page=2&pageSize=1");
            secondPageResponse.EnsureSuccessStatusCode();
            ComplianceEventListResponse? secondPage = await secondPageResponse.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            HttpResponseMessage thirdPageResponse = await client.GetAsync(
                "/api/v1/compliance-events/cases/case-ordered-1/timeline?headRef=head-ordered-1&page=3&pageSize=1");
            thirdPageResponse.EnsureSuccessStatusCode();
            ComplianceEventListResponse? thirdPage = await thirdPageResponse.Content.ReadFromJsonAsync<ComplianceEventListResponse>();

            Assert.That(firstPage, Is.Not.Null);
            Assert.That(secondPage, Is.Not.Null);
            Assert.That(thirdPage, Is.Not.Null);
            Assert.That(firstPage!.TotalCount, Is.EqualTo(3));
            Assert.That(firstPage.Events[0].EventType, Is.EqualTo(ComplianceEventType.ReleaseReadinessEvaluated));
            Assert.That(secondPage!.Events[0].EventType, Is.EqualTo(ComplianceEventType.ProtectedSignOffEvidenceCaptured));
            Assert.That(thirdPage!.Events[0].EventType, Is.EqualTo(ComplianceEventType.ProtectedSignOffApprovalWebhookRecorded));
            Assert.That(firstPage.CurrentState.HighestSeverity, Is.EqualTo(ComplianceEventSeverity.Critical));
            Assert.That(firstPage.CurrentState.CurrentDeliveryStatus, Is.EqualTo(ComplianceEventDeliveryStatus.NotConfigured));
            Assert.That(firstPage.CurrentState.RecommendedAction, Does.Contain("Provide protected environment configuration"));
        }

        private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<BiatecTokensApi.Program> factory)
        {
            HttpClient bootstrapClient = factory.CreateClient();
            string email = $"compliance-events-{Guid.NewGuid():N}@biatec-test.example.com";
            var registerResponse = await bootstrapClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Compliance Events Test"
            });
            registerResponse.EnsureSuccessStatusCode();
            RegisterResponse? registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerBody!.AccessToken);
            return client;
        }

        private sealed class FakeOnboardingCaseService : IKycAmlOnboardingCaseService
        {
            public List<KycAmlOnboardingCase> Cases { get; init; } = new();

            public Task<CreateOnboardingCaseResponse> CreateCaseAsync(CreateOnboardingCaseRequest request, string actorId)
                => Task.FromResult(new CreateOnboardingCaseResponse { Success = true });

            public Task<GetOnboardingCaseResponse> GetCaseAsync(string caseId)
                => Task.FromResult(new GetOnboardingCaseResponse { Success = true, Case = Cases.FirstOrDefault(c => c.CaseId == caseId) });

            public Task<InitiateProviderChecksResponse> InitiateProviderChecksAsync(string caseId, InitiateProviderChecksRequest request, string actorId)
                => Task.FromResult(new InitiateProviderChecksResponse { Success = true });

            public Task<RecordReviewerActionResponse> RecordReviewerActionAsync(string caseId, RecordReviewerActionRequest request, string actorId)
                => Task.FromResult(new RecordReviewerActionResponse { Success = true });

            public Task<GetOnboardingEvidenceSummaryResponse> GetEvidenceSummaryAsync(string caseId)
                => Task.FromResult(new GetOnboardingEvidenceSummaryResponse { Success = true });

            public Task<ListOnboardingCasesResponse> ListCasesAsync(ListOnboardingCasesRequest? request = null)
            {
                IEnumerable<KycAmlOnboardingCase> filtered = Cases;

                if (!string.IsNullOrWhiteSpace(request?.SubjectId))
                {
                    filtered = filtered.Where(c => c.SubjectId == request.SubjectId);
                }

                if (request?.State.HasValue == true)
                {
                    filtered = filtered.Where(c => c.State == request.State.Value);
                }

                return Task.FromResult(new ListOnboardingCasesResponse
                {
                    Success = true,
                    Cases = filtered.ToList(),
                    TotalCount = filtered.Count()
                });
            }
        }

        private sealed class FakeProtectedSignOffEvidencePersistenceService : IProtectedSignOffEvidencePersistenceService
        {
            public GetSignOffReleaseReadinessResponse ReleaseReadiness { get; set; } = new()
            {
                Success = true,
                HeadRef = "default-head",
                Status = SignOffReleaseReadinessStatus.Pending,
                EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                Mode = StrictArtifactMode.NotConfigured,
                EvaluatedAt = DateTimeOffset.UtcNow
            };

            public List<ApprovalWebhookRecord> ApprovalWebhookHistory { get; } = new();
            public List<ProtectedSignOffEvidencePack> EvidencePacks { get; } = new();

            public Task<RecordApprovalWebhookResponse> RecordApprovalWebhookAsync(RecordApprovalWebhookRequest request, string actorId)
                => Task.FromResult(new RecordApprovalWebhookResponse { Success = true });

            public Task<PersistSignOffEvidenceResponse> PersistSignOffEvidenceAsync(PersistSignOffEvidenceRequest request, string actorId)
                => Task.FromResult(new PersistSignOffEvidenceResponse { Success = true });

            public Task<GetSignOffReleaseReadinessResponse> GetReleaseReadinessAsync(GetSignOffReleaseReadinessRequest request)
                => Task.FromResult(ReleaseReadiness);

            public Task<GetApprovalWebhookHistoryResponse> GetApprovalWebhookHistoryAsync(GetApprovalWebhookHistoryRequest request)
                => Task.FromResult(new GetApprovalWebhookHistoryResponse
                {
                    Success = true,
                    Records = ApprovalWebhookHistory
                        .Where(r => string.IsNullOrWhiteSpace(request.CaseId) || r.CaseId == request.CaseId)
                        .Where(r => string.IsNullOrWhiteSpace(request.HeadRef) || r.HeadRef == request.HeadRef)
                        .OrderByDescending(r => r.ReceivedAt)
                        .Take(request.MaxRecords)
                        .ToList()
                });

            public Task<GetEvidencePackHistoryResponse> GetEvidencePackHistoryAsync(GetEvidencePackHistoryRequest request)
                => Task.FromResult(new GetEvidencePackHistoryResponse
                {
                    Success = true,
                    Packs = EvidencePacks
                        .Where(p => string.IsNullOrWhiteSpace(request.CaseId) || p.CaseId == request.CaseId)
                        .Where(p => string.IsNullOrWhiteSpace(request.HeadRef) || p.HeadRef == request.HeadRef)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(request.MaxRecords)
                        .ToList()
                });
        }

        private sealed class FakeComplianceAuditExportService : IComplianceAuditExportService
        {
            public List<ComplianceAuditExportSummary> Exports { get; init; } = new();

            public Task<ComplianceAuditExportResponse> AssembleReleaseReadinessExportAsync(ReleaseReadinessExportRequest request)
                => Task.FromResult(new ComplianceAuditExportResponse { Success = true });

            public Task<ComplianceAuditExportResponse> AssembleOnboardingCaseReviewExportAsync(OnboardingCaseReviewExportRequest request)
                => Task.FromResult(new ComplianceAuditExportResponse { Success = true });

            public Task<ComplianceAuditExportResponse> AssembleBlockerReviewExportAsync(ComplianceBlockerReviewExportRequest request)
                => Task.FromResult(new ComplianceAuditExportResponse { Success = true });

            public Task<ComplianceAuditExportResponse> AssembleApprovalHistoryExportAsync(ApprovalHistoryExportRequest request)
                => Task.FromResult(new ComplianceAuditExportResponse { Success = true });

            public Task<GetComplianceAuditExportResponse> GetExportAsync(string exportId, string? correlationId = null)
                => Task.FromResult(new GetComplianceAuditExportResponse { Success = true });

            public Task<ListComplianceAuditExportsResponse> ListExportsAsync(string subjectId, AuditScenario? scenario = null, int limit = 20, string? correlationId = null)
            {
                IEnumerable<ComplianceAuditExportSummary> filtered = Exports.Where(e => e.SubjectId == subjectId);
                if (scenario.HasValue)
                {
                    filtered = filtered.Where(e => e.Scenario == scenario.Value);
                }

                return Task.FromResult(new ListComplianceAuditExportsResponse
                {
                    Success = true,
                    Exports = filtered.Take(limit).ToList()
                });
            }
        }

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForComplianceEventsTests32Chars!",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForComplianceEventsTests32Chars!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"] = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
                    });
                });
            }
        }

        private sealed class NotConfiguredReleaseReadinessFactory : CustomWebApplicationFactory
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IProtectedSignOffEvidencePersistenceService>(new FakeProtectedSignOffEvidencePersistenceService
                    {
                        ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                        {
                            Success = true,
                            HeadRef = "head-release-1",
                            Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                            EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                            Mode = StrictArtifactMode.NotConfigured,
                            EvaluatedAt = DateTimeOffset.UtcNow,
                            OperatorGuidance = "Provide protected environment configuration before claiming release readiness.",
                            LatestEvidencePack = new ProtectedSignOffEvidencePack
                            {
                                PackId = "release-pack-1",
                                CaseId = "placeholder"
                            }
                        }
                    });
                });
            }
        }

        private sealed class OrderedProtectedEventsFactory : CustomWebApplicationFactory
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IProtectedSignOffEvidencePersistenceService>(new FakeProtectedSignOffEvidencePersistenceService
                    {
                        ReleaseReadiness = new GetSignOffReleaseReadinessResponse
                        {
                            Success = true,
                            HeadRef = "head-ordered-1",
                            Status = SignOffReleaseReadinessStatus.BlockedMissingConfiguration,
                            EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                            Mode = StrictArtifactMode.NotConfigured,
                            EvaluatedAt = DateTimeOffset.Parse("2026-03-27T10:09:00Z"),
                            OperatorGuidance = "Provide protected environment configuration before claiming release readiness."
                        },
                        ApprovalWebhookHistory =
                        {
                            new ApprovalWebhookRecord
                            {
                                RecordId = "webhook-ordered-1",
                                CaseId = "case-ordered-1",
                                HeadRef = "head-ordered-1",
                                ReceivedAt = DateTimeOffset.Parse("2026-03-27T10:07:00Z"),
                                ActorId = "operator-ordered-webhook",
                                Outcome = ApprovalWebhookOutcome.Approved,
                                IsValid = true
                            }
                        },
                        EvidencePacks =
                        {
                            new ProtectedSignOffEvidencePack
                            {
                                PackId = "pack-ordered-1",
                                CaseId = "case-ordered-1",
                                HeadRef = "head-ordered-1",
                                CreatedAt = DateTimeOffset.Parse("2026-03-27T10:08:00Z"),
                                CreatedBy = "operator-ordered-pack",
                                FreshnessStatus = SignOffEvidenceFreshnessStatus.Partial,
                                IsReleaseGrade = false,
                                IsProviderBacked = false
                            }
                        }
                    });
                });
            }
        }
    }
}
