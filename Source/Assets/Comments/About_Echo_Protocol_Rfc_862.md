# About the Echo Protocol 867
The Echo protocol is reasonably simple: you send data, and as long as you send data, the server will echo back exactly what you sent. The primary use case, of couse, is for an interactive session. As you get each packet back, simply display it.

The hard part is setting up an API that makes sense for interactive use (hint: I use an event to signal that the server has sent something) and can also be used for non-interactive testing. Oh, and the API should make sense for UDP (no close needed) and for TCP (need a close)

The solution: make a client, and call WriteAsync(). If there is no socket open, open a socket; otherwise use the existing socket even if the address and service (port) are changed. For UDP, wait for the reply, and bundle the reply into the overall response. For TCP, what you get back from each WriteAsync is an intermediate result; to get the final result with an appendage of all replies, you have to call CloseAsync().

# Sample code
This is the simplest possible code for writing TCP. For UDP, the return from WriteAsync is the complete result.

            using (var server = new EchoServer_Rfc_862())
            {
                await server.StartAsync();

                var client = new EchoClient_Rfc_862();
                // You only get the echo'd result from TCP with the final CloseAsync()
                // But for UDP  you get the returned packet.
                var partialResult = await client.WriteAsync(
                    new Windows.Networking.HostName("localhost"),
                    server.Options.Service,
                    EchoClient_Rfc_862.ProtocolType.Tcp,
                    "Hello, echo server!");
                var completeResult = await client.CloseAsync(); // Close is essential for TCP. 
                return completeResult;
            }

# Summary

* RFC [862](https://tools.ietf.org/html/rfc862) with no errata 
* Port **7**
* Input = Infinite & Used; Output = Infinite & Used; Closed-By = client
