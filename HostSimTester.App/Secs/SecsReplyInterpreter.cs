using System.Text.RegularExpressions;
using Secs4Net;

namespace HostSimTester.App.Secs;

public static class SecsReplyInterpreter
{
    public static IReadOnlyList<string> DescribePrimary(PrimaryMessageWrapper primary)
    {
        var msg = primary.PrimaryMessage;

        var lines = new List<string>
        {
            $"Primary S{msg.S}F{msg.F} Name={msg.Name}"
        };

        switch ((msg.S, msg.F))
        {
            case (6, 11):
                lines.AddRange(DescribeS6F11(msg));
                break;
            case (6, 1):
                lines.AddRange(DescribeS6F1(msg));
                break;
            default:
                lines.Add("Primary message received.");
                break;
        }

        lines.Add($"Raw: {msg}");
        lines.Add($"Body:{Environment.NewLine}{SecsItemFormatter.Format(msg.SecsItem)}");
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
                lines.Add(DescribeAck("COMMACK", reply.SecsItem));
                break;
            case (1, 16):
                lines.Add(DescribeAck("OFLACK", reply.SecsItem));
                break;
            case (1, 18):
                lines.Add(DescribeAck("ONLACK", reply.SecsItem));
                break;
            case (2, 38):
                lines.Add(DescribeAck("ERACK", reply.SecsItem));
                break;
            case (2, 42):
                lines.Add(DescribeAck("HCACK", reply.SecsItem));
                break;
            case (3, 18):
                lines.Add(DescribeCarrierActionReply(reply.SecsItem));
                break;
            case (16, 16):
                lines.Add(DescribeProcessJobCreateReply(reply.SecsItem));
                break;
            case (16, 6):
                lines.Add(DescribeProcessJobCommandReply(reply.SecsItem));
                break;
            case (14, 10):
                lines.Add(DescribeCreateObjectReply(reply.SecsItem));
                break;
            default:
                lines.Add("Secondary message received.");
                break;
        }

        lines.Add($"Raw: {reply}");
        lines.Add($"Body:{Environment.NewLine}{SecsItemFormatter.Format(reply.SecsItem)}");
        return lines;
    }

    private static string DescribeAck(string ackName, Item? item)
    {
        if (!TryReadFirstAckCode(item, out var code))
        {
            return $"{ackName}: unable to parse ack code.";
        }

        return code == 0
            ? $"{ackName}: ACK (0x00)."
            : $"{ackName}: NAK/ERROR (0x{code:X2}).";
    }

    private static string DescribeCarrierActionReply(Item? item)
    {
        if (item is null)
        {
            return "Carrier action reply received (empty body).";
        }

        if (item.Format == SecsFormat.List && item.Count >= 1 && SecsPayload.TryReadUInt(item[0], out var ack))
        {
            return ack == 0
                ? "Carrier action reply: ACK (0)."
                : $"Carrier action reply: NAK/ERROR ({ack}).";
        }

        return "Carrier action reply received.";
    }

    private static string DescribeProcessJobCreateReply(Item? item)
    {
        if (item is null)
        {
            return "ProcessJobCreate: empty reply body.";
        }

        if (item.Format == SecsFormat.List && item.Count >= 2)
        {
            var jobCount = item[0].Format == SecsFormat.List ? item[0].Count : 0;
            var ack = item[1];
            if (ack.Format == SecsFormat.List && ack.Count >= 1)
            {
                var okText = TryReadBool(ack[0], out var ok)
                    ? ok ? "ACK" : "NAK/ERROR"
                    : "unknown ACK flag";
                return $"ProcessJobCreate: {okText}, jobs={jobCount}.";
            }

            return $"ProcessJobCreate: secondary reply received, jobs={jobCount}.";
        }

        return "ProcessJobCreate: secondary reply received.";
    }

    private static string DescribeProcessJobCommandReply(Item? item)
    {
        if (item is null || item.Format != SecsFormat.List || item.Count < 2)
        {
            return "ProcessJobCommand: unable to parse ACK list.";
        }

        var processJobId = TryReadAscii(item[0], out var parsedProcessJobId) ? parsedProcessJobId : "?";
        var ack = item[1];
        if (ack.Format != SecsFormat.List || ack.Count < 1 || !TryReadBool(ack[0], out var ok))
        {
            return $"ProcessJobCommand: unable to parse ACK flag, job={processJobId}.";
        }

        if (ok)
        {
            return $"ProcessJobCommand: ACK, job={processJobId}.";
        }

        var detail = ExtractFirstErrorText(ack.Count >= 2 ? ack[1] : null);
        return string.IsNullOrWhiteSpace(detail)
            ? $"ProcessJobCommand: NAK/ERROR, job={processJobId}."
            : $"ProcessJobCommand: NAK/ERROR, job={processJobId}, reason='{detail}'.";
    }

    private static string DescribeCreateObjectReply(Item? item)
    {
        if (item is null || item.Format != SecsFormat.List || item.Count < 3)
        {
            return "CreateObject: unable to parse ACK list.";
        }

        var ackList = item[2];
        if (ackList.Format != SecsFormat.List || ackList.Count < 1)
        {
            return "CreateObject: unable to parse ACK list.";
        }

        if (!SecsPayload.TryReadUInt(ackList[0], out var code))
        {
            return "CreateObject: unable to parse ACK code.";
        }

        if (code == 0)
        {
            return "CreateObject: ACK (0).";
        }

        var detailCount = 0;
        uint? detailCode = null;
        var reason = "no text";
        if (ackList.Count >= 2 && ackList[1].Format == SecsFormat.List)
        {
            detailCount = ackList[1].Count;
            for (var i = 0; i < ackList[1].Count; i++)
            {
                var detailEntry = ackList[1][i];
                if (detailEntry.Format != SecsFormat.List || detailEntry.Count < 2)
                {
                    continue;
                }

                if (detailCode is null && SecsPayload.TryReadUInt(detailEntry[0], out var parsedCode))
                {
                    detailCode = parsedCode;
                }

                if (TryReadAscii(detailEntry[1], out var parsedReason))
                {
                    reason = parsedReason;
                    break;
                }
            }
        }

        var detail = detailCode.HasValue ? $", detail={detailCode.Value}" : string.Empty;
        return $"CreateObject: NAK/ERROR ({code}){detail}, entries={detailCount}, reason='{reason}'.";
    }

    private static string ExtractFirstErrorText(Item? item)
    {
        if (item is null || item.Format != SecsFormat.List)
        {
            return string.Empty;
        }

        for (var i = 0; i < item.Count; i++)
        {
            var entry = item[i];
            if (entry.Format == SecsFormat.ASCII && TryReadAscii(entry, out var directText))
            {
                return directText;
            }

            if (entry.Format != SecsFormat.List)
            {
                continue;
            }

            for (var j = 0; j < entry.Count; j++)
            {
                if (entry[j].Format == SecsFormat.ASCII && TryReadAscii(entry[j], out var nestedText))
                {
                    return nestedText;
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> DescribeS6F11(SecsMessage message)
    {
        var hasDataId = SecsPayload.TryGetS6F11DataId(message, out var dataId);
        var hasCeid = SecsPayload.TryGetS6F11Ceid(message, out var ceid);
        var rptids = SecsPayload.GetS6F11Rptids(message);
        if (hasDataId && hasCeid)
        {
            yield return $"EventReport: DATAID={dataId}, CEID={ceid}, EventName={SecsPayload.GetEventName(ceid)}, Reports={rptids.Count}.";
        }
        else
        {
            yield return "EventReport: unable to parse DATAID/CEID.";
        }

        if (rptids.Count > 0)
        {
            yield return $"Report IDs: {string.Join(", ", rptids)}.";
        }

        foreach (var summary in ExtractReportValueSummaries(message))
        {
            yield return summary;
        }
    }

    private static IEnumerable<string> DescribeS6F1(SecsMessage message)
    {
        var raw = SecsItemFormatter.Format(message.SecsItem);
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

    private static IEnumerable<string> ExtractReportValueSummaries(SecsMessage message)
    {
        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 3)
        {
            yield break;
        }

        var reports = root[2];
        if (reports.Format != SecsFormat.List)
        {
            yield break;
        }

        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            if (report.Format != SecsFormat.List || report.Count < 2)
            {
                continue;
            }

            var rptId = SecsPayload.TryReadUInt(report[0], out var parsedRptId)
                ? parsedRptId.ToString()
                : "?";
            var valuesItem = report[1];
            if (valuesItem.Format != SecsFormat.List)
            {
                yield return $"Report[{rptId}]: values is not list.";
                continue;
            }

            var values = Enumerable.Range(0, valuesItem.Count)
                .Select(index => $"v{index}={FormatValuePreview(valuesItem[index])}")
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

    private static bool TryReadFirstAckCode(Item? item, out uint code)
    {
        code = 0;
        if (item is null)
        {
            return false;
        }

        if (item.Format != SecsFormat.List)
        {
            return SecsPayload.TryReadUInt(item, out code);
        }

        if (item.Count == 0)
        {
            return false;
        }

        return SecsPayload.TryReadUInt(item[0], out code);
    }

    private static bool TryReadBool(Item item, out bool value)
    {
        value = false;
        if (item.Format != SecsFormat.Boolean || item.Count == 0)
        {
            return false;
        }

        try
        {
            value = item.FirstValue<bool>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadAscii(Item item, out string value)
    {
        value = string.Empty;
        if (item.Format != SecsFormat.ASCII)
        {
            return false;
        }

        try
        {
            value = item.GetString().Trim();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatValuePreview(Item item)
    {
        if (item.Format == SecsFormat.List)
        {
            return $"L[{item.Count}]";
        }

        var formatted = SecsItemFormatter.Format(item).Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return formatted.Length <= 80 ? formatted : formatted[..80] + "...";
    }
}
