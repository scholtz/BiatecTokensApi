using BiatecTokensApi.Models;
using BiatecTokensApi.Models.EVM;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// This test class demonstrates Test-Driven Development (TDD) principles.
    /// It serves as an example for contributors on how to write tests following TDD practices.
    /// 
    /// TDD Workflow:
    /// 1. Write a failing test that describes the desired behavior
    /// 2. Implement minimal code to make the test pass
    /// 3. Refactor while keeping tests green
    /// </summary>
    [TestFixture]
    public class TDDExampleTests
    {
        /// <summary>
        /// Example test demonstrating validation of EVMTokenDeploymentResponse properties.
        /// This follows the AAA pattern: Arrange, Act, Assert
        /// </summary>
        [Test]
        public void EVMTokenDeploymentResponse_WhenSuccessful_ShouldHaveSuccessTrueAndTransactionHash()
        {
            // Arrange - Set up the test data
            var response = new EVMTokenDeploymentResponse
            {
                Success = true,
                TransactionHash = "0x123abc456def",
                ErrorMessage = null
            };

            // Act - Execute the behavior being tested (in this case, just accessing properties)
            var isSuccessful = response.Success;
            var hasTransactionHash = !string.IsNullOrEmpty(response.TransactionHash);

            // Assert - Verify the expected outcome
            Assert.That(isSuccessful, Is.True, "Response should indicate success");
            Assert.That(hasTransactionHash, Is.True, "Successful response should have a transaction hash");
            Assert.That(response.ErrorMessage, Is.Null, "Successful response should not have an error message");
        }

        /// <summary>
        /// Example test demonstrating validation of error responses.
        /// Shows how to test negative cases.
        /// </summary>
        [Test]
        public void BaseResponse_WhenFailed_ShouldHaveSuccessFalseAndErrorMessage()
        {
            // Arrange
            var errorMessage = "Insufficient funds for transaction";
            var response = new BaseResponse
            {
                Success = false,
                ErrorMessage = errorMessage
            };

            // Act
            var isFailed = !response.Success;
            var hasErrorMessage = !string.IsNullOrEmpty(response.ErrorMessage);

            // Assert
            Assert.That(isFailed, Is.True, "Response should indicate failure");
            Assert.That(hasErrorMessage, Is.True, "Failed response should have an error message");
            Assert.That(response.ErrorMessage, Is.EqualTo(errorMessage), "Error message should match");
        }

        /// <summary>
        /// Example test showing how to use TestCase attribute for parameterized tests.
        /// This is useful for testing multiple scenarios with different inputs.
        /// </summary>
        [TestCase("0x123abc", true, Description = "Valid transaction hash should be accepted")]
        [TestCase("", false, Description = "Empty transaction hash should be invalid")]
        [TestCase(null, false, Description = "Null transaction hash should be invalid")]
        public void EVMTokenDeploymentResponse_TransactionHashValidation_ShouldHandleVariousValues(string? transactionHash, bool expectedValid)
        {
            // Arrange & Act
            var response = new EVMTokenDeploymentResponse
            {
                Success = expectedValid,
                TransactionHash = transactionHash ?? string.Empty
            };

            // Assert
            if (expectedValid)
            {
                Assert.That(response.TransactionHash, Is.Not.Null.And.Not.Empty, "Valid response should have a non-empty transaction hash");
            }
            else
            {
                Assert.That(string.IsNullOrEmpty(transactionHash), Is.True, "Invalid transaction hash should be null or empty");
            }
        }

        /// <summary>
        /// Example test demonstrating setup and teardown methods.
        /// This shows best practices for test initialization.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // This method runs before each test
            // Use it to initialize common test data or mock objects
        }

        /// <summary>
        /// Example test demonstrating teardown.
        /// This shows how to clean up after tests.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // This method runs after each test
            // Use it to clean up resources, reset state, etc.
        }

        /// <summary>
        /// Example test that demonstrates the OneTimeSetUp for expensive operations.
        /// This runs once before all tests in the fixture.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // This runs once before all tests in this class
            // Use for expensive operations like database connections
        }

        /// <summary>
        /// Example test that demonstrates the OneTimeTearDown.
        /// This runs once after all tests in the fixture.
        /// </summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // This runs once after all tests in this class
            // Use for cleanup of expensive resources
        }
    }
}
