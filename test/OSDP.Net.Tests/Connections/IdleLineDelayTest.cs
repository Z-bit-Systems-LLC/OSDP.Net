using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Tests.Utilities;

namespace OSDP.Net.Tests.Connections;

/// <summary>
/// Verifies the baud-rate transmission-timing model that the bus uses to honor the OSDP idle-line
/// requirement. The general integration tests run over an in-memory loopback that reports a zero
/// idle-line delay for speed, so this is the dedicated coverage for the baud-based model itself.
/// </summary>
[TestFixture]
[Category("Unit")]
public class IdleLineDelayTest
{
    private sealed class FakeSerialConnection : OsdpConnection
    {
        public FakeSerialConnection(int baudRate) : base(baudRate)
        {
        }

        public override Task Open() => Task.CompletedTask;
        public override Task Close() => Task.CompletedTask;
        public override Task<int> ReadAsync(byte[] buffer, CancellationToken token) => Task.FromResult(0);
        public override Task WriteAsync(byte[] buffer) => Task.CompletedTask;
    }

    [Test]
    public void IdleLineDelay_ModelsTenBitTimesPerByte()
    {
        var connection = new FakeSerialConnection(9600);

        // 100 bytes at 10 bit-times/byte over 9600 baud = 1000 bits / 9600 bps ≈ 104.17 ms.
        // Tolerance accounts for TimeSpan's 100ns tick rounding.
        Assert.That(connection.IdleLineDelay(100).TotalSeconds, Is.EqualTo(1000.0 / 9600).Within(1e-6));
    }

    [Test]
    public void IdleLineDelay_IsProportionalToBytesAndInverseToBaud()
    {
        // Compare on TotalSeconds with tolerance; TimeSpan rounds to 100ns ticks, so exact
        // TimeSpan equality is brittle here.
        Assert.Multiple(() =>
        {
            Assert.That(new FakeSerialConnection(9600).IdleLineDelay(200).TotalSeconds,
                Is.EqualTo(new FakeSerialConnection(9600).IdleLineDelay(100).TotalSeconds * 2).Within(1e-6));
            Assert.That(new FakeSerialConnection(19200).IdleLineDelay(100).TotalSeconds,
                Is.EqualTo(new FakeSerialConnection(9600).IdleLineDelay(50).TotalSeconds).Within(1e-6));
        });
    }

    [Test]
    public void Loopback_ReportsZeroIdleLineDelay_ButKeepsBaudRate()
    {
        var (acuConnection, deviceConnection) = LoopbackOsdpConnection.CreatePair();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(acuConnection.IdleLineDelay(1000), Is.EqualTo(TimeSpan.Zero));
                Assert.That(acuConnection.BaudRate, Is.EqualTo(9600), "BaudRate must be preserved for COMSET behavior");
            });
        }
        finally
        {
            acuConnection.Dispose();
            deviceConnection.Dispose();
        }
    }
}
