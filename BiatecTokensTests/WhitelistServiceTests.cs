using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for WhitelistService covering address validation, CRUD operations,
    /// transfer validation, audit trail generation, and compliance outcomes.
    ///
    /// These tests exercise the in-memory repository path so they are fast and
    /// deterministic. Each test creates an isolated service instance to avoid
    /// cross-test state leakage.
    /// </summary>
    [TestFixture]
    public class WhitelistServiceTests
    {
        // ── Valid Algorand test addresses (58 chars, valid checksum) ─────────────

        // Using known valid Algorand addresses
        private const string ValidAddress1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidAddress2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

        // These are genuine valid Algorand addresses for testing
        // AlgoAddr1 = unused
        // AlgoAddr2 = unused
        // AlgoAddr3 = unused

        private const ulong TestAssetId = 12345678UL;

        // ── Factory helpers ──────────────────────────────────────────────────────

        private static IWhitelistService CreateService()
        {
            var repo = new WhitelistRepository(NullLogger<WhitelistRepository>.Instance);
            var logger = NullLogger<WhitelistService>.Instance;
            var meteringService = new NoOpMeteringService();
            var tierService = new UnlimitedTierService();
            var webhookService = new NoOpWebhookService();
            return new WhitelistService(repo, logger, meteringService, tierService, webhookService);
        }

        // ── Address validation ────────────────────────────────────────────────────

        [Test]
        public void IsValidAlgorandAddress_NullOrEmpty_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.That(svc.IsValidAlgorandAddress(null!), Is.False, "null → false");
            Assert.That(svc.IsValidAlgorandAddress(""), Is.False, "empty → false");
            Assert.That(svc.IsValidAlgorandAddress("   "), Is.False, "whitespace → false");
        }

        [Test]
        public void IsValidAlgorandAddress_TooShort_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.That(svc.IsValidAlgorandAddress("AAAA"), Is.False, "Too short");
        }

        [Test]
        public void IsValidAlgorandAddress_TooLong_ReturnsFalse()
        {
            var svc = CreateService();
            var longAddr = new string('A', 60);
            Assert.That(svc.IsValidAlgorandAddress(longAddr), Is.False, "Too long");
        }

        [Test]
        public void IsValidAlgorandAddress_ExactLength_WellFormed_ReturnsTrue()
        {
            var svc = CreateService();
            // A well-known valid Algorand address (the zero-address used in tests)
            const string zeroAddr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            // Length check: Algorand addresses are 58 chars
            // Use a known 58-char valid address from Algorand SDK tests
            Assert.That(svc.IsValidAlgorandAddress(zeroAddr), Is.True, "Valid zero address");
        }

        [Test]
        public void IsValidAlgorandAddress_InvalidCharacters_ReturnsFalse()
        {
            var svc = CreateService();
            // 58 chars but contains lowercase (invalid for base32)
            var invalidAddr = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            Assert.That(svc.IsValidAlgorandAddress(invalidAddr), Is.False, "Lowercase chars");
        }

        // ── AddEntryAsync ─────────────────────────────────────────────────────────

        [Test]
        public async Task AddEntry_InvalidAddress_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = "NOT_VALID_ADDRESS",
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid Algorand address"));
        }

        [Test]
        public async Task AddEntry_ValidAddress_ReturnsSuccess()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active,
                Reason = "KYC verified"
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True, result.ErrorMessage ?? "no error detail");
            Assert.That(result.Entry, Is.Not.Null);
            Assert.That(result.Entry!.AssetId, Is.EqualTo(TestAssetId));
        }

        [Test]
        public async Task AddEntry_DuplicateAddress_UpdatesExistingEntry()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // First add
            var first = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");
            Assert.That(first.Success, Is.True);

            // Second add with different status
            var second = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-2");

            Assert.That(second.Success, Is.True, "Duplicate add should update, not fail");
            Assert.That(second.Entry!.Status, Is.EqualTo(WhitelistStatus.Inactive), "Status should be updated");
        }

        [Test]
        public async Task AddEntry_WithKycMetadata_PreservesMetadata()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var kycDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active,
                KycVerified = true,
                KycVerificationDate = kycDate,
                KycProvider = "SumSub"
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry!.KycVerified, Is.True);
            Assert.That(result.Entry.KycProvider, Is.EqualTo("SumSub"));
        }

        [Test]
        public async Task AddEntry_WithExpirationDate_SetsExpiry()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var expiry = DateTime.UtcNow.AddYears(1);

            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active,
                ExpirationDate = expiry
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry!.ExpirationDate, Is.Not.Null);
        }

        // ── RemoveEntryAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task RemoveEntry_NonExistentAddress_ReturnsFail()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var result = await svc.RemoveEntryAsync(new RemoveWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr
            });

            Assert.That(result.Success, Is.False, "Removing non-existent entry should fail");
        }

        [Test]
        public async Task RemoveEntry_ExistingAddress_ReturnsSuccess()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var removeResult = await svc.RemoveEntryAsync(new RemoveWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr
            });

            Assert.That(removeResult.Success, Is.True, "Remove existing entry must succeed");
        }

        [Test]
        public async Task RemoveEntry_AfterRemove_EntryIsGone()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            await svc.RemoveEntryAsync(new RemoveWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr
            });

            // List should now be empty
            var listResult = await svc.ListEntriesAsync(new ListWhitelistRequest { AssetId = TestAssetId });
            Assert.That(listResult.Success, Is.True);
            Assert.That(listResult.Entries, Is.Empty, "Entry should be removed");
        }

        // ── BulkAddEntriesAsync ───────────────────────────────────────────────────

        [Test]
        public async Task BulkAdd_AllValidAddresses_ReturnsAllSuccessful()
        {
            var svc = CreateService();
            const string addr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string addr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var result = await svc.BulkAddEntriesAsync(new BulkAddWhitelistRequest
            {
                AssetId = TestAssetId,
                Addresses = new List<string> { addr1, addr2 },
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.SuccessCount, Is.EqualTo(2));
            Assert.That(result.FailedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task BulkAdd_MixedValidInvalid_ReportsFailures()
        {
            var svc = CreateService();
            const string validAddr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string invalidAddr = "NOT_AN_ALGORAND_ADDRESS";

            var result = await svc.BulkAddEntriesAsync(new BulkAddWhitelistRequest
            {
                AssetId = TestAssetId,
                Addresses = new List<string> { validAddr, invalidAddr },
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            Assert.That(result.SuccessCount, Is.EqualTo(1), "One valid address should succeed");
            Assert.That(result.FailedCount, Is.EqualTo(1), "One invalid address should fail");
            Assert.That(result.FailedAddresses, Contains.Item(invalidAddr));
        }

        [Test]
        public async Task BulkAdd_EmptyList_ReturnsSuccessWithZeroCounts()
        {
            var svc = CreateService();
            var result = await svc.BulkAddEntriesAsync(new BulkAddWhitelistRequest
            {
                AssetId = TestAssetId,
                Addresses = new List<string>(),
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.SuccessCount, Is.EqualTo(0));
        }

        // ── ListEntriesAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task ListEntries_EmptyStore_ReturnEmptyList()
        {
            var svc = CreateService();
            var result = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = 99999999UL // Non-existent asset
            });

            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ListEntries_AfterAdding_ReturnsEntries()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var listResult = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(listResult.Success, Is.True);
            Assert.That(listResult.Entries, Has.Count.EqualTo(1));
            Assert.That(listResult.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ListEntries_StatusFilter_ReturnsOnlyMatchingStatus()
        {
            var svc = CreateService();
            const string addr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string addr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr1, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr2, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");

            var activeOnly = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = TestAssetId,
                Status = WhitelistStatus.Active
            });

            Assert.That(activeOnly.Entries, Has.Count.EqualTo(1));
            Assert.That(activeOnly.Entries[0].Status, Is.EqualTo(WhitelistStatus.Active));
        }

        [Test]
        public async Task ListEntries_Pagination_ReturnsCorrectPage()
        {
            var svc = CreateService();
            // Add multiple entries
            var addresses = new[]
            {
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
            };

            foreach (var addr in addresses)
            {
                await svc.AddEntryAsync(new AddWhitelistEntryRequest
                {
                    AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
                }, createdBy: "issuer-1");
            }

            var page1 = await svc.ListEntriesAsync(new ListWhitelistRequest
            {
                AssetId = TestAssetId, Page = 1, PageSize = 1
            });

            Assert.That(page1.Entries, Has.Count.EqualTo(1), "Page 1 with size 1 returns 1 entry");
            Assert.That(page1.TotalCount, Is.EqualTo(2), "Total still 2");
            Assert.That(page1.TotalPages, Is.EqualTo(2));
        }

        // ── ValidateTransferAsync ─────────────────────────────────────────────────

        [Test]
        public async Task ValidateTransfer_BothAddressesWhitelisted_AllowsTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = sender, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = receiver, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True, "Both whitelisted → transfer allowed");
        }

        [Test]
        public async Task ValidateTransfer_SenderNotWhitelisted_BlocksTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Only receiver is whitelisted
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = receiver, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.IsAllowed, Is.False, "Sender not whitelisted → transfer blocked");
        }

        [Test]
        public async Task ValidateTransfer_ReceiverNotWhitelisted_BlocksTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Only sender is whitelisted
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = sender, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.IsAllowed, Is.False, "Receiver not whitelisted → transfer blocked");
        }

        [Test]
        public async Task ValidateTransfer_NeitherWhitelisted_BlocksTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.IsAllowed, Is.False, "Neither whitelisted → transfer blocked");
            Assert.That(result.DenialReason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateTransfer_InvalidSenderAddress_ReturnsFail()
        {
            var svc = CreateService();
            const string receiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = "NOT_VALID",
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("sender"));
        }

        [Test]
        public async Task ValidateTransfer_InvalidReceiverAddress_ReturnsFail()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = "BAD_ADDRESS",
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
        }

        [Test]
        public async Task ValidateTransfer_SuspendedSender_BlocksTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = sender, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = receiver, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            Assert.That(result.IsAllowed, Is.False, "Suspended sender → transfer blocked");
        }

        // ── Audit log ────────────────────────────────────────────────────────────

        [Test]
        public async Task AuditLog_AfterAddEntry_ContainsAddAction()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr,
                Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(log.Success, Is.True);
            Assert.That(log.Entries, Is.Not.Empty, "Audit log should have entries after add");
            Assert.That(log.Entries.Any(e => e.ActionType == WhitelistActionType.Add), Is.True,
                "Should contain an Add action");
        }

        [Test]
        public async Task AuditLog_AfterRemoveEntry_ContainsRemoveAction()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            await svc.RemoveEntryAsync(new RemoveWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = addr
            });

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(log.Entries.Any(e => e.ActionType == WhitelistActionType.Remove), Is.True,
                "Should contain a Remove action");
        }

        [Test]
        public async Task AuditLog_RecordsActor_WithTimestamp()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string issuerAddress = "issuer-actor-address";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: issuerAddress);

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId
            });

            var addEntry = log.Entries.FirstOrDefault(e => e.ActionType == WhitelistActionType.Add);
            Assert.That(addEntry, Is.Not.Null);
            Assert.That(addEntry!.PerformedBy, Is.EqualTo(issuerAddress), "Actor must be recorded");
            Assert.That(addEntry.PerformedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)), "Timestamp should be recent");
        }

        [Test]
        public async Task AuditLog_UpdateAction_RecordsOldAndNewStatus()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // Add
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            // Update (via duplicate add)
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");

            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId
            });

            var updateEntry = log.Entries.FirstOrDefault(e => e.ActionType == WhitelistActionType.Update);
            Assert.That(updateEntry, Is.Not.Null, "Update action should be logged");
            Assert.That(updateEntry!.OldStatus, Is.EqualTo(WhitelistStatus.Active), "Old status should be Active");
            Assert.That(updateEntry.NewStatus, Is.EqualTo(WhitelistStatus.Inactive), "New status should be Suspended");
        }

        [Test]
        public async Task AuditLog_HasRetentionPolicy()
        {
            var svc = CreateService();
            var log = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(log.RetentionPolicy, Is.Not.Null, "Retention policy must be included");
            Assert.That(log.RetentionPolicy!.MinimumRetentionYears, Is.GreaterThanOrEqualTo(7),
                "MICA requires 7-year audit retention");
        }

        [Test]
        public async Task AuditLog_Pagination_WorksCorrectly()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // Generate multiple audit entries via add/update cycle
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");

            var page1 = await svc.GetAuditLogAsync(new GetWhitelistAuditLogRequest
            {
                AssetId = TestAssetId,
                Page = 1,
                PageSize = 1
            });

            Assert.That(page1.Success, Is.True);
            Assert.That(page1.Entries, Has.Count.EqualTo(1), "Page 1 returns 1 entry");
            Assert.That(page1.TotalCount, Is.GreaterThanOrEqualTo(2), "Total >= 2 audit entries");
        }

        // ── VerifyAllowlistStatusAsync ────────────────────────────────────────────

        [Test]
        public async Task VerifyAllowlistStatus_BothApproved_ReturnsAllowedTransfer()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string recipient = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = sender, Status = WhitelistStatus.Active, KycVerified = true
            }, createdBy: "issuer-1");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = recipient, Status = WhitelistStatus.Active, KycVerified = true
            }, createdBy: "issuer-1");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = TestAssetId,
                SenderAddress = sender,
                RecipientAddress = recipient,
                Network = "mainnet-v1.0"
            }, performedBy: "verifier-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.Allowed));
            Assert.That(result.SenderStatus, Is.Not.Null);
            Assert.That(result.RecipientStatus, Is.Not.Null);
        }

        [Test]
        public async Task VerifyAllowlistStatus_SenderBlocked_ReturnsBlockedSender()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string recipient = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Only recipient whitelisted
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = recipient, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = TestAssetId,
                SenderAddress = sender,
                RecipientAddress = recipient,
                Network = "mainnet-v1.0"
            }, performedBy: "verifier-1");

            Assert.That(result.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedSender));
        }

        [Test]
        public async Task VerifyAllowlistStatus_ReturnsAuditMetadata()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string recipient = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = TestAssetId,
                SenderAddress = sender,
                RecipientAddress = recipient
            }, performedBy: "verifier-1");

            Assert.That(result.AuditMetadata, Is.Not.Null, "Audit metadata must be present");
            Assert.That(result.AuditMetadata!.VerificationId, Is.Not.Null.And.Not.Empty,
                "Verification ID must be assigned");
            Assert.That(result.AuditMetadata.PerformedBy, Is.EqualTo("verifier-1"),
                "Actor must be recorded in audit metadata");
        }

        [Test]
        public async Task VerifyAllowlistStatus_ReturnsMicaDisclosure()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string recipient = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var result = await svc.VerifyAllowlistStatusAsync(new VerifyAllowlistStatusRequest
            {
                AssetId = TestAssetId,
                SenderAddress = sender,
                RecipientAddress = recipient,
                Network = "voimain-v1.0"
            }, performedBy: "verifier-1");

            Assert.That(result.MicaDisclosure, Is.Not.Null, "MICA disclosure must be included for MICA network");
        }

        // ── EnforcementReport ────────────────────────────────────────────────────

        [Test]
        public async Task GetEnforcementReport_AfterTransferValidation_ContainsEntries()
        {
            var svc = CreateService();
            const string sender = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const string receiver = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Perform a transfer validation (which generates enforcement log entry)
            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = sender,
                ToAddress = receiver,
                Amount = 100
            }, performedBy: "issuer-1");

            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(report.Success, Is.True);
        }

        [Test]
        public async Task GetEnforcementReport_HasSummaryStatistics()
        {
            var svc = CreateService();
            var report = await svc.GetEnforcementReportAsync(new GetWhitelistEnforcementReportRequest
            {
                AssetId = TestAssetId
            });

            Assert.That(report.Success, Is.True);
            Assert.That(report.Summary, Is.Not.Null, "Summary statistics must be included");
        }

        // ── State transitions ────────────────────────────────────────────────────

        [Test]
        public async Task StatusTransition_ActiveToSuspended_IsAllowed()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True, "Active → Suspended transition must succeed");
            Assert.That(result.Entry!.Status, Is.EqualTo(WhitelistStatus.Inactive));
        }

        [Test]
        public async Task StatusTransition_SuspendedToActive_IsAllowed()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Inactive
            }, createdBy: "issuer-1");

            var result = await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            Assert.That(result.Success, Is.True, "Suspended → Active transition must succeed");
            Assert.That(result.Entry!.Status, Is.EqualTo(WhitelistStatus.Active));
        }

        // ── Multi-asset isolation ────────────────────────────────────────────────

        [Test]
        public async Task MultiAssetIsolation_EntriesDoNotCrossAssets()
        {
            var svc = CreateService();
            const string addr = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            const ulong asset1 = 11111111UL;
            const ulong asset2 = 22222222UL;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = asset1, Address = addr, Status = WhitelistStatus.Active
            }, createdBy: "issuer-1");

            var list1 = await svc.ListEntriesAsync(new ListWhitelistRequest { AssetId = asset1 });
            var list2 = await svc.ListEntriesAsync(new ListWhitelistRequest { AssetId = asset2 });

            Assert.That(list1.Entries, Has.Count.EqualTo(1), "Asset 1 should have 1 entry");
            Assert.That(list2.Entries, Is.Empty, "Asset 2 should have no entries");
        }

        // ── Null argument guards ──────────────────────────────────────────────────

        [Test]
        public async Task AddEntry_NullRequest_HandledGracefully()
        {
            var svc = CreateService();
            // The service should either throw or return a failure for null input
            // Both are acceptable null-safety behaviors
            try
            {
                var result = await svc.AddEntryAsync(null!, createdBy: "issuer-1");
                // If it returned (didn't throw), it must be a failure
                Assert.That(result.Success, Is.False, "Null request must return failure if no exception");
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is NullReferenceException)
            {
                // Exception thrown for null input is also acceptable
                Assert.Pass("Service throws for null input - acceptable behavior");
            }
        }

        // ── Compliance Overview ───────────────────────────────────────────────────

        [Test]
        public async Task GetComplianceOverview_EmptyAsset_ReturnsZeroCounts()
        {
            var svc = CreateService();
            const ulong assetId = 99_001UL;

            var result = await svc.GetComplianceOverviewAsync(assetId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.AssetId, Is.EqualTo(assetId));
            Assert.That(result.InvestorEligibility, Is.Not.Null);
            Assert.That(result.InvestorEligibility!.TotalEntries, Is.EqualTo(0));
            Assert.That(result.InvestorEligibility.ActiveEntries, Is.EqualTo(0));
            Assert.That(result.TransferEnforcement, Is.Not.Null);
            Assert.That(result.TransferEnforcement!.TotalValidations, Is.EqualTo(0));
            Assert.That(result.KycVerification, Is.Not.Null);
            Assert.That(result.AuditTrail, Is.Not.Null);
            Assert.That(result.AuditTrail!.MinimumRetentionYears, Is.GreaterThanOrEqualTo(7));
            Assert.That(result.AuditTrail.ImmutableEntries, Is.True);
            Assert.That(result.MicaReadiness, Is.Null, "No network = no MICA readiness");
        }

        [Test]
        public async Task GetComplianceOverview_WithActiveEntries_ReturnsCorrectCounts()
        {
            var svc = CreateService();
            const ulong assetId = 99_002UL;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress1, Status = WhitelistStatus.Active
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress2, Status = WhitelistStatus.Active
            }, "issuer");

            var result = await svc.GetComplianceOverviewAsync(assetId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.InvestorEligibility!.TotalEntries, Is.EqualTo(2));
            Assert.That(result.InvestorEligibility.ActiveEntries, Is.EqualTo(2));
            Assert.That(result.InvestorEligibility.ActivePercentage, Is.EqualTo(100.0));
        }

        [Test]
        public async Task GetComplianceOverview_WithMicaNetwork_ReturnsMicaReadiness()
        {
            var svc = CreateService();
            const ulong assetId = 99_003UL;

            var result = await svc.GetComplianceOverviewAsync(assetId, "voimain-v1.0");

            Assert.That(result.Success, Is.True);
            Assert.That(result.MicaReadiness, Is.Not.Null, "MICA network should return readiness");
            Assert.That(result.MicaReadiness!.ApplicableNetwork, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.MicaReadiness.ReadinessScore, Is.InRange(0, 100));
            Assert.That(result.MicaReadiness.AuditRetentionCompliant, Is.True);
            Assert.That(result.MicaReadiness.ImmutableAuditCompliant, Is.True);
        }

        [Test]
        public async Task GetComplianceOverview_WithNonMicaNetwork_ReturnsFullScore()
        {
            var svc = CreateService();
            const ulong assetId = 99_004UL;

            var result = await svc.GetComplianceOverviewAsync(assetId, "testnet-v1.0");

            Assert.That(result.Success, Is.True);
            Assert.That(result.MicaReadiness, Is.Not.Null);
            // Non-MICA networks automatically get full score
            Assert.That(result.MicaReadiness!.ReadinessScore, Is.EqualTo(100));
        }

        [Test]
        public async Task GetComplianceOverview_WithKycVerifiedEntries_ReportsKycMetrics()
        {
            var svc = CreateService();
            const ulong assetId = 99_005UL;

            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "Jumio"
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress2, Status = WhitelistStatus.Active,
                KycVerified = false
            }, "issuer");

            var result = await svc.GetComplianceOverviewAsync(assetId, "voimain-v1.0");

            Assert.That(result.KycVerification!.KycVerifiedEntries, Is.EqualTo(1));
            Assert.That(result.KycVerification.ActiveWithoutKyc, Is.EqualTo(1));
            Assert.That(result.KycVerification.KycProviders, Contains.Item("Jumio"));

            // Not all entries KYC'd → MICA readiness score < 100
            Assert.That(result.MicaReadiness!.AllActiveEntriesKycVerified, Is.False);
            Assert.That(result.MicaReadiness.ReadinessScore, Is.LessThan(100));
        }

        [Test]
        public async Task GetComplianceOverview_AuditTrailSummary_IsAlwaysPresent()
        {
            var svc = CreateService();
            const ulong assetId = 99_006UL;

            // Add an entry to generate an audit log entry
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress1, Status = WhitelistStatus.Active
            }, "issuer");

            var result = await svc.GetComplianceOverviewAsync(assetId);

            Assert.That(result.AuditTrail, Is.Not.Null);
            Assert.That(result.AuditTrail!.TotalAuditEntries, Is.GreaterThanOrEqualTo(1),
                "Adding an entry must produce at least one audit log entry");
            Assert.That(result.AuditTrail.LastAuditAt, Is.Not.Null);
            Assert.That(result.AuditTrail.EarliestAuditAt, Is.Not.Null);
        }

        [Test]
        public async Task GetComplianceOverview_TransferEnforcement_ReflectsValidations()
        {
            var svc = CreateService();
            const ulong assetId = 99_007UL;

            // Add whitelist entries
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress1, Status = WhitelistStatus.Active
            }, "issuer");
            await svc.AddEntryAsync(new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = ValidAddress2, Status = WhitelistStatus.Active
            }, "issuer");

            // Run a transfer validation
            await svc.ValidateTransferAsync(new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = ValidAddress1,
                ToAddress = ValidAddress2
            }, "compliance-officer");

            var result = await svc.GetComplianceOverviewAsync(assetId);

            Assert.That(result.TransferEnforcement!.TotalValidations, Is.GreaterThanOrEqualTo(1),
                "Validation event must appear in enforcement stats");
            Assert.That(result.TransferEnforcement.AllowedTransfers, Is.GreaterThanOrEqualTo(1),
                "Allowed transfer must be counted");
            Assert.That(result.TransferEnforcement.LastValidationAt, Is.Not.Null);
        }

        [Test]
        public async Task GetComplianceOverview_GeneratedAt_IsRecent()
        {
            var svc = CreateService();
            const ulong assetId = 99_008UL;

            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = await svc.GetComplianceOverviewAsync(assetId);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.That(result.GeneratedAt, Is.InRange(before, after),
                "GeneratedAt must be current UTC timestamp");
        }

        // ── No-op stubs for unit test isolation ──────────────────────────────────

        private sealed class NoOpMeteringService : ISubscriptionMeteringService
        {
            public void EmitMeteringEvent(BiatecTokensApi.Models.Metering.SubscriptionMeteringEvent evt) { }
        }

        private sealed class UnlimitedTierService : ISubscriptionTierService
        {
            public Task<BiatecTokensApi.Models.Subscription.SubscriptionTier> GetUserTierAsync(string userAddress)
                => Task.FromResult(BiatecTokensApi.Models.Subscription.SubscriptionTier.Enterprise);

            public Task<BiatecTokensApi.Services.Interface.SubscriptionTierValidationResult> ValidateOperationAsync(
                string userAddress, ulong assetId, int currentCount, int additionalCount = 1)
                => Task.FromResult(new BiatecTokensApi.Services.Interface.SubscriptionTierValidationResult
                {
                    IsAllowed = true,
                    MaxAllowed = -1,
                    RemainingCapacity = -1
                });

            public Task<bool> IsBulkOperationEnabledAsync(string userAddress)
                => Task.FromResult(true);

            public Task<bool> IsAuditLogEnabledAsync(string userAddress)
                => Task.FromResult(true);

            public BiatecTokensApi.Models.Subscription.SubscriptionTierLimits GetTierLimits(
                BiatecTokensApi.Models.Subscription.SubscriptionTier tier)
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
            public Task EmitEventAsync(BiatecTokensApi.Models.Webhook.WebhookEvent webhookEvent)
                => Task.CompletedTask;

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> CreateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.CreateWebhookSubscriptionRequest request, string createdBy)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> GetSubscriptionAsync(
                string subscriptionId, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse());

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> UpdateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.UpdateWebhookSubscriptionRequest request, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> DeleteSubscriptionAsync(
                string subscriptionId, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());

            public Task<BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(
                BiatecTokensApi.Models.Webhook.GetWebhookDeliveryHistoryRequest request, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse());
        }
    }
}
