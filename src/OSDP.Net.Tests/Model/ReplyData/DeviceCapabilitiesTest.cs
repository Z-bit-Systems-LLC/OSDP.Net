using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;



namespace OSDP.Net.Tests.Model.ReplyData
{
    [TestFixture]
    [Category("Unit")]
    public class DeviceCapabilitiesTest
    {
        private readonly byte[] _rawCapsFromDennisBrivoKeypad = new byte[] {
            3, 1, 1, 4, 4, 1, 5, 2, 1, 6, 0, 0, 7, 0, 0, 8, 1,
            0, 9, 1, 1, 10, 194, 1, 14, 0, 0, 15, 0, 0, 16, 1, 0
        };

        [Test]
        public void ThrowsWhenDataWrongLength()
        {
            var rawData = new byte[] { 0x01, 0x02 };

            Assert.Throws<Exception>(() => DeviceCapabilities.ParseData(rawData));
        }

        [Test]
        public void ParsesOutFunctionCodes()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var expectedFuncCodes = new[]
            {
                CapabilityFunction.CardDataFormat,
                CapabilityFunction.ReaderLEDControl,
                CapabilityFunction.ReaderAudibleOutput,
                CapabilityFunction.ReaderTextOutput,
                CapabilityFunction.TimeKeeping,
                CapabilityFunction.CheckCharacterSupport,
                CapabilityFunction.CommunicationSecurity,
                CapabilityFunction.ReceiveBufferSize,
                CapabilityFunction.Biometrics,
                CapabilityFunction.SecurePINEntry,
                CapabilityFunction.OSDPVersion
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var actual = DeviceCapabilities.ParseData(_rawCapsFromDennisBrivoKeypad);

            Assert.That(actual.Capabilities.Select((x) => x.Function), Is.EquivalentTo(expectedFuncCodes));
        }

        [Test]
        public void CommSecurityCapInstanceFromDerivedClass()
        {
            var actual = DeviceCapabilities.ParseData(_rawCapsFromDennisBrivoKeypad);

            var commSecCap = actual.Get<CommSecurityDeviceCap>(CapabilityFunction.CommunicationSecurity);

            Assert.That(commSecCap.SupportsAes128, Is.EqualTo(true));
            Assert.That(commSecCap.UsesDefaultKey, Is.EqualTo(true));
        }

        [Test]
        public void ToStringTest()
        {
            var actual = DeviceCapabilities.ParseData(_rawCapsFromDennisBrivoKeypad.AsSpan().Slice(18, 9));
            var expectedText =
                $"  Function: Communication Security{Environment.NewLine}" +
                $"Supports AES-128: True{Environment.NewLine}" +
                $"Uses Default Key: True{Environment.NewLine}" +
                Environment.NewLine +
                $"  Function: Receive Buffer Size{Environment.NewLine}" +
                $"      Size: 450{Environment.NewLine}" +
                Environment.NewLine +
                $"  Function: Biometrics{Environment.NewLine}" +
                $"Compliance: 0{Environment.NewLine}" +
                $" Number Of: 0{Environment.NewLine}" +
                Environment.NewLine;

            Assert.That(actual.ToString(), Is.EqualTo(expectedText).NoClip);
        }
    }
}
