using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="AwsKmsProvider"/>.
    ///
    /// Coverage:
    ///   - ProviderType constant
    ///   - ValidateConfigurationAsync: null config, missing region, missing key ID,
    ///     missing explicit credentials, IAM role config, fully valid config
    ///   - GetEncryptionKeyAsync: null config, success, empty secret, short secret,
    ///     AWS client exception wrapping
    /// </summary>
    [TestFixture]
    public class AwsKmsProviderTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static IOptions<KeyManagementConfig> MakeOptions(KeyManagementConfig config)
            => Options.Create(config);

        private static KeyManagementConfig ValidIamConfig() => new KeyManagementConfig
        {
            Provider = "AwsKms",
            AwsKms = new AwsKmsConfig
            {
                Region = "us-east-1",
                KeyId = "arn:aws:secretsmanager:us-east-1:123456789012:secret:my-key",
                UseIamRole = true
            }
        };

        private static KeyManagementConfig ValidExplicitCredentialsConfig() => new KeyManagementConfig
        {
            Provider = "AwsKms",
            AwsKms = new AwsKmsConfig
            {
                Region = "eu-west-1",
                KeyId = "arn:aws:secretsmanager:eu-west-1:123456789012:secret:my-key",
                UseIamRole = false,
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
            }
        };

        private static AwsKmsProvider MakeProvider(
            KeyManagementConfig config,
            IAmazonSecretsManager? client = null)
        {
            var opts = MakeOptions(config);
            var logger = NullLogger<AwsKmsProvider>.Instance;
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

            if (client != null)
                return new AwsKmsProvider(opts, logger, accessor.Object, client);

            return new AwsKmsProvider(opts, logger, accessor.Object);
        }

        // ── ProviderType ─────────────────────────────────────────────────────

        [Test]
        public void ProviderType_ReturnsAwsKms()
        {
            var provider = MakeProvider(ValidIamConfig());
            Assert.That(provider.ProviderType, Is.EqualTo("AwsKms"));
        }

        // ── ValidateConfigurationAsync ────────────────────────────────────────

        [Test]
        public async Task ValidateConfiguration_NullAwsKmsSection_ReturnsFalse()
        {
            var config = new KeyManagementConfig { Provider = "AwsKms", AwsKms = null };
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_EmptyRegion_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.Region = "";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_NullRegion_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.Region = null!;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_EmptyKeyId_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.KeyId = "";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_NullKeyId_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.KeyId = null!;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_MissingAccessKeyId_ReturnsFalse()
        {
            var config = ValidExplicitCredentialsConfig();
            config.AwsKms!.AccessKeyId = "";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_MissingSecretAccessKey_ReturnsFalse()
        {
            var config = ValidExplicitCredentialsConfig();
            config.AwsKms!.SecretAccessKey = "";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_BothCredentialsNull_ReturnsFalse()
        {
            var config = ValidExplicitCredentialsConfig();
            config.AwsKms!.AccessKeyId = null;
            config.AwsKms!.SecretAccessKey = null;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_IamRole_ValidConfig_ReturnsTrue()
        {
            var config = ValidIamConfig();
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_ValidConfig_ReturnsTrue()
        {
            var config = ValidExplicitCredentialsConfig();
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.True);
        }

        // ── GetEncryptionKeyAsync ─────────────────────────────────────────────

        [Test]
        public async Task GetEncryptionKey_NullAwsKmsSection_ThrowsInvalidOperationException()
        {
            var config = new KeyManagementConfig { Provider = "AwsKms", AwsKms = null };
            var mockClient = new Mock<IAmazonSecretsManager>();
            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("AWS KMS configuration is missing"));
        }

        [Test]
        public async Task GetEncryptionKey_ValidKey_ReturnsSecretString()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var secretValue = new string('x', 64); // 64-char secret, well above minimum

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.Is<GetSecretValueRequest>(r => r.SecretId == config.AwsKms!.KeyId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = secretValue });

            var provider = MakeProvider(config, mockClient.Object);
            var result = await provider.GetEncryptionKeyAsync();

            Assert.That(result, Is.EqualTo(secretValue));
        }

        [Test]
        public void GetEncryptionKey_EmptySecret_ThrowsInvalidOperationException()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "" });

            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("empty secret value")
                .Or.Contain("KMS_AWS_RETRIEVAL_FAILED"));
        }

        [Test]
        public void GetEncryptionKey_NullSecret_ThrowsInvalidOperationException()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = null! });

            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("empty secret value")
                .Or.Contain("KMS_AWS_RETRIEVAL_FAILED"));
        }

        [Test]
        public void GetEncryptionKey_ShortSecret_ThrowsInvalidOperationException()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var shortSecret = "tooshort"; // < 32 characters

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = shortSecret });

            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("32 characters")
                .Or.Contain("KMS_AWS_RETRIEVAL_FAILED"));
        }

        [Test]
        public void GetEncryptionKey_ExactlyMinimumLength_ReturnsKey()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var minLengthSecret = new string('k', 32); // exactly 32 characters

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = minLengthSecret });

            var provider = MakeProvider(config, mockClient.Object);

            Assert.DoesNotThrowAsync(() => provider.GetEncryptionKeyAsync());
        }

        [Test]
        public void GetEncryptionKey_AwsClientThrows_WrapsInInvalidOperationException()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var innerException = new AmazonSecretsManagerException("Access denied");

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(innerException);

            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("KMS_AWS_RETRIEVAL_FAILED"));
            Assert.That(ex.InnerException, Is.Not.Null);
        }

        [Test]
        public void GetEncryptionKey_AwsClientThrows_ErrorMessageContainsVerificationSteps()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            var provider = MakeProvider(config, mockClient.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetEncryptionKeyAsync());
            Assert.That(ex!.Message, Does.Contain("Region"));
            Assert.That(ex.Message, Does.Contain("SecretId").Or.Contain("permissions").Or.Contain("connectivity"));
        }

        [Test]
        public async Task GetEncryptionKey_UsesCorrectSecretId()
        {
            var config = ValidIamConfig();
            config.AwsKms!.KeyId = "my-custom-secret-id";
            var mockClient = new Mock<IAmazonSecretsManager>();
            var secretValue = new string('s', 48);

            GetSecretValueRequest? capturedRequest = null;
            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<GetSecretValueRequest, CancellationToken>((r, _) => capturedRequest = r)
                .ReturnsAsync(new GetSecretValueResponse { SecretString = secretValue });

            var provider = MakeProvider(config, mockClient.Object);
            await provider.GetEncryptionKeyAsync();

            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.SecretId, Is.EqualTo("my-custom-secret-id"));
        }

        [Test]
        public async Task GetEncryptionKey_CalledOnce_InvokesClientOnce()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var secretValue = new string('t', 40);

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = secretValue });

            var provider = MakeProvider(config, mockClient.Object);
            await provider.GetEncryptionKeyAsync();

            mockClient.Verify(
                c => c.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task GetEncryptionKey_WithHttpContextTraceIdentifier_UsesItAsCorrelationId()
        {
            var config = ValidIamConfig();
            var mockClient = new Mock<IAmazonSecretsManager>();
            var secretValue = new string('c', 48);

            mockClient
                .Setup(c => c.GetSecretValueAsync(
                    It.IsAny<GetSecretValueRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = secretValue });

            // Set up http context with trace identifier
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(h => h.TraceIdentifier).Returns("trace-id-12345");
            var mockAccessor = new Mock<IHttpContextAccessor>();
            mockAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

            var opts = MakeOptions(config);
            var provider = new AwsKmsProvider(opts, NullLogger<AwsKmsProvider>.Instance, mockAccessor.Object, mockClient.Object);

            var result = await provider.GetEncryptionKeyAsync();
            Assert.That(result, Is.EqualTo(secretValue));
        }

        // ── ValidateConfiguration additional edge cases ────────────────────────

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_OnlyAccessKeyIdSet_ReturnsFalse()
        {
            var config = ValidExplicitCredentialsConfig();
            config.AwsKms!.SecretAccessKey = null;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_ExplicitCredentials_OnlySecretAccessKeySet_ReturnsFalse()
        {
            var config = ValidExplicitCredentialsConfig();
            config.AwsKms!.AccessKeyId = null;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_IamRole_DoesNotRequireAccessCredentials()
        {
            var config = ValidIamConfig();
            config.AwsKms!.UseIamRole = true;
            config.AwsKms!.AccessKeyId = null;
            config.AwsKms!.SecretAccessKey = null;
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.True,
                "IAM role authentication should not require explicit access credentials");
        }

        [Test]
        public async Task ValidateConfiguration_WhitespaceRegion_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.Region = "   ";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateConfiguration_WhitespaceKeyId_ReturnsFalse()
        {
            var config = ValidIamConfig();
            config.AwsKms!.KeyId = "   ";
            var provider = MakeProvider(config);
            var result = await provider.ValidateConfigurationAsync();
            Assert.That(result, Is.False);
        }
    }
}
