# SimpleEcho_Rfc_862

A simple UWP ECHO server that supports some set of the RFC 862 requirements. The project is a combined server 
and client; the server when run will create a listener socket (TCP only in V1) listening on port 10007 (not 7).

The client can connect to any ECHO server anywhere.

In V1, the output is only to the OUTPUT message area in the debugger. Sorry, that's just how it goes :-)
