using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData
{
    [TestFixture]
    [Category("Unit")]
    internal class ReaderBuzzerControlTest
    {
        private byte[] TestData => [0x00, 0x02, 0x05, 0x02, 0x01];

        private ReaderBuzzerControl TestReaderBuzzerControl => new(0, ToneCode.Default, 5, 2, 1);

        [Test]
        public void CheckConstantValues()
        {
            // Arrange Act Assert
            Assert.That(TestReaderBuzzerControl.CommandType, Is.EqualTo(CommandType.BuzzerControl));
            Assert.That(TestReaderBuzzerControl.SecurityControlBlock().ToArray(),
                Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
        }

        [Test]
        public void BuildData()
        {
            // Arrange
            // Act
            var actual = TestReaderBuzzerControl.BuildData();

            // Assert
            Assert.That(actual, Is.EqualTo(TestData));
        }

        [Test]
        public void ParseData()
        {
            var actual = ReaderBuzzerControl.ParseData(TestData);

            Assert.That(actual.ReaderNumber, Is.EqualTo(TestReaderBuzzerControl.ReaderNumber));
            Assert.That(actual.ToneCode, Is.EqualTo(TestReaderBuzzerControl.ToneCode));
            Assert.That(actual.OnTime, Is.EqualTo(TestReaderBuzzerControl.OnTime));
            Assert.That(actual.OffTime, Is.EqualTo(TestReaderBuzzerControl.OffTime));
            Assert.That(actual.Count, Is.EqualTo(TestReaderBuzzerControl.Count));
        }

        [Test]
        public void ToneCodeOffRoundTrip()
        {
            // Arrange - Test with ToneCode.Off
            var control = new ReaderBuzzerControl(0, ToneCode.Off, 1, 1, 1);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.ToneCode, Is.EqualTo(ToneCode.Off));
        }

        [Test]
        public void ToneCodeDefaultRoundTrip()
        {
            // Arrange - Test with ToneCode.Default
            var control = new ReaderBuzzerControl(0, ToneCode.Default, 1, 1, 1);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.ToneCode, Is.EqualTo(ToneCode.Default));
        }

        [Test]
        public void MaxReaderNumber()
        {
            // Arrange - Test max byte value for reader number
            var control = new ReaderBuzzerControl(byte.MaxValue, ToneCode.Default, 1, 1, 1);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.ReaderNumber, Is.EqualTo(byte.MaxValue));
        }

        [Test]
        public void MaxTimeValues()
        {
            // Arrange - Test max byte values for on/off times
            var control = new ReaderBuzzerControl(0, ToneCode.Default, byte.MaxValue, byte.MaxValue, 1);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.OnTime, Is.EqualTo(byte.MaxValue));
            Assert.That(parsed.OffTime, Is.EqualTo(byte.MaxValue));
        }

        [Test]
        public void MaxCount()
        {
            // Arrange - Test max byte value for count
            var control = new ReaderBuzzerControl(0, ToneCode.Default, 1, 1, byte.MaxValue);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.Count, Is.EqualTo(byte.MaxValue));
        }

        [Test]
        public void ZeroCountForInfiniteRepeat()
        {
            // Arrange - Count value 0 means "until stopped" per OSDP spec
            var control = new ReaderBuzzerControl(0, ToneCode.Default, 1, 1, 0);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.Count, Is.EqualTo(0));
        }

        [Test]
        public void ZeroTimesRoundTrip()
        {
            // Arrange - Test with zero on/off times
            var control = new ReaderBuzzerControl(0, ToneCode.Default, 0, 0, 1);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.OnTime, Is.EqualTo(0));
            Assert.That(parsed.OffTime, Is.EqualTo(0));
        }

        [Test]
        public void AllMaxValues()
        {
            // Arrange - Test all max byte values
            var control = new ReaderBuzzerControl(byte.MaxValue, ToneCode.Default, byte.MaxValue, byte.MaxValue,
                byte.MaxValue);

            // Act
            var data = control.BuildData();
            var parsed = ReaderBuzzerControl.ParseData(data);

            // Assert
            Assert.That(parsed.ReaderNumber, Is.EqualTo(byte.MaxValue));
            Assert.That(parsed.OnTime, Is.EqualTo(byte.MaxValue));
            Assert.That(parsed.OffTime, Is.EqualTo(byte.MaxValue));
            Assert.That(parsed.Count, Is.EqualTo(byte.MaxValue));
        }

        [Test]
        public void ToStringIncludesAllProperties()
        {
            // Arrange
            var control = new ReaderBuzzerControl(1, ToneCode.Default, 5, 3, 2);

            // Act
            var result = control.ToString();

            // Assert
            Assert.That(result, Does.Contain("Reader #: 1"));
            Assert.That(result, Does.Contain("Tone Code: Default"));
            Assert.That(result, Does.Contain("On Time: 5"));
            Assert.That(result, Does.Contain("Off Time: 3"));
            Assert.That(result, Does.Contain("Count: 2"));
        }
    }
}
