using BiatecTokensApi.Models.Compliance;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the compliance validation endpoint
    /// These tests verify JSON serialization and deserialization of the validation models
    /// </summary>
    [TestFixture]
    public class ComplianceValidationIntegrationTests
    {
        [Test]
        public void ValidateTokenPreset_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US",
                RegulatoryFramework = "SEC Reg D",
                ComplianceStatus = ComplianceStatus.Compliant,
                MaxHolders = 500,
                Network = "voimain-v1.0",
                IncludeWarnings = true
            };

            // Act
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<ValidateTokenPresetRequest>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.AssetType, Is.EqualTo("Security Token"));
            Assert.That(deserialized.RequiresAccreditedInvestors, Is.True);
            Assert.That(deserialized.HasWhitelistControls, Is.True);
            Assert.That(deserialized.Jurisdiction, Is.EqualTo("US"));
            Assert.That(deserialized.Network, Is.EqualTo("voimain-v1.0"));
            
            Console.WriteLine("JSON serialization test successful");
            Console.WriteLine($"Serialized: {json}");
        }

        [Test]
        public void ValidateTokenPresetResponse_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var response = new ValidateTokenPresetResponse
            {
                Success = true,
                IsValid = false,
                Errors = new List<ValidationIssue>
                {
                    new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "HasWhitelistControls",
                        Message = "Tokens requiring accredited investors must have whitelist controls enabled",
                        Recommendation = "Enable whitelist controls to restrict token transfers to verified accredited investors",
                        RegulatoryContext = "Securities Act - Accredited Investor Requirements"
                    }
                },
                Warnings = new List<ValidationIssue>
                {
                    new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Field = "MaxHolders",
                        Message = "Maximum number of holders is not specified for security token",
                        Recommendation = "Consider setting a maximum holder limit to comply with securities regulations",
                        RegulatoryContext = "Securities Regulations"
                    }
                },
                Summary = "Token configuration has 1 error(s) that must be fixed before deployment"
            };

            // Act
            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<ValidateTokenPresetResponse>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Success, Is.True);
            Assert.That(deserialized.IsValid, Is.False);
            Assert.That(deserialized.Errors.Count, Is.EqualTo(1));
            Assert.That(deserialized.Warnings.Count, Is.EqualTo(1));
            Assert.That(deserialized.Errors[0].Field, Is.EqualTo("HasWhitelistControls"));
            Assert.That(deserialized.Errors[0].Recommendation, Is.Not.Null);
            Assert.That(deserialized.Warnings[0].Field, Is.EqualTo("MaxHolders"));
            
            Console.WriteLine("Response JSON serialization test successful");
            Console.WriteLine($"Serialized: {json}");
        }

        [Test]
        public void ValidationIssue_AllFields_SerializeCorrectly()
        {
            // Arrange
            var issue = new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Field = "TestField",
                Message = "Test error message",
                Recommendation = "Test recommendation",
                RegulatoryContext = "Test Regulation"
            };

            // Act
            var json = JsonSerializer.Serialize(issue);
            var deserialized = JsonSerializer.Deserialize<ValidationIssue>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(deserialized.Field, Is.EqualTo("TestField"));
            Assert.That(deserialized.Message, Is.EqualTo("Test error message"));
            Assert.That(deserialized.Recommendation, Is.EqualTo("Test recommendation"));
            Assert.That(deserialized.RegulatoryContext, Is.EqualTo("Test Regulation"));
        }

        [Test]
        public void ValidateTokenPreset_ExampleRequest_SerializesToValidJson()
        {
            // Arrange - Example request for documentation
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                VerificationStatus = VerificationStatus.Pending,
                Jurisdiction = null,
                Network = "voimain-v1.0",
                IncludeWarnings = true
            };

            // Act
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });

            // Assert
            Assert.That(json, Is.Not.Null.And.Not.Empty);
            Assert.That(json, Does.Contain("AssetType"));
            Assert.That(json, Does.Contain("RequiresAccreditedInvestors"));
            Assert.That(json, Does.Contain("HasWhitelistControls"));
            
            Console.WriteLine("Example Request JSON:");
            Console.WriteLine(json);
        }
    }
}
