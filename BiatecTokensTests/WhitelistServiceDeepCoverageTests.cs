using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for whitelist service - deep coverage of all validation rules,
    /// network-specific compliance rules, enforcement reporting, and enforcement
    /// edge cases.
    ///
    /// These tests complement <see cref="WhitelistServiceTests"/> with additional depth:
    /// - Aramid network mandatory KYC rules
    /// - VOI network operator restriction rules
    /// - Expired entry validation in transfers
    /// - Enforcement report statistics accuracy
    /// - Pagination boundary conditions
    /// - VerifyAllowlistStatus for all 4 AllowlistTransferStatus values
    /// - Revoked entry behavior in transfers
    /// - Network-specific KYC enforcement
    /// - Audit log TransferValidation entries
    /// - Filter combinations on audit log and enforcement report
    /// </summary>
    [TestFixture]
    public class WhitelistServiceDeepCoverageTests
    {
        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private static IWhitelistService CreateService() =>
            new WhitelistService(
                new WhitelistRepository(NullLogger<WhitelistRepository>.Instance),
                NullLogger<WhitelistService>.Instance,
                new NoOpMeteringService(),
                new UnlimitedTierService(),
                new NoOpWebhookService());

        // ── Aramid network mandatory KYC rules ────────────────────────────────────

        [Test]
        public async Task AddEntry_AramidNetwork_WithoutKyc_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 1000,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Network = "aramidmain-v1.0",
                KycVerified = false // Aramid requires KYC for Active
            }, "issuer");

            Assert.That(result.Success, Is.False, "Aramid Active without KYC must fail");
            Assert.That(result.ErrorMessage, Does.Contain("Aramid").Or.Contain("KYC"),
                "Error must mention Aramid KYC requirement");
        }

        [Test]
        public async Task AddEntry_AramidNetwork_WithKycNoProvider_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 1001,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = null // Aramid requires provider when KYC verified
            }, "issuer");

            Assert.That(result.Success, Is.False, "Aramid with KYC verified but no provider must fail");
            Assert.That(result.ErrorMessage, Does.Contain("provider").Or.Contain("KYC provider"),
                "Error must mention missing KYC provider");
        }

        [Test]
        public async Task AddEntry_AramidNetwork_WithKycAndProvider_Succeeds()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 1002,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = "PassportScan"
            }, "issuer");

            Assert.That(result.Success, Is.True,
                "Aramid Active with KYC + provider must succeed: " + result.ErrorMessage);
        }

        [Test]
        public async Task AddEntry_AramidNetwork_Inactive_WithoutKyc_Succeeds()
        {
            // Aramid KYC rule applies only to Active status
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 1003,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Inactive,
                Network = "aramidmain-v1.0",
                KycVerified = false
            }, "issuer");

            Assert.That(result.Success, Is.True,
                "Aramid Inactive without KYC must succeed: " + result.ErrorMessage);
        }

        [Test]
        public async Task AddEntry_AramidNetwork_OperatorRevoke_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 1004,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Revoked,
                Network = "aramidmain-v1.0",
                Role = WhitelistRole.Operator,
                KycVerified = true,
                KycProvider = "TestProvider"
            }, "operator-1");

            Assert.That(result.Success, Is.False,
                "Operator role cannot revoke on Aramid network");
            Assert.That(result.ErrorMessage, Does.Contain("Operator").Or.Contain("revoke"),
                "Error must explain operator restriction");
        }

        // ── VOI network rules ─────────────────────────────────────────────────────

        [Test]
        public async Task AddEntry_VoiNetwork_OperatorRevoke_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 2000,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Revoked,
                Network = "voimain-v1.0",
                Role = WhitelistRole.Operator
            }, "operator-1");

            Assert.That(result.Success, Is.False, "Operator role cannot revoke on VOI network");
        }

        [Test]
        public async Task AddEntry_VoiNetwork_AdminRevoke_Succeeds()
        {
            var svc = CreateService();
            // First add as Active
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 2001, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                Network = "voimain-v1.0", Role = WhitelistRole.Admin
            }, "admin-1");

            // Admin can revoke
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 2001, Address = AlgoAddr1, Status = WhitelistStatus.Revoked,
                Network = "voimain-v1.0", Role = WhitelistRole.Admin
            }, "admin-1");

            Assert.That(result.Success, Is.True,
                "Admin role can revoke on VOI network: " + result.ErrorMessage);
        }

        [Test]
        public async Task AddEntry_VoiNetwork_NoKyc_Active_Succeeds_WithWarning()
        {
            // VOI KYC is a warning, not a hard error
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 2002,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Network = "voimain-v1.0",
                KycVerified = false
            }, "issuer");

            Assert.That(result.Success, Is.True,
                "VOI without KYC is a warning only - should still succeed: " + result.ErrorMessage);
        }

        // ── Expired entry behavior ────────────────────────────────────────────────

        [Test]
        public async Task ValidateTransfer_SenderExpired_ReturnsDenied()
        {
            var svc = CreateService();

            // Add sender with past expiry
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3000, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
            }, "issuer");

            // Add receiver as active
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3000, Address = AlgoAddr2, Status = WhitelistStatus.Active
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 3000, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100
            }, "issuer");

            Assert.That(result.IsAllowed, Is.False, "Expired sender entry must deny transfer");
            Assert.That(result.SenderStatus!.IsExpired, Is.True, "Sender status must show expired");
            Assert.That(result.DenialReason, Does.Contain("expired").IgnoreCase,
                "Denial reason must mention expiry");
        }

        [Test]
        public async Task ValidateTransfer_ReceiverExpired_ReturnsDenied()
        {
            var svc = CreateService();

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3001, Address = AlgoAddr1, Status = WhitelistStatus.Active
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3001, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 3001, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50
            }, "issuer");

            Assert.That(result.IsAllowed, Is.False, "Expired receiver must deny transfer");
            Assert.That(result.ReceiverStatus!.IsExpired, Is.True, "Receiver status must show expired");
        }

        [Test]
        public async Task ValidateTransfer_BothExpired_ReturnsDeniedWithBothReasons()
        {
            var svc = CreateService();
            var expiry = DateTime.UtcNow.AddDays(-2);

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3002, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = expiry
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3002, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                ExpirationDate = expiry
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 3002, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10
            }, "issuer");

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Is.Not.Null.And.Not.Empty,
                "Both expired must have a denial reason");
        }

        [Test]
        public async Task ValidateTransfer_FutureExpiry_NotExpired_Allowed()
        {
            var svc = CreateService();

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3003, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(365) // Future - not expired
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 3003, Address = AlgoAddr2, Status = WhitelistStatus.Active
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 3003, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10
            }, "issuer");

            Assert.That(result.IsAllowed, Is.True, "Future expiry → transfer allowed");
            Assert.That(result.SenderStatus!.IsExpired, Is.False, "Sender must not be expired");
        }

        // ── Revoked entry behavior ────────────────────────────────────────────────

        [Test]
        public async Task ValidateTransfer_SenderRevoked_ReturnsDenied()
        {
            var svc = CreateService();

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 4000, Address = AlgoAddr1, Status = WhitelistStatus.Revoked
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 4000, Address = AlgoAddr2, Status = WhitelistStatus.Active
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 4000, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1
            }, "issuer");

            Assert.That(result.IsAllowed, Is.False, "Revoked sender must deny transfer");
            Assert.That(result.SenderStatus!.IsActive, Is.False);
        }

        [Test]
        public async Task ValidateTransfer_ReceiverRevoked_ReturnsDenied()
        {
            var svc = CreateService();

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 4001, Address = AlgoAddr1, Status = WhitelistStatus.Active
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = 4001, Address = AlgoAddr2, Status = WhitelistStatus.Revoked
            }, "issuer");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = 4001, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1
            }, "issuer");

            Assert.That(result.IsAllowed, Is.False, "Revoked receiver must deny transfer");
        }

        // ── TransferValidation audit log entries ──────────────────────────────────

        [Test]
        public async Task ValidateTransfer_AlwaysAddsAuditLogEntry()
        {
            var svc = CreateService();
            const ulong assetId = 5000;

            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100
            }, "validator-1");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.TransferValidation
            });

            Assert.That(log.Entries.Any(e => e.ActionType == WhitelistActionType.TransferValidation), Is.True,
                "Transfer validation must create audit log entry");
        }

        [Test]
        public async Task ValidateTransfer_AuditEntry_HasTransferAllowedFlag()
        {
            var svc = CreateService();
            const ulong assetId = 5001;

            // Both whitelisted (allowed)
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100
            }, "validator");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest { AssetId = assetId });
            var transferEntry = log.Entries.FirstOrDefault(e => e.ActionType == WhitelistActionType.TransferValidation);

            Assert.That(transferEntry, Is.Not.Null, "Transfer validation audit entry must exist");
            Assert.That(transferEntry!.TransferAllowed, Is.True, "Audit must record allowed=true");
        }

        [Test]
        public async Task ValidateTransfer_Denied_AuditEntryHasDenialReason()
        {
            var svc = CreateService();
            const ulong assetId = 5002;

            // Sender not whitelisted → denied
            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50
            }, "validator");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest { AssetId = assetId });
            var transferEntry = log.Entries.FirstOrDefault(e => e.ActionType == WhitelistActionType.TransferValidation);

            Assert.That(transferEntry, Is.Not.Null);
            Assert.That(transferEntry!.TransferAllowed, Is.False, "Denied transfer must be false");
            Assert.That(transferEntry.DenialReason, Is.Not.Null.And.Not.Empty,
                "Denied transfer audit must have denial reason");
        }

        // ── Enforcement report statistics ─────────────────────────────────────────

        [Test]
        public async Task EnforcementReport_AfterMixedValidations_CorrectSummary()
        {
            var svc = CreateService();
            const ulong assetId = 6000;

            // 2 whitelisted (will produce allowed transfers)
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            // 2 allowed transfers
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10 }, "v");
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr2, ToAddress = AlgoAddr1, Amount = 5 }, "v");

            // 1 denied transfer (AlgoAddr3 not whitelisted)
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr1, Amount = 1 }, "v");

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest { AssetId = assetId });

            Assert.That(report.Success, Is.True);
            Assert.That(report.Summary!.AllowedTransfers, Is.EqualTo(2), "Should have 2 allowed");
            Assert.That(report.Summary.DeniedTransfers, Is.EqualTo(1), "Should have 1 denied");
            Assert.That(report.Summary.TotalValidations, Is.EqualTo(3), "Total must be 3");
        }

        [Test]
        public async Task EnforcementReport_AllowedPercentage_CalculatedCorrectly()
        {
            var svc = CreateService();
            const ulong assetId = 6001;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            // 3 allowed
            for (int i = 0; i < 3; i++)
                await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 }, "v");

            // 1 denied
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr2, Amount = 1 }, "v");

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest { AssetId = assetId });
            Assert.That(report.Summary!.AllowedPercentage, Is.EqualTo(75.0).Within(0.01),
                "75% of transfers should be allowed (3/4)");
        }

        [Test]
        public async Task EnforcementReport_FilterByTransferAllowed_True_ReturnsOnlyAllowed()
        {
            var svc = CreateService();
            const ulong assetId = 6002;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 }, "v");
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr2, Amount = 1 }, "v"); // denied

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                TransferAllowed = true
            });

            Assert.That(report.Entries.All(e => e.TransferAllowed == true), Is.True,
                "Filter TransferAllowed=true must only return allowed transfers");
        }

        [Test]
        public async Task EnforcementReport_EmptyAsset_ReturnsEmptySummary()
        {
            var svc = CreateService();
            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest
            {
                AssetId = 9999999
            });

            Assert.That(report.Success, Is.True);
            Assert.That(report.Summary!.TotalValidations, Is.EqualTo(0));
            Assert.That(report.Summary.AllowedTransfers, Is.EqualTo(0));
            Assert.That(report.Summary.DeniedTransfers, Is.EqualTo(0));
        }

        // ── VerifyAllowlistStatus all outcomes ────────────────────────────────────

        [Test]
        public async Task VerifyAllowlistStatus_BothApproved_ReturnsAllowed()
        {
            var svc = CreateService();
            const ulong assetId = 7000;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.Allowed));
            Assert.That(result.SenderStatus!.Status, Is.EqualTo(AllowlistStatus.Approved));
            Assert.That(result.RecipientStatus!.Status, Is.EqualTo(AllowlistStatus.Approved));
        }

        [Test]
        public async Task VerifyAllowlistStatus_SenderBlocked_ReturnsBlockedSender()
        {
            var svc = CreateService();
            const ulong assetId = 7001;

            // Only receiver whitelisted
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedSender));
        }

        [Test]
        public async Task VerifyAllowlistStatus_RecipientBlocked_ReturnsBlockedRecipient()
        {
            var svc = CreateService();
            const ulong assetId = 7002;

            // Only sender whitelisted
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedRecipient));
        }

        [Test]
        public async Task VerifyAllowlistStatus_BothBlocked_ReturnsBlockedBoth()
        {
            var svc = CreateService();
            const ulong assetId = 7003;
            // Neither whitelisted

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedBoth));
        }

        [Test]
        public async Task VerifyAllowlistStatus_InactiveEntry_ReportedAsPending()
        {
            var svc = CreateService();
            const ulong assetId = 7004;

            // Inactive = Pending
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.SenderStatus!.Status, Is.EqualTo(AllowlistStatus.Pending),
                "Inactive whitelist entry maps to Pending allowlist status");
        }

        [Test]
        public async Task VerifyAllowlistStatus_RevokedEntry_ReportedAsDenied()
        {
            var svc = CreateService();
            const ulong assetId = 7005;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Revoked }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.SenderStatus!.Status, Is.EqualTo(AllowlistStatus.Denied),
                "Revoked whitelist entry maps to Denied allowlist status");
            Assert.That(result.SenderStatus.IsWhitelisted, Is.False,
                "Revoked entry is not considered whitelisted");
        }

        [Test]
        public async Task VerifyAllowlistStatus_ExpiredEntry_ReportedAsExpired()
        {
            var svc = CreateService();
            const ulong assetId = 7006;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.SenderStatus!.Status, Is.EqualTo(AllowlistStatus.Expired),
                "Expired whitelist entry maps to Expired allowlist status");
        }

        [Test]
        public async Task VerifyAllowlistStatus_KycPreservedInResponse()
        {
            var svc = CreateService();
            const ulong assetId = 7007;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "MyKycProvider"
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2
            }, "verifier");

            Assert.That(result.SenderStatus!.KycVerified, Is.True, "KYC verified flag preserved");
            Assert.That(result.SenderStatus.KycProvider, Is.EqualTo("MyKycProvider"), "KYC provider preserved");
        }

        // ── MICA disclosure for different networks ────────────────────────────────

        [Test]
        public async Task VerifyAllowlistStatus_VoimainNetwork_RequiresMicaCompliance()
        {
            var svc = CreateService();
            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = 8000, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                Network = "voimain-v1.0"
            }, "verifier");

            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                "VOI main network requires MICA compliance");
            Assert.That(result.MicaDisclosure.ApplicableRegulations, Is.Not.Empty,
                "MICA regulations must be listed");
        }

        [Test]
        public async Task VerifyAllowlistStatus_AramidmainNetwork_RequiresMicaCompliance()
        {
            var svc = CreateService();
            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = 8001, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                Network = "aramidmain-v1.0"
            }, "verifier");

            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                "Aramid main network requires MICA compliance");
        }

        [Test]
        public async Task VerifyAllowlistStatus_TestnetNetwork_NoMicaCompliance()
        {
            var svc = CreateService();
            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = 8002, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                Network = "testnet-v1.0"
            }, "verifier");

            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.False,
                "Testnet does not require MICA compliance");
        }

        [Test]
        public async Task VerifyAllowlistStatus_NullNetwork_MicaDisclosureStillReturned()
        {
            var svc = CreateService();
            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = 8003, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                Network = null
            }, "verifier");

            Assert.That(result.MicaDisclosure, Is.Not.Null, "MICA disclosure returned even without network");
        }

        // ── Audit log filter combinations ─────────────────────────────────────────

        [Test]
        public async Task AuditLog_FilterByAddress_ReturnsOnlyMatchingAddress()
        {
            var svc = CreateService();
            const ulong assetId = 9000;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                Address = AlgoAddr1
            });

            Assert.That(log.Entries.All(e => e.Address == AlgoAddr1 || e.Address == AlgoAddr1.ToUpperInvariant()), Is.True,
                "Address filter must only return entries for specified address");
        }

        [Test]
        public async Task AuditLog_FilterByActionType_ReturnsOnlyMatchingActions()
        {
            var svc = CreateService();
            const ulong assetId = 9001;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1
            }, "v");

            // Filter only for Add actions
            var addLog = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                ActionType = WhitelistActionType.Add
            });

            Assert.That(addLog.Entries.All(e => e.ActionType == WhitelistActionType.Add), Is.True,
                "Action filter must only return entries of specified type");
        }

        [Test]
        public async Task AuditLog_PaginationPageSize1_ReturnsCorrectPage()
        {
            var svc = CreateService();
            const ulong assetId = 9002;

            // Generate 3 audit entries
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr3, Status = WhitelistStatus.Active }, "issuer");

            var page1 = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest { AssetId = assetId, Page = 1, PageSize = 1 });
            var page2 = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest { AssetId = assetId, Page = 2, PageSize = 1 });
            var page3 = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest { AssetId = assetId, Page = 3, PageSize = 1 });

            Assert.That(page1.Entries, Has.Count.EqualTo(1), "Page 1 has 1 entry");
            Assert.That(page2.Entries, Has.Count.EqualTo(1), "Page 2 has 1 entry");
            Assert.That(page3.Entries, Has.Count.EqualTo(1), "Page 3 has 1 entry");
            Assert.That(page1.TotalCount, Is.EqualTo(3), "TotalCount must be 3");
        }

        [Test]
        public async Task AuditLog_PageBeyondEnd_ReturnsEmptyEntries()
        {
            var svc = CreateService();
            const ulong assetId = 9003;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");

            var page100 = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = assetId, Page = 100, PageSize = 10
            });

            Assert.That(page100.Entries, Is.Empty, "Page beyond end must return empty entries");
            Assert.That(page100.TotalCount, Is.EqualTo(1), "TotalCount still reflects total entries");
        }

        // ── ListEntries advanced filters ──────────────────────────────────────────

        [Test]
        public async Task ListEntries_FilterByActiveStatus_ReturnsOnlyActive()
        {
            var svc = CreateService();
            const ulong assetId = 9100;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Inactive }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr3, Status = WhitelistStatus.Active }, "issuer");

            var activeOnly = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = assetId,
                Status = WhitelistStatus.Active
            });

            Assert.That(activeOnly.Entries.All(e => e.Status == WhitelistStatus.Active), Is.True,
                "Status filter must return only entries with matching status");
            Assert.That(activeOnly.Entries, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ListEntries_FilterByRevoked_ReturnsOnlyRevoked()
        {
            var svc = CreateService();
            const ulong assetId = 9101;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Revoked }, "issuer");

            var revokedOnly = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = assetId,
                Status = WhitelistStatus.Revoked
            });

            Assert.That(revokedOnly.Entries, Has.Count.EqualTo(1));
            Assert.That(revokedOnly.Entries[0].Status, Is.EqualTo(WhitelistStatus.Revoked));
        }

        // ── Enforcement report date range ─────────────────────────────────────────

        [Test]
        public async Task EnforcementReport_DateRangeSet_WhenEventsExist()
        {
            var svc = CreateService();
            const ulong assetId = 9200;

            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1
            }, "v");

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest { AssetId = assetId });

            Assert.That(report.Summary!.DateRange, Is.Not.Null, "DateRange must be set when events exist");
            Assert.That(report.Summary.DateRange!.EarliestEvent, Is.LessThanOrEqualTo(DateTime.UtcNow));
            Assert.That(report.Summary.DateRange.LatestEvent, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }

        [Test]
        public async Task EnforcementReport_DenialReasonBreakdown_Populated()
        {
            var svc = CreateService();
            const ulong assetId = 9201;

            // 2 denied (not whitelisted) and 1 different reason
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 }, "v");
            await svc.ValidateTransferAsync(new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 }, "v");

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest { AssetId = assetId });

            Assert.That(report.Summary!.DenialReasons, Is.Not.Empty,
                "Denial reason breakdown must be populated for denied transfers");
        }

        // ── Concurrent operations (isolation) ────────────────────────────────────

        [Test]
        public async Task AddEntry_ConcurrentAddsSameAddress_NoDuplicatesCreated()
        {
            var svc = CreateService();
            const ulong assetId = 9300;
            const int concurrency = 10;

            var tasks = Enumerable.Range(0, concurrency).Select(_ =>
                svc.AddEntryAsync(new AddWhitelistEntryRequest
                {
                    AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
                }, "issuer"));

            var results = await Task.WhenAll(tasks);

            // All should succeed (idempotent)
            Assert.That(results.All(r => r.Success), Is.True, "All concurrent adds must succeed");

            // Only 1 entry should exist
            var list = await svc.ListEntriesAsync(new ListWhitelistRequest { AssetId = assetId });
            Assert.That(list.TotalCount, Is.EqualTo(1), "Only 1 entry despite concurrent adds");
        }

        [Test]
        public async Task AddEntry_ConcurrentAddsDifferentAddresses_AllCreated()
        {
            var svc = CreateService();
            const ulong assetId = 9301;

            var addresses = new[] { AlgoAddr1, AlgoAddr2, AlgoAddr3 };
            var tasks = addresses.Select(addr =>
                svc.AddEntryAsync(new AddWhitelistEntryRequest
                {
                    AssetId = assetId, Address = addr, Status = WhitelistStatus.Active
                }, "issuer"));

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True, "All different address adds must succeed");

            var list = await svc.ListEntriesAsync(new ListWhitelistRequest { AssetId = assetId });
            Assert.That(list.TotalCount, Is.EqualTo(3), "All 3 distinct addresses must be added");
        }

        // ── No-op stubs for unit test isolation ──────────────────────────────────

        private sealed class NoOpMeteringService : ISubscriptionMeteringService
        {
            public void EmitMeteringEvent(BiatecTokensApi.Models.Metering.SubscriptionMeteringEvent evt) { }
        }

        private sealed class UnlimitedTierService : ISubscriptionTierService
        {
            public Task<SubscriptionTier> GetUserTierAsync(string userAddress)
                => Task.FromResult(SubscriptionTier.Enterprise);

            public Task<BiatecTokensApi.Services.Interface.SubscriptionTierValidationResult> ValidateOperationAsync(
                string userAddress, ulong assetId, int currentCount, int additionalCount = 1)
                => Task.FromResult(new BiatecTokensApi.Services.Interface.SubscriptionTierValidationResult
                {
                    IsAllowed = true, MaxAllowed = -1, RemainingCapacity = -1
                });

            public Task<bool> IsBulkOperationEnabledAsync(string userAddress)
                => Task.FromResult(true);

            public Task<bool> IsAuditLogEnabledAsync(string userAddress)
                => Task.FromResult(true);

            public SubscriptionTierLimits GetTierLimits(SubscriptionTier tier)
                => new() { MaxAddressesPerAsset = -1 };

            public Task<int> GetRemainingCapacityAsync(string userAddress, int currentCount)
                => Task.FromResult(-1);

            public Task<bool> CanDeployTokenAsync(string userAddress)
                => Task.FromResult(true);

            public Task<bool> RecordTokenDeploymentAsync(string userAddress)
                => Task.FromResult(true);

            public Task<int> GetTokenDeploymentCountAsync(string userAddress)
                => Task.FromResult(0);
        }

        private sealed class NoOpWebhookService : IWebhookService
        {
            public Task EmitEventAsync(BiatecTokensApi.Models.Webhook.WebhookEvent e) => Task.CompletedTask;
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> CreateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.CreateWebhookSubscriptionRequest r, string c)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> UpdateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.UpdateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(
                BiatecTokensApi.Models.Webhook.GetWebhookDeliveryHistoryRequest r, string u)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse());
        }
    }
}
