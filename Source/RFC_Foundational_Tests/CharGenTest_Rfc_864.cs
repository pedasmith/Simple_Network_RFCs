using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Networking.RFC_Foundational_Tests
{
    class CharGenTest_Rfc_864
    {
        public static async Task Test()
        {
            await Task.Delay(0); // TODO: when the test code is done, can remove.
            var start = DateTimeOffset.UtcNow;
            Infrastructure.Log($"Starting test: CharGenTest_Rfc_864");
            var testObject = new CharGenTest_Rfc_864();
            try
            {
                testObject.Test_CharGen();
            }
            catch (Exception ex)
            {
                // There should be absolutely no exceptions thrown by the tests.
                Infrastructure.Error($"Uncaught exception thrown in tests {ex.Message} hresult {ex.HResult:X}");
            }
            var delta = DateTimeOffset.UtcNow.Subtract(start).TotalSeconds;
            Infrastructure.Log($"Ending test: CharGenTest_Rfc_864  time={delta} seconds");
        }

        public void Test_CharGen()
        {
            CharGenServer_Rfc_864.TestAscii95();
        }
    }
}
