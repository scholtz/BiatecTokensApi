using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TransferAuditLogTests
    {
        private WhitelistRepository _repository;
        private Mock<ILogger<WhitelistService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private Mock<ILogger<WhitelistRepository>> _repoLoggerMock;
        private WhitelistService _service;
        private const string TestPerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidAddress1 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidAddress2 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _repository = new WhitelistRepository(_repoLoggerMock.Object);
            _loggerMock = new Mock<ILogger<WhitelistService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new WhitelistService(_repository, _loggerMock.Object, _meteringServiceMock.Object);
        }

        [Test]
        public async Task ValidateTransferAsync_SuccessfulTransfer_ShouldRecordAuditLog()
        {
            // Arrange - Add both addresses to whitelist
            var assetId = (ulong)12345;
            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress1.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress2.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            var request = new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = ValidAddress1,
                ToAddress = ValidAddress2,
                Amount = 1000
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, TestPerformedBy);

            // Assert - Verify transfer is allowed
            Assert.That(result.Success, Is.True, $"Expected Success=true, got ErrorMessage: {result.ErrorMessage}");
            Assert.That(result.IsAllowed, Is.True);

            // Verify audit log was created
            var auditLogRequest = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditLogRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one transfer validation audit log entry");
            
            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(assetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(WhitelistActionType.TransferValidation));
            Assert.That(auditEntry.Address, Is.EqualTo(ValidAddress1.ToUpperInvariant()));
            Assert.That(auditEntry.ToAddress, Is.EqualTo(ValidAddress2));
            Assert.That(auditEntry.PerformedBy, Is.EqualTo(TestPerformedBy));
            Assert.That(auditEntry.TransferAllowed, Is.True);
            Assert.That(auditEntry.DenialReason, Is.Null);
            Assert.That(auditEntry.Amount, Is.EqualTo(1000));
            Assert.That(auditEntry.Notes, Is.EqualTo("Transfer validation passed"));
        }

        [Test]
        public async Task ValidateTransferAsync_DeniedTransfer_ShouldRecordAuditLogWithReason()
        {
            // Arrange - Only add sender, not receiver
            var assetId = (ulong)12345;
            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress1.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            var request = new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = ValidAddress1,
                ToAddress = ValidAddress2,
                Amount = 500
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, TestPerformedBy);

            // Assert - Verify transfer is denied
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));

            // Verify audit log was created with denial reason
            var auditLogRequest = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditLogRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one transfer validation audit log entry");
            
            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(assetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(WhitelistActionType.TransferValidation));
            Assert.That(auditEntry.Address, Is.EqualTo(ValidAddress1.ToUpperInvariant()));
            Assert.That(auditEntry.ToAddress, Is.EqualTo(ValidAddress2));
            Assert.That(auditEntry.PerformedBy, Is.EqualTo(TestPerformedBy));
            Assert.That(auditEntry.TransferAllowed, Is.False);
            Assert.That(auditEntry.DenialReason, Is.Not.Null);
            Assert.That(auditEntry.DenialReason, Does.Contain("not whitelisted"));
            Assert.That(auditEntry.Amount, Is.EqualTo(500));
            Assert.That(auditEntry.Notes, Is.EqualTo("Transfer validation failed"));
        }

        [Test]
        public async Task ValidateTransferAsync_InvalidAddress_ShouldRecordAuditLog()
        {
            // Arrange
            var assetId = (ulong)12345;
            var invalidAddress = "INVALID";

            var request = new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = invalidAddress,
                ToAddress = ValidAddress2
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, TestPerformedBy);

            // Assert - Verify validation failed
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Invalid"));

            // Verify audit log was created
            var auditLogRequest = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditLogRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one transfer validation audit log entry");
            
            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(assetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(WhitelistActionType.TransferValidation));
            Assert.That(auditEntry.PerformedBy, Is.EqualTo(TestPerformedBy));
            Assert.That(auditEntry.TransferAllowed, Is.False);
            Assert.That(auditEntry.DenialReason, Does.Contain("Invalid"));
        }

        [Test]
        public async Task ValidateTransferAsync_MultipleTransfers_ShouldRecordMultipleAuditLogs()
        {
            // Arrange
            var assetId = (ulong)12345;
            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress1.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress2.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            // Act - Perform multiple transfer validations
            for (int i = 0; i < 3; i++)
            {
                var request = new ValidateTransferRequest
                {
                    AssetId = assetId,
                    FromAddress = ValidAddress1,
                    ToAddress = ValidAddress2,
                    Amount = (ulong)(100 * (i + 1))
                };

                await _service.ValidateTransferAsync(request, TestPerformedBy);
            }

            // Assert - Verify all audit logs were created
            var auditLogRequest = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditLogRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(3), "Should have three transfer validation audit log entries");
            
            // Verify amounts are different (confirming they are different requests)
            var amounts = auditLogs.Select(a => a.Amount).OrderBy(a => a).ToList();
            Assert.That(amounts[0], Is.EqualTo(100));
            Assert.That(amounts[1], Is.EqualTo(200));
            Assert.That(amounts[2], Is.EqualTo(300));
        }

        [Test]
        public async Task ValidateTransferAsync_ShouldEmitMeteringEvent()
        {
            // Arrange
            var assetId = (ulong)12345;
            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress1.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            await _repository.AddEntryAsync(new WhitelistEntry
            {
                AssetId = assetId,
                Address = ValidAddress2.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = TestPerformedBy,
                CreatedAt = DateTime.UtcNow
            });

            var request = new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = ValidAddress1,
                ToAddress = ValidAddress2
            };

            // Act
            await _service.ValidateTransferAsync(request, TestPerformedBy);

            // Assert - Verify metering event was emitted
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                e => e.Category == MeteringCategory.Whitelist &&
                     e.OperationType == MeteringOperationType.TransferValidation &&
                     e.AssetId == assetId &&
                     e.PerformedBy == TestPerformedBy &&
                     e.ItemCount == 1
            )), Times.Once);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByTransferValidation_ShouldReturnOnlyTransferValidations()
        {
            // Arrange - Create mix of audit log entries
            var assetId = (ulong)12345;
            
            // Add whitelist change entries
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow
            });

            // Add transfer validation entries
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                ToAddress = ValidAddress2,
                TransferAllowed = true
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                ToAddress = ValidAddress2,
                TransferAllowed = false,
                DenialReason = "Test denial"
            });

            // Act - Get only transfer validation logs
            var auditLogRequest = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditLogRequest);

            // Assert
            Assert.That(auditLogs.Count, Is.EqualTo(2), "Should return only transfer validation entries");
            Assert.That(auditLogs.All(a => a.ActionType == WhitelistActionType.TransferValidation), Is.True);
        }
    }
}
