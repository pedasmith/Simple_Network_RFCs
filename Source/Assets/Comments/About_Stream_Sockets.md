# About Stream Sockets in WinRT

TCP sockets (StreamSockets) are a little more finicky than you might think. The ideal is that a stream socket is "just" a stream; you open it, write bytes, read bytes, and eventually close the thing.

Some of the hidden surprises:

1. You want the socket to end up with a graceful close. In the WinRT world, this means that you can't have any unread data; unread data == failure and failure == connection reset by peer. The value to no connection resets is that connetion resets can show up as errors in the server logs, and your IT admins will be unhappy.
2. You can't really tell when your 

## Good Exceptions, bad exceptions

TCP Sockets can either be closed *gracefull* (good) or *forcefully* (bad). Forcefully also shows up as *reset*. Reset is bad mostly because of server logs: a graceful shutdown is just a shutdown. A forceful shutdown will show up in the IT Admin dashboard.

### Good Exceptions
The object has been closed. (Exception from HRESULT: 0x80000013)

The ECHO server gets this on the client side when you press Close. It's thrown by the DataReader when the TCP socket is tcpSocket.Disposed (aka, closed).

### Bad Exception
An existing connection was forcibly closed by the remote host. (Exception from HRESULT: 0x80072746).

Common cause of this: trying to write to a socket which is in the process of shutting down. For example, 
if you look at the Character Generator (CharGen) code, the Close button will close and flush the dataWriter 
and then streamSocket.Dispose the socket. This starts the close process on the socket; the CharGen server's
reader will stop reading with 0 bytes. This in turn triggers the main loop to cancel the writer; it will
almost certainly stop the Task.Delay() loop. 

If that write loop instead had continued to try to write, the socket would be foracbly closed, and the **client** 
would get an exception on its reader with forceful close. This is, of course, bad.

### Frustrating exceptions

#### A task was canceled. 8013153B

Sigh. SIGH. Network code has lots of polling loops and read/write timeout values; a common way to handle at least
some of these is with an
    await Task.Delay (timeMillisecond, cancellationToken);

when the task is cancelled, instead of being nice and just returning, the Task.Delay throws an exception which
you have to catch and almost certainly simply ignore.

#### The I/O operation has been aborted because of either a thread exit or an application request. 0x800703E3

This is what you get when you are reading from a datareader on a TCP socket, and then close the socket.

