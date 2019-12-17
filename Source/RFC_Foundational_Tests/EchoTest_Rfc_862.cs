using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace Networking.RFC_Foundational_Tests
{
    class EchoTest_Rfc_862
    {
        public static async Task Test()
        {
            var start = DateTimeOffset.UtcNow;
            Infrastructure.Log($"Starting test: EchoTest_Rfc_862");
            var testObject = new EchoTest_Rfc_862();
            try
            {
                await testObject.Test_Echo_Simple_Tcp();
                await testObject.Test_Echo_Simple_Udp();
            }
            catch (Exception ex)
            {
                // There should be absolutely no exceptions thrown by the tests.
                Infrastructure.Error($"Uncaught exception thrown in tests {ex.Message} hresult {ex.HResult:X}");
            }
            var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
            Infrastructure.Log($"Ending test: EchoTest_Rfc_862  time={delta} seconds");
        }

        public async Task Test_Echo_Simple_Tcp()
        {
            using (var server = new EchoServer_Rfc_862())
            {
                var client = new EchoClient_Rfc_862();
                await server.StartAsync();
                var result1 = await client.WriteAsync(new HostName("localhost"), server.Service, EchoClient_Rfc_862.ProtocolType.Tcp, "ABCdef");
                Infrastructure.IfTrueError(result1.Succeeded != EchoClient_Rfc_862.EchoResult.State.InProgress, "TCP status should be in progress");

                var result2 = await client.WriteAsync(new HostName("localhost"), server.Service, EchoClient_Rfc_862.ProtocolType.Tcp, "123456");
                Infrastructure.IfTrueError(result2.Succeeded != EchoClient_Rfc_862.EchoResult.State.InProgress, "TCP status should be in progress");

                await Task.Delay(100); //TODO: artificial delay to get the results.
                var result3 = await client.CloseAsync();
                Infrastructure.IfTrueError(result3.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result3.Value != "ABCdef123456", $"Should have gotten back abcDEF123456, not {result3.Value}");
            }
        }

        public async Task Test_Echo_Simple_Udp()
        {
            using (var server = new EchoServer_Rfc_862())
            {
                var client = new EchoClient_Rfc_862();
                await server.StartAsync();

                var result1 = await client.WriteAsync(new HostName("localhost"), server.Service, EchoClient_Rfc_862.ProtocolType.Udp, "ABC");

                Infrastructure.IfTrueError(result1.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result1.Value != "ABC", $"Should have gotten back abcDEF123456, not {result1.Value}");

                var result2 = await client.WriteAsync(new HostName("localhost"), server.Service, EchoClient_Rfc_862.ProtocolType.Udp, "123");

                Infrastructure.IfTrueError(result2.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result2.Value != "123", $"Should have gotten back abcDEF123456, not {result2.Value}");
            }
        }

    }
}
