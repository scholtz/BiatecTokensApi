using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Compliance Decision Repository
    /// </summary>
    [TestFixture]
    public class ComplianceDecisionRepositoryTests
    {
        private ComplianceDecisionRepository _repository = null!;
        private Mock<ILogger<ComplianceDecisionRepository>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ComplianceDecisionRepository>>();
            _repository = new ComplianceDecisionRepository(_loggerMock.Object);
        }

        [Test]
        public async Task CreateDecisionAsync_WithValidDecision_Succeeds()
        {
            // Arrange
            var decision = new ComplianceDecision
            {
                Id = "dec-001",
                OrganizationId = "org-123",
                Step = OnboardingStep.KycKybVerification,
                Outcome = DecisionOutcome.Approved
            };

            // Act
            await _repository.CreateDecisionAsync(decision);

            // Assert
            var retrieved = await _repository.GetDecisionByIdAsync("dec-001");
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Id, Is.EqualTo("dec-001"));
        }

        [Test]
        public void CreateDecisionAsync_WithDuplicateId_ThrowsException()
        {
            // Arrange
            var decision1 = new ComplianceDecision { Id = "dup-001", OrganizationId = "org-1", Step = OnboardingStep.KycKybVerification };
            var decision2 = new ComplianceDecision { Id = "dup-001", OrganizationId = "org-2", Step = OnboardingStep.AmlScreening };

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _repository.CreateDecisionAsync(decision1));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _repository.CreateDecisionAsync(decision2));
        }

        [Test]
        public async Task GetDecisionByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var result = await _repository.GetDecisionByIdAsync("nonexistent");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task QueryDecisionsAsync_WithOrganizationFilter_ReturnsMatchingDecisions()
        {
            // Arrange
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-org1-1",
                OrganizationId = "org-filter",
                Step = OnboardingStep.KycKybVerification
            });
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-org1-2",
                OrganizationId = "org-filter",
                Step = OnboardingStep.AmlScreening
            });
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-org2-1",
                OrganizationId = "org-other",
                Step = OnboardingStep.KycKybVerification
            });

            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-filter"
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(totalCount, Is.EqualTo(2));
            Assert.That(decisions.All(d => d.OrganizationId == "org-filter"), Is.True);
        }

        [Test]
        public async Task QueryDecisionsAsync_WithStepFilter_ReturnsMatchingDecisions()
        {
            // Arrange
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-step1",
                OrganizationId = "org-step",
                Step = OnboardingStep.KycKybVerification
            });
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-step2",
                OrganizationId = "org-step",
                Step = OnboardingStep.AmlScreening
            });

            var request = new QueryComplianceDecisionsRequest
            {
                Step = OnboardingStep.KycKybVerification
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(decisions.All(d => d.Step == OnboardingStep.KycKybVerification), Is.True);
        }

        [Test]
        public async Task QueryDecisionsAsync_WithDateRangeFilter_ReturnsMatchingDecisions()
        {
            // Arrange
            var now = DateTime.UtcNow;
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-old",
                OrganizationId = "org-date",
                Step = OnboardingStep.KycKybVerification,
                DecisionTimestamp = now.AddDays(-10)
            });
            await _repository.CreateDecisionAsync(new ComplianceDecision
            {
                Id = "dec-recent",
                OrganizationId = "org-date",
                Step = OnboardingStep.KycKybVerification,
                DecisionTimestamp = now.AddDays(-2)
            });

            var request = new QueryComplianceDecisionsRequest
            {
                FromDate = now.AddDays(-5),
                ToDate = now
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(decisions.Count, Is.EqualTo(1));
            Assert.That(decisions[0].Id, Is.EqualTo("dec-recent"));
        }

        [Test]
        public async Task QueryDecisionsAsync_WithPagination_ReturnsCorrectPage()
        {
            // Arrange
            for (int i = 0; i < 25; i++)
            {
                await _repository.CreateDecisionAsync(new ComplianceDecision
                {
                    Id = $"dec-page-{i}",
                    OrganizationId = "org-page",
                    Step = OnboardingStep.KycKybVerification,
                    DecisionTimestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-page",
                Page = 2,
                PageSize = 10
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(totalCount, Is.EqualTo(25));
            Assert.That(decisions.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task QueryDecisionsAsync_ExcludesSupersededByDefault()
        {
            // Arrange
            var activeDecision = new ComplianceDecision
            {
                Id = "dec-active",
                OrganizationId = "org-supersede",
                Step = OnboardingStep.KycKybVerification,
                IsSuperseded = false
            };
            var supersededDecision = new ComplianceDecision
            {
                Id = "dec-superseded",
                OrganizationId = "org-supersede",
                Step = OnboardingStep.KycKybVerification,
                IsSuperseded = true
            };

            await _repository.CreateDecisionAsync(activeDecision);
            await _repository.CreateDecisionAsync(supersededDecision);

            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-supersede",
                IncludeSuperseded = false
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(totalCount, Is.EqualTo(1));
            Assert.That(decisions[0].Id, Is.EqualTo("dec-active"));
        }

        [Test]
        public async Task QueryDecisionsAsync_ExcludesExpiredByDefault()
        {
            // Arrange
            var validDecision = new ComplianceDecision
            {
                Id = "dec-valid",
                OrganizationId = "org-expire",
                Step = OnboardingStep.KycKybVerification,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            var expiredDecision = new ComplianceDecision
            {
                Id = "dec-expired",
                OrganizationId = "org-expire",
                Step = OnboardingStep.KycKybVerification,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };

            await _repository.CreateDecisionAsync(validDecision);
            await _repository.CreateDecisionAsync(expiredDecision);

            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-expire",
                IncludeExpired = false
            };

            // Act
            var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(totalCount, Is.EqualTo(1));
            Assert.That(decisions[0].Id, Is.EqualTo("dec-valid"));
        }

        [Test]
        public async Task GetActiveDecisionAsync_ReturnsLatestNonSupersededNonExpired()
        {
            // Arrange
            var supersededDecision = new ComplianceDecision
            {
                Id = "dec-superseded",
                OrganizationId = "org-active",
                Step = OnboardingStep.KycKybVerification,
                IsSuperseded = true,
                DecisionTimestamp = DateTime.UtcNow.AddDays(-2)
            };
            var expiredDecision = new ComplianceDecision
            {
                Id = "dec-expired",
                OrganizationId = "org-active",
                Step = OnboardingStep.KycKybVerification,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                DecisionTimestamp = DateTime.UtcNow.AddDays(-1)
            };
            var activeDecision = new ComplianceDecision
            {
                Id = "dec-active",
                OrganizationId = "org-active",
                Step = OnboardingStep.KycKybVerification,
                IsSuperseded = false,
                DecisionTimestamp = DateTime.UtcNow
            };

            await _repository.CreateDecisionAsync(supersededDecision);
            await _repository.CreateDecisionAsync(expiredDecision);
            await _repository.CreateDecisionAsync(activeDecision);

            // Act
            var result = await _repository.GetActiveDecisionAsync("org-active", OnboardingStep.KycKybVerification);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("dec-active"));
        }

        [Test]
        public async Task SupersedeDecisionAsync_UpdatesDecisionStatus()
        {
            // Arrange
            var decision = new ComplianceDecision
            {
                Id = "dec-to-supersede",
                OrganizationId = "org-sup",
                Step = OnboardingStep.KycKybVerification
            };
            await _repository.CreateDecisionAsync(decision);

            // Act
            var result = await _repository.SupersedeDecisionAsync("dec-to-supersede", "dec-new");

            // Assert
            Assert.That(result, Is.True);
            var updated = await _repository.GetDecisionByIdAsync("dec-to-supersede");
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated.IsSuperseded, Is.True);
            Assert.That(updated.SupersededById, Is.EqualTo("dec-new"));
            Assert.That(updated.SupersededAt, Is.Not.Null);
        }

        [Test]
        public async Task SupersedeDecisionAsync_WithNonExistentId_ReturnsFalse()
        {
            // Act
            var result = await _repository.SupersedeDecisionAsync("nonexistent", "dec-new");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task GetDecisionsRequiringReviewAsync_ReturnsOnlyDueForReview()
        {
            // Arrange
            var dueDecision = new ComplianceDecision
            {
                Id = "dec-due",
                OrganizationId = "org-review",
                Step = OnboardingStep.KycKybVerification,
                RequiresReview = true,
                NextReviewDate = DateTime.UtcNow.AddDays(-1)
            };
            var futureDecision = new ComplianceDecision
            {
                Id = "dec-future",
                OrganizationId = "org-review",
                Step = OnboardingStep.AmlScreening,
                RequiresReview = true,
                NextReviewDate = DateTime.UtcNow.AddDays(30)
            };
            var noReviewDecision = new ComplianceDecision
            {
                Id = "dec-no-review",
                OrganizationId = "org-review",
                Step = OnboardingStep.TermsAcceptance,
                RequiresReview = false
            };

            await _repository.CreateDecisionAsync(dueDecision);
            await _repository.CreateDecisionAsync(futureDecision);
            await _repository.CreateDecisionAsync(noReviewDecision);

            // Act
            var result = await _repository.GetDecisionsRequiringReviewAsync();

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("dec-due"));
        }

        [Test]
        public async Task GetExpiredDecisionsAsync_ReturnsOnlyExpired()
        {
            // Arrange
            var expiredDecision = new ComplianceDecision
            {
                Id = "dec-expired",
                OrganizationId = "org-exp",
                Step = OnboardingStep.KycKybVerification,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };
            var validDecision = new ComplianceDecision
            {
                Id = "dec-valid",
                OrganizationId = "org-exp",
                Step = OnboardingStep.AmlScreening,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            var noExpiryDecision = new ComplianceDecision
            {
                Id = "dec-no-expiry",
                OrganizationId = "org-exp",
                Step = OnboardingStep.TermsAcceptance
            };

            await _repository.CreateDecisionAsync(expiredDecision);
            await _repository.CreateDecisionAsync(validDecision);
            await _repository.CreateDecisionAsync(noExpiryDecision);

            // Act
            var result = await _repository.GetExpiredDecisionsAsync();

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("dec-expired"));
        }

        [Test]
        public async Task FindDuplicateDecisionAsync_WithMatchingParams_ReturnsDecision()
        {
            // Arrange
            var decision = new ComplianceDecision
            {
                Id = "dec-dup",
                OrganizationId = "org-dup",
                Step = OnboardingStep.KycKybVerification,
                PolicyVersion = "1.0.0",
                EvidenceReferences = new List<EvidenceReference>
                {
                    new EvidenceReference { ReferenceId = "ref-001" },
                    new EvidenceReference { ReferenceId = "ref-002" }
                },
                DecisionTimestamp = DateTime.UtcNow
            };
            await _repository.CreateDecisionAsync(decision);

            // Act
            var result = await _repository.FindDuplicateDecisionAsync(
                "org-dup",
                OnboardingStep.KycKybVerification,
                "1.0.0",
                new List<string> { "ref-001", "ref-002" }
            );

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("dec-dup"));
        }

        [Test]
        public async Task FindDuplicateDecisionAsync_WithDifferentEvidence_ReturnsNull()
        {
            // Arrange
            var decision = new ComplianceDecision
            {
                Id = "dec-diff",
                OrganizationId = "org-diff",
                Step = OnboardingStep.KycKybVerification,
                PolicyVersion = "1.0.0",
                EvidenceReferences = new List<EvidenceReference>
                {
                    new EvidenceReference { ReferenceId = "ref-001" }
                },
                DecisionTimestamp = DateTime.UtcNow
            };
            await _repository.CreateDecisionAsync(decision);

            // Act - Different evidence
            var result = await _repository.FindDuplicateDecisionAsync(
                "org-diff",
                OnboardingStep.KycKybVerification,
                "1.0.0",
                new List<string> { "ref-999" }
            );

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task FindDuplicateDecisionAsync_OlderThanOneHour_ReturnsNull()
        {
            // Arrange
            var oldDecision = new ComplianceDecision
            {
                Id = "dec-old",
                OrganizationId = "org-old",
                Step = OnboardingStep.KycKybVerification,
                PolicyVersion = "1.0.0",
                EvidenceReferences = new List<EvidenceReference>
                {
                    new EvidenceReference { ReferenceId = "ref-001" }
                },
                DecisionTimestamp = DateTime.UtcNow.AddHours(-2)
            };
            await _repository.CreateDecisionAsync(oldDecision);

            // Act - Same params but decision is too old
            var result = await _repository.FindDuplicateDecisionAsync(
                "org-old",
                OnboardingStep.KycKybVerification,
                "1.0.0",
                new List<string> { "ref-001" }
            );

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task QueryDecisionsAsync_OrdersByTimestampDescending()
        {
            // Arrange
            var decision1 = new ComplianceDecision
            {
                Id = "dec-1",
                OrganizationId = "org-order",
                Step = OnboardingStep.KycKybVerification,
                DecisionTimestamp = DateTime.UtcNow.AddDays(-3)
            };
            var decision2 = new ComplianceDecision
            {
                Id = "dec-2",
                OrganizationId = "org-order",
                Step = OnboardingStep.KycKybVerification,
                DecisionTimestamp = DateTime.UtcNow.AddDays(-1)
            };
            var decision3 = new ComplianceDecision
            {
                Id = "dec-3",
                OrganizationId = "org-order",
                Step = OnboardingStep.KycKybVerification,
                DecisionTimestamp = DateTime.UtcNow
            };

            await _repository.CreateDecisionAsync(decision1);
            await _repository.CreateDecisionAsync(decision2);
            await _repository.CreateDecisionAsync(decision3);

            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-order"
            };

            // Act
            var (decisions, _) = await _repository.QueryDecisionsAsync(request);

            // Assert
            Assert.That(decisions[0].Id, Is.EqualTo("dec-3")); // Most recent first
            Assert.That(decisions[1].Id, Is.EqualTo("dec-2"));
            Assert.That(decisions[2].Id, Is.EqualTo("dec-1"));
        }
    }
}
