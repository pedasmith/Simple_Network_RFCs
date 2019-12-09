using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echo_Rfc_862.Utilities
{
    public static class BufferToString
    {
        /// <summary>
        /// Converts a byte array to a string. Control characters 0..31 and values >=128 are printed in HEX
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string ToString(byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
            {
                if (b >= 32 && b < 128)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append($"0x{b:X2}");
                }
            }
            return sb.ToString();
        }

        public static string ToString(Windows.Storage.Streams.IBuffer buffer)
        {
            var dr = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
            byte[] bytes = new byte[dr.UnconsumedBufferLength];
            dr.ReadBytes(bytes);
            return ToString(bytes);
        }
    }
}
