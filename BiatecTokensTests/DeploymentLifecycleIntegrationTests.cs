using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive integration tests for deployment lifecycle including new states and error handling
    /// </summary>
    [TestFixture]
    public class DeploymentLifecycleIntegrationTests
    {
        private IDeploymentStatusService _service = null!;
        private IDeploymentAuditService _auditService = null!;
        private DeploymentStatusRepository _repository = null!;
        private Mock<IWebhookService> _webhookServiceMock = null!;

        [SetUp]
        public void Setup()
        {
            var repositoryLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            _repository = new DeploymentStatusRepository(repositoryLogger.Object);

            _webhookServiceMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();

            _service = new DeploymentStatusService(
                _repository,
                _webhookServiceMock.Object,
                serviceLogger.Object);

            var auditServiceLogger = new Mock<ILogger<DeploymentAuditService>>();
            _auditService = new DeploymentAuditService(
                _repository,
                auditServiceLogger.Object);
        }

        [Test]
        public async Task CompleteLifecycle_WithIndexedState_ShouldFollowCorrectTransitions()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234567890abcdef",
                "Indexed Test Token",
                "ITT");

            // Act & Assert - Full lifecycle with Indexed state
            
            // Queued -> Submitted
            var result1 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Transaction submitted",
                transactionHash: "0xtxhash123");
            Assert.That(result1, Is.True, "Queued -> Submitted should succeed");

            // Submitted -> Pending
            var result2 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Pending,
                "Transaction pending");
            Assert.That(result2, Is.True, "Submitted -> Pending should succeed");

            // Pending -> Confirmed
            var result3 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Confirmed,
                "Transaction confirmed",
                confirmedRound: 12345);
            Assert.That(result3, Is.True, "Pending -> Confirmed should succeed");

            // Confirmed -> Indexed (NEW STATE)
            var result4 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Indexed,
                "Transaction indexed by explorers");
            Assert.That(result4, Is.True, "Confirmed -> Indexed should succeed");

            // Indexed -> Completed
            await _service.UpdateAssetIdentifierAsync(deploymentId, "0xcontractaddress");
            var result5 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Deployment completed");
            Assert.That(result5, Is.True, "Indexed -> Completed should succeed");

            // Verify final state
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            
            // Verify complete history
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(6), "Should have 6 status entries");
            
            // Verify order
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Queued));
            Assert.That(history[1].Status, Is.EqualTo(DeploymentStatus.Submitted));
            Assert.That(history[2].Status, Is.EqualTo(DeploymentStatus.Pending));
            Assert.That(history[3].Status, Is.EqualTo(DeploymentStatus.Confirmed));
            Assert.That(history[4].Status, Is.EqualTo(DeploymentStatus.Indexed));
            Assert.That(history[5].Status, Is.EqualTo(DeploymentStatus.Completed));
            
            // Verify timestamps are in ascending order
            for (int i = 1; i < history.Count; i++)
            {
                Assert.That(history[i].Timestamp, Is.GreaterThanOrEqualTo(history[i-1].Timestamp),
                    $"History entry {i} timestamp should be >= entry {i-1}");
            }
        }

        [Test]
        public async Task AlternativeLifecycle_SkipIndexed_ShouldStillSucceed()
        {
            // Test that Confirmed -> Completed is still valid (Indexed is optional)
            var deploymentId = await _service.CreateDeploymentAsync(
                "ASA_FT", "voimain-v1.0", "0xabcd", "Quick Token", "QTK");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed);
            
            // Skip Indexed, go directly to Completed
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Completed without indexing wait");

            Assert.That(result, Is.True, "Confirmed -> Completed should still be valid");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
        }

        [Test]
        public async Task FailureAndRecovery_WithStructuredError_ShouldMaintainAuditTrail()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ARC200_Mintable", "testnet-v1.0", "0x9999", "Retry Token", "RTK");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);

            // Act - Simulate network failure with structured error
            var networkError = DeploymentErrorFactory.NetworkError(
                "RPC connection timeout after 30 seconds",
                "Node: https://testnet-api.example.com");

            await _service.MarkDeploymentFailedAsync(deploymentId, networkError);

            // Verify failed state
            var deployment1 = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment1!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));
            Assert.That(deployment1.ErrorMessage, Does.Contain("RPC connection timeout"));

            // Verify error metadata is captured
            var history1 = await _service.GetStatusHistoryAsync(deploymentId);
            var failedEntry = history1.Last(e => e.Status == DeploymentStatus.Failed);
            Assert.That(failedEntry.Metadata, Is.Not.Null);
            Assert.That(failedEntry.Metadata!.ContainsKey("errorCategory"), Is.True);
            Assert.That(failedEntry.Metadata!["errorCategory"].ToString(), Is.EqualTo("NetworkError"));
            Assert.That(failedEntry.Metadata!["isRetryable"], Is.EqualTo(true));

            // Act - Retry deployment
            var retryResult = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Queued,
                "Retrying after network error");
            Assert.That(retryResult, Is.True, "Failed -> Queued retry should succeed");

            // Complete successful retry
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, confirmedRound: 54321);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed);

            // Verify final state
            var deployment2 = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment2!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));

            // Verify complete audit trail
            var history2 = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history2.Count, Is.EqualTo(9), "Should have 9 entries: initial attempt (4) + failed (1) + retry (4)");
            
            // Verify chronological order
            for (int i = 1; i < history2.Count; i++)
            {
                Assert.That(history2[i].Timestamp, Is.GreaterThanOrEqualTo(history2[i-1].Timestamp),
                    "Audit trail should maintain chronological order");
            }

            // Verify we can export audit trail
            var auditJson = await _auditService.ExportAuditTrailAsJsonAsync(deploymentId);
            Assert.That(auditJson, Is.Not.Null);
            Assert.That(auditJson, Does.Contain("NetworkError"));
            Assert.That(auditJson, Does.Contain("Retrying after network error"));
        }

        [Test]
        public async Task CancelledDeployment_ShouldBeTerminalState()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Preminted", "base-mainnet", "0xcafe", "Cancelled Token", "CTK");

            // Act - Cancel from Queued state
            var cancelResult = await _service.CancelDeploymentAsync(
                deploymentId,
                "User decided to change parameters");

            Assert.That(cancelResult, Is.True, "Should successfully cancel from Queued");

            // Verify cancelled state
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Cancelled));

            // Verify history
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(2)); // Queued, Cancelled
            Assert.That(history[1].Status, Is.EqualTo(DeploymentStatus.Cancelled));
            Assert.That(history[1].Message, Does.Contain("User decided to change parameters"));

            // Verify cancelled is terminal - cannot transition to other states
            var attemptSubmit = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Try to submit after cancel");

            Assert.That(attemptSubmit, Is.False, "Cancelled -> Submitted should fail");

            var attemptComplete = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Try to complete after cancel");

            Assert.That(attemptComplete, Is.False, "Cancelled -> Completed should fail");
        }

        [Test]
        public async Task IdempotentUpdates_AcrossMultipleRetries_ShouldNotDuplicateHistory()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ASA_NFT", "testnet-v1.0", "0xbeef", "Idempotent Token", "IDM");

            // Act - Simulate network issues causing duplicate status update attempts
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "First attempt");
            
            // Duplicate submission (idempotent)
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Duplicate 1");
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Duplicate 2");
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Duplicate 3");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
            
            // Duplicate pending
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Duplicate pending");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Confirmed", confirmedRound: 99999);

            // Assert - Should only have unique state transitions
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(4), "Should have only 4 entries: Queued, Submitted, Pending, Confirmed");

            var statusCounts = history.GroupBy(h => h.Status).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(statusCounts[DeploymentStatus.Queued], Is.EqualTo(1), "Should have exactly 1 Queued entry");
            Assert.That(statusCounts[DeploymentStatus.Submitted], Is.EqualTo(1), "Should have exactly 1 Submitted entry");
            Assert.That(statusCounts[DeploymentStatus.Pending], Is.EqualTo(1), "Should have exactly 1 Pending entry");
            Assert.That(statusCounts[DeploymentStatus.Confirmed], Is.EqualTo(1), "Should have exactly 1 Confirmed entry");
        }

        [Test]
        public async Task MultipleFailureTypes_ShouldTrackDifferentErrorCategories()
        {
            // Test that different error types are properly categorized

            // Network error
            var id1 = await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x1", "T1", "T1");
            await _service.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Submitted);
            await _service.MarkDeploymentFailedAsync(id1, DeploymentErrorFactory.NetworkError("Timeout", "node down"));

            // Insufficient funds error
            var id2 = await _service.CreateDeploymentAsync("ARC200", "voimain-v1.0", "0x2", "T2", "T2");
            await _service.UpdateDeploymentStatusAsync(id2, DeploymentStatus.Submitted);
            await _service.MarkDeploymentFailedAsync(id2, DeploymentErrorFactory.InsufficientFunds("100 ALGO", "50 ALGO"));

            // Validation error
            var id3 = await _service.CreateDeploymentAsync("ASA", "testnet-v1.0", "0x3", "T3", "T3");
            await _service.MarkDeploymentFailedAsync(id3, DeploymentErrorFactory.ValidationError("Invalid supply", "Supply must be positive"));

            // Verify each has correct error category
            var hist1 = await _service.GetStatusHistoryAsync(id1);
            var fail1 = hist1.Last(e => e.Status == DeploymentStatus.Failed);
            Assert.That(fail1.Metadata!["errorCategory"].ToString(), Is.EqualTo("NetworkError"));

            var hist2 = await _service.GetStatusHistoryAsync(id2);
            var fail2 = hist2.Last(e => e.Status == DeploymentStatus.Failed);
            Assert.That(fail2.Metadata!["errorCategory"].ToString(), Is.EqualTo("InsufficientFunds"));

            var hist3 = await _service.GetStatusHistoryAsync(id3);
            var fail3 = hist3.Last(e => e.Status == DeploymentStatus.Failed);
            Assert.That(fail3.Metadata!["errorCategory"].ToString(), Is.EqualTo("ValidationError"));
        }

        [Test]
        public async Task ConcurrentStatusUpdates_OnDifferentDeployments_ShouldSucceed()
        {
            // Create multiple deployments
            var ids = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var id = await _service.CreateDeploymentAsync(
                    "ERC20", "base-mainnet", $"0x{i:X4}", $"Token{i}", $"TK{i}");
                ids.Add(id);
            }

            // Act - Update them concurrently
            var tasks = ids.Select(async (id, index) =>
            {
                await _service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted);
                await Task.Delay(10); // Simulate some delay
                await _service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Pending);
                await Task.Delay(10);
                
                if (index % 2 == 0)
                {
                    await _service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Confirmed);
                    await _service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Completed);
                }
                else
                {
                    await _service.MarkDeploymentFailedAsync(id, "Test failure");
                }
            }).ToList();

            await Task.WhenAll(tasks);

            // Verify all deployments completed without corruption
            foreach (var id in ids)
            {
                var deployment = await _service.GetDeploymentAsync(id);
                Assert.That(deployment, Is.Not.Null, $"Deployment {id} should exist");
                
                var history = await _service.GetStatusHistoryAsync(id);
                Assert.That(history.Count, Is.GreaterThanOrEqualTo(2), $"Deployment {id} should have history");
                
                // Verify chronological order
                for (int i = 1; i < history.Count; i++)
                {
                    Assert.That(history[i].Timestamp, Is.GreaterThanOrEqualTo(history[i-1].Timestamp),
                        $"Deployment {id} should have ordered history");
                }
            }

            // Verify counts
            var completedCount = 0;
            var failedCount = 0;
            foreach (var id in ids)
            {
                var dep = await _service.GetDeploymentAsync(id);
                if (dep!.CurrentStatus == DeploymentStatus.Completed) completedCount++;
                if (dep!.CurrentStatus == DeploymentStatus.Failed) failedCount++;
            }

            Assert.That(completedCount, Is.EqualTo(5), "Should have 5 completed deployments");
            Assert.That(failedCount, Is.EqualTo(5), "Should have 5 failed deployments");
        }

        [Test]
        public async Task AuditTrailExport_ShouldIncludeAllRelevantData()
        {
            // Use isolated repository for this test
            var isolatedRepoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var isolatedRepo = new DeploymentStatusRepository(isolatedRepoLogger.Object);
            var isolatedService = new DeploymentStatusService(
                isolatedRepo,
                _webhookServiceMock.Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);
            var isolatedAuditService = new DeploymentAuditService(
                isolatedRepo,
                new Mock<ILogger<DeploymentAuditService>>().Object);

            // Create a deployment with full lifecycle and failure
            var deploymentId = await isolatedService.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234567890abcdef1234567890abcdef12345678",
                "Audit Test Token",
                "ATT");

            await isolatedService.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, transactionHash: "0xabcdef123456");
            await isolatedService.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            await isolatedService.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, confirmedRound: 12345678);
            await isolatedService.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Indexed);
            await isolatedService.UpdateAssetIdentifierAsync(deploymentId, "0xcontract1234567890");
            await isolatedService.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed);

            // Export as JSON
            var jsonExport = await isolatedAuditService.ExportAuditTrailAsJsonAsync(deploymentId);
            Assert.That(jsonExport, Does.Contain("deploymentId"));
            Assert.That(jsonExport, Does.Contain(deploymentId));
            Assert.That(jsonExport, Does.Contain("Audit Test Token"));
            Assert.That(jsonExport, Does.Contain("ATT"));
            Assert.That(jsonExport, Does.Contain("base-mainnet"));
            Assert.That(jsonExport, Does.Contain("0x1234567890abcdef1234567890abcdef12345678"));
            Assert.That(jsonExport, Does.Contain("0xabcdef123456"));
            Assert.That(jsonExport, Does.Contain("0xcontract1234567890"));
            // Indexed status is enum value 6, check for it in status history
            Assert.That(jsonExport, Does.Contain("\"status\": 6"), "Should contain Indexed status (enum value 6)");
            Assert.That(jsonExport, Does.Contain("12345678")); // Confirmed round

            // Export as CSV
            var csvExport = await isolatedAuditService.ExportAuditTrailAsCsvAsync(deploymentId);
            Assert.That(csvExport, Does.Contain("DeploymentId"));
            Assert.That(csvExport, Does.Contain(deploymentId));
            Assert.That(csvExport, Does.Contain("Audit Test Token"));
            // CSV should have Indexed as enum name
            Assert.That(csvExport, Does.Contain("Indexed"), "CSV should contain Indexed status");
            
            // Verify CSV has header and data rows (exactly 6 status entries from this deployment)
            var lines = csvExport.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(7), "Should have header + exactly 6 status entries");
        }

        [Test]
        public async Task InvalidTransition_FromCompleted_ShouldFail()
        {
            // Arrange - Create and complete a deployment
            var deploymentId = await _service.CreateDeploymentAsync(
                "ASA_FT", "voimain-v1.0", "0xdone", "Done Token", "DN");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed);

            // Act & Assert - Try invalid transitions from Completed
            var result1 = await _service.UpdateDeploymentStatusAsync(
                deploymentId, DeploymentStatus.Pending, "Invalid");
            Assert.That(result1, Is.False, "Completed -> Pending should fail");

            var result2 = await _service.UpdateDeploymentStatusAsync(
                deploymentId, DeploymentStatus.Failed, "Invalid");
            Assert.That(result2, Is.False, "Completed -> Failed should fail");

            var result3 = await _service.UpdateDeploymentStatusAsync(
                deploymentId, DeploymentStatus.Queued, "Invalid");
            Assert.That(result3, Is.False, "Completed -> Queued should fail");

            // Verify deployment is still Completed
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));

            // Verify no new history entries were added
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(5)); // Only valid transitions
        }

        [Test]
        public async Task Metrics_ShouldReflectAllDeploymentStates()
        {
            // Use isolated repository for this test
            var isolatedRepoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var isolatedRepo = new DeploymentStatusRepository(isolatedRepoLogger.Object);
            var isolatedService = new DeploymentStatusService(
                isolatedRepo,
                _webhookServiceMock.Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            // Create deployments in various states
            var id1 = await isolatedService.CreateDeploymentAsync("ERC20", "base-mainnet", "0x1", "T1", "T1");
            await isolatedService.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Submitted);
            await isolatedService.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Pending);
            await isolatedService.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Confirmed);
            await isolatedService.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Indexed);
            await isolatedService.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Completed);

            var id2 = await isolatedService.CreateDeploymentAsync("ARC200", "voimain-v1.0", "0x2", "T2", "T2");
            await isolatedService.UpdateDeploymentStatusAsync(id2, DeploymentStatus.Submitted);
            await isolatedService.MarkDeploymentFailedAsync(id2, "Test failure");

            var id3 = await isolatedService.CreateDeploymentAsync("ASA", "testnet-v1.0", "0x3", "T3", "T3");
            // Leave in Queued

            var id4 = await isolatedService.CreateDeploymentAsync("ERC20", "base-mainnet", "0x4", "T4", "T4");
            await isolatedService.CancelDeploymentAsync(id4, "Cancelled");

            // Get metrics
            var metrics = await isolatedService.GetDeploymentMetricsAsync(new GetDeploymentMetricsRequest
            {
                FromDate = DateTime.UtcNow.AddHours(-1)
            });

            // Verify metrics - should have exactly these 4 deployments
            Assert.That(metrics.TotalDeployments, Is.EqualTo(4), "Should have exactly 4 deployments");
            Assert.That(metrics.SuccessfulDeployments, Is.EqualTo(1), "Should have 1 completed deployment");
            Assert.That(metrics.FailedDeployments, Is.EqualTo(1), "Should have 1 failed deployment");
            Assert.That(metrics.CancelledDeployments, Is.EqualTo(1), "Should have 1 cancelled deployment");
            Assert.That(metrics.PendingDeployments, Is.EqualTo(1), "Should have 1 pending deployment (queued)");

            // Verify duration tracking for completed deployment (may be 0 if tests execute very fast)
            Assert.That(metrics.AverageDurationMs, Is.GreaterThanOrEqualTo(0), "Average duration should be >= 0");
            
            // Verify metrics are properly structured
            Assert.That(metrics.PeriodStart, Is.LessThanOrEqualTo(DateTime.UtcNow));
            Assert.That(metrics.PeriodEnd, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }
    }
}
