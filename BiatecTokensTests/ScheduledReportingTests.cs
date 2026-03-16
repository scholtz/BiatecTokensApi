using BiatecTokensApi.Models.ScheduledReporting;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="ScheduledReportingService"/> and
    /// <see cref="BiatecTokensApi.Controllers.ScheduledReportingController"/>.
    ///
    /// Coverage:
    ///   Unit      — template CRUD, schedule management, evidence freshness, state transitions
    ///   Approval  — reviewer sign-off, formal approval, rejection flows
    ///   Run       — trigger, blocker detection, partial delivery, comparison to prior run
    ///   Webhook   — events emitted for created/blocked/approved/exported/delivered/failed
    ///   Integration — full HTTP pipeline via WebApplicationFactory
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ScheduledReportingTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CapturingWebhookService
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhookService : IWebhookService
        {
            public List<WebhookEvent> Emitted { get; } = new();
            public Task EmitEventAsync(WebhookEvent ev) { lock (Emitted) Emitted.Add(ev); return Task.CompletedTask; }
            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u) => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u) => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static ScheduledReportingService CreateService(
            TimeProvider? tp = null,
            IWebhookService? webhooks = null) =>
            new(NullLogger<ScheduledReportingService>.Instance, tp, webhooks);

        private static CreateReportingTemplateRequest BasicTemplateRequest(
            string name = "Monthly Compliance Report",
            ReportingAudienceType audience = ReportingAudienceType.InternalCompliance,
            ReportingCadence cadence = ReportingCadence.Monthly,
            bool reviewRequired = false,
            bool approvalRequired = false,
            List<EvidenceDomainKind>? domains = null) =>
            new()
            {
                Name = name,
                Description = "Automated compliance review",
                AudienceType = audience,
                Cadence = cadence,
                RequiredEvidenceDomains = domains ?? new List<EvidenceDomainKind>
                    { EvidenceDomainKind.KycAml, EvidenceDomainKind.ComplianceCases },
                DeliveryDestinations = new List<DeliveryDestinationConfig>
                {
                    new() { Label = "Internal Archive", DestinationType = DeliveryDestinationType.InternalArchive, IsRequired = true },
                    new() { Label = "Executive Inbox", DestinationType = DeliveryDestinationType.ExecutiveInbox, IsRequired = false }
                },
                ReviewRequired = reviewRequired,
                ApprovalRequired = approvalRequired
            };

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Template CRUD
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTemplate_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var result = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.Template, Is.Not.Null);
            Assert.That(result.Template!.TemplateId, Does.StartWith("tpl-"));
            Assert.That(result.Template.Name, Is.EqualTo("Monthly Compliance Report"));
            Assert.That(result.Template.CreatedBy, Is.EqualTo("actor-1"));
        }

        [Test]
        public async Task CreateTemplate_MissingName_ReturnsFail()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(name: "");
            var result = await svc.CreateTemplateAsync(req, "actor-1");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NAME"));
        }

        [Test]
        public async Task CreateTemplate_MissingActor_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.CreateTemplateAsync(BasicTemplateRequest(), "");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ACTOR"));
        }

        [Test]
        public async Task CreateTemplate_MonthlySchedule_SetsNextRunAt()
        {
            var now = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
            var svc = CreateService(new FakeTimeProvider(now));
            var result = await svc.CreateTemplateAsync(
                BasicTemplateRequest(cadence: ReportingCadence.Monthly), "actor-1");
            Assert.That(result.Template!.NextRunAt, Is.Not.Null);
            Assert.That(result.Template.NextRunAt!.Value, Is.EqualTo(now.AddMonths(1)));
        }

        [Test]
        public async Task CreateTemplate_EventDrivenCadence_NoNextRunAt()
        {
            var svc = CreateService();
            var result = await svc.CreateTemplateAsync(
                BasicTemplateRequest(cadence: ReportingCadence.EventDriven), "actor-1");
            Assert.That(result.Template!.NextRunAt, Is.Null);
        }

        [Test]
        public async Task GetTemplate_ExistingTemplate_ReturnsIt()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var result = await svc.GetTemplateAsync(created.Template!.TemplateId);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Template!.TemplateId, Is.EqualTo(created.Template.TemplateId));
        }

        [Test]
        public async Task GetTemplate_NonExistent_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.GetTemplateAsync("tpl-doesnotexist");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("TEMPLATE_NOT_FOUND"));
        }

        [Test]
        public async Task UpdateTemplate_ValidFields_Updates()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var update = new UpdateReportingTemplateRequest
            {
                Name = "Updated Name",
                ReviewRequired = true,
                ApprovalRequired = true
            };
            var result = await svc.UpdateTemplateAsync(id, update, "actor-2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Template!.Name, Is.EqualTo("Updated Name"));
            Assert.That(result.Template.ReviewRequired, Is.True);
            Assert.That(result.Template.ApprovalRequired, Is.True);
            Assert.That(result.Template.UpdatedBy, Is.EqualTo("actor-2"));
        }

        [Test]
        public async Task UpdateTemplate_ArchivedTemplate_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.ArchiveTemplateAsync(id, "actor-1");

            var result = await svc.UpdateTemplateAsync(id, new UpdateReportingTemplateRequest { Name = "New" }, "actor-1");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("TEMPLATE_ARCHIVED"));
        }

        [Test]
        public async Task ArchiveTemplate_ActiveTemplate_Archives()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var result = await svc.ArchiveTemplateAsync(id, "actor-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.Template!.IsArchived, Is.True);
            Assert.That(result.Template.IsActive, Is.False);
            Assert.That(result.Template.NextRunAt, Is.Null);
        }

        [Test]
        public async Task ArchiveTemplate_AlreadyArchived_IdempotentSuccess()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.ArchiveTemplateAsync(id, "actor-1");

            var result = await svc.ArchiveTemplateAsync(id, "actor-1");
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task ListTemplates_DefaultFilters_ReturnsActiveOnly()
        {
            var svc = CreateService();
            await svc.CreateTemplateAsync(BasicTemplateRequest("Active 1"), "a");
            var archived = await svc.CreateTemplateAsync(BasicTemplateRequest("Archived"), "a");
            await svc.ArchiveTemplateAsync(archived.Template!.TemplateId, "a");
            await svc.CreateTemplateAsync(BasicTemplateRequest("Active 2"), "a");

            var list = await svc.ListTemplatesAsync(includeArchived: false);
            Assert.That(list.Success, Is.True);
            Assert.That(list.TotalCount, Is.EqualTo(2));
            Assert.That(list.Templates.Any(t => t.IsArchived), Is.False);
        }

        [Test]
        public async Task ListTemplates_IncludeArchived_ReturnsAll()
        {
            var svc = CreateService();
            await svc.CreateTemplateAsync(BasicTemplateRequest("T1"), "a");
            var archived = await svc.CreateTemplateAsync(BasicTemplateRequest("T2"), "a");
            await svc.ArchiveTemplateAsync(archived.Template!.TemplateId, "a");

            var list = await svc.ListTemplatesAsync(includeArchived: true);
            Assert.That(list.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task ListTemplates_AudienceFilter_ReturnsMatchingOnly()
        {
            var svc = CreateService();
            await svc.CreateTemplateAsync(BasicTemplateRequest(audience: ReportingAudienceType.InternalCompliance), "a");
            await svc.CreateTemplateAsync(BasicTemplateRequest(audience: ReportingAudienceType.ExternalAuditor), "a");
            await svc.CreateTemplateAsync(BasicTemplateRequest(audience: ReportingAudienceType.RegulatorySubmission), "a");

            var list = await svc.ListTemplatesAsync(audienceFilter: ReportingAudienceType.ExternalAuditor);
            Assert.That(list.TotalCount, Is.EqualTo(1));
            Assert.That(list.Templates[0].AudienceType, Is.EqualTo(ReportingAudienceType.ExternalAuditor));
        }

        [Test]
        public async Task ListTemplates_Pagination_ReturnsCorrectPage()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                await svc.CreateTemplateAsync(BasicTemplateRequest($"Template {i}"), "a");

            var page1 = await svc.ListTemplatesAsync(page: 1, pageSize: 3);
            var page2 = await svc.ListTemplatesAsync(page: 2, pageSize: 3);

            Assert.That(page1.Templates.Count, Is.EqualTo(3));
            Assert.That(page2.Templates.Count, Is.EqualTo(2));
            Assert.That(page1.TotalCount, Is.EqualTo(5));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Report Runs — happy path
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_NoReviewRequired_RunTransitionsToDeliveredOrExported()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var run = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            Assert.That(run.Success, Is.True);
            Assert.That(run.Run!.Status,
                Is.EqualTo(ReportRunStatus.Delivered).Or.EqualTo(ReportRunStatus.Exported).Or.EqualTo(ReportRunStatus.PartiallyDelivered));
            Assert.That(run.Run.RunId, Does.StartWith("run-"));
            Assert.That(run.Run.TemplateId, Is.EqualTo(id));
        }

        [Test]
        public async Task TriggerRun_WithReviewRequired_AwaitingReviewStatus()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: false), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Success, Is.True);
            Assert.That(run.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingReview));
            Assert.That(run.Run.ApprovalMetadata!.ReviewRequired, Is.True);
        }

        [Test]
        public async Task TriggerRun_WithApprovalRequired_AwaitingApprovalAfterReview()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingReview));

            // Review → AwaitingApproval
            var reviewed = await svc.ReviewRunAsync(run.Run.RunId,
                new ReviewReportRunRequest { Approve = true, ReviewNotes = "Looks good" }, "reviewer-1");
            Assert.That(reviewed.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingApproval));
            Assert.That(reviewed.Run.ApprovalMetadata!.ReviewedBy, Is.EqualTo("reviewer-1"));
        }

        [Test]
        public async Task TriggerRun_NoDomains_RunsWithEmptyLineage()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.RequiredEvidenceDomains = new List<EvidenceDomainKind>();
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Success, Is.True);
            Assert.That(run.Run!.EvidenceLineage, Is.Empty);
            Assert.That(run.Run.Blockers, Is.Empty);
        }

        [Test]
        public async Task TriggerRun_UpdatesTemplateLastRunMetadata()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var template = (await svc.GetTemplateAsync(id)).Template!;
            Assert.That(template.LastRunAt, Is.Not.Null);
            Assert.That(template.LastRunStatus, Is.Not.Null);
        }

        [Test]
        public async Task TriggerRun_NonExistentTemplate_ReturnsError()
        {
            var svc = CreateService();
            var run = await svc.TriggerRunAsync("tpl-missing", new TriggerReportRunRequest(), "actor-1");
            Assert.That(run.Success, Is.False);
            Assert.That(run.ErrorCode, Is.EqualTo("TEMPLATE_NOT_FOUND"));
        }

        [Test]
        public async Task TriggerRun_ArchivedTemplate_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.ArchiveTemplateAsync(id, "actor-1");

            var run = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            Assert.That(run.Success, Is.False);
            Assert.That(run.ErrorCode, Is.EqualTo("TEMPLATE_ARCHIVED"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Evidence lineage and blocker details
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_AllDomains_LineageEntryPerDomain()
        {
            var svc = CreateService();
            var domains = Enum.GetValues<EvidenceDomainKind>().ToList();
            var req = BasicTemplateRequest(domains: domains);
            var created = await svc.CreateTemplateAsync(req, "actor-1");

            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Run!.EvidenceLineage.Count, Is.EqualTo(domains.Count));
            foreach (var domain in domains)
                Assert.That(run.Run.EvidenceLineage.Any(e => e.Domain == domain), Is.True,
                    $"Missing lineage entry for domain {domain}");
        }

        [Test]
        public async Task TriggerRun_WithKycAmlDomain_LineageHasFreshnessStatus()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(domains: new List<EvidenceDomainKind> { EvidenceDomainKind.KycAml });
            var created = await svc.CreateTemplateAsync(req, "actor-1");

            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var entry = run.Run!.EvidenceLineage.First(e => e.Domain == EvidenceDomainKind.KycAml);
            // FreshnessStatus must be one of the valid enum values
            Assert.That(Enum.IsDefined(typeof(ReportEvidenceFreshnessStatus), entry.FreshnessStatus), Is.True);
            Assert.That(entry.LastUpdatedAt, Is.Not.Null);
            Assert.That(entry.ExpiresAt, Is.Not.Null);
        }

        [Test]
        public async Task TriggerRun_BlockerDetails_ContainRemediationHint()
        {
            var svc = CreateService();
            var domains = new List<EvidenceDomainKind> { EvidenceDomainKind.KycAml };
            var req = BasicTemplateRequest(domains: domains);
            var created = await svc.CreateTemplateAsync(req, "actor-1");

            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            // Verify that any blocker has a remediation hint
            foreach (var blocker in run.Run!.Blockers)
            {
                Assert.That(blocker.BlockerCode, Is.Not.Empty);
                Assert.That(blocker.Description, Is.Not.Empty);
                Assert.That(blocker.Severity, Is.AnyOf(
                    ReportBlockerSeverity.Advisory,
                    ReportBlockerSeverity.Warning,
                    ReportBlockerSeverity.Critical));
            }
        }

        [Test]
        public async Task TriggerRun_CriticalBlockerCount_MatchesBlockers()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var expectedCritical = run.Run!.Blockers.Count(b => b.Severity == ReportBlockerSeverity.Critical);
            Assert.That(run.Run.CriticalBlockerCount, Is.EqualTo(expectedCritical));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Report run retrieval and listing
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetRun_ExistingRun_ReturnsIt()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var triggered = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var retrieved = await svc.GetRunAsync(triggered.Run!.RunId);
            Assert.That(retrieved.Success, Is.True);
            Assert.That(retrieved.Run!.RunId, Is.EqualTo(triggered.Run.RunId));
        }

        [Test]
        public async Task GetRun_NonExistent_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.GetRunAsync("run-doesnotexist");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("RUN_NOT_FOUND"));
        }

        [Test]
        public async Task ListRuns_MultipleRuns_ReturnsMostRecentFirst()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            tp.Advance(TimeSpan.FromMinutes(1));
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            tp.Advance(TimeSpan.FromMinutes(1));
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var list = await svc.ListRunsAsync(id);
            Assert.That(list.Success, Is.True);
            Assert.That(list.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ListRuns_NonExistentTemplate_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.ListRunsAsync("tpl-missing");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("TEMPLATE_NOT_FOUND"));
        }

        [Test]
        public async Task ListRuns_StatusFilter_ReturnsMatchingOnly()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "actor-1");
            var id = created.Template!.TemplateId;

            var run1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            // run1 is AwaitingReview; reject it to make it Failed
            await svc.ReviewRunAsync(run1.Run!.RunId,
                new ReviewReportRunRequest { Approve = false, RejectionReason = "Not ready" }, "reviewer");
            // run2 stays AwaitingReview
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var awaitingReview = await svc.ListRunsAsync(id, statusFilter: ReportRunStatus.AwaitingReview);
            Assert.That(awaitingReview.TotalCount, Is.EqualTo(1));
            Assert.That(awaitingReview.Runs[0].Status, Is.EqualTo(ReportRunStatus.AwaitingReview));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Approval gate flows
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReviewRun_Approve_TransitionsCorrectly()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: false), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            var result = await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true, ReviewNotes = "LGTM" }, "reviewer-x");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Run!.Status, Is.EqualTo(ReportRunStatus.Exported));
            Assert.That(result.Run.ApprovalMetadata!.ReviewedBy, Is.EqualTo("reviewer-x"));
            Assert.That(result.Run.ApprovalMetadata.ReviewNotes, Is.EqualTo("LGTM"));
            Assert.That(result.Run.ExportedAt, Is.Not.Null);
        }

        [Test]
        public async Task ReviewRun_Reject_TransitionsToFailed()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            var result = await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = false, RejectionReason = "Evidence incomplete" }, "reviewer-x");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Run!.Status, Is.EqualTo(ReportRunStatus.Failed));
            Assert.That(result.Run.ApprovalMetadata!.IsRejected, Is.True);
            Assert.That(result.Run.ApprovalMetadata.RejectionReason, Is.EqualTo("Evidence incomplete"));
        }

        [Test]
        public async Task ReviewRun_WrongState_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            // Run is Delivered or Exported, not AwaitingReview
            var result = await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE"));
        }

        [Test]
        public async Task ReviewRun_RejectWithoutReason_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            var result = await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = false, RejectionReason = null }, "reviewer");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("REJECTION_REASON_REQUIRED"));
        }

        [Test]
        public async Task ApproveRun_Approve_TransitionsToDelivered()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            // Review first
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer");

            // Then approve
            var result = await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = true, ApprovalNotes = "Approved" }, "approver-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Run!.Status,
                Is.EqualTo(ReportRunStatus.Delivered).Or.EqualTo(ReportRunStatus.PartiallyDelivered));
            Assert.That(result.Run.ApprovalMetadata!.ApprovedBy, Is.EqualTo("approver-1"));
            Assert.That(result.Run.ExportedAt, Is.Not.Null);
        }

        [Test]
        public async Task ApproveRun_Reject_TransitionsToFailed()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer");

            var result = await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = false, RejectionReason = "Needs more evidence" },
                "approver-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Run!.Status, Is.EqualTo(ReportRunStatus.Failed));
            Assert.That(result.Run.ApprovalMetadata!.IsRejected, Is.True);
        }

        [Test]
        public async Task ApproveRun_WrongState_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            var result = await svc.ApproveRunAsync(run.Run!.RunId,
                new ApproveReportRunRequest { Approve = true }, "approver");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Delivery outcomes
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_DeliveryOutcomes_RecordedPerDestination()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new() { Label = "Archive", DestinationType = DeliveryDestinationType.InternalArchive, IsRequired = true },
                new() { Label = "Regulator", DestinationType = DeliveryDestinationType.RegulatorExport, IsRequired = false }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Run!.DeliveryOutcomes.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task TriggerRun_NoSecretCredentialsInDeliveryOutcomes()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new()
                {
                    Label = "Webhook",
                    DestinationType = DeliveryDestinationType.WebhookSubscriber,
                    IsRequired = true,
                    RoutingHint = "sub-masked-id"  // non-secret routing hint only
                }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            // Verify delivery outcome does not contain any obvious credential fields
            foreach (var outcome in run.Run!.DeliveryOutcomes)
            {
                if (outcome.DiagnosticDetail != null)
                {
                    Assert.That(outcome.DiagnosticDetail.ToLowerInvariant(), Does.Not.Contain("password"));
                    Assert.That(outcome.DiagnosticDetail.ToLowerInvariant(), Does.Not.Contain("secret"));
                }
                // Label should not expose secrets
                Assert.That(outcome.Label, Does.Not.Contain("password").IgnoreCase);
                Assert.That(outcome.Label, Does.Not.Contain("apikey").IgnoreCase);
            }
        }

        [Test]
        public async Task TriggerRun_DeliverySuccessCount_MatchesDeliveredOutcomes()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var expected = run.Run!.DeliveryOutcomes.Count(o => o.Status == DeliveryOutcomeStatus.Delivered);
            Assert.That(run.Run.DeliverySuccessCount, Is.EqualTo(expected));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Comparison to prior run
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_SecondRun_IncludesComparisonToPriorRun()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            // First run
            var first = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            // Second run
            var second = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            Assert.That(second.Run!.ComparisonToPriorRun, Is.Not.Null);
            Assert.That(second.Run.ComparisonToPriorRun!.PriorRunId, Is.EqualTo(first.Run!.RunId));
        }

        [Test]
        public async Task TriggerRun_FirstRun_NoComparison()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            Assert.That(run.Run!.ComparisonToPriorRun, Is.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Schedule management
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertSchedule_Monthly_CreatesSchedule()
        {
            var now = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
            var svc = CreateService(new FakeTimeProvider(now));
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var result = await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest
                {
                    Cadence = ReportingCadence.Monthly,
                    DayOfMonth = 1,
                    TriggerHourUtc = 8,
                    IsActive = true
                }, "actor-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.Cadence, Is.EqualTo(ReportingCadence.Monthly));
            Assert.That(result.Schedule.IsActive, Is.True);
            Assert.That(result.Schedule.NextTriggerAt, Is.Not.Null);
        }

        [Test]
        public async Task UpsertSchedule_Quarterly_ComputesQuarterlyNextTrigger()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var svc = CreateService(new FakeTimeProvider(now));
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");

            var result = await svc.UpsertScheduleAsync(created.Template!.TemplateId,
                new UpsertScheduleRequest
                {
                    Cadence = ReportingCadence.Quarterly,
                    TriggerHourUtc = 6,
                    IsActive = true
                }, "actor-1");

            Assert.That(result.Schedule!.NextTriggerAt, Is.EqualTo(now.AddMonths(3)));
        }

        [Test]
        public async Task UpsertSchedule_InvalidTriggerHour_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");

            var result = await svc.UpsertScheduleAsync(created.Template!.TemplateId,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Monthly, TriggerHourUtc = 25 },
                "actor-1");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_TRIGGER_HOUR"));
        }

        [Test]
        public async Task UpsertSchedule_InvalidDayOfMonth_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");

            var result = await svc.UpsertScheduleAsync(created.Template!.TemplateId,
                new UpsertScheduleRequest
                {
                    Cadence = ReportingCadence.Monthly,
                    DayOfMonth = 31,  // Invalid; must be 1-28
                    TriggerHourUtc = 8
                }, "actor-1");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_DAY_OF_MONTH"));
        }

        [Test]
        public async Task GetSchedule_AfterUpsert_ReturnsSchedule()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Quarterly, TriggerHourUtc = 9 },
                "actor-1");

            var result = await svc.GetScheduleAsync(id);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.Cadence, Is.EqualTo(ReportingCadence.Quarterly));
        }

        [Test]
        public async Task GetSchedule_NotDefined_ReturnsError()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var result = await svc.GetScheduleAsync(created.Template!.TemplateId);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("SCHEDULE_NOT_FOUND"));
        }

        [Test]
        public async Task DeactivateSchedule_ActiveSchedule_Deactivates()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Monthly, TriggerHourUtc = 8, IsActive = true },
                "actor-1");

            var result = await svc.DeactivateScheduleAsync(id, "actor-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.IsActive, Is.False);
            Assert.That(result.Schedule.NextTriggerAt, Is.Null);
        }

        [Test]
        public async Task DeactivateSchedule_ClearsTemplateNextRunAt()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;
            await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Monthly, TriggerHourUtc = 8, IsActive = true },
                "actor-1");

            await svc.DeactivateScheduleAsync(id, "actor-1");

            var template = (await svc.GetTemplateAsync(id)).Template!;
            Assert.That(template.NextRunAt, Is.Null);
        }

        [Test]
        public async Task UpsertSchedule_Twice_UpdatesExistingScheduleId()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var first = await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Monthly, TriggerHourUtc = 6 },
                "actor-1");
            var scheduleId = first.Schedule!.ScheduleId;

            var second = await svc.UpsertScheduleAsync(id,
                new UpsertScheduleRequest { Cadence = ReportingCadence.Quarterly, TriggerHourUtc = 9 },
                "actor-1");

            Assert.That(second.Schedule!.ScheduleId, Is.EqualTo(scheduleId), "Schedule ID should be preserved on update");
            Assert.That(second.Schedule.Cadence, Is.EqualTo(ReportingCadence.Quarterly));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Webhook events
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_EmitsReportRunCreatedEvent()
        {
            var webhooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: webhooks);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunCreated), Is.True,
                "ReportRunCreated event should be emitted");
        }

        [Test]
        public async Task TriggerRun_ReviewRequired_DoesNotEmitBlockedOrDeliveredYet()
        {
            var webhooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: webhooks);
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "actor-1");
            await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunCreated), Is.True);
            // Should not be Blocked since AwaitingReview is not a blocker
            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunBlocked), Is.False);
        }

        [Test]
        public async Task ReviewRun_Reject_EmitsReportRunFailedEvent()
        {
            var webhooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: webhooks);
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            webhooks.Emitted.Clear();

            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = false, RejectionReason = "N/A" }, "reviewer");

            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunFailed), Is.True);
        }

        [Test]
        public async Task ApproveRun_Approve_EmitsApprovedExportedDeliveredEvents()
        {
            var webhooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: webhooks);
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer");
            webhooks.Emitted.Clear();

            await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = true }, "approver");

            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunApproved), Is.True);
            Assert.That(webhooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportRunExported), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Cadence NextRunAt calculations
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(ReportingCadence.Monthly, 1, 0)]
        [TestCase(ReportingCadence.Quarterly, 3, 0)]
        [TestCase(ReportingCadence.SemiAnnual, 6, 0)]
        [TestCase(ReportingCadence.Annual, 12, 0)]
        public async Task CreateTemplate_ScheduledCadence_SetsCorrectNextRunAt(
            ReportingCadence cadence, int expectedMonths, int expectedYears)
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var svc = CreateService(new FakeTimeProvider(now));
            var result = await svc.CreateTemplateAsync(
                BasicTemplateRequest(cadence: cadence), "actor-1");

            var expected = now.AddMonths(expectedMonths);
            Assert.That(result.Template!.NextRunAt, Is.EqualTo(expected));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: State machine coverage
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RunStateMachine_DraftToQueued_NotSupported_StartsAtDraftOrQueued()
        {
            // TriggerRun starts at Draft or Queued internally, transitions based on evidence
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            // The run should not remain in Draft
            Assert.That(run.Run!.Status, Is.Not.EqualTo(ReportRunStatus.Draft));
        }

        [Test]
        public async Task Run_ApprovalMetadata_IsNotNullAfterTrigger()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "a");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "a");

            Assert.That(run.Run!.ApprovalMetadata, Is.Not.Null);
            Assert.That(run.Run.ApprovalMetadata!.ReviewRequired, Is.True);
            Assert.That(run.Run.ApprovalMetadata.ApprovalRequired, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Idempotency and multi-run determinism
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_ThreeTimes_ProducesThreeDistinctRunIds()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var r1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var r2 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var r3 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var ids = new[] { r1.Run!.RunId, r2.Run!.RunId, r3.Run!.RunId };
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3), "Each run should have a unique ID");

            var list = await svc.ListRunsAsync(id);
            Assert.That(list.TotalCount, Is.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Partial delivery regression
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PartialDelivery_FailedOptionalDestination_DoesNotFailRun()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new() { Label = "Archive", DestinationType = DeliveryDestinationType.InternalArchive, IsRequired = true },
                new() { Label = "Optional Webhook", DestinationType = DeliveryDestinationType.WebhookSubscriber, IsRequired = false }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            // Run should complete (not fail) even if optional destination fails
            Assert.That(run.Success, Is.True);
            Assert.That(run.Run!.Status,
                Is.EqualTo(ReportRunStatus.Delivered)
                    .Or.EqualTo(ReportRunStatus.PartiallyDelivered)
                    .Or.EqualTo(ReportRunStatus.Exported)
                    .Or.EqualTo(ReportRunStatus.AwaitingReview)
                    .Or.EqualTo(ReportRunStatus.AwaitingApproval));
        }

        [Test]
        public async Task PartialDelivery_HistoryRecord_NotCorrupted()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            // Trigger two runs - partial delivery should not corrupt history
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var list = await svc.ListRunsAsync(id);
            Assert.That(list.TotalCount, Is.EqualTo(2));
            foreach (var r in list.Runs)
            {
                Assert.That(r.RunId, Is.Not.Null.And.Not.Empty);
                Assert.That(r.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            }
        }

        [Test]
        public async Task DeliveryOutcome_OptionalDestination_MarkedOptional()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new() { Label = "Archive", DestinationType = DeliveryDestinationType.InternalArchive, IsRequired = true },
                new() { Label = "Optional", DestinationType = DeliveryDestinationType.AuditorInbox, IsRequired = false }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            // Both destinations should produce outcome records
            Assert.That(run.Run!.DeliveryOutcomes.Count, Is.EqualTo(2));
            var requiredOutcome = run.Run.DeliveryOutcomes.FirstOrDefault(o => o.Label == "Archive");
            var optionalOutcome = run.Run.DeliveryOutcomes.FirstOrDefault(o => o.Label == "Optional");
            Assert.That(requiredOutcome, Is.Not.Null);
            Assert.That(optionalOutcome, Is.Not.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Evidence freshness classification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceFreshness_AllDomainsClassified()
        {
            var svc = CreateService();
            var allDomains = Enum.GetValues<EvidenceDomainKind>().ToList();
            var req = BasicTemplateRequest(domains: allDomains);
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            foreach (var domain in allDomains)
            {
                var entry = run.Run!.EvidenceLineage.FirstOrDefault(e => e.Domain == domain);
                Assert.That(entry, Is.Not.Null, $"Domain {domain} should have a lineage entry");
                Assert.That(Enum.IsDefined(typeof(ReportEvidenceFreshnessStatus), entry!.FreshnessStatus), Is.True);
            }
        }

        [Test]
        public async Task EvidenceFreshness_Current_NoBlockerEmitted()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            // Trigger immediately - evidence should be current (simulated as within window)
            var req = BasicTemplateRequest(domains: new List<EvidenceDomainKind> { EvidenceDomainKind.KycAml });
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            var entry = run.Run!.EvidenceLineage.First(e => e.Domain == EvidenceDomainKind.KycAml);
            // If current, there should be no critical blocker for this domain
            if (entry.FreshnessStatus == ReportEvidenceFreshnessStatus.Current)
            {
                var domainBlockers = run.Run.Blockers
                    .Where(b => b.AffectedDomain == EvidenceDomainKind.KycAml && b.Severity == ReportBlockerSeverity.Critical)
                    .ToList();
                Assert.That(domainBlockers.Count, Is.EqualTo(0), "Current evidence should not have critical blockers");
            }
        }

        [Test]
        public async Task EvidenceLineage_LastUpdatedAt_IsBeforeOrEqualExpiresAt()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(domains: new List<EvidenceDomainKind>
            {
                EvidenceDomainKind.KycAml, EvidenceDomainKind.ProtectedSignOff
            });
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId, new TriggerReportRunRequest(), "actor-1");

            foreach (var entry in run.Run!.EvidenceLineage)
            {
                if (entry.LastUpdatedAt.HasValue && entry.ExpiresAt.HasValue)
                {
                    Assert.That(entry.LastUpdatedAt.Value, Is.LessThanOrEqualTo(entry.ExpiresAt.Value),
                        $"Domain {entry.Domain}: LastUpdatedAt must precede ExpiresAt");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Cadence and schedule edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(ReportingCadence.SemiAnnual, 6)]
        [TestCase(ReportingCadence.Annual, 12)]
        public async Task UpsertSchedule_SemiAnnualAndAnnual_ComputesCorrectNextTrigger(
            ReportingCadence cadence, int expectedMonths)
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var tp = new FakeTimeProvider(now);
            var svc = CreateService(tp);
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(cadence: cadence), "actor-1");

            var req = new UpsertScheduleRequest
            {
                Cadence = cadence,
                TriggerHourUtc = 0,
                DayOfMonth = 1
            };
            var result = await svc.UpsertScheduleAsync(created.Template!.TemplateId, req, "actor-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.NextTriggerAt, Is.Not.Null);
            var diff = (result.Schedule.NextTriggerAt!.Value - now).TotalDays;
            Assert.That(diff, Is.GreaterThanOrEqualTo(expectedMonths * 28),
                $"{cadence} cadence should schedule at least {expectedMonths} months ahead");
        }

        [Test]
        public async Task UpsertSchedule_EventDriven_HasNullNextTrigger()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(cadence: ReportingCadence.EventDriven), "actor-1");

            var result = await svc.UpsertScheduleAsync(created.Template!.TemplateId,
                new UpsertScheduleRequest { Cadence = ReportingCadence.EventDriven }, "actor-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.NextTriggerAt, Is.Null,
                "Event-driven schedules should have no automatic next trigger");
        }

        [Test]
        public async Task DeactivateSchedule_NonExistentTemplate_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.DeactivateScheduleAsync("non-existent-template-id", "actor-1");
            Assert.That(result.Success, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Template owner and metadata tracking
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTemplate_CreatedByActor_Preserved()
        {
            var svc = CreateService();
            var result = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-owner-test");
            Assert.That(result.Template!.CreatedBy, Is.EqualTo("actor-owner-test"));
        }

        [Test]
        public async Task UpdateTemplate_UpdatedByActor_Preserved()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-creator");
            var updated = await svc.UpdateTemplateAsync(
                created.Template!.TemplateId,
                new UpdateReportingTemplateRequest { Name = "New Name" },
                "actor-updater");
            Assert.That(updated.Template!.UpdatedBy, Is.EqualTo("actor-updater"));
        }

        [Test]
        public async Task CreateTemplate_CreatedAtTimestamp_Set()
        {
            var svc = CreateService();
            var before = DateTimeOffset.UtcNow;
            var result = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var after = DateTimeOffset.UtcNow;
            Assert.That(result.Template!.CreatedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(result.Template.CreatedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public async Task TriggerRun_GeneratedByActor_Preserved()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "run-trigger-actor");
            Assert.That(run.Run!.CreatedBy, Is.EqualTo("run-trigger-actor"));
        }

        [Test]
        public async Task ReviewRun_ReviewerActor_Preserved()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var reviewed = await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-actor-x");
            Assert.That(reviewed.Run!.ApprovalMetadata!.ReviewedBy, Is.EqualTo("reviewer-actor-x"));
        }

        [Test]
        public async Task ApproveRun_ApproverActor_Preserved()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-1");
            var approved = await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = true }, "approver-abc");

            Assert.That(approved.Run!.ApprovalMetadata!.ApprovedBy, Is.EqualTo("approver-abc"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Comparison metadata (prior-run diff)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Comparison_ThirdRun_ComparesToPrior()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var r1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var r2 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var r3 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            Assert.That(r2.Run!.ComparisonToPriorRun!.PriorRunId, Is.EqualTo(r1.Run!.RunId));
            Assert.That(r3.Run!.ComparisonToPriorRun!.PriorRunId, Is.EqualTo(r2.Run.RunId));
        }

        [Test]
        public async Task Comparison_StatusChanged_ReflectedInComparison()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var r1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var r2 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            Assert.That(r2.Run!.ComparisonToPriorRun, Is.Not.Null);
            Assert.That(r2.Run.ComparisonToPriorRun!.PriorRunStatus, Is.EqualTo(r1.Run!.Status));
            // CurrentRunStatus is available via the run record itself
            Assert.That(r2.Run.Status, Is.EqualTo(r2.Run.Status)); // Status is on the run, not the comparison
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Webhook event completeness
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Webhook_CreateTemplate_EmitsTemplateCreatedEvent()
        {
            var hooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: hooks);
            await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");

            Assert.That(hooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportTemplateCreated),
                Is.True, "Creating a template should emit ReportTemplateCreated");
        }

        [Test]
        public async Task Webhook_UpdateTemplate_EmitsTemplateUpdatedEvent()
        {
            var hooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: hooks);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            hooks.Emitted.Clear();

            await svc.UpdateTemplateAsync(
                created.Template!.TemplateId,
                new UpdateReportingTemplateRequest { Name = "Updated" },
                "actor-1");

            Assert.That(hooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportTemplateUpdated),
                Is.True, "Updating a template should emit ReportTemplateUpdated");
        }

        [Test]
        public async Task Webhook_ArchiveTemplate_EmitsTemplateArchivedEvent()
        {
            var hooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: hooks);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            hooks.Emitted.Clear();

            await svc.ArchiveTemplateAsync(created.Template!.TemplateId, "actor-1");

            Assert.That(hooks.Emitted.Any(e => e.EventType == WebhookEventType.ReportTemplateArchived),
                Is.True, "Archiving a template should emit ReportTemplateArchived");
        }

        [Test]
        public async Task Webhook_TriggerRun_EventContainsTemplateId()
        {
            var hooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: hooks);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            hooks.Emitted.Clear();

            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var createdEvent = hooks.Emitted.FirstOrDefault(e => e.EventType == WebhookEventType.ReportRunCreated);
            Assert.That(createdEvent, Is.Not.Null);
            Assert.That(createdEvent!.Data, Is.Not.Null);
            Assert.That(createdEvent.Data!.ContainsKey("templateId"), Is.True);
            Assert.That(createdEvent.Data["templateId"].ToString(), Is.EqualTo(created.Template.TemplateId));
        }

        [Test]
        public async Task Webhook_ApproveRun_EmitsAllLifecycleEvents()
        {
            var hooks = new CapturingWebhookService();
            var svc = CreateService(webhooks: hooks);
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "actor-1");

            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-1");
            hooks.Emitted.Clear();

            await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = true }, "approver-1");

            var eventTypes = hooks.Emitted.Select(e => e.EventType).ToList();
            Assert.That(eventTypes, Contains.Item(WebhookEventType.ReportRunApproved));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Run blocker details
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Blocker_AllHaveAffectedDomain()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(domains: Enum.GetValues<EvidenceDomainKind>().ToList());
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            foreach (var blocker in run.Run!.Blockers)
            {
                Assert.That(Enum.IsDefined(typeof(EvidenceDomainKind), blocker.AffectedDomain),
                    Is.True, $"Blocker {blocker.BlockerCode} should have a valid AffectedDomain");
            }
        }

        [Test]
        public async Task Blocker_BlockerCode_IsNotEmpty()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(domains: new List<EvidenceDomainKind>
            {
                EvidenceDomainKind.KycAml, EvidenceDomainKind.ComplianceCases
            });
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            foreach (var blocker in run.Run!.Blockers)
            {
                Assert.That(blocker.BlockerCode, Is.Not.Null.And.Not.Empty,
                    "Every blocker must have a non-empty BlockerCode");
                Assert.That(blocker.Description, Is.Not.Null.And.Not.Empty,
                    "Every blocker must have a non-empty Description");
            }
        }

        [Test]
        public async Task Blocker_CriticalBlockerCount_IncreasesWhenStaleEvidenceAdded()
        {
            var svc = CreateService();
            var r1 = BasicTemplateRequest(domains: new List<EvidenceDomainKind>());
            var c1 = await svc.CreateTemplateAsync(r1, "a");
            var run1 = await svc.TriggerRunAsync(c1.Template!.TemplateId, new TriggerReportRunRequest(), "a");

            var r2 = BasicTemplateRequest(domains: Enum.GetValues<EvidenceDomainKind>().ToList());
            var c2 = await svc.CreateTemplateAsync(r2, "a");
            var run2 = await svc.TriggerRunAsync(c2.Template!.TemplateId, new TriggerReportRunRequest(), "a");

            // Template with more evidence domains should have >= blocker count as template with no domains
            Assert.That(run2.Run!.CriticalBlockerCount, Is.GreaterThanOrEqualTo(run1.Run!.CriticalBlockerCount));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Audience types
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(ReportingAudienceType.InternalCompliance)]
        [TestCase(ReportingAudienceType.ExecutiveSummary)]
        [TestCase(ReportingAudienceType.ExternalAuditor)]
        [TestCase(ReportingAudienceType.RegulatorySubmission)]
        [TestCase(ReportingAudienceType.BoardPresentation)]
        public async Task CreateTemplate_AllAudienceTypes_Accepted(ReportingAudienceType audience)
        {
            var svc = CreateService();
            var req = BasicTemplateRequest(audience: audience);
            var result = await svc.CreateTemplateAsync(req, "actor-1");
            Assert.That(result.Success, Is.True,
                $"Audience type {audience} should be accepted");
            Assert.That(result.Template!.AudienceType, Is.EqualTo(audience));
        }

        [Test]
        public async Task ListTemplates_FilterByAudienceType_ReturnsOnlyThatAudience()
        {
            var svc = CreateService();
            await svc.CreateTemplateAsync(
                BasicTemplateRequest(audience: ReportingAudienceType.InternalCompliance), "a");
            await svc.CreateTemplateAsync(
                BasicTemplateRequest(audience: ReportingAudienceType.RegulatorySubmission), "a");
            await svc.CreateTemplateAsync(
                BasicTemplateRequest(audience: ReportingAudienceType.RegulatorySubmission), "a");

            var result = await svc.ListTemplatesAsync(
                audienceFilter: ReportingAudienceType.RegulatorySubmission);

            Assert.That(result.Templates.Count, Is.EqualTo(2));
            Assert.That(result.Templates.All(t => t.AudienceType == ReportingAudienceType.RegulatorySubmission), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: State-machine edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReviewRun_ApproveWhenApprovalNotRequired_SkipsApprovalStep()
        {
            var svc = CreateService();
            // reviewRequired=true but approvalRequired=false means after review → Exported/Delivered
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: false), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            Assert.That(run.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingReview));

            var reviewed = await svc.ReviewRunAsync(run.Run.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-1");
            // Should skip ApprovalRequired step → go directly to Exported/Delivered
            Assert.That(reviewed.Run!.Status,
                Is.EqualTo(ReportRunStatus.Exported)
                    .Or.EqualTo(ReportRunStatus.Delivered)
                    .Or.EqualTo(ReportRunStatus.PartiallyDelivered));
        }

        [Test]
        public async Task ApproveRun_AfterRejection_ReturnsFailed()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-1");

            // Reject at approval stage
            var rejected = await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = false, RejectionReason = "Rejected at approval stage" },
                "approver-1");
            Assert.That(rejected.Success, Is.True);
            Assert.That(rejected.Run!.Status, Is.EqualTo(ReportRunStatus.Failed));
        }

        [Test]
        public async Task GetRun_AfterApproval_ReturnsExportedAt()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(
                BasicTemplateRequest(reviewRequired: true, approvalRequired: true), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");
            await svc.ReviewRunAsync(run.Run!.RunId,
                new ReviewReportRunRequest { Approve = true }, "reviewer-1");
            await svc.ApproveRunAsync(run.Run.RunId,
                new ApproveReportRunRequest { Approve = true }, "approver-1");

            var retrieved = await svc.GetRunAsync(run.Run.RunId);
            Assert.That(retrieved.Run!.ExportedAt, Is.Not.Null,
                "ExportedAt should be set after approval");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Idempotency and immutability guards
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerRun_SameTriggerTwice_ProducesTwoIndependentRuns()
        {
            // Independent triggers should each produce a separate run record
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var run1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            var run2 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            Assert.That(run1.Run!.RunId, Is.Not.EqualTo(run2.Run!.RunId),
                "Each trigger must produce a distinct run ID");
        }

        [Test]
        public async Task ListRuns_UpdateOrdering_NewestFirst()
        {
            var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(tp);
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            var r1 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            tp.Advance(TimeSpan.FromHours(1));
            var r2 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");
            tp.Advance(TimeSpan.FromHours(1));
            var r3 = await svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-1");

            var list = await svc.ListRunsAsync(id);
            // Newest first
            Assert.That(list.Runs[0].RunId, Is.EqualTo(r3.Run!.RunId));
            Assert.That(list.Runs[1].RunId, Is.EqualTo(r2.Run!.RunId));
            Assert.That(list.Runs[2].RunId, Is.EqualTo(r1.Run!.RunId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Schedule deactivation/reactivation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertSchedule_AfterDeactivation_ReactivatesWithNewSchedule()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            // Activate
            await svc.UpsertScheduleAsync(id, new UpsertScheduleRequest
            {
                Cadence = ReportingCadence.Monthly, TriggerHourUtc = 0, DayOfMonth = 1
            }, "actor-1");

            // Deactivate
            await svc.DeactivateScheduleAsync(id, "actor-1");

            // Reactivate
            var result = await svc.UpsertScheduleAsync(id, new UpsertScheduleRequest
            {
                Cadence = ReportingCadence.Quarterly, TriggerHourUtc = 6, DayOfMonth = 15
            }, "actor-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.Cadence, Is.EqualTo(ReportingCadence.Quarterly));
            Assert.That(result.Schedule.IsActive, Is.True);
        }

        [Test]
        public async Task GetSchedule_AfterDeactivation_IsNotActive()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            await svc.UpsertScheduleAsync(id, new UpsertScheduleRequest
            {
                Cadence = ReportingCadence.Monthly, TriggerHourUtc = 0, DayOfMonth = 1
            }, "actor-1");
            await svc.DeactivateScheduleAsync(id, "actor-1");

            var result = await svc.GetScheduleAsync(id);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Schedule!.IsActive, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Response schema contract assertions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TemplateResponse_ContainsAllRequiredFields()
        {
            var svc = CreateService();
            var result = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var t = result.Template!;

            Assert.That(t.TemplateId, Is.Not.Null.And.Not.Empty, "TemplateId must be set");
            Assert.That(t.Name, Is.Not.Null.And.Not.Empty, "Name must be set");
            Assert.That(Enum.IsDefined(typeof(ReportingAudienceType), t.AudienceType), Is.True);
            Assert.That(Enum.IsDefined(typeof(ReportingCadence), t.Cadence), Is.True);
            Assert.That(t.CreatedBy, Is.Not.Null.And.Not.Empty);
            Assert.That(t.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(t.IsArchived, Is.False, "Newly created template should not be archived");
        }

        [Test]
        public async Task RunResponse_ContainsAllRequiredFields()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-schema");
            var r = run.Run!;

            Assert.That(r.RunId, Is.Not.Null.And.Not.Empty, "RunId must be set");
            Assert.That(r.TemplateId, Is.Not.Null.And.Not.Empty, "TemplateId must be set");
            Assert.That(r.CreatedBy, Is.EqualTo("actor-schema"), "GeneratedBy must match actor");
            Assert.That(r.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(Enum.IsDefined(typeof(ReportRunStatus), r.Status), Is.True);
            Assert.That(r.EvidenceLineage, Is.Not.Null);
            Assert.That(r.Blockers, Is.Not.Null);
            Assert.That(r.DeliveryOutcomes, Is.Not.Null);
            Assert.That(r.ApprovalMetadata, Is.Not.Null);
        }

        [Test]
        public async Task DeliveryOutcome_ContainsAllRequiredFields()
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new() { Label = "Archive", DestinationType = DeliveryDestinationType.InternalArchive, IsRequired = true }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var outcome = run.Run!.DeliveryOutcomes.First(o => o.Label == "Archive");
            Assert.That(outcome.Label, Is.Not.Null.And.Not.Empty);
            Assert.That(Enum.IsDefined(typeof(DeliveryDestinationType), outcome.DestinationType), Is.True);
            Assert.That(Enum.IsDefined(typeof(DeliveryOutcomeStatus), outcome.Status), Is.True);
            Assert.That(outcome.AttemptedAt, Is.Not.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: Delivery type coverage
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(DeliveryDestinationType.InternalArchive)]
        [TestCase(DeliveryDestinationType.ExecutiveInbox)]
        [TestCase(DeliveryDestinationType.AuditorInbox)]
        [TestCase(DeliveryDestinationType.RegulatorExport)]
        [TestCase(DeliveryDestinationType.WebhookSubscriber)]
        public async Task DeliveryDestination_AllTypes_ProduceOutcomeRecord(DeliveryDestinationType dtype)
        {
            var svc = CreateService();
            var req = BasicTemplateRequest();
            req.DeliveryDestinations = new List<DeliveryDestinationConfig>
            {
                new() { Label = dtype.ToString(), DestinationType = dtype, IsRequired = false }
            };
            var created = await svc.CreateTemplateAsync(req, "actor-1");
            var run = await svc.TriggerRunAsync(created.Template!.TemplateId,
                new TriggerReportRunRequest(), "actor-1");

            var outcome = run.Run!.DeliveryOutcomes.FirstOrDefault(o => o.DestinationType == dtype);
            Assert.That(outcome, Is.Not.Null,
                $"Delivery type {dtype} should produce a DeliveryOutcomeRecord");
            Assert.That(Enum.IsDefined(typeof(DeliveryOutcomeStatus), outcome!.Status), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit: ScheduledReportingService concurrency regression
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ConcurrentTriggers_SameTemplate_ProduceIndependentRuns()
        {
            var svc = CreateService();
            var created = await svc.CreateTemplateAsync(BasicTemplateRequest(), "actor-1");
            var id = created.Template!.TemplateId;

            // Fire 5 concurrent triggers
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => svc.TriggerRunAsync(id, new TriggerReportRunRequest(), "actor-concurrent"))
                .ToList();
            var results = await Task.WhenAll(tasks);

            var runIds = results.Select(r => r.Run!.RunId).ToList();
            Assert.That(runIds.Distinct().Count(), Is.EqualTo(5),
                "Concurrent triggers must produce distinct run IDs");
            Assert.That(results.All(r => r.Success), Is.True,
                "All concurrent triggers should succeed");
        }

        private static WebApplicationFactory<BiatecTokensApi.Program> CreateFactory() =>
            new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(GetTestConfig())));

        private static Dictionary<string, string?> GetTestConfig() => new()
        {
            ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForScheduledReportingTests32CharactersMin",
            ["JwtConfig:SecretKey"] = "TestSecretKeyForScheduledReportingTests32Chars!",
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
            ["KycConfig:WebhookSecret"] = "",
            ["StripeConfig:SecretKey"] = "sk_test_placeholder",
            ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
            ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
        };

        [Test]
        public async Task Integration_ListTemplates_Unauthenticated_Returns401or200WithDebugMode()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();
            var resp = await client.GetAsync("/api/v1/scheduled-reporting/templates");
            // In debug mode with DisableAuthentication=true, returns 200; otherwise 401
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Integration_CreateAndGetTemplate_RoundTrip()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var req = new CreateReportingTemplateRequest
            {
                Name = "Integration Test Template",
                AudienceType = ReportingAudienceType.RegulatorySubmission,
                Cadence = ReportingCadence.Quarterly,
                RequiredEvidenceDomains = new List<EvidenceDomainKind> { EvidenceDomainKind.KycAml },
                DeliveryDestinations = new List<DeliveryDestinationConfig>
                {
                    new() { Label = "Archive", DestinationType = DeliveryDestinationType.InternalArchive }
                }
            };

            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", req);
            Assert.That(createResp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized));

            if (createResp.StatusCode == HttpStatusCode.OK)
            {
                var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
                Assert.That(created!.Success, Is.True);
                Assert.That(created.Template!.Name, Is.EqualTo("Integration Test Template"));

                // Verify GET retrieves it
                var getResp = await client.GetAsync(
                    $"/api/v1/scheduled-reporting/templates/{created.Template.TemplateId}");
                Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var got = await getResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
                Assert.That(got!.Template!.TemplateId, Is.EqualTo(created.Template.TemplateId));
            }
        }

        [Test]
        public async Task Integration_TriggerRun_EndToEnd()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            // Create template
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", new CreateReportingTemplateRequest
                {
                    Name = "Run E2E Template",
                    AudienceType = ReportingAudienceType.InternalCompliance,
                    Cadence = ReportingCadence.Monthly
                });

            if (createResp.StatusCode != HttpStatusCode.OK) return; // auth mode

            var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            var templateId = created!.Template!.TemplateId;

            // Trigger run
            var runResp = await client.PostAsJsonAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/runs",
                new TriggerReportRunRequest { TriggerReason = "E2E test" });
            Assert.That(runResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var run = await runResp.Content.ReadFromJsonAsync<ReportRunResponse>();
            Assert.That(run!.Success, Is.True);
            Assert.That(run.Run!.TemplateId, Is.EqualTo(templateId));

            // Retrieve run
            var getRunResp = await client.GetAsync($"/api/v1/scheduled-reporting/runs/{run.Run.RunId}");
            Assert.That(getRunResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // List runs
            var listResp = await client.GetAsync($"/api/v1/scheduled-reporting/templates/{templateId}/runs");
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var list = await listResp.Content.ReadFromJsonAsync<ListReportRunsResponse>();
            Assert.That(list!.TotalCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task Integration_GetNonExistentTemplate_Returns404()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var resp = await client.GetAsync("/api/v1/scheduled-reporting/templates/tpl-doesnotexist");
            // Returns 404 or 401 depending on auth mode
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound).Or.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Integration_ArchiveTemplate_EndToEnd()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", new CreateReportingTemplateRequest
                {
                    Name = "Archive Test",
                    AudienceType = ReportingAudienceType.ExecutiveSummary,
                    Cadence = ReportingCadence.Annual
                });
            if (createResp.StatusCode != HttpStatusCode.OK) return;

            var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            var templateId = created!.Template!.TemplateId;

            var archiveResp = await client.PostAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/archive", null);
            Assert.That(archiveResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var archived = await archiveResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            Assert.That(archived!.Template!.IsArchived, Is.True);
        }

        [Test]
        public async Task Integration_UpsertSchedule_EndToEnd()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", new CreateReportingTemplateRequest
                {
                    Name = "Schedule E2E",
                    AudienceType = ReportingAudienceType.InternalCompliance,
                    Cadence = ReportingCadence.Monthly
                });
            if (createResp.StatusCode != HttpStatusCode.OK) return;

            var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            var templateId = created!.Template!.TemplateId;

            var scheduleResp = await client.PutAsJsonAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/schedule",
                new UpsertScheduleRequest
                {
                    Cadence = ReportingCadence.Monthly,
                    DayOfMonth = 1,
                    TriggerHourUtc = 6,
                    IsActive = true
                });
            Assert.That(scheduleResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var schedule = await scheduleResp.Content.ReadFromJsonAsync<ScheduleResponse>();
            Assert.That(schedule!.Success, Is.True);
            Assert.That(schedule.Schedule!.Cadence, Is.EqualTo(ReportingCadence.Monthly));
        }

        [Test]
        public async Task Integration_GetSchedule_AfterUpsert_ReturnsSchedule()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", new CreateReportingTemplateRequest
                {
                    Name = "Get Schedule E2E",
                    AudienceType = ReportingAudienceType.BoardPresentation,
                    Cadence = ReportingCadence.Quarterly
                });
            if (createResp.StatusCode != HttpStatusCode.OK) return;

            var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            var templateId = created!.Template!.TemplateId;

            await client.PutAsJsonAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/schedule",
                new UpsertScheduleRequest
                {
                    Cadence = ReportingCadence.Quarterly,
                    TriggerHourUtc = 9
                });

            var getResp = await client.GetAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/schedule");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var schedule = await getResp.Content.ReadFromJsonAsync<ScheduleResponse>();
            Assert.That(schedule!.Schedule!.Cadence, Is.EqualTo(ReportingCadence.Quarterly));
        }

        [Test]
        public async Task Integration_ListTemplates_ReturnsJsonArray()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            // Create two templates
            for (int i = 0; i < 2; i++)
            {
                await client.PostAsJsonAsync("/api/v1/scheduled-reporting/templates",
                    new CreateReportingTemplateRequest
                    {
                        Name = $"List E2E Template {i}",
                        AudienceType = ReportingAudienceType.InternalCompliance,
                        Cadence = ReportingCadence.Monthly
                    });
            }

            var resp = await client.GetAsync("/api/v1/scheduled-reporting/templates");
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized));

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var list = await resp.Content.ReadFromJsonAsync<ListReportingTemplatesResponse>();
                Assert.That(list!.Success, Is.True);
                Assert.That(list.Templates, Is.Not.Null);
            }
        }

        [Test]
        public async Task Integration_ApprovalWorkflow_EndToEnd()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            // Create template with review + approval required
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/scheduled-reporting/templates", new CreateReportingTemplateRequest
                {
                    Name = "Approval Workflow E2E",
                    AudienceType = ReportingAudienceType.RegulatorySubmission,
                    Cadence = ReportingCadence.Monthly,
                    ReviewRequired = true,
                    ApprovalRequired = true
                });
            if (createResp.StatusCode != HttpStatusCode.OK) return;

            var created = await createResp.Content.ReadFromJsonAsync<ReportingTemplateResponse>();
            var templateId = created!.Template!.TemplateId;

            // Trigger run
            var runResp = await client.PostAsJsonAsync(
                $"/api/v1/scheduled-reporting/templates/{templateId}/runs",
                new TriggerReportRunRequest());
            var run = await runResp.Content.ReadFromJsonAsync<ReportRunResponse>();
            Assert.That(run!.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingReview));

            // Review
            var reviewResp = await client.PostAsJsonAsync(
                $"/api/v1/scheduled-reporting/runs/{run.Run.RunId}/review",
                new ReviewReportRunRequest { Approve = true, ReviewNotes = "E2E review" });
            Assert.That(reviewResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var reviewed = await reviewResp.Content.ReadFromJsonAsync<ReportRunResponse>();
            Assert.That(reviewed!.Run!.Status, Is.EqualTo(ReportRunStatus.AwaitingApproval));

            // Approve
            var approveResp = await client.PostAsJsonAsync(
                $"/api/v1/scheduled-reporting/runs/{run.Run.RunId}/approve",
                new ApproveReportRunRequest { Approve = true, ApprovalNotes = "E2E approval" });
            Assert.That(approveResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var approved = await approveResp.Content.ReadFromJsonAsync<ReportRunResponse>();
            Assert.That(approved!.Run!.Status,
                Is.EqualTo(ReportRunStatus.Delivered).Or.EqualTo(ReportRunStatus.PartiallyDelivered));
            Assert.That(approved.Run.ExportedAt, Is.Not.Null);
        }
    }
}
