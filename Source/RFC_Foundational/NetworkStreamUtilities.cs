using Networking.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    static class NetworkStreamUtilities
    {
        public class DrainStreamResult
        {
            public uint NBytesRead = 0;
            public int NExceptions = 0;
            public string LogText = "";
        }
        public static async Task<DrainStreamResult> DrainStream(StreamSocket tcpSocket, int tcpReadTimeInMilliseconds)
        {
            var retval = new DrainStreamResult();
            var s = tcpSocket.InputStream;
            var buffer = new Windows.Storage.Streams.Buffer(2048);

            string stringresult = "";
            var keepGoing = tcpReadTimeInMilliseconds >= 0; // Read time is negative? Then don't read at all!
            while (keepGoing)
            {
                try
                {
                    var readTask = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                    var taskList = new Task[]
                    {
                        readTask.AsTask(),
                        Task.Delay (tcpReadTimeInMilliseconds),
                    };
                    var waitResult = await Task.WhenAny(taskList);
                    if (waitResult == taskList[0])
                    {
                        var result = readTask.GetResults();
                        retval.NBytesRead += result.Length;
                        var partialresult = BufferToString.ToString(result);
                        stringresult += partialresult;
                        retval.LogText += $"Got data from client: {stringresult} Length={result.Length}\n";
                    }
                    else
                    {
                        keepGoing = false;
                    }
                }
                catch (Exception ex2)
                {
                    retval.NExceptions++;
                    keepGoing = false;
                    retval.LogText += $"EXCEPTION while reading: {ex2.Message} {ex2.HResult:X}\n";
                }
            }
            return retval;
        }
    }
}
