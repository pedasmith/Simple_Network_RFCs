using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace Networking.RFC_PortScan
{
    public class HostNameOptions
    {
        public enum HostType {  IP4 };
        public HostType GenerateType { get; set; } = HostType.IP4;
    }
    public class HostNameGeneratorState
    {
        public Random r = new Random();
    }
    public static class GenerateHostName
    {
        public static HostName Generate(HostNameOptions options, ref HostNameGeneratorState state)
        {
            if (state == null) state = new HostNameGeneratorState();

            byte b1 = (byte)state.r.Next(0, 255);
            byte b2 = (byte)state.r.Next(0, 255);
            byte b3 = (byte)state.r.Next(0, 255);
            byte b4 = (byte)state.r.Next(0, 255);
            var name = $"{b1}.{b2}.{b3}.{b4}";
            var retval = new HostName(name);
            return retval;
        }
    }
}
