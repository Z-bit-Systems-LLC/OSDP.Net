using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OSDP.Net.LineQuality.Cli
{
    /// <summary>
    /// A small hand-rolled option parser. Three verbs and a handful of options do not justify a
    /// dependency, and the repository has no command line parser precedent to follow.
    /// </summary>
    internal sealed class CommandLineArguments
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CommandLineArguments()
        {
        }

        /// <summary>
        /// Parses tokens of the form <c>--name value</c>, <c>--name=value</c>, or a bare
        /// <c>--flag</c>.
        /// </summary>
        public static CommandLineArguments Parse(IEnumerable<string> tokens)
        {
            var arguments = new CommandLineArguments();
            var queue = new Queue<string>(tokens);

            while (queue.Count > 0)
            {
                string token = queue.Dequeue();

                if (!token.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Unexpected argument '{token}'.");
                }

                string name = token.Substring(2);

                int separator = name.IndexOf('=');
                if (separator >= 0)
                {
                    arguments._values[name.Substring(0, separator)] = name.Substring(separator + 1);
                    continue;
                }

                // A following token that is not itself an option is this option's value.
                if (queue.Count > 0 && !queue.Peek().StartsWith("--", StringComparison.Ordinal))
                {
                    arguments._values[name] = queue.Dequeue();
                }
                else
                {
                    arguments._flags.Add(name);
                }
            }

            return arguments;
        }

        public bool HasFlag(string name) => _flags.Contains(name);

        public string GetRequired(string name)
        {
            if (!_values.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Missing required option --{name}.");
            }

            return value;
        }

        public string GetOptional(string name, string fallback = null) =>
            _values.TryGetValue(name, out string value) ? value : fallback;

        public int GetInt32(string name, int fallback)
        {
            if (!_values.TryGetValue(name, out string value)) return fallback;

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException($"--{name} must be a whole number, but was '{value}'.");
            }

            return parsed;
        }

        public byte GetByte(string name, byte fallback)
        {
            int value = GetInt32(name, fallback);
            if (value < 0 || value > byte.MaxValue)
            {
                throw new ArgumentException($"--{name} must be between 0 and 255.");
            }

            return (byte)value;
        }

        public TestProfile GetProfile(string name, TestProfile fallback)
        {
            string value = GetOptional(name);
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            foreach (TestProfile profile in Enum.GetValues(typeof(TestProfile)))
            {
                if (string.Equals(profile.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            throw new ArgumentException(
                $"--{name} must be one of screening, qualification, or extended, but was '{value}'.");
        }

        public IReadOnlyList<int> GetBaudRates(string name)
        {
            string value = GetOptional(name);
            if (string.IsNullOrWhiteSpace(value)) return null;

            var rates = new List<int>();
            foreach (string part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int rate))
                {
                    throw new ArgumentException($"--{name} contains '{part.Trim()}', which is not a number.");
                }

                if (!LineQualityProtocol.TryGetBaudRateId(rate, out _))
                {
                    throw new ArgumentException(
                        $"--{name} contains {rate}, which has no line quality baud rate ID. " +
                        $"Supported rates: {string.Join(", ", SupportedRates())}.");
                }

                rates.Add(rate);
            }

            if (rates.Count == 0)
            {
                throw new ArgumentException($"--{name} did not contain any baud rates.");
            }

            return rates;
        }

        private static IEnumerable<int> SupportedRates() =>
            Enum.GetValues(typeof(LineQualityBaudRate))
                .Cast<LineQualityBaudRate>()
                .Select(LineQualityProtocol.ToBaudRate);
    }
}
