using Networking.Simplest_Possible_Versions;
using Networking.RFC_Foundational;
using System;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace Networking.RFC_Foundational_Tests
{
    class TimeTest_Rfc_868
    {
        public static async Task Test()
        {
            var start = DateTimeOffset.UtcNow;
            Infrastructure.Log($"Starting test: TimeTest_Rfc_868");
            var testObject = new TimeTest_Rfc_868();
            try
            {
                await testObject.Test_Simplest_Code();

                var protocol = TimeClient_Rfc_868.ProtocolType.Udp;
                await testObject.Test_Good_Path(protocol);
                await testObject.Test_Bad_Host(protocol); // Will print an exception for bad host.
                await testObject.Test_Bad_Service(protocol); // Will print an exception for connection refused.
                await testObject.Test_Stress(protocol);

                protocol = TimeClient_Rfc_868.ProtocolType.Tcp;
                await testObject.Test_Good_Path(protocol);
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
            Infrastructure.Log($"Ending test: TimeTest_Rfc_868  time={delta} seconds");
        }

        public async Task Test_Simplest_Code()
        {
            var result = await Simplest_Time_Sample_Rfc_868.RunAsync();
            if (!Infrastructure.IfTrueError(!result.Succeeded, "!result.Succeeded"))
            {
                Infrastructure.IfTrueError(result.Value == null, "result.Value is null");
                Infrastructure.IfTrueError(result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60), $"result.Value is weird size ({result.Value})");
            }
            Infrastructure.IfTrueError(result.TimeInSeconds < 0, $"result.TimeInSeconds too small ({result.TimeInSeconds})");
            Infrastructure.IfTrueError(result.TimeInSeconds > 1.50, $"result.TimeInSeconds too large ({result.TimeInSeconds})");
        }
        public async Task Test_Good_Path(TimeClient_Rfc_868.ProtocolType protocol)
        {
            var host = new HostName("localhost");
            var serverOptions = new TimeServer_Rfc_868.ServerOptions()
            {
                LoggingLevel = TimeServer_Rfc_868.ServerOptions.Verbosity.None
            };
            var clientOptions = new TimeClient_Rfc_868.ClientOptions()
            {
                LoggingLevel = TimeClient_Rfc_868.ClientOptions.Verbosity.None
            };


            using (var server = new TimeServer_Rfc_868(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"unable to start server");
                    return;
                }

                var client = new TimeClient_Rfc_868(clientOptions);
                var result = await client.SendAsync(host, serverOptions.Service, protocol);
                if (!Infrastructure.IfTrueError(!result.Succeeded, "!result.Succeeded"))
                {
                    Infrastructure.IfTrueError(result.Value == null, "result.Value is null");
                    Infrastructure.IfTrueError(result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60), $"result.Value is weird size ({result.Value})");
                }
                Infrastructure.IfTrueError(result.TimeInSeconds < 0, $"result.TimeInSeconds too small ({result.TimeInSeconds})");
                Infrastructure.IfTrueError(result.TimeInSeconds > 1.50, $"result.TimeInSeconds too large ({result.TimeInSeconds})");
            }
        }

        public async Task Test_Bad_Host(TimeClient_Rfc_868.ProtocolType protocol)
        {
            var host = new HostName("invalid.host.doesnt.exist.example.com");
            var clientOptions = new TimeClient_Rfc_868.ClientOptions()
            {
                LoggingLevel = TimeClient_Rfc_868.ClientOptions.Verbosity.None
            };

            var client = new TimeClient_Rfc_868(clientOptions);
            var result = await client.SendAsync(host, "10013", protocol);
            Infrastructure.IfTrueError(result.Succeeded, "result.Succeeded ");
            Infrastructure.IfTrueError(!String.IsNullOrEmpty(result.Value), "result.Value is not null");
            Infrastructure.IfTrueError(result.Error != SocketErrorStatus.HostNotFound, $"result.Error is wrong ({result.Error})");
        }
        public async Task Test_Bad_Service(TimeClient_Rfc_868.ProtocolType protocol)
        {
            var host = new HostName("localhost");
            var serverOptions = new TimeServer_Rfc_868.ServerOptions()
            {
                LoggingLevel = TimeServer_Rfc_868.ServerOptions.Verbosity.None
            };
            var clientOptions = new TimeClient_Rfc_868.ClientOptions()
            {
                LoggingLevel = TimeClient_Rfc_868.ClientOptions.Verbosity.None
            };

            using (var server = new TimeServer_Rfc_868(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"unable to start server");
                    return;
                }

                var client = new TimeClient_Rfc_868(clientOptions);
                var result = await client.SendAsync(host, "79", protocol);
                Infrastructure.IfTrueError(result.Succeeded, "result.Succeeded ");
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
        public async Task Test_Stress(TimeClient_Rfc_868.ProtocolType protocol)
        {
            string pname = protocol.ToString();

            const int NLOOP = 10;
            const int NBUNCH = 50;

            const double ALLOWED_TIME = 20.0; // Normally we expect everything to be fast. No so much for a stress test!
            const int ALLOWED_CONN_RESET = (NLOOP * NBUNCH) * 5 / 100; // Allow 5% conn reset

            int NBytesSent = 0; // In the Time protocol we never send any bytes, so this will stay at zero.
            int NConnReset = 0;
            var host = new HostName("localhost");
            var serverOptions = new TimeServer_Rfc_868.ServerOptions()
            {
                LoggingLevel = TimeServer_Rfc_868.ServerOptions.Verbosity.None,
            };
            var clientOptions = new TimeClient_Rfc_868.ClientOptions()
            {
                LoggingLevel = TimeClient_Rfc_868.ClientOptions.Verbosity.None,
            };

            using (var server = new TimeServer_Rfc_868(serverOptions))
            {
                bool serverOk = await server.StartAsync();
                if (!serverOk)
                {
                    Infrastructure.Error($"Client: Stress: {pname} unable to start server");
                    return;
                }

                var start = DateTimeOffset.UtcNow;
                var client = new TimeClient_Rfc_868(clientOptions);
                for (int i = 0; i < NLOOP; i++)
                {
                    if (i % 100 == 0 && i > 0)
                    {
                        Infrastructure.Log($"Client: Stress: {pname} starting loop {i} of {NLOOP}");
                    }
                    var allTasks = new Task<TimeClient_Rfc_868.TimeResult>[NBUNCH];
                    for (int j = 0; j < allTasks.Length; j++)
                    {
                        allTasks[j] = client.SendAsync(host, serverOptions.Service, protocol);
                    }
                    await Task.WhenAll(allTasks);

                    for (int j = 0; j < allTasks.Length; j++)
                    {
                        var result = allTasks[j].Result;
                        if (!result.Succeeded && result.Error == SocketErrorStatus.ConnectionResetByPeer)
                        {
                            // Connection reset by peer is an error only if we get a bunch of them.
                            NConnReset++;
                            Infrastructure.IfTrueError(NConnReset > ALLOWED_CONN_RESET, $"Too many connection resets {NConnReset}");
                        }
                        else if (!Infrastructure.IfTrueError(!result.Succeeded, $"!result.Succeeded with error {result.Error.ToString()} for {pname}"))
                        {
                            Infrastructure.IfTrueError(result.Value == null, "result.Value is null");
                            if (result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60))
                            {
                                ;
                            }
                            Infrastructure.IfTrueError(result.Value != null && (result.Value.Length < 10 || result.Value.Length > 60), $"result.Value is weird size ({result.Value.Length}) for {pname}");

                            var correctTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var correctDelta = Math.Abs(result.UnixTimeSeconds - correctTime);
                            Infrastructure.IfTrueError(correctDelta > 5, $"Time is off; got {result.UnixTimeSeconds} but expected about {correctTime} delta is {correctDelta}");
                        }
                        Infrastructure.IfTrueError(result.TimeInSeconds < 0, $"result.TimeInSeconds too small {result.TimeInSeconds} for {pname}");
                        Infrastructure.IfTrueError(result.TimeInSeconds > ALLOWED_TIME, $"result.TimeInSeconds too large {result.TimeInSeconds} for {pname}");
                    }

                    await Task.Delay(300); //TODO: pause just to make the test runs easier to figure out
                }
                // timing data
                var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
                double timePerCall = delta / (double)(NLOOP * NBUNCH);
                Infrastructure.Log($"Stress test average time: {NLOOP} {NBUNCH} {timePerCall} for {pname}");


                // How's the server doing?
                const int ExpectedCount = NLOOP * NBUNCH;
                Infrastructure.IfTrueError(server.Stats.NConnections != ExpectedCount, $"Server got {server.Stats.NConnections} connections but expected {ExpectedCount} for {pname}");
                int nr = server.Stats.NResponses;
                Infrastructure.IfTrueError(server.Stats.NResponses != ExpectedCount, $"Server sent {server.Stats.NResponses} responses but expected {ExpectedCount} for {pname}");

                // Why is the expected not equal to the NBytesSent? Because TCP has a weird timing thing:
                // the server has to close down reading from the client relatively quickly and can't waste
                // time for the client to send bytes that probably won't come.
                var expectedMinBytes = protocol == TimeClient_Rfc_868.ProtocolType.Udp ? NBytesSent : NBytesSent / 2;
                Infrastructure.IfTrueError(server.Stats.NBytes > NBytesSent, $"Server got {server.Stats.NBytes} bytes but expected {NBytesSent} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NBytes < expectedMinBytes, $"Server got {server.Stats.NBytes} bytes but expected {NBytesSent} with a minimum value of {expectedMinBytes} for {pname}");
                Infrastructure.IfTrueError(server.Stats.NExceptions != 0, $"Server got {server.Stats.NExceptions} exceptions but expected {0} for {pname}");
            }
        }
    }
}
