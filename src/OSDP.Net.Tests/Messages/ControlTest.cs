using NUnit.Framework;
using OSDP.Net.Messages;

namespace OSDP.Net.Tests.Messages
{
    [TestFixture]
    [Category("Unit")]
    public class ControlTest
    {
        [TestCase(0, true, false, ExpectedResult = 0x04)]
        [TestCase(1, true, false, ExpectedResult = 0x05)]
        [TestCase(1, true, true, ExpectedResult = 0x0D)]
        [TestCase(4, true, true, ExpectedResult = 0x0C)]
        public byte ControlBlockTestCases(byte sequence, bool useCrc, bool hasSecurityControlBlock)
        {
            return new Control(sequence, useCrc, hasSecurityControlBlock).ControlByte;
        }
    }
}