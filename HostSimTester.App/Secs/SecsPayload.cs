using Secs4Net;

namespace HostSimTester.App.Secs;

/// <summary>
/// Object-level helpers for reading well-known SECS payload positions.
/// Mirrors the reference TOOLID.xml shape: S6F11 = L[3]( DataID, CEID, ReportList ).
/// </summary>
public static class SecsPayload
{
    public static readonly IReadOnlyDictionary<uint, string> KnownEventNames = new Dictionary<uint, string>
    {
        [3] = "ToolModeChange_Remote",
        [41] = "PJStatusChange_Pooled",
        [195] = "AutoDocking",
        [1005] = "ReadyToUnload",
        [5118] = "CarrierUnloadComplete",
        [5790] = "WaferProcessEnd",
        [5799] = "WaferProcessStart",
        [1000] = "CarrierIDRead",
        [1004] = "ReadyToLoad",
        [1006] = "CarrierClamped",
        [6199] = "ControlJobStart"
    };

    public static bool TryGetS6F11DataId(SecsMessage message, out uint dataId)
    {
        dataId = 0;
        if (message.S != 6 || message.F != 11)
        {
            return false;
        }

        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 1)
        {
            return false;
        }

        return TryReadUInt(root[0], out dataId);
    }

    /// <summary>
    /// Try to read CEID directly from an S6F11 SecsItem tree.
    /// CEID is always the second numeric child of the top-level L[3].
    /// Supports U1/U2/U4/U8 source formats.
    /// </summary>
    public static bool TryGetS6F11Ceid(SecsMessage message, out uint ceid)
    {
        ceid = 0;
        if (message.S != 6 || message.F != 11)
        {
            return false;
        }

        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 2)
        {
            return false;
        }

        return TryReadUInt(root[1], out ceid);
    }

    /// <summary>
    /// Try to read RPTIDs from S6F11 report list. Returns the first RPTID found.
    /// Shape: L[3](DataID, CEID, L[reports]( L[2]( RPTID, L[values] )* ) ).
    /// </summary>
    public static bool TryGetS6F11FirstRptid(SecsMessage message, out uint rptid)
    {
        rptid = 0;
        if (message.S != 6 || message.F != 11)
        {
            return false;
        }

        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 3)
        {
            return false;
        }

        var reportList = root[2];
        if (reportList.Format != SecsFormat.List || reportList.Count < 1)
        {
            return false;
        }

        var firstReport = reportList[0];
        if (firstReport.Format != SecsFormat.List || firstReport.Count < 1)
        {
            return false;
        }

        return TryReadUInt(firstReport[0], out rptid);
    }

    public static IReadOnlyList<uint> GetS6F11Rptids(SecsMessage message)
    {
        if (message.S != 6 || message.F != 11)
        {
            return Array.Empty<uint>();
        }

        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 3)
        {
            return Array.Empty<uint>();
        }

        var reportList = root[2];
        if (reportList.Format != SecsFormat.List || reportList.Count == 0)
        {
            return Array.Empty<uint>();
        }

        var rptids = new List<uint>();
        for (var i = 0; i < reportList.Count; i++)
        {
            var report = reportList[i];
            if (report.Format != SecsFormat.List || report.Count < 1)
            {
                continue;
            }

            if (TryReadUInt(report[0], out var rptid))
            {
                rptids.Add(rptid);
            }
        }

        return rptids;
    }

    public static string GetEventName(uint ceid)
    {
        return KnownEventNames.TryGetValue(ceid, out var name) ? name : $"Unknown_CEID_{ceid}";
    }

    public static bool TryExtractCarrierReadInfo(SecsMessage message, out byte portId, out string carrierId, out string locationId)
    {
        portId = 0;
        carrierId = string.Empty;
        locationId = string.Empty;

        if (message.S != 6 || message.F != 11)
        {
            return false;
        }

        if (TryGetS6F11Ceid(message, out var ceid) && ceid != 1000)
        {
            return false;
        }

        var root = message.SecsItem;
        if (root is null || root.Format != SecsFormat.List || root.Count < 3)
        {
            return false;
        }

        var reports = root[2];
        if (reports.Format != SecsFormat.List || reports.Count == 0)
        {
            return false;
        }

        for (var reportIndex = 0; reportIndex < reports.Count; reportIndex++)
        {
            var report = reports[reportIndex];
            if (report.Format != SecsFormat.List || report.Count < 2)
            {
                continue;
            }

            var values = report[1];
            if (values.Format != SecsFormat.List || values.Count < 2)
            {
                continue;
            }

            byte? parsedPortId = null;
            string? parsedCarrierId = null;
            string? parsedLocationId = null;

            if (values.Count > 1 && TryReadAsciiValue(values[1], out var preferredCarrierId) && IsLikelyCarrierId(preferredCarrierId))
            {
                parsedCarrierId = preferredCarrierId.Trim();
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];

                if (!parsedPortId.HasValue && TryReadByteValue(value, out var numericPortId) && numericPortId is >= 1 and <= 4)
                {
                    parsedPortId = numericPortId;
                    continue;
                }

                if (!TryReadAsciiValue(value, out var asciiValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parsedLocationId) && TryExtractPortIdFromLocation(asciiValue, out var locationPortId))
                {
                    parsedLocationId = asciiValue.Trim();
                    parsedPortId ??= locationPortId;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parsedCarrierId) && IsLikelyCarrierId(asciiValue))
                {
                    parsedCarrierId = asciiValue.Trim();
                }
            }

            if (!parsedPortId.HasValue || string.IsNullOrWhiteSpace(parsedCarrierId))
            {
                continue;
            }

            portId = parsedPortId.Value;
            carrierId = parsedCarrierId.Trim();
            locationId = parsedLocationId ?? string.Empty;
            return true;
        }

        return false;
    }

    public static bool TryReadUInt(Item item, out uint value)
    {
        value = 0;
        if (item is null || item.Count == 0)
        {
            return false;
        }

        try
        {
            switch (item.Format)
            {
                case SecsFormat.U1:
                    value = item.FirstValue<byte>();
                    return true;
                case SecsFormat.Binary:
                    value = item.FirstValue<byte>();
                    return true;
                case SecsFormat.U2:
                    value = item.FirstValue<ushort>();
                    return true;
                case SecsFormat.U4:
                    value = item.FirstValue<uint>();
                    return true;
                case SecsFormat.U8:
                    var u8 = item.FirstValue<ulong>();
                    value = u8 > uint.MaxValue ? uint.MaxValue : (uint)u8;
                    return true;
                case SecsFormat.I1:
                    value = (uint)item.FirstValue<sbyte>();
                    return true;
                case SecsFormat.I2:
                    value = (uint)item.FirstValue<short>();
                    return true;
                case SecsFormat.I4:
                    value = (uint)item.FirstValue<int>();
                    return true;
                case SecsFormat.I8:
                    value = (uint)item.FirstValue<long>();
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractPortIdFromLocation(string value, out byte portId)
    {
        portId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(
            value.Trim(),
            @"^(?:LO|LOAD)?PORT\s*([1-4])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success || !byte.TryParse(match.Groups[1].Value, out var parsedPortId))
        {
            return false;
        }

        portId = parsedPortId;
        return true;
    }

    private static bool IsLikelyCarrierId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (TryExtractPortIdFromLocation(normalized, out _) || IsLikelyClockValue(normalized))
        {
            return false;
        }

        if (normalized.Equals("CarrierID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PortID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("LocationID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("CLOCK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ContentMap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z0-9._-]{3,64}$");
    }

    private static bool IsLikelyClockValue(string value)
    {
        var normalized = value.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^20\d{12,15}$"))
        {
            return false;
        }

        var year = int.Parse(normalized[..4]);
        var month = int.Parse(normalized.Substring(4, 2));
        var day = int.Parse(normalized.Substring(6, 2));
        var hour = int.Parse(normalized.Substring(8, 2));
        var minute = int.Parse(normalized.Substring(10, 2));
        var second = int.Parse(normalized.Substring(12, 2));

        return year is >= 2000 and <= 2099 &&
            month is >= 1 and <= 12 &&
            day is >= 1 and <= 31 &&
            hour is >= 0 and <= 23 &&
            minute is >= 0 and <= 59 &&
            second is >= 0 and <= 59;
    }

    private static bool TryReadByteValue(Item item, out byte value)
    {
        value = 0;

        if (!TryReadUInt(item, out var unsignedValue) || unsignedValue > byte.MaxValue)
        {
            return false;
        }

        value = (byte)unsignedValue;
        return true;
    }

    private static bool TryReadAsciiValue(Item item, out string value)
    {
        value = string.Empty;

        if (item is null || item.Format == SecsFormat.List || item.Count == 0)
        {
            return false;
        }

        try
        {
            value = item.GetString().Trim();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }
}
