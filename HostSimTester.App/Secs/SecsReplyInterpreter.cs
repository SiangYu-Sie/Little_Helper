using System.Text.RegularExpressions;
using Secs4Net;

namespace HostSimTester.App.Secs;

public static class SecsReplyInterpreter
{
    public static IReadOnlyList<string> DescribePrimary(PrimaryMessageWrapper primary)
    {
        var msg = primary.PrimaryMessage;
        var raw = msg.ToString();

        var lines = new List<string>
        {
            $"Primary S{msg.S}F{msg.F} Name={msg.Name}"
        };

        switch ((msg.S, msg.F))
        {
            case (6, 11):
                lines.AddRange(DescribeS6F11(raw));
                break;
            case (6, 1):
                lines.AddRange(DescribeS6F1(raw));
                break;
            default:
                lines.Add("Primary message received.");
                break;
        }

        lines.Add($"Raw: {raw}");
        return lines;
    }

    public static IReadOnlyList<string> Describe(SecsMessage? reply)
    {
        if (reply is null)
        {
            return ["No secondary reply (fire-and-forget or timeout)."];
        }

        var lines = new List<string>
        {
            $"Reply S{reply.S}F{reply.F} Name={reply.Name}"
        };

        switch ((reply.S, reply.F))
        {
            case (1, 14):
                lines.Add(DescribeAck("COMMACK", reply.ToString()));
                break;
            case (1, 16):
                lines.Add(DescribeAck("OFLACK", reply.ToString()));
                break;
            case (1, 18):
                lines.Add(DescribeAck("ONLACK", reply.ToString()));
                break;
            case (2, 38):
                lines.Add(DescribeAck("ERACK", reply.ToString()));
                break;
            case (2, 42):
                lines.Add(DescribeAck("HCACK", reply.ToString()));
                break;
            case (3, 18):
                lines.Add("Carrier action reply received.");
                break;
            default:
                lines.Add("Secondary message received.");
                break;
        }

        lines.Add($"Raw: {reply}");
        return lines;
    }

    private static string DescribeAck(string ackName, string raw)
    {
        var m = Regex.Match(raw, @"0x([0-9A-Fa-f]{2})");
        if (!m.Success)
        {
            return $"{ackName}: unable to parse ack code.";
        }

        var code = Convert.ToInt32(m.Groups[1].Value, 16);
        return code == 0
            ? $"{ackName}: ACK (0x00)."
            : $"{ackName}: NAK/ERROR (0x{code:X2}).";
    }

    private static IEnumerable<string> DescribeS6F11(string raw)
    {
        var m = Regex.Match(
            raw,
            @"<U4\s*\[\d+\]\s*(\d+)\s*>\s*<U4\s*\[\d+\]\s*(\d+)\s*>\s*<L\s*\[(\d+)\]",
            RegexOptions.Singleline);

        if (m.Success)
        {
            yield return $"EventReport: DATAID={m.Groups[1].Value}, CEID={m.Groups[2].Value}, Reports={m.Groups[3].Value}.";
        }
        else
        {
            yield return "EventReport: unable to parse DATAID/CEID.";
        }

        var aliases = Regex.Matches(raw, @"/\*\s*([^*]+?)\s*\*/")
            .Cast<Match>()
            .Select(x => x.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (aliases.Length > 0)
        {
            yield return $"Reported fields: {string.Join(", ", aliases.Take(8))}.";
        }

        var reportIds = Regex.Matches(raw, @"<L\s*\[2\]\s*<U4\s*\[\d+\]\s*(\d+)\s*>")
            .Cast<Match>()
            .Select(x => x.Groups[1].Value)
            .Distinct()
            .ToArray();

        if (reportIds.Length > 0)
        {
            yield return $"Report IDs: {string.Join(", ", reportIds)}.";
        }

        foreach (var summary in ExtractReportValueSummaries(raw))
        {
            yield return summary;
        }
    }

    private static IEnumerable<string> DescribeS6F1(string raw)
    {
        var m = Regex.Match(raw, @"<U1\s*\[\d+\]\s*(\d+)\s*>\s*<A\s*\[\d+\]\s*'([^']*)'", RegexOptions.Singleline);
        if (m.Success)
        {
            yield return $"TraceData: TRID={m.Groups[1].Value}, DSPER='{m.Groups[2].Value}'.";
        }
        else
        {
            yield return "TraceData: primary trace sample received.";
        }

        var scalarTokens = Regex.Matches(
                raw,
                @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*(?:/\*\s*([^*]+?)\s*\*/)?",
                RegexOptions.Singleline)
            .Cast<Match>()
            .Select(x =>
            {
                var tokenType = x.Groups[1].Value;
                var tokenValue = x.Groups[2].Value.Trim();
                var tokenName = x.Groups[3].Success ? x.Groups[3].Value.Trim() : string.Empty;
                return string.IsNullOrWhiteSpace(tokenName)
                    ? $"{tokenType}={tokenValue}"
                    : $"{tokenName}={tokenValue}";
            })
            .Skip(2)
            .Take(6)
            .ToArray();

        if (scalarTokens.Length > 0)
        {
            yield return $"Trace values: {string.Join(", ", scalarTokens)}.";
        }
    }

    private static IEnumerable<string> ExtractReportValueSummaries(string raw)
    {
        var reportMatches = Regex.Matches(
            raw,
            @"<L\s*\[2\]\s*<U4\s*\[\d+\]\s*(\d+)\s*>\s*<L\s*\[\d+\]\s*(.*?)\s*>\s*>",
            RegexOptions.Singleline);

        foreach (Match report in reportMatches)
        {
            var rptId = report.Groups[1].Value;
            var valueBody = report.Groups[2].Value;

            var values = Regex.Matches(
                    valueBody,
                    @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*(?:/\*\s*([^*]+?)\s*\*/)?",
                    RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m =>
                {
                    var type = m.Groups[1].Value;
                    var rawValue = m.Groups[2].Value.Trim();
                    var alias = m.Groups[3].Success ? m.Groups[3].Value.Trim() : string.Empty;
                    return string.IsNullOrWhiteSpace(alias)
                        ? $"{type}={rawValue}"
                        : $"{alias}={rawValue}";
                })
                .Take(4)
                .ToArray();

            if (values.Length == 0)
            {
                yield return $"Report[{rptId}]: no scalar values parsed.";
            }
            else
            {
                yield return $"Report[{rptId}]: {string.Join(", ", values)}.";
            }
        }
    }
}
