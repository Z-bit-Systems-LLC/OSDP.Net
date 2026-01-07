using System;
using System.Collections;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Messages;

[TestFixture]
[Category("Unit")]
internal class CommandOutgoingMessageTest
{
    [TestCaseSource(typeof(CommunicationConfigurationBuildMessageTestClass),
        nameof(CommunicationConfigurationBuildMessageTestClass.TestCases))]
    public string CommunicationConfigurationBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new CommunicationConfiguration(1, 9600));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class CommunicationConfigurationBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-6E-AC-A3-C2-FF-E4-D9-E5-99-CD-77-1B-8E-F3-D1-3F-23-FA-AB-63-4E-08-95");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-0D-00-06-6E-01-80-25-00-00-EC-7B");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-0C-00-02-6E-01-80-25-00-00-8B");
            }
        }
    }

    [TestCaseSource(typeof(DeviceCapabilitiesBuildMessageTestClass),
        nameof(DeviceCapabilitiesBuildMessageTestClass.TestCases))]
    public string DeviceCapabilitiesBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new DeviceCapabilities());

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class DeviceCapabilitiesBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-62-D1-4E-B7-AD-98-2C-C5-F9-AA-25-31-8A-7E-AE-D6-26-0F-6A-2A-A7-38-70");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-09-00-06-62-00-F3-5D");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-08-00-02-62-00-41");
            }
        }
    }

    [TestCaseSource(typeof(EncryptionKeyConfigurationBuildMessageTestClass),
        nameof(EncryptionKeyConfigurationBuildMessageTestClass.TestCases))]
    public string EncryptionKeyConfigurationBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey,
                new byte[]
                {
                    0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07
                }));

        return BitConverter.ToString(outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class EncryptionKeyConfigurationBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-2E-00-0E-02-17-75-F0-DD-EC-9A-0E-D8-CE-4C-80-BA-F5-D5-5C-18-43-6F-91-89-FD-66-C9-F2-23-DC-04-14-86-D8-33-57-DF-98-6C-1D-4B-C2-B2-F2");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-1A-00-06-75-01-10-00-01-02-03-04-05-06-07-00-01-02-03-04-05-06-07-CF-A3");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-19-00-02-75-01-10-00-01-02-03-04-05-06-07-00-01-02-03-04-05-06-07-D4");
            }
        }
    }

    [TestCaseSource(typeof(FileTransferBuildMessageTestClass), nameof(FileTransferBuildMessageTestClass.TestCases))]
    public string FileTransferBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new FileTransferFragment(3, new MessageDataFragment(1000, 10, 5, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 })));

        return BitConverter.ToString(outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class FileTransferBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-2E-00-0E-02-17-7C-92-71-38-DD-E0-09-A3-47-53-67-F8-2A-2C-94-29-70-0D-23-48-A8-77-B9-55-09-6D-8A-24-DD-71-88-AC-3C-9A-98-D6-C5-4A-EE");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-18-00-06-7C-03-E8-03-00-00-0A-00-00-00-05-00-01-02-03-04-05-59-21");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-17-00-02-7C-03-E8-03-00-00-0A-00-00-00-05-00-01-02-03-04-05-0C");
            }
        }
    }

    [TestCaseSource(typeof(GetPIVDataBuildMessageTestDataClass), nameof(GetPIVDataBuildMessageTestDataClass.TestCases))]
    public string GetPIVDataBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new GetPIVData([0x5F, 0xC1, 0x02], 0, 0));

        return BitConverter.ToString(outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class GetPIVDataBuildMessageTestDataClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-A3-BB-89-C6-E0-F9-EB-82-AD-FC-BC-16-C4-EE-CF-42-B0-51-FC-BF-83-95-B2");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-0E-00-06-A3-5F-C1-02-00-00-00-41-23");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-0D-00-02-A3-5F-C1-02-00-00-00-D9");
            }
        }
    }
    
    [TestCaseSource(typeof(IdReportBuildMessageTestClass), nameof(IdReportBuildMessageTestClass.TestCases))]
    public string IdReportBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);
        
        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new IdReport());

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class IdReportBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-61-D1-4E-B7-AD-98-2C-C5-F9-AA-25-31-8A-7E-AE-D6-26-9A-E9-95-73-19-83");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-09-00-06-61-00-A0-08");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-08-00-02-61-00-42");
            }
        }
    }
    
    [TestCaseSource(typeof(InputStatusBuildMessageTestClass), nameof(InputStatusBuildMessageTestClass.TestCases))]
    public string InputStatusBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);
        
        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new NoPayloadCommandData(CommandType.InputStatus));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class InputStatusBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-0E-00-0E-02-15-65-46-0E-3D-4E-2A-6A");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-08-00-06-65-2C-9C");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-07-00-02-65-3F");
            }
        }
    }
    
    [TestCaseSource(typeof(LocalStatusBuildMessageTestClass), nameof(LocalStatusBuildMessageTestClass.TestCases))]
    public string LocalStatusBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);
        
        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new NoPayloadCommandData(CommandType.LocalStatus));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class LocalStatusBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-0E-00-0E-02-15-64-6F-63-24-6F-D0-50");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-08-00-06-64-0D-8C");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-07-00-02-64-40");
            }
        }
    }
    
    [TestCaseSource(typeof(ManufacturerSpecificBuildMessageTestClass), nameof(ManufacturerSpecificBuildMessageTestClass.TestCases))]
    public string ManufacturerSpecificBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);
        
        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new ManufacturerSpecific(new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0x0A, 0x0B, 0x0C }));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class ManufacturerSpecificBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-80-C8-67-0F-CB-60-C0-25-BA-48-15-41-92-A4-2B-B8-F3-B9-78-FF-AC-1A-91");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-0E-00-06-80-01-02-03-0A-0B-0C-6B-14");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-0D-00-02-80-01-02-03-0A-0B-0C-F7");
            }
        }
    }

    [TestCaseSource(typeof(OutputControlBuildMessageTestClass), nameof(OutputControlBuildMessageTestClass.TestCases))]
    public string OutputControlBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new OutputControls(new[]
            {
                new OutputControl(0, OutputControlCode.PermanentStateOffAllowTimedOperation, 100),
                new OutputControl(1, OutputControlCode.TemporaryStateOffResumePermanentState, 120)
            }));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class OutputControlBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-1E-00-0E-02-17-68-F7-EF-A0-D4-76-4A-4E-29-26-75-96-F2-16-D8-A9-E8-C5-88-76-08-B9-08");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-10-00-06-68-00-03-64-00-01-06-78-00-0B-5B");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-0F-00-02-68-00-03-64-00-01-06-78-00-4E");
            }
        }
    }
    
    [TestCaseSource(typeof(OutputStatusBuildMessageTestClass), nameof(OutputStatusBuildMessageTestClass.TestCases))]
    public string OutputStatusBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);
        
        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new NoPayloadCommandData(CommandType.OutputStatus));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }

    public class OutputStatusBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x0, true, true)).Returns(
                    "FF-53-00-0E-00-0E-02-15-66-62-DE-05-16-12-4B");
                yield return new TestCaseData(new DeviceProxy(0x0, true, false)).Returns(
                    "FF-53-00-08-00-06-66-4F-AC");
                yield return new TestCaseData(new DeviceProxy(0x0, false, false)).Returns(
                    "FF-53-00-07-00-02-66-3E");
            }
        }
    }

    [TestCaseSource(typeof(PollBuildMessageTestClass), nameof(PollBuildMessageTestClass.TestCases))]
    public string PollBuildMessage_TestCases(DeviceProxy device)
    {
        device.MessageControl.IncrementSequence(1);

        var outgoingMessage = new OutgoingMessage(device.Address, device.MessageControl,
            new NoPayloadCommandData(CommandType.Poll));

        return BitConverter.ToString(
            outgoingMessage.BuildMessage(CreateSecureChannel(device.UseSecureChannel)));
    }


    public class PollBuildMessageTestClass
    {
        public static IEnumerable TestCases
        {
            get
            {
                yield return new TestCaseData(new DeviceProxy(0x00, true, true)).Returns(
                    "FF-53-00-0E-00-0E-02-15-60-4B-32-76-77-73-B6");
                yield return new TestCaseData(new DeviceProxy(0x00, true, false)).Returns(
                    "FF-53-00-08-00-06-60-89-CC");
                yield return new TestCaseData(new DeviceProxy(0x00, false, false)).Returns(
                    "FF-53-00-07-00-02-60-44");
            }
        }
    }

    private ACUMessageSecureChannel CreateSecureChannel(bool useSecureChannel)
    {
        return new ACUMessageSecureChannel(new SecurityContext(SecurityContext.DefaultKey)
        {
            IsSecurityEstablished = useSecureChannel,
            Enc = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            RMac = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            SMac1 = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            SMac2 = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
        });
    }
}