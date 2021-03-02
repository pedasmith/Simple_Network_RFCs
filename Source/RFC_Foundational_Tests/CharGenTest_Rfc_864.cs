using Networking.RFC_Foundational;
using Networking.Simplest_Possible_Versions;
using System;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace Networking.RFC_Foundational_Tests
{
    class CharGenTest_Rfc_864
    {
        public static async Task Test()
        {
            await Task.Delay(0); // TODO: when the test code is done, can remove.
            var start = DateTimeOffset.UtcNow;
            Infrastructure.Log($"Starting test: CharGenTest_Rfc_864");
            var testObject = new CharGenTest_Rfc_864();
            try
            {
                testObject.Test_CharGen();

                await testObject.Test_CharGen_Simplest();
                await testObject.Test_CharGen_Good_Path_Tcp();

#if NEVER_EVER_DEFINED
//TODO: reenable all these tests
                await testObject.Test_CharGen_Good_Path_Udp();

                var protocol = CharGenClient_Rfc_864.ProtocolType.Tcp;
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                await testObject.Test_Stress(protocol);

                protocol = CharGenClient_Rfc_864.ProtocolType.Udp;
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                await testObject.Test_Stress(protocol);
#endif

            }
            catch (Exception ex)
            {
                // There should be absolutely no exceptions thrown by the tests.
                Infrastructure.Error($"CharGen Test: Error: uncaught exception thrown in tests {ex.Message} hresult {ex.HResult:X}");
            }
            var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
            Infrastructure.Log($"Ending test: CharGenTest_Rfc_864  time={delta} seconds");
        }

        public void Test_CharGen()
        {
            CharGenServer_Rfc_864.TestAscii95();
        }

        public async Task Test_CharGen_Simplest()
        {
            // This is a magic string; it's the data that's returned by the server.
            var expected = CharGenServer_Rfc_864.GetSampleReturn();

            var resultTcp = await Simplest_CharGen_Sample_Rfc_864.RunTcpAsync(CharGenServer_Rfc_864.ServerOptions.PatternType.Fixed);
            Infrastructure.IfTrueError(resultTcp.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Succeeded, "UDP status should be in progress");
            Infrastructure.IfTrueError(resultTcp.Value != expected, $"CharGen TCP got {resultTcp.Value} but expected {expected}");

            var resultUdp = await Simplest_CharGen_Sample_Rfc_864.RunUdpAsync(CharGenServer_Rfc_864.ServerOptions.PatternType.Fixed);
            Infrastructure.IfTrueError(resultUdp.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Succeeded, "UDP status should be in progress");
            Infrastructure.IfTrueError(resultUdp.Value != expected, $"CharGen UDP got {resultUdp.Value} but expected {expected}");
        }

        public async Task Test_CharGen_Good_Path_Tcp()
        {
            using (var server = new CharGenServer_Rfc_864())
            {
                server.Options.OutputPattern = CharGenServer_Rfc_864.ServerOptions.PatternType.Fixed;
                var client = new CharGenClient_Rfc_864();
                await server.StartAsync();
                var result1 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, CharGenClient_Rfc_864.ProtocolType.Tcp);
                Infrastructure.IfTrueError(result1.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.InProgress, "TCP status should be in progress");

                await Task.Delay(100); //TODO: artificial delay to get the results.
                var expected = CharGenServer_Rfc_864.GetSampleReturn();
                var result3 = await client.CloseAsync();
                Infrastructure.IfTrueError(result3.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result3.Value != expected, $"Should have gotten back {expected}, not {result3.Value}");
            }
        }

        public async Task Test_CharGen_Good_Path_Udp()
        {
            using (var server = new CharGenServer_Rfc_864())
            {
                server.Options.OutputPattern = CharGenServer_Rfc_864.ServerOptions.PatternType.Fixed;
                var client = new CharGenClient_Rfc_864();
                await server.StartAsync();

                var result1 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, CharGenClient_Rfc_864.ProtocolType.Udp, "ABC");

                var expected = CharGenServer_Rfc_864.GetSampleReturn();
                Infrastructure.IfTrueError(result1.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result1.Value != expected, $"Should have gotten back {expected}, not {result1.Value}");

                var result2 = await client.WriteAsync(new HostName("localhost"), server.Options.Service, CharGenClient_Rfc_864.ProtocolType.Udp, "123");

                Infrastructure.IfTrueError(result2.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Succeeded, "Close TCP status should be succeeded");
                Infrastructure.IfTrueError(result2.Value != expected, $"Should have gotten back {expected}, not {result2.Value}");
            }
        }

        public async Task Test_Bad_Host(CharGenClient_Rfc_864.ProtocolType protocol)
        {
            var host = new HostName("invalid.host.doesnt.exist.example.com");
            var clientOptions = new CharGenClient_Rfc_864.ClientOptions()
            {
                LoggingLevel = CharGenClient_Rfc_864.ClientOptions.Verbosity.None
            };

            var client = new CharGenClient_Rfc_864(clientOptions);
            var result = await client.WriteAsync(host, "10013", protocol, "ABCDEF");
            Infrastructure.IfTrueError(result.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Failed, "result.Succeeded ");
            Infrastructure.IfTrueError(!String.IsNullOrEmpty(result.Value), "result.Value is not null");
            Infrastructure.IfTrueError(result.Error != SocketErrorStatus.HostNotFound, $"result.Error is wrong ({result.Error})");
        }
        public async Task Test_Bad_Service(CharGenClient_Rfc_864.ProtocolType protocol)
        {
            var host = new HostName("localhost");
            var serverOptions = new CharGenServer_Rfc_864.ServerOptions()
            {
                LoggingLevel = CharGenServer_Rfc_864.ServerOptions.Verbosity.None
            };
            var clientOptions = new CharGenClient_Rfc_864.ClientOptions()
            {
                LoggingLevel = CharGenClient_Rfc_864.ClientOptions.Verbosity.None
            };

            using (var server = new CharGenServer_Rfc_864(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"unable to start server");
                    return;
                }

                var client = new CharGenClient_Rfc_864(clientOptions);
                var result = await client.WriteAsync(host, "79", protocol, "WontCauseAnyTraffic");
                Infrastructure.IfTrueError(result.Succeeded != CharGenClient_Rfc_864.CharGenResult.State.Failed, "result.Succeeded ");
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
        public async Task Test_Stress(CharGenClient_Rfc_864.ProtocolType protocol)
        {
            string pname = protocol.ToString();

            const int NLOOP = 4;
            const int NBUNCH = 10;

            const double ALLOWED_TIME = 20.0; // Normally we expect everything to be fast. No so much for a stress test!
            const int ALLOWED_CONN_RESET = (NLOOP * NBUNCH) * 5 / 100; // Allow 5% conn reset
            const int ALLOWED_EXCEPTION = (NLOOP * NBUNCH) * 5 / 100; // allow 5% exception on server

            int NBytesWrite = 0;
            int NConnReset = 0;
            var host = new HostName("localhost");
            var serverOptions = new CharGenServer_Rfc_864.ServerOptions()
            {
                OutputPattern = CharGenServer_Rfc_864.ServerOptions.PatternType.Fixed,
                LoggingLevel = CharGenServer_Rfc_864.ServerOptions.Verbosity.None,
            };
            var clientOptions = new CharGenClient_Rfc_864.ClientOptions()
            {
                LoggingLevel = CharGenClient_Rfc_864.ClientOptions.Verbosity.None,
            };

            using (var server = new CharGenServer_Rfc_864(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"Client: Stress: {pname} unable to start server");
                    return;
                }
                var expected = CharGenServer_Rfc_864.GetSampleReturn();

                var start = DateTimeOffset.UtcNow;
                for (int bigLoop = 0; bigLoop < NLOOP; bigLoop++)
                {
                    if (bigLoop % 100 == 0 && bigLoop > 0)
                    {
                        Infrastructure.Log($"Client: Stress: {pname} starting loop {bigLoop} of {NLOOP}");
                    }
                    var allClients = new CharGenClient_Rfc_864[NBUNCH];

                    var allWriteTasks = new Task<CharGenClient_Rfc_864.CharGenResult>[NBUNCH];
                    for (int i = 0; i < NBUNCH; i++)
                    {
                        var send = $"ABC-Loop {bigLoop} Item {i}"; // This is sent and ignored by the server.
                        NBytesWrite += send.Length;
                        allClients[i] = new CharGenClient_Rfc_864(clientOptions);
                        allWriteTasks[i] = allClients[i].WriteAsync(host, serverOptions.Service, protocol, send);
                    }
                    await Task.WhenAll(allWriteTasks);

                    await Task.Delay(100); //TODO: magic number

                    // TCP has to wait for the close to happen.
                    Task<CharGenClient_Rfc_864.CharGenResult>[] allCloseTasks = null;
                    if (protocol == CharGenClient_Rfc_864.ProtocolType.Tcp)
                    {
                        allCloseTasks = new Task<CharGenClient_Rfc_864.CharGenResult>[NBUNCH];
                        for (int i = 0; i < NBUNCH; i++)
                        {
                            allCloseTasks[i] = allClients[i].CloseAsync();
                        }

                        await Task.WhenAll(allCloseTasks);
                    }

                    for (int i = 0; i < NBUNCH; i++)
                    {
                        var result = protocol == CharGenClient_Rfc_864.ProtocolType.Tcp
                            ? allCloseTasks[i].Result
                            : allWriteTasks[i].Result;

                        var didSucceed = result.Succeeded == CharGenClient_Rfc_864.CharGenResult.State.Succeeded;
                        if (!didSucceed && result.Error == SocketErrorStatus.ConnectionResetByPeer)
                        {
                            // Connection reset by peer is an error only if we get a bunch of them.
                            NConnReset++;
                            Infrastructure.IfTrueError(NConnReset > ALLOWED_CONN_RESET, $"Too many connection resets {NConnReset}");
                        }
                        else if (!Infrastructure.IfTrueError(!didSucceed, $"!result.Succeeded with error {result.Error.ToString()} for {pname}"))
                        {
                            Infrastructure.IfTrueError(result.Value == null, "result.Value is null");
                            if (result.Value != null)
                            {
                                double nreads = (double)result.Value.Length / (double)expected.Length;
                                int nreadsInt = (int)Math.Round(nreads);
                                Infrastructure.IfTrueError(nreadsInt == 0, $"Got no data back for {pname}");
                                Infrastructure.IfTrueError(nreadsInt != nreads, $"Got partial read {nreads} for {pname}");
                                Infrastructure.IfTrueError(nreads > 5, $"Got {nreads} reads for {pname}");

                                //Infrastructure.IfTrueError(result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60), $"result.Value is weird size ({result.Value.Length}) for {pname}");
                            }
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
                var expectedMinBytes = protocol == CharGenClient_Rfc_864.ProtocolType.Udp ? NBytesWrite : NBytesWrite / 4;
                Infrastructure.IfTrueError(server.Stats.NBytesRead > NBytesWrite, $"Server got {server.Stats.NBytesRead} bytes but expected {NBytesWrite} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NBytesRead < expectedMinBytes, $"Server got {server.Stats.NBytesRead} bytes but expected {NBytesWrite} with a minimum value of {expectedMinBytes} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NExceptions > ALLOWED_EXCEPTION, $"Server got {server.Stats.NExceptions} exceptions but expected no more than {ALLOWED_EXCEPTION} for {pname}");
            }
        }
    }
}
