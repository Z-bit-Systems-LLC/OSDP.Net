using System;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tests.Utilities;

namespace OSDP.Net.Tests
{
    [TestFixture]
    [Category("ComponentTest")]
    public class ControlPanelTest
    {
        [Test]
        public async Task DeviceGoesOnlineTest()
        {
            // Arrange
            var mockConnection = new MockConnection();

            var panel = new ControlPanel(NullLoggerFactory.Instance);
            Guid id = panel.StartConnection(mockConnection.Object);
            panel.AddDevice(id, 0, true, false);

            // Act
            // Assert
            await TaskEx.WaitUntil(() => panel.IsOnline(id, 0), TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
        }

        [Test]
        public async Task ShutdownTest()
        {
            // Arrange
            var mockConnection = new MockConnection();

            var panel = new ControlPanel(NullLoggerFactory.Instance);
            Guid id = panel.StartConnection(mockConnection.Object);
            panel.AddDevice(id, 0, true, false);

            // Act
            await panel.Shutdown();

            // Assert
            await TaskEx.WaitUntil(() => mockConnection.NumberOfTimesCalledOpen == 1, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
            await TaskEx.WaitUntil(() => mockConnection.NumberOfTimesCalledClose == 1, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
        }

        [Test]
        public async Task StartConnectionTest()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var panel = new ControlPanel(NullLoggerFactory.Instance);

            // Act
            Guid id = panel.StartConnection(mockConnection.Object);
            panel.AddDevice(id, 0, true, false);

            // Assert
            await TaskEx.WaitUntil(() => mockConnection.NumberOfTimesCalledOpen == 1, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
        }

        [Test]
        public void StartConnectionWithSameConnectionTwiceTest()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var panel = new ControlPanel(NullLoggerFactory.Instance);
            var id = panel.StartConnection(mockConnection.Object);

            // Act/Assert
            var exception = Assert.Throws<InvalidOperationException>(() => panel.StartConnection(mockConnection.Object), "");
            Assert.That(exception?.Message, Is.EqualTo(
                $"The IOsdpConnection is already active in connection {id}. " +
                    "That connection must be stopped before starting a new one."));
        }

        [Test]
        public void StartSameConnectionConcurrentlyShouldOnlyStartItOnce()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var instance = mockConnection.Object;
            var panel = new ControlPanel(NullLoggerFactory.Instance);

            // Act
            var tasks = Enumerable
                .Range(0, 100)
                .Select(_ => Task.Run(() => panel.StartConnection(instance)))
                .ToArray();
            try
            {
                // ReSharper disable once CoVariantArrayConversion
                Task.WaitAll(tasks);
            }
            catch { /* We handle errors later */ }

            // Assert
            Assert.That(tasks.Count(t => t.Status == TaskStatus.RanToCompletion), Is.EqualTo(1));
            Assert.That(tasks.Count(t => t.Status == TaskStatus.Faulted), Is.EqualTo(99));
        }

        [Test]
        public void StopSameConnectionConcurrentlyShouldSucceed()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var panel = new ControlPanel(NullLoggerFactory.Instance);
            var id = panel.StartConnection(mockConnection.Object);

            // Act
            var tasks = Enumerable
                .Range(0, 100)
                .Select(_ => Task.Run(async () => await panel.StopConnection(id)))
                .ToArray();
            try
            {
                // ReSharper disable once CoVariantArrayConversion
                Task.WaitAll(tasks);
            }
            catch { /* We handle errors later */ }

            // Assert
            Assert.That(tasks.Count(t => t.Status == TaskStatus.RanToCompletion), Is.EqualTo(100));
        }

        [Test]
        public async Task StartConnectionRestartWithSameConnectionTest()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var panel = new ControlPanel(NullLoggerFactory.Instance);

            // Act
            var id1 = panel.StartConnection(mockConnection.Object);
            await panel.StopConnection(id1);
            var id2 = panel.StartConnection(mockConnection.Object);
            await panel.StopConnection(id2);

            // Assert
            Assert.That(id1, Is.Not.EqualTo(id2));
        }


        [Test]
        public async Task StopConnectionTest()
        {
            // Arrange
            var mockConnection = new MockConnection();
            var panel = new ControlPanel(NullLoggerFactory.Instance);

            Guid id = panel.StartConnection(mockConnection.Object);
            panel.AddDevice(id, 0, true, false);

            // Act
            await panel.StopConnection(id);

            // Assert
            await TaskEx.WaitUntil(() => mockConnection.NumberOfTimesCalledOpen == 1, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
            await TaskEx.WaitUntil(() => mockConnection.NumberOfTimesCalledClose == 1, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(3));
        }

        [TestFixture]
        public class IdRequestCommandTest
        {
            [Test]
            public async Task ReturnsValidReportTest()
            {
                var panel = new ControlPanel(NullLoggerFactory.Instance);
                var idReportCommand = new IdReport();
                var deviceIdentificationReply =
                    new DeviceIdentification([0x5C, 0x26, 0x23], 0x19, 0x02, 719912960, 0x03, 0x00, 0x00);

                var mockConnection = new MockConnection();
                mockConnection.OnCommand(idReportCommand).Reply(deviceIdentificationReply);

                Guid id = panel.StartConnection(mockConnection.Object);
                panel.AddDevice(id, 0, true, false);

                var response = await panel.IdReport(id, 0);

                Assert.That(response.ToString(), Is.EqualTo(
                    $"     Vendor Code: 5C-26-23{Environment.NewLine}" +
                    $"    Model Number: 25{Environment.NewLine}" +
                    $"         Version: 2{Environment.NewLine}" +
                    $"   Serial Number: 00-00-E9-2A{Environment.NewLine}" +
                    $"Firmware Version: 3.0.0{Environment.NewLine}"
                ));
            }

            [Test]
            public void ThrowOnNakReplyTest()
            {
                var panel = new ControlPanel(NullLoggerFactory.Instance);
                var idReportCommand = new IdReport();
                var nakReply = new Nak(ErrorCode.UnknownCommandCode);

                var mockConnection = new MockConnection();
                mockConnection.OnCommand(idReportCommand).Reply(nakReply);

                Guid id = panel.StartConnection(mockConnection.Object);
                panel.AddDevice(id, 0, true, false);

                var exception = Assert.ThrowsAsync<NackReplyException>(async () => await panel.IdReport(id, 0));

                Assert.That(exception?.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode));
            }

        }

        private class MockConnection : Mock<IOsdpConnection>
        {
            private static readonly CommandData PollCommand = new NoPayloadCommandData(CommandType.Poll);
            private static readonly PayloadData Ack = new Ack();

            public MockConnection() : base(MockBehavior.Strict)
            {
                var readStream = _incomingData.Reader.AsStream(true);

                Setup(x => x.IsOpen).Returns(true);
                Setup(x => x.BaudRate).Returns(9600);
                Setup(x => x.ReplyTimeout).Returns(TimeSpan.FromSeconds(999999));
                Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                    .Returns(async (byte[] buffer, CancellationToken cancellationToken) =>
                        await readStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    );

                Setup(x => x.Open()).Callback(() => NumberOfTimesCalledOpen++);
                Setup(x => x.Open()).Callback(() => NumberOfTimesCalledClose++);

                // Setup handling for a polling command which always gets issued when connection is alive
                // Here we'll just reply with a generic ACK to signal to ACU that the command was successfully
                // handled.
                OnCommand(PollCommand).Reply(Ack);
            }

            public ExpectedCommand OnCommand(CommandData command) => new(this, command);

            /// <summary>
            /// Number of times the Open method is called
            /// </summary>
            public int NumberOfTimesCalledOpen { get; private set; }

            /// <summary>
            /// Number of times the Close method is called
            /// </summary>
            public int NumberOfTimesCalledClose { get; private set; }

            public class ExpectedCommand
            {
                public ExpectedCommand(MockConnection parent, CommandData command)
                {
                    _parent = parent;
                    _command = command;
                }

                public void Reply(PayloadData replyData)
                {
                    for (byte seq = 0; seq < 4; seq++)
                    {
                        var replyMessage = new OutgoingMessage(0x80, new Control(seq, true, false), replyData);

                        _parent.Setup(x => x.WriteAsync(It.Is<byte[]>(
                            messageData => IsMatchingCommandType(messageData, _command.Code)
                        ))).Returns(
                            async (byte[] _) =>
                                await _parent._incomingData.Writer.WriteAsync(
                                    replyMessage.BuildMessage(new PdMessageSecureChannelBase()))
                        );
                    }
                }

                private static bool IsMatchingCommandType(byte[] messageData, byte commandType)
                {
                    var receivedCommand =
                        new IncomingMessage(messageData.Skip(1).ToArray(), new ACUMessageSecureChannel());
                    return receivedCommand.Type == commandType;
                }

                private readonly MockConnection _parent;
                private readonly CommandData _command;
            }

            private readonly Pipe _incomingData = new();
        }
    }
}