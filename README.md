# Simple Network RFCs

Simple implementations of classic request for comments (RFCs) like ECHO (RFC 867) and Daytime (RFC 867). 

The code demonstrates
* using streams and datareader and datawriter
* closing down stream sockets without the Connection Reset by Peer problem
* sending empty UDP packets

## Links

* Comments on [Stream Sockets](Source/Assets/Comments/About_Stream_Sockets.md)
* Comments on [Datagram Sockets](Source/Assets/Comments/About_Datagram_Sockets.md)
* Snarky [Daytime](Source/Assets/Comments/About_Daytime_Protocol_Rfc_867.md) comments
* Snarky [Echo](Source/Assets/Comments/About_Daytime_Protocol_Rfc_867.md) comments

## Code organization

The foundational code for each protocol is in the **RFC_Foundational** directory. This is the only directory with code that directly uses the C# UWP WinRt StreamSocket and DatagramSocket. Each RFC is implemented as a server and as a client in seperate classes; each class includes both TCP and UDP implementations

Each protocol includes tests in the **RFC_Foundational_Tests** directory. These tests don't rely on any particular testing framework. You can run all of the tests straight from the UI

Each protocol includes samples in **Simplest_Possible_Version**; these demonstrate using the foundational code

Each protocol includes a simple (and, sorry, ugly) UWP UI in the **RFC_UI_UWP** directory.
