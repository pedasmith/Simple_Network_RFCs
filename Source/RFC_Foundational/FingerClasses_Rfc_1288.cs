using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace Networking.RFC_Foundational
{
    /// <summary>
    /// Correctly parsed Finger request. Some fields are send-only.
    /// </summary>
    public class ParsedFingerCommand
    {
        private ParsedFingerCommand()
        {
        }
        private ParsedFingerCommand(HostName host, string user, bool whoIsMode)
        {
            SendToHost = host;
            User = user;
            HasWSwitch = whoIsMode;
        }

        public enum CommandType { Error, ListAll, ListUser, ListRemote };
        public CommandType FingerCommand { get; set; } = CommandType.Error;
        public bool HasWSwitch { get; set; } = false; // The /W in front of the command
        public string WSwitchText { get { return HasWSwitch ? "/W" : null; } }

        public string User { get; set; } = "";
        public HostName SendToHost { get; set; }
        public string SendToPort { get; set; } = DefaultService;
        public const string DefaultService = "79";
        public string[] ReceivedHostList { get; set; } = null;
        public string OriginalCommand { get; set; } = null;

        /// <summary>
        /// Parse network finger command input. Will handle /W and remote commands. Assumes that the input is already trimmed
        /// correctly (e.g., it's a single input line).
        /// </summary>
        /// <param name="commandFromNetwork"></param>
        /// <returns></returns>
        public static ParsedFingerCommand ParseFromNetwork(string commandFromNetwork)
        {
            ParsedFingerCommand retval = null;
            bool hasWSwitch = commandFromNetwork.StartsWith("/W");
            var command = (hasWSwitch ? commandFromNetwork.Substring(2) : commandFromNetwork).Trim();
            // Remove extra whitespace chars in order to be more forgiving of bad input.
            if (command == "")
            {
                retval = new ParsedFingerCommand()
                {
                    FingerCommand = CommandType.ListUser,
                    HasWSwitch = hasWSwitch,
                    OriginalCommand = commandFromNetwork,
                };
            }
            else if (!command.Contains('@'))
            {
                retval = new ParsedFingerCommand()
                {
                    FingerCommand = CommandType.ListUser,
                    HasWSwitch = hasWSwitch,
                    User = command,
                    OriginalCommand = commandFromNetwork,
                };
            }
            else
            {
                var split = command.Split(new char[] { '@' });
                var hostlist = new String[split.Length - 1];
                Array.Copy(split, 1, hostlist, 0, hostlist.Length);
                retval = new ParsedFingerCommand()
                {
                    FingerCommand = CommandType.ListRemote,
                    HasWSwitch = hasWSwitch,
                    User = split[0],
                    ReceivedHostList = hostlist,
                    OriginalCommand = commandFromNetwork,
                };
            }

            if (retval == null)
            {
                retval = new ParsedFingerCommand() { FingerCommand = CommandType.Error, OriginalCommand = commandFromNetwork };
            }
            return retval;
        }
        /// <summary>
        /// Top-level parser for user interfaces. Can parse both @ style string (user@example.com) and finger urls.
        /// You can provide default values for port and the /W switch; these will be used instead of the system defaults
        /// when the format can't include them (e.g., there's no way to specify /W for user@example.com. The normal
        /// system default is false (no /W), but you can override that as needed.
        /// </summary>
        /// <param name="userString"></param>
        /// <param name="defaultService"></param>
        /// <param name="defaultWswitch"></param>
        /// <returns></returns>

        public static ParsedFingerCommand ParseFromUxString(string userString, string defaultService = null, bool defaultWswitch = false)
        {
            Uri uri;
            bool isUri = Uri.TryCreate(userString, UriKind.Absolute, out uri);
            ParsedFingerCommand request = null;
            if (isUri)
            {
                request = ParsedFingerCommand.ParseFromUri(uri);
                if (uri.IsDefaultPort)
                {
                    request.SendToPort = defaultService;
                }
            }
            else
            {
                request = ParsedFingerCommand.ParseFromAtString(userString, defaultWswitch);
                request.SendToPort = defaultService;
            }
            return request;
        }

        /// <summary>
        /// Converts input like user@example.com or @example.com or example.com into a request.
        /// Can also handle user@sub.example.com example.com in which case user=user@sub.example.coma dnd host=example.com
        /// Does not handle uri (see FromUri for that)
        /// </summary>
        /// <param name="address"></param>
        /// <param name="wswitch"></param>
        /// <returns></returns>
        private static ParsedFingerCommand ParseFromAtString(String address, bool wswitch = false)
        {
            ParsedFingerCommand request = null;
            if (address.Contains(' '))
            {
                // is of the form user@sub.example.com@subB.example.com example.com
                // where the request goes to example.com and the "user" is the long string with @ signs.
                var userSplit = address.Split(new char[] { ' ' }, 2);
                var user = userSplit[0];
                var host = new HostName(userSplit[1]);
                request = new ParsedFingerCommand(host, user, wswitch);
            }
            else
            {
                var userSplit = address.Split(new char[] { '@' }, 2);
                switch (userSplit.Length)
                {
                    case 1:
                        {
                            var host = new HostName(userSplit[0]);
                            request = new ParsedFingerCommand(host, "", wswitch);
                            break;
                        }
                    case 2:
                        {
                            var user = userSplit[0];
                            var host = new HostName(userSplit[1]);
                            request = new ParsedFingerCommand(host, user, wswitch);
                            break;
                        }
                }
            }
            return request;
        }
        public static ParsedFingerCommand ParseFromUri(Uri uri)
        {
            // See: https://tools.ietf.org/html/draft-ietf-uri-url-finger-03
            // finger://host[:port][/<request>]
            // examples from spec: finger://space.mit.edu/nasanews finger://status.nlak.net
            if (uri.Scheme.ToLowerInvariant() != "finger")
            {
                throw new Exception($"ERROR: FingerRequest requires URI that starts with finger://");
            }
            // The LocalPath will be //W user for urls like finger://example.com//W%20user
            bool isW = false;
            string user = uri.LocalPath;
            string port = DefaultService;
            if (uri.LocalPath.StartsWith("//W"))
            {
                user = uri.LocalPath.Substring(3).Trim();
                isW = true;
            }
            else if (uri.LocalPath.StartsWith("/"))
            {
                user = uri.LocalPath.Substring(1);
            }
            // technical violation of the url spec: I allow finger://user@example.com and finger://user@example.com//W
            // In both cases, the user will be the one before the @sign/
            // If there's both a real path AND an user@ given, the user@ will take priority.
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                user = uri.UserInfo;
            }
            if (!uri.IsDefaultPort)
            {
                port = uri.Port.ToString(); // is -1 by default for finger://
            }
            var retval = new ParsedFingerCommand(new HostName(uri.Host), user, isW);
            retval.SendToPort = port;
            retval.OriginalCommand = uri.OriginalString;
            return retval;
        }


        public string ToDebugString()
        {
            var hoststr = "(no host list)";
            if (ReceivedHostList != null && ReceivedHostList.Length > 0)
            {
                hoststr = "";
                foreach (var host in ReceivedHostList)
                {
                    if (hoststr != "") hoststr += ",";
                    hoststr += host;
                }
            }
            return $"type={FingerCommand} wswitch={HasWSwitch} user={User} hostlist={hoststr}";
        }

        public override string ToString()
        {
            var wswitchSpace = HasWSwitch && !string.IsNullOrEmpty(User) ? " " : "";
            var data = WSwitchText + wswitchSpace + User + "\r\n";
            return data;
        }

        public string ToStringAtFormat()
        {
            string retval = string.IsNullOrEmpty(User) ? "" : User;
            retval += "@";
            retval += SendToHost.CanonicalName;
            return retval;
        }

        public string ToReceivedNetworkCommand()
        {
            var wswitch = HasWSwitch ? "/W " : "";
            var hoststr = "";
            if (ReceivedHostList != null && ReceivedHostList.Length > 0)
            {
                foreach (var host in ReceivedHostList)
                {
                    hoststr += "@" + host;
                }
            }
            return $"{wswitch}{User}{hoststr}\r\n";
        }
    }
}
