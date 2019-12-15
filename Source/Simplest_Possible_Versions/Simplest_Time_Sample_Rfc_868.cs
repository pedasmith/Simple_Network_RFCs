using Networking.RFC_Foundational;
using System.Threading.Tasks;

namespace Networking.Simplest_Possible_Versions
{
    class Simplest_Time_Sample_Rfc_868
    {
        public static async Task<TimeClient_Rfc_868.TimeResult> RunAsync()
        {
            // Hint: the using here means that the server is automatically closed
            // after the code is done. If you don't do that, the server stays open.
            using (var server = new TimeServer_Rfc_868())
            {
                await server.StartAsync();

                var client = new TimeClient_Rfc_868();
                var result = await client.SendAsync(new Windows.Networking.HostName("localhost"));
                return result;
            }
        }
    }
}
