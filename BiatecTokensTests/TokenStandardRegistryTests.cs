using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for TokenStandardRegistry service
    /// </summary>
    [TestFixture]
    public class TokenStandardRegistryTests
    {
        private Mock<ILogger<TokenStandardRegistry>> _loggerMock;
        private TokenStandardRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<TokenStandardRegistry>>();
            _registry = new TokenStandardRegistry(_loggerMock.Object);
        }

        [Test]
        public async Task GetAllStandardsAsync_ReturnsActiveStandards()
        {
            // Act
            var result = await _registry.GetAllStandardsAsync(activeOnly: true);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result.All(p => p.IsActive), Is.True);
        }

        [Test]
        public async Task GetAllStandardsAsync_ReturnsAllStandards_WhenActiveOnlyFalse()
        {
            // Act
            var result = await _registry.GetAllStandardsAsync(activeOnly: false);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public async Task GetAllStandardsAsync_ContainsExpectedStandards()
        {
            // Act
            var result = await _registry.GetAllStandardsAsync();

            // Assert
            var standards = result.Select(p => p.Standard).ToList();
            Assert.That(standards, Does.Contain(TokenStandard.Baseline));
            Assert.That(standards, Does.Contain(TokenStandard.ARC3));
            Assert.That(standards, Does.Contain(TokenStandard.ARC19));
            Assert.That(standards, Does.Contain(TokenStandard.ARC69));
            Assert.That(standards, Does.Contain(TokenStandard.ERC20));
        }

        [Test]
        public async Task GetStandardProfileAsync_ReturnsProfile_ForValidStandard()
        {
            // Act
            var result = await _registry.GetStandardProfileAsync(TokenStandard.ARC3);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Standard, Is.EqualTo(TokenStandard.ARC3));
            Assert.That(result.Name, Is.EqualTo("ARC-3"));
            Assert.That(result.Version, Is.Not.Empty);
        }

        [Test]
        public async Task GetStandardProfileAsync_ReturnsNull_ForInactiveStandard()
        {
            // Act - requesting a standard that doesn't exist will return null
            var result = await _registry.GetStandardProfileAsync((TokenStandard)999);

            // Assert
            Assert.That(result, Is.Null);
        }

        [TestCase(TokenStandard.Baseline)]
        [TestCase(TokenStandard.ARC3)]
        [TestCase(TokenStandard.ARC19)]
        [TestCase(TokenStandard.ARC69)]
        [TestCase(TokenStandard.ERC20)]
        public async Task GetStandardProfileAsync_ReturnsValidProfile_ForEachStandard(TokenStandard standard)
        {
            // Act
            var result = await _registry.GetStandardProfileAsync(standard);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Standard, Is.EqualTo(standard));
            Assert.That(result.Name, Is.Not.Empty);
            Assert.That(result.Version, Is.Not.Empty);
            Assert.That(result.Description, Is.Not.Empty);
        }

        [Test]
        public async Task GetDefaultStandardAsync_ReturnsBaselineProfile()
        {
            // Act
            var result = await _registry.GetDefaultStandardAsync();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Standard, Is.EqualTo(TokenStandard.Baseline));
            Assert.That(result.IsActive, Is.True);
        }

        [TestCase(TokenStandard.Baseline, true)]
        [TestCase(TokenStandard.ARC3, true)]
        [TestCase(TokenStandard.ARC19, true)]
        [TestCase(TokenStandard.ARC69, true)]
        [TestCase(TokenStandard.ERC20, true)]
        public async Task IsStandardSupportedAsync_ReturnsExpectedResult(TokenStandard standard, bool expectedSupported)
        {
            // Act
            var result = await _registry.IsStandardSupportedAsync(standard);

            // Assert
            Assert.That(result, Is.EqualTo(expectedSupported));
        }

        [Test]
        public async Task ARC3Profile_HasRequiredFields()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ARC3);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.RequiredFields.Any(f => f.Name == "name"), Is.True);
        }

        [Test]
        public async Task ARC3Profile_HasOptionalFields()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ARC3);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.OptionalFields, Is.Not.Empty);
            Assert.That(profile.OptionalFields.Any(f => f.Name == "image"), Is.True);
            Assert.That(profile.OptionalFields.Any(f => f.Name == "description"), Is.True);
            Assert.That(profile.OptionalFields.Any(f => f.Name == "properties"), Is.True);
        }

        [Test]
        public async Task ARC3Profile_HasValidationRules()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ARC3);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.ValidationRules, Is.Not.Empty);
        }

        [Test]
        public async Task ARC19Profile_HasNameLengthConstraint()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ARC19);

            // Assert
            Assert.That(profile, Is.Not.Null);
            var nameField = profile!.RequiredFields.FirstOrDefault(f => f.Name == "name");
            Assert.That(nameField, Is.Not.Null);
            Assert.That(nameField!.MaxLength, Is.EqualTo(32));
        }

        [Test]
        public async Task ARC69Profile_RequiresStandardField()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ARC69);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.RequiredFields.Any(f => f.Name == "standard"), Is.True);
        }

        [Test]
        public async Task ERC20Profile_HasRequiredMetadataFields()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ERC20);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.RequiredFields.Any(f => f.Name == "name"), Is.True);
            Assert.That(profile.RequiredFields.Any(f => f.Name == "symbol"), Is.True);
            Assert.That(profile.RequiredFields.Any(f => f.Name == "decimals"), Is.True);
        }

        [Test]
        public async Task ERC20Profile_SymbolHasMaxLength()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ERC20);

            // Assert
            Assert.That(profile, Is.Not.Null);
            var symbolField = profile!.RequiredFields.FirstOrDefault(f => f.Name == "symbol");
            Assert.That(symbolField, Is.Not.Null);
            Assert.That(symbolField!.MaxLength, Is.EqualTo(11));
        }

        [Test]
        public async Task ERC20Profile_DecimalsHasRange()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.ERC20);

            // Assert
            Assert.That(profile, Is.Not.Null);
            var decimalsField = profile!.RequiredFields.FirstOrDefault(f => f.Name == "decimals");
            Assert.That(decimalsField, Is.Not.Null);
            Assert.That(decimalsField!.MinValue, Is.EqualTo(0));
            Assert.That(decimalsField.MaxValue, Is.EqualTo(18));
        }

        [Test]
        public async Task BaselineProfile_HasMinimalRequirements()
        {
            // Act
            var profile = await _registry.GetStandardProfileAsync(TokenStandard.Baseline);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.RequiredFields, Has.Count.EqualTo(1));
            Assert.That(profile.RequiredFields[0].Name, Is.EqualTo("name"));
        }

        [Test]
        public async Task AllProfiles_HaveValidVersionNumbers()
        {
            // Act
            var profiles = await _registry.GetAllStandardsAsync();

            // Assert
            foreach (var profile in profiles)
            {
                Assert.That(profile.Version, Is.Not.Empty);
                Assert.That(profile.Version, Does.Match(@"^\d+\.\d+\.\d+$"));
            }
        }

        [Test]
        public async Task AllProfiles_HaveUniqueIds()
        {
            // Act
            var profiles = await _registry.GetAllStandardsAsync();

            // Assert
            var ids = profiles.Select(p => p.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public async Task AllProfiles_HaveSpecificationUrls()
        {
            // Act
            var profiles = await _registry.GetAllStandardsAsync();

            // Assert - Baseline may not have a spec URL, but others should
            var profilesWithSpecs = profiles.Where(p => p.Standard != TokenStandard.Baseline);
            foreach (var profile in profilesWithSpecs)
            {
                Assert.That(profile.SpecificationUrl, Is.Not.Null);
                Assert.That(profile.SpecificationUrl, Does.StartWith("http"));
            }
        }
    }
}
