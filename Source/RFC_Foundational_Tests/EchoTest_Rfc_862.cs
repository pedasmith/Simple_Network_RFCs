using Networking.RFC_Foundational;
using Networking.Simplest_Possible_Versions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

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
                await testObject.Test_Echo_Simplest();
                await testObject.Test_Echo_Good_Path_Tcp();
                await testObject.Test_Echo_Good_Path_Udp();

                var protocol = EchoClient_Rfc_862.ProtocolType.Udp;
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                //TODO: await testObject.Test_Stress(protocol);

            }
            catch (Exception ex)
            {
                // There should be absolutely no exceptions thrown by the tests.
                Infrastructure.Error($"Uncaught exception thrown in tests {ex.Message} hresult {ex.HResult:X}");
            }
            var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
            Infrastructure.Log($"Ending test: EchoTest_Rfc_862  time={delta} seconds");
        }

        public async Task Test_Echo_Simplest()
        {
            var result = await Simplest_Echo_Sample_Rfc_862.RunAsync();
            Infrastructure.IfTrueError(result.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "UDP status should be in progress");
            var expected = "Hello, echo server!";
            Infrastructure.IfTrueError(result.Value != expected, $"Echo UDP got {result.Value} but expected {expected}");
        }

        public async Task Test_Echo_Good_Path_Tcp()
        {
            using (var server = new EchoServer_Rfc_862())
            {
                var client = new EchoClient_Rfc_862();
                await server.StartAsync();
                var result1 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, EchoClient_Rfc_862.ProtocolType.Tcp, "ABCdef");
                Infrastructure.IfTrueError(result1.Succeeded != EchoClient_Rfc_862.EchoResult.State.InProgress, "TCP status should be in progress");

                var result2 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, EchoClient_Rfc_862.ProtocolType.Tcp, "123456");
                Infrastructure.IfTrueError(result2.Succeeded != EchoClient_Rfc_862.EchoResult.State.InProgress, "TCP status should be in progress");

                await Task.Delay(100); //TODO: artificial delay to get the results.
                var result3 = await client.CloseAsync();
                Infrastructure.IfTrueError(result3.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result3.Value != "ABCdef123456", $"Should have gotten back abcDEF123456, not {result3.Value}");
            }
        }

        public async Task Test_Echo_Good_Path_Udp()
        {
            using (var server = new EchoServer_Rfc_862())
            {
                var client = new EchoClient_Rfc_862();
                await server.StartAsync();

                var result1 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, EchoClient_Rfc_862.ProtocolType.Udp, "ABC");

                Infrastructure.IfTrueError(result1.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result1.Value != "ABC", $"Should have gotten back abcDEF123456, not {result1.Value}");

                var result2 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, EchoClient_Rfc_862.ProtocolType.Udp, "123");

                Infrastructure.IfTrueError(result2.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result2.Value != "123", $"Should have gotten back abcDEF123456, not {result2.Value}");
            }
        }
        


        public async Task Test_Bad_Host(EchoClient_Rfc_862.ProtocolType protocol)
        {
            var host = new HostName("invalid.host.doesnt.exist.example.com");
            var clientOptions = new EchoClient_Rfc_862.ClientOptions()
            {
                LoggingLevel = EchoClient_Rfc_862.ClientOptions.Verbosity.None
            };

            var client = new EchoClient_Rfc_862(clientOptions);
            var result = await client.WriteAsync(host, "10013", protocol, "ABCDEF");
            Infrastructure.IfTrueError(result.Succeeded != EchoClient_Rfc_862.EchoResult.State.Failed, "result.Succeeded ");
            Infrastructure.IfTrueError(!String.IsNullOrEmpty(result.Value), "result.Value is not null");
            Infrastructure.IfTrueError(result.Error != SocketErrorStatus.HostNotFound, $"result.Error is wrong ({result.Error})");
        }
        public async Task Test_Bad_Service(EchoClient_Rfc_862.ProtocolType protocol)
        {
            var host = new HostName("localhost");
            var serverOptions = new EchoServer_Rfc_862.ServerOptions()
            {
                LoggingLevel = EchoServer_Rfc_862.ServerOptions.Verbosity.None
            };
            var clientOptions = new EchoClient_Rfc_862.ClientOptions()
            {
                LoggingLevel = EchoClient_Rfc_862.ClientOptions.Verbosity.None
            };

            using (var server = new EchoServer_Rfc_862(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"unable to start server");
                    return;
                }

                var client = new EchoClient_Rfc_862(clientOptions);
                var result = await client.WriteAsync(host, "79", protocol, "WontBeEchoed");
                Infrastructure.IfTrueError(result.Succeeded != EchoClient_Rfc_862.EchoResult.State.Failed, "result.Succeeded ");
                Infrastructure.IfTrueError(!String.IsNullOrEmpty(result.Value), "result.Value is not null");
                // ConnectionRefused is for TCP
                // ConnectionResetByPeer is for UDP
                Infrastructure.IfTrueError(result.Error != SocketErrorStatus.ConnectionRefused && result.Error != SocketErrorStatus.ConnectionResetByPeer, $"result.Error is wrong ({result.Error})");
            }
        }

    }
}
