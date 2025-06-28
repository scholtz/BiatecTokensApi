using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Web3;

namespace BiatecTokensTests
{
    public static class TestHelper
    {
        // Ganache default URL
        public const string LocalBlockchainUrl = "http://127.0.0.1:8545";
        
        /// <summary>
        /// Checks if a local blockchain (like Ganache) is running
        /// </summary>
        /// <returns>True if local blockchain is running</returns>
        public static async Task<bool> IsLocalBlockchainRunning()
        {
            try
            {
                var web3 = new Web3(LocalBlockchainUrl);
                await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Provides instructions on how to set up Ganache for testing
        /// </summary>
        public static string GetGanacheSetupInstructions()
        {
            var instructions = new StringBuilder();
            
            instructions.AppendLine("Local blockchain for testing is not running. Please follow these steps:");
            instructions.AppendLine();
            instructions.AppendLine("1. Install Ganache from https://trufflesuite.com/ganache/");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                instructions.AppendLine("2. Run Ganache and select 'QUICKSTART' to launch a local Ethereum blockchain");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                instructions.AppendLine("2. Open Ganache and select 'QUICKSTART' to launch a local Ethereum blockchain");
            }
            else
            {
                instructions.AppendLine("2. Launch Ganache and select 'QUICKSTART' to start a local Ethereum blockchain");
                instructions.AppendLine("   Alternatively, use ganache-cli: npm install -g ganache-cli && ganache-cli");
            }
            
            instructions.AppendLine("3. Ensure it's running on http://127.0.0.1:8545");
            instructions.AppendLine("4. Run the tests again");
            instructions.AppendLine();
            instructions.AppendLine("Note: The tests use the first two accounts from Ganache's default accounts.");
            
            return instructions.ToString();
        }
        
        /// <summary>
        /// Gets the default Ganache accounts private keys
        /// </summary>
        public static (string Owner, string User) GetDefaultGanachePrivateKeys()
        {
            // Default private keys from Ganache (these are well-known test keys, never use in production)
            return (
                "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80", // Account 0
                "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d"  // Account 1
            );
        }
    }
}