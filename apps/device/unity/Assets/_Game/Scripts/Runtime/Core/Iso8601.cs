using System;
using System.Globalization;

namespace Plemu.Core
{
    /// <summary>
    /// Strict parser/formatter for the manifest timestamp format mandated by SPEC §1.2:
    /// ISO 8601 UTC, seconds precision, trailing <c>Z</c> (e.g. <c>2026-07-12T00:00:00Z</c>).
    /// Parsing is culture-invariant and always yields a UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    public static class Iso8601
    {
        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>
        /// Attempts to parse a SPEC timestamp. Only the exact <c>YYYY-MM-DDTHH:MM:SSZ</c>
        /// shape is accepted; anything else (fractional seconds, offset other than Z,
        /// missing Z) fails.
        /// </summary>
        /// <returns><c>true</c> and a UTC value on success; <c>false</c> otherwise.</returns>
        public static bool TryParse(string value, out DateTimeOffset result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = default;
                return false;
            }

            if (DateTimeOffset.TryParseExact(
                    value,
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                result = parsed;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>Formats a UTC instant as a SPEC timestamp (seconds precision, trailing Z).</summary>
        public static string Format(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }
    }
}
