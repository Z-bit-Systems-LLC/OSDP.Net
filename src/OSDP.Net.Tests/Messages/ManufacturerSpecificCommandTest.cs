using System;
using System.Collections;
using NUnit.Framework;
using OSDP.Net.Messages.ACU;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Messages
{
    [TestFixture]
    public class ManufacturerSpecificCommandTest
    {
        [TestCaseSource(typeof(ManufacturerSpecificCommandTestDataClass), nameof(ManufacturerSpecificCommandTestDataClass.TestCases))]
        public string BuildCommand_TestCases(byte address, bool useCrc, bool useSecureChannel, ManufacturerSpecific manufacturerSpecific)
        {
            var manufacturerSpecificCommand = new ManufacturerSpecificCommand(address, manufacturerSpecific);
            var device = new DeviceProxy(0, useCrc, useSecureChannel, null);
            device.MessageControl.IncrementSequence(1);
            return BitConverter.ToString(manufacturerSpecificCommand.BuildCommand(device));
        }

        public class ManufacturerSpecificCommandTestDataClass
        {
            public static IEnumerable TestCases
            {
                get
                {
                    var data = new ManufacturerSpecific(new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0x0A, 0x0B, 0x0C });

                    yield return new TestCaseData((byte) 0x0, true, true, data).Returns(
                        "53-00-10-00-0E-02-17-80-01-02-03-0A-0B-0C-3F-D3");
                    yield return new TestCaseData((byte) 0x0, true, false, data).Returns(
                        "53-00-0E-00-06-80-01-02-03-0A-0B-0C-6B-14");
                    yield return new TestCaseData((byte) 0x0, false, false, data).Returns(
                        "53-00-0D-00-02-80-01-02-03-0A-0B-0C-F7");
                }
            }
        }
    }
}