using Networking.RFC_Foundational;
using System.Threading.Tasks;

namespace Networking.Simplest_Possible_Versions
{
    public class Simplest_CharGen_Sample_Rfc_864
    {
        /// <summary>
        /// Demonstrates the simplest calling pattern.
        /// </summary>
        /// <param name="patternType">Normally only set when testing that the code works (setting the pattern to the 'Fixed' pattern)</param>
        /// <returns></returns>
        public static async Task<CharGenClient_Rfc_864.CharGenResult> RunTcpAsync(CharGenServer_Rfc_864.ServerOptions.PatternType patternType = CharGenServer_Rfc_864.ServerOptions.PatternType.Classic72)
        {
            // Hint: the using here means that the server is automatically closed
            // after the code is done. If you don't do that, the server stays open.
            using (var server = new CharGenServer_Rfc_864())
            {
                server.Options.OutputPattern = patternType;
                await server.StartAsync();

                var client = new CharGenClient_Rfc_864();
                // You only get the CharGen'd result from TCP with the final CloseAsync()
                // But for UDP  you get the returned packet.
                var partialResult = await client.WriteAsync(
                    new Windows.Networking.HostName("localhost"),
                    server.Options.Service,
                    CharGenClient_Rfc_864.ProtocolType.Tcp);
                // MAGIC: why is 100 the right value?
                // Wait for the CharGen to have worked. The server is right here, so the
                // CharGen should be almost instantaneous.
                await Task.Delay(100);
                var completeResult = await client.CloseAsync(); // Close is essential for TCP. 
                return completeResult;
            }
        }

        /// <summary>
        /// Demonstrates the simplest calling pattern
        /// </summary>
        /// <param name="patternType">Normally only set when testing that the code works (setting the pattern to the 'Fixed' pattern)</param>
        /// <returns></returns>
        public static async Task<CharGenClient_Rfc_864.CharGenResult> RunUdpAsync(CharGenServer_Rfc_864.ServerOptions.PatternType patternType = CharGenServer_Rfc_864.ServerOptions.PatternType.Classic72)
        {
            // Hint: the using here means that the server is automatically closed
            // after the code is done. If you don't do that, the server stays open.
            using (var server = new CharGenServer_Rfc_864())
            {
                server.Options.OutputPattern = patternType;
                await server.StartAsync();

                var client = new CharGenClient_Rfc_864();
                var result = await client.WriteAsync(
                    new Windows.Networking.HostName("localhost"),
                    server.Options.Service,
                    CharGenClient_Rfc_864.ProtocolType.Udp);

                await client.CloseAsync(); // Close is really needed for TCP. 
                return result;
            }
        }
    }
}
