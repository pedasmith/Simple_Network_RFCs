# About the Daytime Protocol 867

The Daytime protocol (RFC 867) is one of the "wish they hadn't" kind of specs. On the surface, it is a simple spec in TCP: the client opens a connection, optionally sends some bytes, and the server just sends back a string in whatever format and closes the connection.

See the problem? The server can't close the connection until it has completely drained the incoming bytes. Likewise, the client cannot close the connection until the server has sent  all the bytes. Each is edlessly waiting for the other. 

In theory, a bad client could trickle down a slow set of bytes over the course of days, and the server is supposed to just keep the connection open, reading the bytes. Similarly, a bad server could write part of the response and then keep the connection open, and the client has no choice but to wait for the never-ending stream of bytes to actually finish.

The UDP version, on the other hand, is essentially trivial: the server reads in a single packet and write a single packet back.

In my verison, I have timeouts for both the client and server, set to "reasonable" values.

The simplest possible code for the Daytime classes looks like this:

            var server = new DaytimeServer_Rfc_867();
            await server.StartAsync();

            var client = new DaytimeClient_Rfc_867();
            var result = await client.SendAsync(new Windows.Networking.HostName("localhost"));

            return result;
            
Changes from the spec: in my code, the default service (port) is 10013. This change is done simply so that the server will work with WinRT. A WinRT network program is not able to create a server on any of the restricted ports.

