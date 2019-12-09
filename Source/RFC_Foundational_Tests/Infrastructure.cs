using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFC_Foundational_Tests
{
    public static class Infrastructure
    {
        public static int NError { get; set; } = 0;

        public static bool IfTrueError(bool test, string str, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (!test) return false;
            NError++;
            var errorstring = $"TEST ERROR: {memberName}: {str}";
            System.Diagnostics.Debug.WriteLine(errorstring);
            return true;
        }

        public static void Error(string str, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            NError++;
            var errorstring = $"TEST ERROR: {memberName}: {str}";
            System.Diagnostics.Debug.WriteLine(errorstring);
        }

        public static void Log(string str, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            var logstring = $"TEST LOG: {memberName}: {str}";
            System.Diagnostics.Debug.WriteLine(logstring);
        }
    }
}
