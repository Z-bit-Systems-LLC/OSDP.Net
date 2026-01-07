using System;
using System.Collections;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model
{
    [TestFixture]
    [Category("Unit")]
    public class ToStringTests
    {
        [TestFixture]
        public class CommandDataTests
        {
            [Test]
            public void CommunicationConfiguration_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var command = new OSDP.Net.Model.CommandData.CommunicationConfiguration(42, 9600);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Address"));
                Assert.That(result, Does.Contain("42"));
                Assert.That(result, Does.Contain("Baud Rate"));
                Assert.That(result, Does.Contain("9600"));
            }

            [Test]
            public void ReaderBuzzerControl_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var command = new ReaderBuzzerControl(0, ToneCode.Default, 5, 3, 2);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader #"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("Tone Code"));
                Assert.That(result, Does.Contain("Default"));
                Assert.That(result, Does.Contain("On Time"));
                Assert.That(result, Does.Contain("5"));
                Assert.That(result, Does.Contain("Off Time"));
                Assert.That(result, Does.Contain("3"));
                Assert.That(result, Does.Contain("Count"));
                Assert.That(result, Does.Contain("2"));
            }

            [Test]
            public void ReaderTextOutput_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var command = new ReaderTextOutput(
                    readerNumber: 1,
                    textCommand: TextCommand.PermanentTextNoWrap,
                    temporaryTextTime: 10,
                    row: 1,
                    column: 1,
                    text: "Hello World");

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader Number"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Text Command"));
                Assert.That(result, Does.Contain("PermanentTextNoWrap"));
                Assert.That(result, Does.Contain("Temp Text Time"));
                Assert.That(result, Does.Contain("10"));
                Assert.That(result, Does.Contain("Row, Column"));
                Assert.That(result, Does.Contain("1, 1"));
                Assert.That(result, Does.Contain("Display Text"));
                Assert.That(result, Does.Contain("Hello World"));
            }

            [Test]
            public void OutputControl_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var control = new OutputControl(3, OutputControlCode.PermanentStateOnAbortTimedOperation, 100);

                // Act
                var result = control.ToString();

                // Assert
                Assert.That(result, Does.Contain("Output #"));
                Assert.That(result, Does.Contain("3"));
                Assert.That(result, Does.Contain("Ctrl Code"));
                Assert.That(result, Does.Contain("PermanentStateOnAbortTimedOperation"));
                Assert.That(result, Does.Contain("Timer"));
                Assert.That(result, Does.Contain("100"));
            }

            [Test]
            public void OutputControls_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var controls = new OutputControls(new[]
                {
                    new OutputControl(0, OutputControlCode.TemporaryStateOnResumePermanentState, 50),
                    new OutputControl(1, OutputControlCode.TemporaryStateOffResumePermanentState, 75)
                });

                // Act
                var result = controls.ToString();

                // Assert
                Assert.That(result, Does.Contain("Output Count"));
                Assert.That(result, Does.Contain("2"));
                Assert.That(result, Does.Contain("Output #"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Code"));
                Assert.That(result, Does.Contain("TemporaryStateOnResumePermanentState"));
                Assert.That(result, Does.Contain("TemporaryStateOffResumePermanentState"));
                Assert.That(result, Does.Contain("Time"));
                Assert.That(result, Does.Contain("50"));
                Assert.That(result, Does.Contain("75"));
            }

            [Test]
            public void ReaderLedControls_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var controls = new ReaderLedControls(new[]
                {
                    new ReaderLedControl(
                        readerNumber: 0,
                        ledNumber: 1,
                        temporaryMode: TemporaryReaderControlCode.SetTemporaryAndStartTimer,
                        temporaryOnTime: 5,
                        temporaryOffTime: 3,
                        temporaryOnColor: LedColor.Red,
                        temporaryOffColor: LedColor.Black,
                        temporaryTimer: 10,
                        permanentMode: PermanentReaderControlCode.Nop,
                        permanentOnTime: 0,
                        permanentOffTime: 0,
                        permanentOnColor: LedColor.Black,
                        permanentOffColor: LedColor.Black)
                });

                // Act
                var result = controls.ToString();

                // Assert
                Assert.That(result, Does.Contain("LED Count"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Reader #"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("LED #"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Temp Mode"));
                Assert.That(result, Does.Contain("SetTemporaryAndStartTimer"));
                Assert.That(result, Does.Contain("On Time"));
                Assert.That(result, Does.Contain("5"));
                Assert.That(result, Does.Contain("Off Time"));
                Assert.That(result, Does.Contain("3"));
                Assert.That(result, Does.Contain("On Color"));
                Assert.That(result, Does.Contain("Red"));
                Assert.That(result, Does.Contain("Off Color"));
                Assert.That(result, Does.Contain("Black"));
                Assert.That(result, Does.Contain("Timer"));
                Assert.That(result, Does.Contain("10"));
            }

            [Test]
            public void ManufacturerSpecific_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var vendorCode = new byte[] { 0x12, 0x34, 0x56 };
                var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
                var command = new OSDP.Net.Model.CommandData.ManufacturerSpecific(vendorCode, data);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Vendor Code"));
                Assert.That(result, Does.Contain("12-34-56"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("AA-BB-CC-DD"));
            }

            [Test]
            public void BiometricReadData_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var command = new BiometricReadData(
                    readerNumber: 0,
                    biometricType: BiometricType.LeftIndexFingerPrint,
                    biometricFormatType: BiometricFormat.FingerPrintTemplate,
                    quality: 80);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader #"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("Bio Type"));
                Assert.That(result, Does.Contain("LeftIndexFingerPrint"));
                Assert.That(result, Does.Contain("Format"));
                Assert.That(result, Does.Contain("FingerPrintTemplate"));
                Assert.That(result, Does.Contain("Quality"));
                Assert.That(result, Does.Contain("80"));
            }
        }

        [TestFixture]
        public class ReplyDataTests
        {
            [Test]
            public void DeviceIdentification_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var vendorCode = new byte[] { 0x00, 0x01, 0x02 };
                var reply = new DeviceIdentification(
                    vendorCode: vendorCode,
                    modelNumber: 42,
                    version: 1,
                    serialNumber: 123456,
                    firmwareMajor: 2,
                    firmwareMinor: 3,
                    firmwareBuild: 5);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Vendor Code"));
                Assert.That(result, Does.Contain("00-01-02"));
                Assert.That(result, Does.Contain("Model Number"));
                Assert.That(result, Does.Contain("42"));
                Assert.That(result, Does.Contain("Version"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Serial Number"));
                Assert.That(result, Does.Contain("Firmware Version"));
                Assert.That(result, Does.Contain("2.3.5"));
            }

            [Test]
            public void DeviceCapabilities_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var capabilities = new OSDP.Net.Model.ReplyData.DeviceCapabilities(new[]
                {
                    new DeviceCapability(CapabilityFunction.ContactStatusMonitoring, 1, 4),
                    new DeviceCapability(CapabilityFunction.OutputControl, 1, 2)
                });

                // Act
                var result = capabilities.ToString();

                // Assert
                Assert.That(result, Does.Contain("Function"));
                Assert.That(result, Does.Contain("Contact Status Monitoring"));
                Assert.That(result, Does.Contain("Output Control"));
                Assert.That(result, Does.Contain("Compliance"));
                Assert.That(result, Does.Contain("Number Of"));
                Assert.That(result, Does.Contain("4"));
                Assert.That(result, Does.Contain("2"));
            }

            [Test]
            public void DeviceCapability_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var capability = new DeviceCapability(CapabilityFunction.ReaderLEDControl, 2, 3);

                // Act
                var result = capability.ToString();

                // Assert
                Assert.That(result, Does.Contain("Function"));
                Assert.That(result, Does.Contain("LED Control"));
                Assert.That(result, Does.Contain("Compliance"));
                Assert.That(result, Does.Contain("2"));
                Assert.That(result, Does.Contain("Number Of"));
                Assert.That(result, Does.Contain("3"));
            }

            [Test]
            public void LocalStatus_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var status = new LocalStatus(tamper: true, powerFailure: false);

                // Act
                var result = status.ToString();

                // Assert
                Assert.That(result, Does.Contain("Tamper"));
                Assert.That(result, Does.Contain("True"));
                Assert.That(result, Does.Contain("Power Failure"));
                Assert.That(result, Does.Contain("False"));
            }

            [Test]
            public void InputStatus_ToString_ContainsExpectedProperties()
            {
                // Arrange
                InputStatusValue[] statuses =
                [
                    InputStatusValue.Active,
                    InputStatusValue.Inactive,
                    InputStatusValue.Active
                ];
                var reply = new InputStatus(statuses);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Input Number 00"));
                Assert.That(result, Does.Contain("Input Number 01"));
                Assert.That(result, Does.Contain("Input Number 02"));
                Assert.That(result, Does.Contain("Active"));
                Assert.That(result, Does.Contain("Inactive"));
            }

            [Test]
            public void OutputStatus_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var statuses = new[] { true, false, true, false };
                var reply = new OutputStatus(statuses);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Output Number 00"));
                Assert.That(result, Does.Contain("Output Number 01"));
                Assert.That(result, Does.Contain("Output Number 02"));
                Assert.That(result, Does.Contain("Output Number 03"));
                Assert.That(result, Does.Contain("True"));
                Assert.That(result, Does.Contain("False"));
            }

            [Test]
            public void ReaderStatus_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var statuses = new[]
                {
                    ReaderTamperStatus.Normal,
                    ReaderTamperStatus.NotConnected
                };
                var reply = new ReaderStatus(statuses);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader Number 00"));
                Assert.That(result, Does.Contain("Reader Number 01"));
                Assert.That(result, Does.Contain("Normal"));
                Assert.That(result, Does.Contain("Not Connected"));
            }

            [Test]
            public void CommunicationConfiguration_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var reply = new OSDP.Net.Model.ReplyData.CommunicationConfiguration(address: 5, baudRate: 115200);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Address"));
                Assert.That(result, Does.Contain("5"));
                Assert.That(result, Does.Contain("Baud Rate"));
                Assert.That(result, Does.Contain("115200"));
            }

            [Test]
            public void RawCardData_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var bitArray = new BitArray(new[] { true, false, true, true, false, false, true, false });
                var reply = new RawCardData(
                    readerNumber: 0,
                    format: FormatCode.Wiegand,
                    data: bitArray);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader Number"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("Format Code"));
                Assert.That(result, Does.Contain("Wiegand"));
                Assert.That(result, Does.Contain("Bit Count"));
                Assert.That(result, Does.Contain("8"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("10110010"));
            }

            [Test]
            public void FormattedCardData_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var reply = new FormattedCardData(
                    readerNumber: 1,
                    direction: ReadDirection.Forward,
                    data: "1234567890");

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader Number"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Read Direction"));
                Assert.That(result, Does.Contain("Forward"));
                Assert.That(result, Does.Contain("Data Length"));
                Assert.That(result, Does.Contain("10"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("1234567890"));
            }

            [Test]
            public void KeypadData_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var reply = new KeypadData(readerNumber: 0, data: "1234*#");

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Reader Number"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("Digit Count"));
                Assert.That(result, Does.Contain("6"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("1234*#"));
            }

            [Test]
            public void Nak_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var nak = new Nak(ErrorCode.UnknownCommandCode);

                // Act
                var result = nak.ToString();

                // Assert
                Assert.That(result, Does.Contain("Error"));
                Assert.That(result, Does.Contain("Unknown Command Code"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("(none)"));
            }

            [Test]
            public void Nak_ToString_WithExtraData_ContainsExpectedProperties()
            {
                // Arrange
                var nakData = new byte[] { (byte)ErrorCode.GenericError, 0xAA, 0xBB };
                var nak = Nak.ParseData(nakData);

                // Act
                var result = nak.ToString();

                // Assert
                Assert.That(result, Does.Contain("Error"));
                Assert.That(result, Does.Contain("Generic Error"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("AA-BB"));
            }

            [Test]
            public void DataFragmentResponse_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var testData = new byte[]
                {
                    0x64, 0x00, // WholeMessageLength = 100 (little endian)
                    0x10, 0x00, // Offset = 16 (little endian)
                    0x08, 0x00, // LengthOfFragment = 8 (little endian)
                    0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 // Data
                };
                var response = DataFragmentResponse.ParseData(testData);

                // Act
                var result = response.ToString();

                // Assert
                Assert.That(result, Does.Contain("Whole Message Length"));
                Assert.That(result, Does.Contain("100"));
                Assert.That(result, Does.Contain("Offset"));
                Assert.That(result, Does.Contain("16"));
                Assert.That(result, Does.Contain("Length of Fragment"));
                Assert.That(result, Does.Contain("8"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("01-02-03-04-05-06-07-08"));
            }

            [Test]
            public void ManufacturerSpecific_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var testData = new byte[] { 0xAA, 0xBB, 0xCC, 0x11, 0x22, 0x33 };
                var reply = OSDP.Net.Model.ReplyData.ManufacturerSpecific.ParseData(testData);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Vendor Code"));
                Assert.That(result, Does.Contain("AA-BB-CC"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("11-22-33"));
            }

            [Test]
            public void FileTransferStatus_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var testData = new byte[]
                {
                    0x01, // Action (Interleave flag)
                    0x10, 0x00, // RequestedDelay = 16 (little endian)
                    0x00, 0x00, // StatusDetail = 0 (OkToProceed)
                    0x00, 0x04  // UpdateMessageMaximum = 1024 (little endian)
                };
                var status = FileTransferStatus.ParseData(testData);

                // Act
                var result = status.ToString();

                // Assert
                Assert.That(result, Does.Contain("Action"));
                Assert.That(result, Does.Contain("Interleave"));
                Assert.That(result, Does.Contain("Requested Delay"));
                Assert.That(result, Does.Contain("16"));
                Assert.That(result, Does.Contain("Status Detail"));
                Assert.That(result, Does.Contain("OkToProceed"));
                Assert.That(result, Does.Contain("Update Message Max"));
                Assert.That(result, Does.Contain("1024"));
            }
        }

        [TestFixture]
        public class SpecializedCapabilityTests
        {
            [Test]
            public void CommSecurityDeviceCap_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var data = new byte[] { (byte)CapabilityFunction.CommunicationSecurity, 0x01, 0x00 };
                var capability = DeviceCapability.ParseData(data) as CommSecurityDeviceCap;

                // Act
                var result = capability!.ToString();

                // Assert
                Assert.That(result, Does.Contain("Function"));
                Assert.That(result, Does.Contain("Communication Security"));
                Assert.That(result, Does.Contain("Supports AES-128"));
                Assert.That(result, Does.Contain("True"));
                Assert.That(result, Does.Contain("Uses Default Key"));
                Assert.That(result, Does.Contain("False"));
            }

            [Test]
            public void RcvBuffSizeDeviceCap_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var data = new byte[] { (byte)CapabilityFunction.ReceiveBufferSize, 0x00, 0x02 }; // 512 bytes
                var capability = DeviceCapability.ParseData(data) as RcvBuffSizeDeviceCap;

                // Act
                var result = capability!.ToString();

                // Assert
                Assert.That(result, Does.Contain("Function"));
                Assert.That(result, Does.Contain("Receive Buffer Size"));
                Assert.That(result, Does.Contain("Size"));
                Assert.That(result, Does.Contain("512"));
            }

            [Test]
            public void LargestCombMsgSizeDeviceCap_ToString_ContainsExpectedProperties()
            {
                // Arrange
                var data = new byte[] { (byte)CapabilityFunction.LargestCombinedMessageSize, 0x00, 0x04 }; // 1024 bytes
                var capability = DeviceCapability.ParseData(data) as LargestCombMsgSizeDeviceCap;

                // Act
                var result = capability!.ToString();

                // Assert
                Assert.That(result, Does.Contain("Function"));
                Assert.That(result, Does.Contain("Largest Combined Message Size"));
                Assert.That(result, Does.Contain("Size"));
                Assert.That(result, Does.Contain("1024"));
            }
        }

        [TestFixture]
        public class EdgeCaseTests
        {
            [Test]
            public void CommunicationConfiguration_ToString_WithZeroValues()
            {
                // Arrange
                var command = new OSDP.Net.Model.CommandData.CommunicationConfiguration(0, 0);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Address"));
                Assert.That(result, Does.Contain("0"));
                Assert.That(result, Does.Contain("Baud Rate"));
            }

            [Test]
            public void ReaderTextOutput_ToString_WithEmptyText()
            {
                // Arrange
                var command = new ReaderTextOutput(0, TextCommand.PermanentTextNoWrap, 0, 1, 1, "");

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Display Text"));
                Assert.That(result, Is.Not.Null);
            }

            [Test]
            public void ManufacturerSpecific_ToString_WithEmptyData()
            {
                // Arrange
                var vendorCode = new byte[] { 0xFF, 0xFF, 0xFF };
                var emptyData = Array.Empty<byte>();
                var command = new OSDP.Net.Model.CommandData.ManufacturerSpecific(vendorCode, emptyData);

                // Act
                var result = command.ToString();

                // Assert
                Assert.That(result, Does.Contain("Vendor Code"));
                Assert.That(result, Does.Contain("FF-FF-FF"));
                Assert.That(result, Does.Contain("Data"));
            }

            [Test]
            public void RawCardData_ToString_WithSingleBit()
            {
                // Arrange
                var singleBit = new BitArray(new[] { true });
                var reply = new RawCardData(0, FormatCode.NotSpecified, singleBit);

                // Act
                var result = reply.ToString();

                // Assert
                Assert.That(result, Does.Contain("Bit Count"));
                Assert.That(result, Does.Contain("1"));
                Assert.That(result, Does.Contain("Data"));
                Assert.That(result, Does.Contain("1"));
            }

            [Test]
            public void InputStatus_ToString_WithNoInputs()
            {
                // Arrange
                var reply = new InputStatus(Array.Empty<InputStatusValue>());

                // Act
                var result = reply.ToString();

                // Assert - Should return empty or minimal string, not throw
                Assert.That(result, Is.Not.Null);
            }

            [Test]
            public void DeviceCapabilities_ToString_WithNoCapabilities()
            {
                // Arrange
                var capabilities = new OSDP.Net.Model.ReplyData.DeviceCapabilities(Array.Empty<DeviceCapability>());

                // Act
                var result = capabilities.ToString();

                // Assert - Should return empty or minimal string, not throw
                Assert.That(result, Is.Not.Null);
            }

            [Test]
            public void DataFragmentResponse_ToString_WithMinimalData()
            {
                // Arrange
                var minimalData = new byte[]
                {
                    0x00, 0x00, // WholeMessageLength = 0
                    0x00, 0x00, // Offset = 0
                    0x00, 0x00  // LengthOfFragment = 0
                };
                var response = DataFragmentResponse.ParseData(minimalData);

                // Act
                var result = response.ToString();

                // Assert
                Assert.That(result, Does.Contain("Whole Message Length"));
                Assert.That(result, Does.Contain("Offset"));
                Assert.That(result, Does.Contain("Length of Fragment"));
            }
        }
    }
}
