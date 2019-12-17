using Networking.RFC_Foundational;
using System.Threading.Tasks;

namespace Networking.Simplest_Possible_Versions
{
    public class Simplest_Daytime_Sample_Rfc_867
    {
        public static async Task<DaytimeClient_Rfc_867.DaytimeResult> RunAsync()
        {
            // Hint: the using here means that the server is automatically closed
            // after the code is done. If you don't do that, the server stays open.
            using (var server = new DaytimeServer_Rfc_867())
            {
                await server.StartAsync();

                var client = new DaytimeClient_Rfc_867();
                var result = await client.WriteAsync(new Windows.Networking.HostName("localhost"));

                return result;
            }
        }
    }
}
