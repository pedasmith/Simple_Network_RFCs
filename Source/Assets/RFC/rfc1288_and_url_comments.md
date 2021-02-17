# Comments on the FINGER RFC 1288 and the FINGER URL scheme

## Links
The FINGER RFC is [RFC 1288](https://tools.ietf.org/html/rfc1288). The [Finger://example.com/user](Finger://example.com/user) 
scheme is [registered here](https://www.iana.org/assignments/uri-schemes/prov/finger) and the [draft specification](https://tools.ietf.org/html/draft-ietf-uri-url-finger-03) is here.


## Comments about the FINGER spec

The Finger spec had a spasm of pretend precision. The BNF is this hard-to-read "spec":

````
   The Finger query specification is defined:

        {Q1}    ::= [{W}|{W}{S}{U}]{C}

        {Q2}    ::= [{W}{S}][{U}]{H}{C}

        {U}     ::= username

        {H}     ::= @hostname | @hostname{H}

        {W}     ::= /W

        {S}     ::= <SP> | <SP>{S}

        {C}     ::= <CRLF>
````

Problems with this are:

1. The Q1 and Q2 are given numbers instead of simply naming them for what they are: direct queries and (Q1) and networked queries to be sent to some other computer.
2. The spec has a horror of simply including simple terminals -- instead of writing in the <CRLF> straight, it's given C as a short and meaningless name

My rewrite of the rules without changing how they work:
````
        {DirectQuery}    ::= [/W|/W<SP>+{username}]<CRLF>
        {NetworkQuery}   ::= [/W<SP>+][{username}]{@hostname}+<CRLF>
````

Arguably this should get one more change. There's not really a good reason to jam the "list all users" case into the "list this one users".

````
        {DirectQuery}    ::= /W<SP>+{username}<CRLF>
        {AllUsersQuery}  ::= [/W]<CRLF>
        {NetworkQuery}   ::= [/W<SP>+][{username}]{@hostname}+<CRLF>
````
Note that for direct queries, the /W is defined to be non-optional. This is 100% contrary to the text and every single implementation. It's 
probably a bad idea that the AllUsersQuery can have a /W but not /W followed by spaces. And in modern terms, we would allow any kind
of white space, not just the one space character.


## Bugs in the specs

### /W should always be optional
The direct query {Q1} is given as ```[{W}|{W}{S}{U}]{C}```. This is incorrect; the \W  {W} is optional for both global queries (e.g., just a CRLF)
and for specific queries (e.g., username<CRLF>). For example, ```person<CRLF>``` should be a valid query, but isn't 

Alas, because the finger proposal is long obsolete, there is no way to file an errata.

## Earlier spec: RFC 742

Link to [RFC 742 NAME/FINGER](https://tools.ietf.org/rfc/rfc742.txt)
The earlier RFC 742 simply sends a single line to a remote system, and prints the results. It's sheer happenstance that the 
actual request is for a "user" and the result is their "status".

Notable in the old spec: they were very loosy-goosy, anything goes. In particular, if a /W appears anywhere [sic] in the input line, 
and the command is sent to a system running the Incompatible Time-Sharing System [ITS](https://en.wikipedia.org/wiki/Incompatible_Timesharing_System) server OS,
then WHOIS type output was produced.

The user could be multiple uses, seperated by commas, on some systems.

# Github repo's for FINGER

## ajensenwaud/finger-weather-js
Link to [ajensenwaud/finger-weather-js](https://github.com/ajensenwaud/finger-weather-js)
Super fun! do a "finger location" and it uses Yahoo weather to get the weather from location!
Server side of a specialized Finger server
Implementation language: **JavaScript** (node.js)
Forwarding: no
/W handling: /W is not handled in any way

## Comments
1. Super fun! this is the first Finger that seems kind of useful!
2. weatherserver.js line 24: the address is just localhost



## astrohart/Inetnav
Link to [astrohart/Inetnav](https://github.com/astrohart/Inetnav)
Credit to Building Internet Applications with Visual C++ by Kate Gregory, Paul Robichaux, Brady Merkel, and Markus Pope (Que Corporation, 1995)
Client side of the finger protocol
Implementation language: **C**


## dlitz/fingerd

Link to [dlitz/fingerd](https://github.com/dlitz/lfingerd)
Server side of finger. "lfingerd is a simple Finger User Information Protocol (RFC 1288) server,
written in Python, that allows each individual user to have full control over
what is returned."
Implementation language: **python**
Forwarding: **no** (specifically banned)
/W handling: /W is stripped 

## Comments

1. Will fail to handle user names with spaces
2. Nice: users can include ".nofinger" files and they won't be fingered.
3. Reads/writes stdin/stdout instead of going over network

## Special users

1. lfingerd-version
2. lfingerd-copyright




## earoland/Finger_Protocol_Example  (classroom assignment)
Link to [earoland/Finger_Protocol_Example](https://github.com/earoland/Finger_Protocol_Example)
Client side of the Finger protocol
Implementation language: **Java**
As a class assignment, not interesting for understanding Finger usage.



## HouseMommy/Broken_Fingerz (like a classroom assignment)
Link to [HouseMommy/Broken_Fingerz](https://github.com/HouseMommy/Broken_Fingerz)
Code is a mostly-empty repo with no actual finger code.



## jonroig/finger.farm

Link to [jonroig/finger.farm](https://github.com/jonroig/finger.farm)
Server side of finger with a REST admin interface
Implementation language: *javascript** (nodejs)
Forwarding:
/W handling: /W is stripped out and not replaced (so that users then start with space)

## Comments

Main code is in lib/fingerserver.js
1. The cleanUserName function (fingerserver.js line 22) simply replaces /W with '' which means the resulting name still has spaces
2. The main index page handling (fingerserver.js line 48) completely doesn't handle /W
3. Default port is **7979** instead of the more normal 79

## Special users

1. help
2. about
3. info
4. finger



## J-o-s-h-S-i-m-s/The-Finger-Protocol/  (classroom assignment)
Link to [J-o-s-h-S-i-m-s/The-Finger-Protocol/](https://github.com/J-o-s-h-S-i-m-s/The-Finger-Protocol)
Client side of the Finger protocol
Implementation language: **Java**
As a class assignment, not interesting for understanding Finger usage.




## KWMalik/finger (incomplete)
Link to [KWMalik/finger](https://github.com/KWMalik/finger)
Client side of finger
Implementation Language: C
Code is not complete and does not actually perform FINGER socket. As of 2021, it was last updated 9 years prior (2012)

## leshow/finger-tokio

Link to [leshow/finger-tokio](https://github.com/leshow/finger-tokio)
Server side of finger for a class
Implementation language: **rust** 
Forwarding: can parse out the @ stuff, but then doesn't forward the requests? (proto.rs line 167)
/W handling: not handled at all (AFAICT)

## Comments

1. In proto.rs line 16, the hostname field in the FingerRequest seems to be a single hostname. The spec says that it can be multiple hostnames (e.g., user@host@otherhost@thirdhost)
2. In proto.rs line 7, the delim is a \n char. Finger ends with CRLF so the correct delim would be a \r\n
3. In proto.rs line 81, the /W switch should be handled but isn't



## lindsayevans/node-finger-server/
Link to [lindsayevans/node-finger-server/](https://github.com/lindsayevans/node-finger-server/blob/master/finger_server.js)
Server implementation of the Finger protocol
Implementation language: **JavaScript** (node)
Forwarding: yes!
/W handling: parsed but not handled (is either DENIED or NOT IMPLEMENTED based on an allow-recursive flag)

## Comments
1. at finger_server.js:line 8 is the request match. It's arguably wrong because it's pretty restrictive about user and hosts
2. at finger_server.js:line 50 the socket is set to utf8. This is probably the best choice


## MasterQ32/kristall

Link to [MasterQ32/kristall](https://github.com/MasterQ32/kristall)
A multi-protocol app for windows and mac that supports the new Gemini format along with finger and gopher
Most code is in src/protocols in the fingerclient.cpp file
Client side of finger for a full portable GUI app using the Qt library
Implementation language: **C++**
Forwarding: not support from the client
/W handling: no way to send a /W in the command

## Comments

1. In fingerclient.cpp line 43, the requested_user is set directly from the url.userName. This is incorrect and contrary to the protocol; the sent data is supposed to be from the url path
2. There isn't any way to send a /W switch



## matthewp/finger.js
Link to [matthewp/finger.js](https://github.com/matthewp/finger.js)
Server side of finger
Implementation language: **JavaScript** (node.js?)
Forwarding: none
/W handling: not handled at all (AFAICT)

## Comments
1. In src/Request.js the request is parsed. But it doesn't handle the /W switch


## mitchellh/go-finger
Link to [mitchellh/go-finger](https://github.com/mitchellh/go-finger)
Client and server side of finger. "go-finger is a finger library written in Go. This contains both a client and server implementation."
Implementation language: **go**
Forwarding: parsed and it's up to the handler function to handle
/W handling: not handled? 

## Comments
1. Looking at the regexp in query.go, it doesn't look like /W is handled
2. Looking at the regexp in query.go, user names are restricted to 0-9A-Za-z and -. But the inger spec allows almost any char including 8bit ones (albeit with no character encoding specified)
3. Although the docs talk about a client, I don't see on in the code
4. Forwarding is clumsy: forwarding is parsed, but then the call to the handler function will almost certainly never handle it correctly.



## mcroydon/phalanges
Link to [mcroydon/phalanges](https://github.com/mcroydon/phalanges)
**Warning**: this is for RFC 742 Finger -- aka without the /W switch
Phalanges is latin (or maybe greek) for fingers. See also [this](https://www.youtube.com/watch?v=9UZRHJakNEA) at about 2:40
Code is very complete (build system, logging, rate limiting...)
Main code is in **src/main/scale/com/postneo/phalanges** and FingerHandler.scala in particular.
Implementation language: **scala** (Java)
Forwarding:
/W handling: nope, because this is a RFC 742 implementation



## morganjweaver/proj1_5510
Link to [morganjweaver/proj1_5510](https://github.com/morganjweaver/proj1_5510)
Class project for a finger client and server
Implementation language: **c**
Forwarding: no
/W handling: nope



## Net-Finger-Server
Link to [Net-Finger-Server](https://metacpan.org/release/Net-Finger-Server/source/lib/Net/Finger/Server.pm)
This is a MetaCPAN file for the Finger server
Implementation language: **Perl**
Forwarding: seemingly parsed but not handled
/W handling: handled correctly!



## OS2World/APP-INTERNET-NetGrab
Link to [OS2World/APP-INTERNET-NetGrab](https://github.com/OS2World/APP-INTERNET-NetGrab)
A published finger client app for OS/2! Can be used for finger, gopher, time, http, and more
The main code is in the very large netgrab.c; finger is the routine handle_finger, netgrab.c:line 714
Implementation language: **C**
Forwarding: not supported
/W handling: not supported

### Comments
1. netgrab.c line 718: the user is placed in a string %s\n\r aka LF then CR. It should be the other way around: \r\n



## pflugkf/Finger (trivial implementation for a course)
Link to [pflugkf/Finger](https://github.com/pflugkf/Finger)
Client side of finger
Implementation language: **Java**
Was created for a course; is a trivial client side implementation. As of 2021, was last updated 4 years ago (2017)



## pkrul/afinger client
Link to [pkrul/afinger](https://github.com/pkrul/afinger)
Main source is in afinger file which is a perl program. 
Implementation language: **perl**
Forwarding: no
/W switch: not supported

This is a slightly odd program: it's both just a plain command-line program you can run and also the program that a network deamon will call 
when a finger command is received over port 79.



## softwareguycoder/QFinger
Link to [softwareguycoder/QFinger](https://github.com/softwareguycoder/QFinger)
Client side finger program for WinSock
Implementation language: **C**
Forwarding: n/a
/W handling: n/a

### Comments
1. Winsock 1.1? Seriously? Winsock 2 was shipped in the fricken 1990s.
2. Nothing but A (ascii) methods? All windows is fully unicode aware by now
3. hostent is long deprecated
4. the query is pre-set to always be bchart
5. qfinger.c line 248: nothing prevents a bufffer overrun



## SpamapS/dedo (no code)
Link to [SpamapS/dedo](https://github.com/SpamapS/dedo)
Is just two files of an aspirational side-project



## rahman1996/ITT440-Finger-Client-Server
## syahiranchesuliman/ITT440-Finger-Client-Server
Link to [syahiranchesuliman/ITT440-Finger-Client-Server](https://github.com/syahiranchesuliman/ITT440-Finger-Client-Server)
Is seemingly a class project. Doesn't actually return user data
Client and server sides of 
Implementation language: **c** for both client and server plus a server in **Java**
Forwarding: not implementated
/W switch: not supported

### Comments
1. Uses #include <winsock.h> instead of the more modern <winsock2.h> which has been the standard since the 90s.

## spoike/fingerer

Link to [spoike/fingerer](https://github.com/spoike/fingerer)
Server side of finger. "The Super Simple Finger Protocol (RFC 1288) Application Framework"
Implementation language: **javascript** (nodejs)
Forwarding: **no** (not handled specially)
/W handling: /W is stripped but it seems like the space isn't removed (so user is matched but /W user will be matched as " user")

### Comments

Primary file is lib/index.js

1. Will fail to handle a legit user with a /W in their name. Query for ```/W user/W``` will fail because all /W are removed.
2. It looks like /W is removed but the following spaces are not stripped.
3. The cleanData function strips out all CRLF so that a two-line query ```userCRLFname``` will query for ```username``` instead of just ```user``` 
4. Listens on localhost (127.0.0.1) instead of on all adapters. Doesn't handle IPv6.




## the-mandarine/fingertoe
Link to [the-mandarine/fingertoe](https://github.com/the-mandarine/fingertoe)
Server side of Finger
Implementation language: **Python** (python3, of course)
Forwarding: nope
/W handling: none. The /W is assumed to be part of the user's name

### Comments
Is a simple python program. Main code is just fingertoed.py.

1. Uses port 7979 (but notice that in ferm.rules there's some kind of NAT thing to forward from port 79?)
2. In a major security violation, will accept user names like ../../../root/all_my_secrets and will attempt to open that plan file
3. Maximum user name size is 1024 or so char, which is probably OK



## tristan-weil/ttserver
Link to [tristan-weil/ttserver](https://github.com/tristan-weil/ttserver/tree/master/server/handler/finger)
An ambitious concept: handle all of the text-like internet protocols. Supports finger and gopher.
Server side of finger
Implementation language: **go**

Is a dup of mitchellh/go-finger (or that's a dup of this).