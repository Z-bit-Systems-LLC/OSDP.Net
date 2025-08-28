using System.Collections;
using System.Numerics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.ReplyData;

namespace CardReader;

internal class Program
{
    private static async Task Main()
    {
        var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true).Build();
        var osdpSection = config.GetSection("OSDP");

        string portName = osdpSection["PortName"] ?? throw new NullReferenceException("A port name is required in the configuration file.");
        int baudRate = int.Parse(osdpSection["BaudRate"] ?? "9600");
        byte deviceAddress = byte.Parse(osdpSection["DeviceAddress"] ?? "0");
        bool requireSecurity = bool.Parse(osdpSection["RequireSecurity"] ?? "False");
        var securityKey = System.Text.Encoding.ASCII.GetBytes(osdpSection["SecurityKey"] ?? "0011223344556677889900AABBCCDDEEFF");

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Warning)
                .AddFilter("CardReader.Program", LogLevel.Information)
                .AddFilter("OSDP.Net", LogLevel.Debug);
        });

        var deviceConfiguration = new DeviceConfiguration
        {
            Address = deviceAddress,
            RequireSecurity = requireSecurity,
            SecurityKey = securityKey
        };
        
        // Replace commented out code for test reader to listen on TCP port rather than serial
        //var communications = new TcpConnectionListener(5000, baudRate, loggerFactory);
        var communications = new SerialPortConnectionListener(portName, baudRate, loggerFactory);

        using var device = new MySampleDevice(deviceConfiguration, loggerFactory);
        device.DeviceComSetUpdated += async (sender, args) =>
        {
            Console.WriteLine("A command has been processed to update the communication settings.");
            Console.WriteLine($"Old settings - Baud Rate {args.OldBaudRate} - Address {args.OldAddress}");
            Console.WriteLine($"New settings - Baud Rate {args.NewBaudRate} - Address {args.NewAddress}");
            Console.WriteLine("A command has been processed to update the communication settings.");

            if (sender is MySampleDevice mySampleDevice && args.OldBaudRate != args.NewBaudRate)
            {
                Console.WriteLine("Restarting communications with new baud rate");
                communications = new SerialPortConnectionListener(portName, args.NewBaudRate, loggerFactory);
                await mySampleDevice.StopListening();
                mySampleDevice.StartListening(communications);
            }
        };
        
        device.StartListening(communications);

        await Task.Run(async () =>
        {
            // The card format number for this example is 128 bit (as raw card data)
            const string cardNumberHexValue = "30313233343536373839303030303032";
            var cardNumberInt = BigInteger.Parse(cardNumberHexValue, System.Globalization.NumberStyles.HexNumber);
            var cardNumberBitString = BigIntegerToBinaryString(cardNumberInt).PadLeft(128, '0');
            var cardNumber = new BitArray(cardNumberBitString.Select(c => c == '1').ToArray());

            while (true)
            {
                await Task.Delay(500);

                // ReSharper disable once AccessToDisposedClosure
                if (!device.IsConnected) continue;

                Console.WriteLine($"Device is connected!\nPress any key to send card data.");
                Console.ReadKey();
                device.EnqueuePollReply(new RawCardData(deviceAddress, FormatCode.NotSpecified, cardNumber));
                Console.WriteLine($"Sent card data: {cardNumberHexValue}");
                return;
            }
        });

        Console.WriteLine("Press any key to finish the program.");
        Console.ReadKey();

        await device.StopListening();
    }

    private static string BigIntegerToBinaryString(BigInteger value)
    {
        var result = "";

        while (value > 0)
        {
            result = value % 2 + result;
            value /= 2;
        }

        return string.IsNullOrEmpty(result) ? "0" : result;
    }
}
