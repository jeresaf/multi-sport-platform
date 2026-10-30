using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// RFC 8785, the JSON Canonicalization Scheme: object members sorted by their names' UTF-16 code units, no
/// insignificant whitespace, strings escaped and numbers written as ECMAScript's <c>JSON.stringify</c> does. Hashing
/// it gives configuration content one hash however its file was written (S05 D4).
/// </summary>
public static class CanonicalJson
{
    private const string _hashScheme = "sha256:";

    // A lone surrogate has no UTF-8 form (RFC 8785 §3.2.2.2); replacing it with U+FFFD would let two strings collide.
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The canonical text of <paramref name="node"/>.</summary>
    public static string Serialize(JsonNode? node)
    {
        var builder = new StringBuilder();
        Write(builder, node);
        return builder.ToString();
    }

    /// <summary><c>sha256:</c> and the lower-case hex SHA-256 of the canonical text's UTF-8 bytes.</summary>
    public static string Hash(JsonNode? node) =>
        _hashScheme + Convert.ToHexStringLower(SHA256.HashData(_utf8.GetBytes(Serialize(node))));

    private static void Write(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject members:
                builder.Append('{');
                var first = true;
                foreach (var (name, value) in members.OrderBy(member => member.Key, StringComparer.Ordinal))
                {
                    builder.Append(first ? string.Empty : ",");
                    WriteString(builder, name);
                    builder.Append(':');
                    Write(builder, value);
                    first = false;
                }

                builder.Append('}');
                break;
            case JsonArray items:
                builder.Append('[');
                for (var i = 0; i < items.Count; i++)
                {
                    builder.Append(i > 0 ? "," : string.Empty);
                    Write(builder, items[i]);
                }

                builder.Append(']');
                break;
            default:
                WriteValue(builder, node.AsValue());
                break;
        }
    }

    private static void WriteValue(StringBuilder builder, JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                WriteString(builder, value.GetValue<string>());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Number:
                // The value as written ("4.50", "1E30") or created (a long, a decimal), read as the double it denotes.
                builder.Append(Number(double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)));
                break;
            default:
                builder.Append("null");
                break;
        }
    }

    // ECMAScript's escapes: the two-character ones, \u00xx (lower case) for other control characters, the rest as is.
    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }

        builder.Append('"');
    }

    /// <summary>
    /// ECMAScript's Number::toString: the shortest digits that round-trip, plain from 1e-6 up to 1e21, otherwise
    /// with an exponent (<c>1e+21</c>, <c>1e-7</c>, <c>1.5e+300</c>). <c>-0</c> is <c>0</c>.
    /// </summary>
    private static string Number(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON has no NaN or infinity.");
        }

        if (value == 0)
        {
            return "0";
        }

        // "R" gives the shortest round-trip digits, e.g. "-1.5E-07" or "123.45".
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var sign = text.StartsWith('-') ? "-" : string.Empty;
        var parts = text.TrimStart('-').Split('E');
        var exponent = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var point = parts[0].IndexOf('.', StringComparison.Ordinal);
        var digits = parts[0].Replace(".", string.Empty, StringComparison.Ordinal);
        var integerDigits = (point < 0 ? parts[0].Length : point) + exponent;

        // value = 0.digits × 10^n, digits without leading or trailing zeros (k of them).
        var leadingZeros = digits.Length - digits.TrimStart('0').Length;
        digits = digits.Trim('0');
        var n = integerDigits - leadingZeros;
        var k = digits.Length;

        var result = n switch
        {
            _ when k <= n && n <= 21 => digits + new string('0', n - k),
            > 0 and <= 21 => $"{digits[..n]}.{digits[n..]}",
            > -6 and <= 0 => $"0.{new string('0', -n)}{digits}",
            _ => $"{digits[0]}{(k > 1 ? "." + digits[1..] : string.Empty)}e{(n - 1 >= 0 ? "+" : "-")}{Math.Abs(n - 1)}",
        };
        return sign + result;
    }
}
