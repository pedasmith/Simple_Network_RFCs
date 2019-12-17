# About Datagram Sockets (UDP)

## Weird messages on MessageReceived

You would think that every time you get a MessageReceived event on a UDP socket that
there were certain unalterable parts of the argument contract. In failure conditions 
like a service (port) not being available, this is not true.

In particular, when you try to send to e.g. localhost in this sample but you deliberately 
send to the wrong service, you will get a bounce message. None of the args will be anything
you can examine!