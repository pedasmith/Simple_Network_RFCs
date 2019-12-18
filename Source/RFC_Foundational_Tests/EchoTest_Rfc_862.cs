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

                var protocol = EchoClient_Rfc_862.ProtocolType.Tcp;
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                await testObject.Test_Stress(protocol);

                protocol = EchoClient_Rfc_862.ProtocolType.Udp;
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                await testObject.Test_Stress(protocol);
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
            // This is a magic string; it's the data that's sent by the client.
            var expected = "Hello, echo server!";

            var resultTcp = await Simplest_Echo_Sample_Rfc_862.RunTcpAsync();
            Infrastructure.IfTrueError(resultTcp.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "UDP status should be in progress");
            Infrastructure.IfTrueError(resultTcp.Value != expected, $"Echo TCP got {resultTcp.Value} but expected {expected}");

            var resultUdp = await Simplest_Echo_Sample_Rfc_862.RunUdpAsync();
            Infrastructure.IfTrueError(resultUdp.Succeeded != EchoClient_Rfc_862.EchoResult.State.Succeeded, "UDP status should be in progress");
            Infrastructure.IfTrueError(resultUdp.Value != expected, $"Echo UDP got {resultUdp.Value} but expected {expected}");
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


        /// <summary>
        /// System test, not a unit tests. Creates a server, pumps a bunch of requests at it,
        /// and verifies that everything worked.
        /// </summary>
        /// <returns></returns>
        public async Task Test_Stress(EchoClient_Rfc_862.ProtocolType protocol)
        {
            string pname = protocol.ToString();

            const int NLOOP = 4;
            const int NBUNCH = 200;

            const double ALLOWED_TIME = 20.0; // Normally we expect everything to be fast. No so much for a stress test!
            const int ALLOWED_CONN_RESET = (NLOOP * NBUNCH) * 5 / 100; // Allow 5% conn reset

            int NBytesWrite = 0;
            int NConnReset = 0;
            var host = new HostName("localhost");
            var serverOptions = new EchoServer_Rfc_862.ServerOptions()
            {
                LoggingLevel = EchoServer_Rfc_862.ServerOptions.Verbosity.None,
            };
            var clientOptions = new EchoClient_Rfc_862.ClientOptions()
            {
                LoggingLevel = EchoClient_Rfc_862.ClientOptions.Verbosity.None,
            };

            using (var server = new EchoServer_Rfc_862(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"Client: Stress: {pname} unable to start server");
                    return;
                }

                var start = DateTimeOffset.UtcNow;
                for (int i = 0; i < NLOOP; i++)
                {
                    if (i % 100 == 0 && i > 0)
                    {
                        Infrastructure.Log($"Client: Stress: {pname} starting loop {i} of {NLOOP}");
                    }
                    var allClients = new EchoClient_Rfc_862[NBUNCH];

                    var allWriteTasks = new Task<EchoClient_Rfc_862.EchoResult>[NBUNCH];
                    for (int j = 0; j < NBUNCH; j++)
                    {
                        var send = $"ABC-Loop {i} Item {j}";
                        NBytesWrite += send.Length;
                        allClients[i] = new EchoClient_Rfc_862(clientOptions);
                        allWriteTasks[j] = allClients[i].WriteAsync(host, serverOptions.Service, protocol, send);
                    }
                    await Task.WhenAll(allWriteTasks);

                    // TCP has to wait for the close to happen.
                    Task<EchoClient_Rfc_862.EchoResult>[] allCloseTasks = null;
                    if (protocol == EchoClient_Rfc_862.ProtocolType.Tcp)
                    {
                        allCloseTasks = new Task<EchoClient_Rfc_862.EchoResult>[NBUNCH];
                        for (int j = 0; j < NBUNCH; j++)
                        {
                            allCloseTasks[j] = allClients[i].CloseAsync();
                        }
                        await Task.WhenAll(allCloseTasks);
                    }

                    for (int j = 0; j < NBUNCH; j++)
                    {
                        var result = protocol == EchoClient_Rfc_862.ProtocolType.Tcp 
                            ? allCloseTasks[i].Result 
                            : allWriteTasks[j].Result;

                        var didSucceed = result.Succeeded == EchoClient_Rfc_862.EchoResult.State.Succeeded;
                        if (!didSucceed && result.Error == SocketErrorStatus.ConnectionResetByPeer)
                        {
                            // Connection reset by peer is an error only if we get a bunch of them.
                            NConnReset++;
                            Infrastructure.IfTrueError(NConnReset > ALLOWED_CONN_RESET, $"Too many connection resets {NConnReset}");
                        }
                        else if (!Infrastructure.IfTrueError(!didSucceed, $"!result.Succeeded with error {result.Error.ToString()} for {pname}"))
                        {
                            Infrastructure.IfTrueError(result.Value == null, "result.Value is null");
                            if (result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60))
                            {
                                ;
                            }
                            Infrastructure.IfTrueError(result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60), $"result.Value is weird size ({result.Value.Length}) for {pname}");
                        }
                        Infrastructure.IfTrueError(result.TimeInSeconds < -1, $"result.TimeInSeconds too small {result.TimeInSeconds} for {pname}");
                        Infrastructure.IfTrueError(result.TimeInSeconds > ALLOWED_TIME, $"result.TimeInSeconds too large {result.TimeInSeconds} for {pname}");
                    }

                    await Task.Delay(300); //NOTE: pause just to make the test runs easier to figure out
                }
                // timing data
                var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
                double timePerCall = delta / (double)(NLOOP * NBUNCH);
                Infrastructure.Log($"Stress test average time: {NLOOP} {NBUNCH} {timePerCall} for {pname}");


                // How's the server doing?
                const int ExpectedCount = NLOOP * NBUNCH;
                Infrastructure.IfTrueError(server.Stats.NConnections != ExpectedCount, $"Server got {server.Stats.NConnections} connections but expected {ExpectedCount} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NResponses != ExpectedCount, $"Server sent {server.Stats.NResponses} responses but expected {ExpectedCount} for {pname}");

                // Why is the expected not equal to the NBytesSent? Because TCP has a weird timing thing:
                // the server has to close down reading from the client relatively quickly and can't waste
                // time for the client to send bytes that probably won't come.
                var expectedMinBytes = protocol == EchoClient_Rfc_862.ProtocolType.Udp ? NBytesWrite : NBytesWrite / 4;
                Infrastructure.IfTrueError(server.Stats.NBytesRead > NBytesWrite, $"Server got {server.Stats.NBytesRead} bytes but expected {NBytesWrite} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NBytesRead < expectedMinBytes, $"Server got {server.Stats.NBytesRead} bytes but expected {NBytesWrite} with a minimum value of {expectedMinBytes} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NExceptions != 0, $"Server got {server.Stats.NExceptions} exceptions but expected {0} for {pname}");
            }
        }
    }
}
