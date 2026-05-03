using System.Globalization;
using Secs4Net;

namespace HostSimTester.App.Secs;

public static class SecsItemFormatter
{
    public static string Format(Item? item)
    {
        if (item is null)
        {
            return "<EMPTY>";
        }

        var lines = new List<string>();
        AppendItem(lines, item, 0);
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatMessage(SecsMessage message)
    {
        return $"'S{message.S}F{message.F}' {message.Name}{Environment.NewLine}{Format(message.SecsItem)}";
    }

    private static void AppendItem(List<string> lines, Item item, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (item.Format == SecsFormat.List)
        {
            lines.Add($"{indent}<L [{item.Count}]");
            for (var i = 0; i < item.Count; i++)
            {
                AppendItem(lines, item[i], depth + 1);
            }
            lines.Add($"{indent}>");
            return;
        }

        lines.Add($"{indent}<{FormatName(item.Format)} [{item.Count}] {FormatScalarValues(item)}>");
    }

    private static string FormatName(SecsFormat format)
    {
        return format switch
        {
            SecsFormat.ASCII => "A",
            SecsFormat.Binary => "B",
            SecsFormat.Boolean => "BOOLEAN",
            _ => format.ToString().ToUpperInvariant()
        };
    }

    private static string FormatScalarValues(Item item)
    {
        try
        {
            return item.Format switch
            {
                SecsFormat.ASCII => $"'{EscapeAscii(item.GetString())}'",
                SecsFormat.Binary => FormatBytes(item.GetMemory<byte>().ToArray()),
                SecsFormat.Boolean => string.Join(" ", item.GetMemory<bool>().ToArray().Select(v => v ? "True" : "False")),
                SecsFormat.U1 => string.Join(" ", item.GetMemory<byte>().ToArray()),
                SecsFormat.U2 => string.Join(" ", item.GetMemory<ushort>().ToArray()),
                SecsFormat.U4 => string.Join(" ", item.GetMemory<uint>().ToArray()),
                SecsFormat.U8 => string.Join(" ", item.GetMemory<ulong>().ToArray()),
                SecsFormat.I1 => string.Join(" ", item.GetMemory<sbyte>().ToArray()),
                SecsFormat.I2 => string.Join(" ", item.GetMemory<short>().ToArray()),
                SecsFormat.I4 => string.Join(" ", item.GetMemory<int>().ToArray()),
                SecsFormat.I8 => string.Join(" ", item.GetMemory<long>().ToArray()),
                SecsFormat.F4 => string.Join(" ", item.GetMemory<float>().ToArray().Select(v => v.ToString(CultureInfo.InvariantCulture))),
                SecsFormat.F8 => string.Join(" ", item.GetMemory<double>().ToArray().Select(v => v.ToString(CultureInfo.InvariantCulture))),
                _ => item.ToString()
            };
        }
        catch (Exception ex)
        {
            return $"<format-failed: {ex.Message}>";
        }
    }

    private static string FormatBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", bytes.Select(b => $"0x{b:X2}"));
    }

    private static string EscapeAscii(string value)
    {
        return value.Replace("'", "\\'", StringComparison.Ordinal);
    }
}