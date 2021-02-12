using System;
using System.Text;

namespace Networking.Utilities
{
    public static class BufferToString
    {
        [FlagsAttribute] public enum ToStringOptions {  None=0, ProcessCrLf=1, ProcessTab=2, };
        /// <summary>
        /// Converts a byte array to a string. Control characters 0..31 and values >=128 are printed in HEX
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string ToString(byte[] buffer, ToStringOptions options = ToStringOptions.None)
        {
            byte lastByte = 0x0;
            bool lastIsCrLf = false;

            var sb = new StringBuilder();
            foreach (var b in buffer)
            {
                bool isCrLf = (b == 0x0A) || (b == 0x0D);
                if (isCrLf && options.HasFlag(ToStringOptions.ProcessCrLf))
                {
                    // <CR><CR> => \r\r
                    // <LF><LF> ==? \r\r
                    // but <CR><LF> ==> \r
                    if (lastIsCrLf == false || b == lastByte)
                    {
                        // super common case. Worse case is that this is only
                        // called half the time.
                        sb.Append('\n');
                    }
                }
                else if (b == 0x09 && options.HasFlag(ToStringOptions.ProcessTab))
                {
                    sb.Append('\t');
                }
                else if (b >= 32 && b < 128)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append($"0x{b:X2}");
                }

                lastByte = b;
                lastIsCrLf = isCrLf;
            }
            return sb.ToString();
        }

        public static string ToString(Windows.Storage.Streams.IBuffer buffer, ToStringOptions options = ToStringOptions.None)
        {
            var dr = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
            byte[] bytes = new byte[dr.UnconsumedBufferLength];
            dr.ReadBytes(bytes);
            return ToString(bytes, options);
        }
    }
}
