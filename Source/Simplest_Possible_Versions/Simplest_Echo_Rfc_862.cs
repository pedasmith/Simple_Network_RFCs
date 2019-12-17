using Networking.RFC_Foundational;
using System.Threading.Tasks;

namespace Networking.Simplest_Possible_Versions
{
    public class Simplest_Echo_Sample_Rfc_862
    {
        public static async Task<EchoClient_Rfc_862.EchoResult> RunAsync()
        {
            // Hint: the using here means that the server is automatically closed
            // after the code is done. If you don't do that, the server stays open.
            using (var server = new EchoServer_Rfc_862())
            {
                await server.StartAsync();

                var client = new EchoClient_Rfc_862();
                // You only get the echo'd result from TCP with the final CloseAsync()
                // But for UDP  you get the returned packet.
                var result = await client.WriteAsync(
                    new Windows.Networking.HostName("localhost"),
                    server.Options.Service,
                    EchoClient_Rfc_862.ProtocolType.Udp,
                    "Hello, echo server!");

                await client.CloseAsync(); // Close is really needed for TCP. 
                return result;
            }
        }
    }
}
