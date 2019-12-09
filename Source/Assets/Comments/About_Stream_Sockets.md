# About Stream Sockets in WinRT

TCP sockets (StreamSockets) are a little more finicky than you might think. The ideal is that a stream socket is "just" a stream; you open it, send bytes, read bytes, and eventually close the thing.

Some of the hidden surprises:

1. You want the socket to end up with a graceful close. In the WinRT world, this means that you can't have any unread data; unread data == failure and failure == connection reset by peer. The value to no connection resets is that connetion resets can show up as errors in the server logs, and your IT admins will be unhappy.
2. You can't really tell when your 