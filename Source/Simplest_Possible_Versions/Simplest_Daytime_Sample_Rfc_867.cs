using RFC_Foundational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Networking.Simplest_Possible_Versions
{
    public class Simplest_Daytime_Sample_Rfc_867
    {
        public static async Task<DaytimeClient_Rfc_867.DaytimeResult> RunAsync()
        {
            var server = new DaytimeServer_Rfc_867();
            await server.StartAsync();

            var client = new DaytimeClient_Rfc_867();
            var result = await client.SendAsync(new Windows.Networking.HostName("localhost"));

            return result;
        }
    }
}
