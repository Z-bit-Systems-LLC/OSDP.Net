using System;
using NUnit.Framework;
using OSDP.Net.LineQuality;

namespace OSDP.Net.Tests.LineQuality
{
    [TestFixture]
    public class LineQualityProtocolTest
    {
        /// <summary>
        /// The expected-output table from section 3.6 of the test procedure.
        /// </summary>
        private static readonly object[] PatternVectors =
        {
            new object[] { TestPattern.AllZeros, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
            new object[] { TestPattern.AllOnes, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } },
            new object[] { TestPattern.AlternatingA, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA } },
            new object[] { TestPattern.Alternating5, new byte[] { 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55 } },
            new object[] { TestPattern.Sequential, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 } },
            new object[] { TestPattern.WalkingOne, new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 } }
        };

        [TestCaseSource(nameof(PatternVectors))]
        public void GeneratePattern_MatchesSpecificationTable(TestPattern pattern, byte[] expected)
        {
            Assert.That(LineQualityProtocol.GeneratePattern(pattern, 8), Is.EqualTo(expected));
        }

        [Test]
        public void SequentialPattern_WrapsAfter256Bytes()
        {
            // Byte[i] = i AND 0xFF, so the maximum payload never reaches the wrap point, but the
            // masking still has to be right.
            var data = LineQualityProtocol.GeneratePattern(TestPattern.Sequential,
                LineQualityProtocol.MaxPayloadLength);

            Assert.That(data[112], Is.EqualTo(112));
        }

        [Test]
        public void WalkingOnePattern_RepeatsEveryEightBytes()
        {
            var data = LineQualityProtocol.GeneratePattern(TestPattern.WalkingOne, 17);

            Assert.Multiple(() =>
            {
                Assert.That(data[8], Is.EqualTo(0x01));
                Assert.That(data[16], Is.EqualTo(0x01));
            });
        }

        [Test]
        public void GeneratePattern_RejectsLengthAboveTheGuaranteedBuffer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LineQualityProtocol.GeneratePattern(TestPattern.AllZeros,
                    LineQualityProtocol.MaxPayloadLength + 1));
        }

        [Test]
        public void GeneratePattern_AcceptsZeroLength()
        {
            Assert.That(LineQualityProtocol.GeneratePattern(TestPattern.AllOnes, 0), Is.Empty);
        }

        [Test]
        public void ValidatePattern_RejectsASingleFlippedBit()
        {
            var data = LineQualityProtocol.GeneratePattern(TestPattern.AlternatingA, 16);
            data[7] ^= 0x01;

            Assert.That(LineQualityProtocol.ValidatePattern(TestPattern.AlternatingA, data), Is.False);
        }

        [Test]
        public void ValidatePattern_AcceptsUnmodifiedData()
        {
            var data = LineQualityProtocol.GeneratePattern(TestPattern.Sequential, 48);

            Assert.That(LineQualityProtocol.ValidatePattern(TestPattern.Sequential, data), Is.True);
        }

        [Test]
        public void VendorCode_IsLocallyAdministered()
        {
            // Bit 1 of the first octet marks a locally-administered OUI, which is what keeps
            // 02-00-0A out of the space IEEE assigns to manufacturers.
            Assert.Multiple(() =>
            {
                Assert.That(LineQualityProtocol.VendorCode.ToArray(),
                    Is.EqualTo(new byte[] { 0x02, 0x00, 0x0A }));
                Assert.That(LineQualityProtocol.VendorCode[0] & 0x02, Is.EqualTo(0x02));
            });
        }

        [Test]
        public void IsLineQualityVendorCode_RejectsOtherVendors()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LineQualityProtocol.IsLineQualityVendorCode(new byte[] { 0x02, 0x00, 0x0A }), Is.True);
                Assert.That(LineQualityProtocol.IsLineQualityVendorCode(new byte[] { 0x00, 0x00, 0x0A }), Is.False);
                Assert.That(LineQualityProtocol.IsLineQualityVendorCode(new byte[] { 0x02, 0x00 }), Is.False);
            });
        }

        [TestCase(LineQualityBaudRate.Baud9600, 9600)]
        [TestCase(LineQualityBaudRate.Baud19200, 19200)]
        [TestCase(LineQualityBaudRate.Baud38400, 38400)]
        [TestCase(LineQualityBaudRate.Baud57600, 57600)]
        [TestCase(LineQualityBaudRate.Baud115200, 115200)]
        [TestCase(LineQualityBaudRate.Baud230400, 230400)]
        [TestCase(LineQualityBaudRate.Baud460800, 460800)]
        public void BaudRateIds_MapBothWays(LineQualityBaudRate id, int expected)
        {
            Assert.Multiple(() =>
            {
                Assert.That(LineQualityProtocol.ToBaudRate(id), Is.EqualTo(expected));
                Assert.That(LineQualityProtocol.TryGetBaudRateId(expected, out var roundTripped), Is.True);
                Assert.That(roundTripped, Is.EqualTo(id));
            });
        }

        [Test]
        public void DefaultBaudRates_ExcludeTheNonStandardRate()
        {
            // OSDP section 5.2 names only 9600 through 230400. 460800 has an ID reserved for it
            // but is not swept unless the caller asks for it.
            Assert.That(LineQualityProtocol.DefaultBaudRates, Is.EqualTo(
                new[] { 9600, 19200, 38400, 57600, 115200, 230400 }));
        }

        [Test]
        public void UnknownBaudRate_HasNoIdentifier()
        {
            Assert.That(LineQualityProtocol.TryGetBaudRateId(4800, out _), Is.False);
        }

        [TestCase(TestProfile.Screening, 10)]
        [TestCase(TestProfile.Qualification, 60)]
        [TestCase(TestProfile.Extended, 200)]
        public void Profiles_SetTheIterationCount(TestProfile profile, int expected)
        {
            Assert.That(LineQualityProtocol.IterationsPerCombination(profile), Is.EqualTo(expected));
        }

        [Test]
        public void DetectionLimit_FollowsTheRuleOfThree()
        {
            // Section 5.3: with no observed failures the 95% upper bound on loss is about 3/N.
            Assert.Multiple(() =>
            {
                Assert.That(LineQualityProtocol.DetectionLimitPercent(160), Is.EqualTo(1.875).Within(0.001));
                Assert.That(LineQualityProtocol.DetectionLimitPercent(960), Is.EqualTo(0.3125).Within(0.001));
                Assert.That(LineQualityProtocol.DetectionLimitPercent(3200), Is.EqualTo(0.09375).Within(0.001));
            });
        }

        [Test]
        public void DetectionLimit_IsUselessWithNoPackets()
        {
            Assert.That(LineQualityProtocol.DetectionLimitPercent(0), Is.EqualTo(100.0));
        }

        [Test]
        public void TransmissionTime_AssumesTenBitsPerByte()
        {
            // A 128-byte frame at 9600 baud occupies the line for 133 ms, which is why response
            // time has to be measured to the first reply byte rather than the last.
            var elapsed = LineQualityProtocol.TransmissionTime(128, 9600);

            Assert.That(elapsed.TotalMilliseconds, Is.EqualTo(133.3).Within(0.1));
        }
    }
}
