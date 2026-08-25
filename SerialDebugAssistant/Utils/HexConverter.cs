using System;
using System.Globalization;
using System.Text;

namespace SerialDebugAssistant.Utils;

public static class HexConverter
{
    public static byte[] HexStringToBytes(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        var cleaned = hex.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                         .Replace(" ", string.Empty)
                         .Replace("\t", string.Empty)
                         .Replace("\r", string.Empty)
                         .Replace("\n", string.Empty);
        if (cleaned.Length == 0) return Array.Empty<byte>();
        if (cleaned.Length % 2 != 0) throw new FormatException("HEX 字符串长度必须为偶数");
        var bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < cleaned.Length; i += 2)
        {
            var pair = cleaned.Substring(i, 2);
            if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                throw new FormatException($"非法 HEX 字符: {pair}");
            bytes[i / 2] = b;
        }
        return bytes;
    }

    public static string BytesToHexString(byte[] bytes)
    {
        if (bytes is null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length == 0) return string.Empty;
        var sb = new StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public static byte[] AsciiToBytes(string ascii)
    {
        if (ascii is null) throw new ArgumentNullException(nameof(ascii));
        return Encoding.UTF8.GetBytes(ascii);
    }

    public static string BytesToAscii(byte[] bytes)
    {
        if (bytes is null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length == 0) return string.Empty;
        return Encoding.UTF8.GetString(bytes);
    }
}
