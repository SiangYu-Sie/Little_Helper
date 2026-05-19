using Secs4Net;
using static Secs4Net.Item;

namespace HostSimTester.App.Secs;

public static class SecsMessageFactory
{
    private const int FormattedRecipeRequiredPparmCount = 40;

    public sealed record FormattedRecipeTemplate(
        string Model,
        string SoftRev,
        string CCode,
        IReadOnlyList<Item> Pparms,
        Item? RawFormattedBody = null);

    private static readonly byte[] DefaultRecipeBody =
    [
        0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x01,
        0x00, 0x2D, 0x00, 0x5A, 0x00, 0x87, 0x00, 0xB4
    ];

    public static Item S1F13EstablishCommunicationRequest()
    {
        return L();
    }

    public static Item S1F14EstablishCommunicationAck(byte commAck = 0x00, string model = "HostSimTester", string softwareRev = "1.0.0")
    {
        return L(
            B([commAck]),
            L(
                A(model),
                A(softwareRev)));
    }

    public static Item S2F37EnableDisableEventReport(bool enable)
    {
        return L(Boolean([enable]), L());
    }

    public static Item S2F33DefineReport(uint reportId, IEnumerable<uint> dvids)
    {
        var dvidArray = dvids as uint[] ?? dvids.ToArray();
        return L(
            U4([0]),
            L(
                L(
                    U2([(ushort)reportId]),
                    L(dvidArray.Select(dvid => U4([dvid]))))));
    }

    public static Item S2F33DeleteAllReports()
    {
        return L(U4([0]), L());
    }

    public static Item S2F35LinkEventReport(uint ceid, uint reportId)
    {
        return L(
            U4([0]),
            L(
                L(
                    U4([ceid]),
                    L(U2([(ushort)reportId])))));
    }

    public static Item S2F35UnlinkAllReports()
    {
        return L(U4([0]), L());
    }

    public static Item S3F27SetAccessMode(byte mode)
    {
        return L(U1([mode]));
    }

    public static Item S3F27SetAccessMode(byte mode, IEnumerable<byte> portIds)
    {
        var ids = portIds as byte[] ?? portIds.ToArray();
        return L(
            U1([mode]),
            L(ids.Select(id => U1([id]))));
    }

    public static Item S2F41HostCommand(string command)
    {
        return L(A(command), L());
    }

    public static Item S2F41HostCommand(string command, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return L(
            A(command),
            L(parameters.Select(p => L(A(p.Key), A(p.Value)))));
    }

    public static Item S1F3SelectedEquipmentStatusRequest(uint svid)
    {
        return L(U4([svid]));
    }

    public static Item S3F17ProceedWithCarrier(string carrierId, byte portId)
    {
        return L(
            U4([0]),
            A("ProceedWithCarrier"),
            A(carrierId),
            U1([portId]),
            L());
    }

    /// <summary>
    /// S3F17 ProceedWithCarrier 含 ContentMap 屬性（由對話視窗取得 slot 資料）。
    /// </summary>
    public static Item S3F17ProceedWithCarrierContentMap(
        string carrierId,
        byte portId,
        string cattrid,
        IReadOnlyList<(string LotId, string WaferId)> slots)
    {
        return L(
            U4([0]),
            A("ProceedWithCarrier"),
            A(carrierId),
            U1([portId]),
            L(
                L(
                    A(cattrid),
                    L(slots.Select(s => L(
                        A(s.LotId),
                        A(s.WaferId),
                        A(string.Empty),
                        A(string.Empty),
                        A(string.Empty),
                        A(string.Empty)))))));
    }

    public static Item S3F17ProceedWithSlotMap(string carrierId, byte portId, IEnumerable<string>? slotIds = null)
    {
        var slots = (slotIds ?? Array.Empty<string>())
            .Select(slotId => slotId.Trim())
            .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
            .Distinct()
            .Select(slotId => (LotId: string.Empty, WaferId: slotId))
            .ToArray();

        return S3F17ProceedWithCarrierContentMap(carrierId, portId, "ContentMap", slots);
    }

    public static Item S3F17CancelCarrier(string carrierId, byte portId)
    {
        return L(
            U4([0]),
            A("CancelCarrier"),
            A(carrierId),
            U1([portId]),
            L());
    }

    public static Item S3F17CancelCarrierAtPort(byte portId)
    {
        return L(
            U4([0]),
            A("CancelCarrierAtPort"),
            A(string.Empty),
            U1([portId]),
            L());
    }

    public static Item S3F17CarrierRelease(string carrierId, byte portId)
    {
        return L(
            U4([0]),
            A("CarrierRelease"),
            A(carrierId),
            U1([portId]),
            L());
    }

    public static Item S16F15ProcessJobCreate(string processJobId, string recipeId, string carrierId)
    {
        return S16F15ProcessJobCreate(processJobId, recipeId, carrierId, null, autoStart: true);
    }

    public static Item S16F15ProcessJobCreate(string processJobId, string recipeId, string carrierId, IEnumerable<string>? slotIds, bool autoStart = true)
    {
        return L(
            U4([1]),
            L(
                L(
                    A(processJobId),
                    B([0x0D]),
                    L(
                        L(
                            A(carrierId),
                            BuildSlotIdList(slotIds))),
                    L(
                        U1([1]),
                        A(recipeId),
                        L()),
                    Boolean([autoStart]),
                    L())));
    }

    public static Item S14F9ControlJobCreate(string controlJobId, string carrierId)
    {
        return S14F9ControlJobCreate(controlJobId, carrierId, Array.Empty<string>());
    }

    public static Item S14F9ControlJobCreate(string controlJobId, string carrierId, IEnumerable<string>? processJobIds, byte processOrderMgmt = 2, IEnumerable<string>? slotIds = null)
    {
        var processJobList = processJobIds is null
            ? []
            : processJobIds
                .Select(processJobId => processJobId.Trim())
                .Where(processJobId => !string.IsNullOrWhiteSpace(processJobId))
                .Distinct()
                .Select(processJobId => L(
                    A(processJobId),
                    BuildSlotIdList(slotIds),
                    L()))
                .ToArray();

        return L(
            A("Equipment"),
            A("ControlJob"),
            L(
                L(A("ObjID"), A(controlJobId)),
                L(A("CarrierInputSpec"), L(A(carrierId))),
                L(A("MtrlOutSpec"), L()),
                L(A("ProcessingCtrlSpec"), L(processJobList)),
                L(A("ProcessOrderMgmt"), U1([processOrderMgmt])),
                L(A("StartMethod"), Boolean([true]))));
    }

    public static Item S14F9ControlJobCreate(
        string controlJobId,
        string carrierId,
        IEnumerable<(string ProcessJobId, IEnumerable<string> SlotIds)> processJobs,
        byte processOrderMgmt = 2)
    {
        var processJobList = processJobs
            .Select(job => (ProcessJobId: job.ProcessJobId.Trim(), SlotIds: job.SlotIds))
            .Where(job => !string.IsNullOrWhiteSpace(job.ProcessJobId))
            .Select(job => L(
                A(job.ProcessJobId),
                BuildSlotIdList(job.SlotIds),
                L()))
            .ToArray();

        return L(
            A("Equipment"),
            A("ControlJob"),
            L(
                L(A("ObjID"), A(controlJobId)),
                L(A("CarrierInputSpec"), L(A(carrierId))),
                L(A("MtrlOutSpec"), L()),
                L(A("ProcessingCtrlSpec"), L(processJobList)),
                L(A("ProcessOrderMgmt"), U1([processOrderMgmt])),
                L(A("StartMethod"), Boolean([true]))));
    }

    public static Item S5F3EnableDisableAlarm(bool enable)
    {
        return L(B([enable ? (byte)0x80 : (byte)0x00]), U4());
    }

    public static Item S5F5QueryAlarmList()
    {
        return U4();
    }

    public static Item S5F7QueryEnabledAlarmList()
    {
        return L();
    }

    public static Item S7F25FormattedProcessProgramRequest(string ppid)
    {
        return A(ppid);
    }

    public static Item S7F5UnformattedProcessProgramRequest(string ppid)
    {
        return A(ppid);
    }

    public static Item S7F23FormattedProcessProgramSend(string ppid)
    {
        var fallbackPparms = Enumerable.Range(1, FormattedRecipeRequiredPparmCount)
            .Select(i => L(U2([(ushort)i]), L(A("0"))))
            .ToArray();

        var fallback = new FormattedRecipeTemplate(
            Model: "HostSimTester",
            SoftRev: "1.0.0",
            CCode: "100",
            Pparms: fallbackPparms);

        return S7F23FormattedProcessProgramSend(ppid, fallback, refreshTime: false);
    }

    public static Item S7F23FormattedProcessProgramSend(string ppid, FormattedRecipeTemplate template, bool refreshTime = false)
    {
        if (template.RawFormattedBody is not null)
        {
            return L(
                A(ppid),
                A(template.Model),
                A(template.SoftRev),
                CloneItem(template.RawFormattedBody));
        }

        var pparms = NormalizeFormattedPparms(template.Pparms);
        if (refreshTime)
        {
            RefreshTrailingAsciiDateTime(pparms);
        }

        return L(
            A(ppid),
            A(template.Model),
            A(template.SoftRev),
            L(
                L(
                    BuildCcodeItem(template.CCode),
                    L(pparms))));
    }

    private static Item BuildCcodeItem(string ccode)
    {
        var normalized = ccode.Trim();
        return ushort.TryParse(normalized, out var u2)
            ? U2([u2])
            : A(normalized);
    }

    public static bool TryExtractFormattedRecipeTemplateFromS7F26(Item? s7f26Body, out FormattedRecipeTemplate template, out string reason)
    {
        template = default!;
        reason = string.Empty;

        if (s7f26Body is null || s7f26Body.Format != SecsFormat.List || s7f26Body.Count < 4)
        {
            reason = "S7F26 root format mismatch.";
            return false;
        }

        if (!TryGetScalarString(s7f26Body[1], out var model) || string.IsNullOrWhiteSpace(model))
        {
            reason = "S7F26 MDLN missing.";
            return false;
        }

        if (!TryGetScalarString(s7f26Body[2], out var softRev) || string.IsNullOrWhiteSpace(softRev))
        {
            reason = "S7F26 SOFTREV missing.";
            return false;
        }

        var formattedBody = s7f26Body[3];
        if (formattedBody.Format != SecsFormat.List || formattedBody.Count == 0)
        {
            reason = "S7F26 formatted body missing.";
            return false;
        }

        var candidates = new List<(string CCode, Item[] Pparms)>();
        for (var i = 0; i < formattedBody.Count; i++)
        {
            var ccodeGroup = formattedBody[i];
            if (ccodeGroup.Format != SecsFormat.List || ccodeGroup.Count < 2)
            {
                continue;
            }

            if (!TryGetScalarString(ccodeGroup[0], out var ccode) || string.IsNullOrWhiteSpace(ccode))
            {
                continue;
            }

            var pparmList = ccodeGroup[1];
            if (!IsLegacyPparmList(pparmList))
            {
                continue;
            }

            var pparms = new Item[pparmList.Count];
            for (var p = 0; p < pparmList.Count; p++)
            {
                pparms[p] = CloneItem(pparmList[p]);
            }

            candidates.Add((ccode.Trim(), pparms));
        }

        if (candidates.Count == 0)
        {
            template = new FormattedRecipeTemplate(
                model.Trim(),
                softRev.Trim(),
                "RAW",
                Array.Empty<Item>(),
                CloneItem(formattedBody));
            return true;
        }

        var selected = candidates.FirstOrDefault(x => string.Equals(x.CCode, "100", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(selected.CCode))
        {
            selected = candidates[0];
        }

        template = new FormattedRecipeTemplate(model.Trim(), softRev.Trim(), selected.CCode, selected.Pparms);
        return true;
    }

    public static Item S7F23UnformattedProcessProgramSend(string ppid)
    {
        return L(A(ppid), B(DefaultRecipeBody));
    }

    public static Item S7F3UnformattedProcessProgramSend(string ppid)
    {
        return L(A(ppid), B(DefaultRecipeBody));
    }

    public static Item S7F3UnformattedProcessProgramSend(string ppid, byte[] recipeBody)
    {
        return L(A(ppid), B(recipeBody));
    }

    public static bool TryExtractUnformattedRecipeBodyFromS7F6(Item? s7f6Body, out byte[] recipeBody, out string reason)
    {
        recipeBody = Array.Empty<byte>();
        reason = string.Empty;

        if (s7f6Body is null || s7f6Body.Format != SecsFormat.List || s7f6Body.Count < 2)
        {
            reason = "S7F6 root format mismatch.";
            return false;
        }

        var bodyItem = s7f6Body[1];
        if (bodyItem.Format != SecsFormat.Binary)
        {
            reason = "S7F6 body is not binary.";
            return false;
        }

        recipeBody = bodyItem.GetMemory<byte>().ToArray();
        if (recipeBody.Length == 0)
        {
            reason = "S7F6 body is empty.";
            return false;
        }

        return true;
    }

    public static Item S7F17DeleteProcessProgramSend(string ppid)
    {
        return L(A(ppid));
    }

    public static Item S2F23TraceInitialize(byte traceId, string dsper, uint totalSamples, byte reportGroupSize, IEnumerable<uint> svids)
    {
        var svidArray = svids as uint[] ?? svids.ToArray();

        return L(
            U1([traceId]),
            A(dsper),
            U4([totalSamples]),
            U1([reportGroupSize]),
            L(svidArray.Select(s => U4([s]))));
    }

    public static Item S1F21DataVariableNameListRequest(IEnumerable<uint>? queryIds = null)
    {
        return BuildIdQueryList(queryIds);
    }

    public static Item S1F11StatusVariableNameListRequest(IEnumerable<uint>? queryIds = null)
    {
        return BuildIdQueryList(queryIds);
    }

    public static Item S1F23CollectionEventNameListRequest(IEnumerable<uint>? queryIds = null)
    {
        return BuildIdQueryList(queryIds);
    }

    public static Item S2F29EquipmentConstantNameListRequest(IEnumerable<uint>? queryIds = null)
    {
        return BuildIdQueryList(queryIds);
    }

    public static Item S2F13EquipmentConstantRequest(IEnumerable<byte> ecids)
    {
        var ids = ecids as byte[] ?? ecids.ToArray();
        return L(ids.Select(id => U1([id])));
    }

    public static Item S2F15EquipmentConstantSendU1(byte ecid, byte ecv)
    {
        return L(
            L(U1([ecid]), U1([ecv])));
    }

    public static Item S16F5ProcessJobCommand(string processJobId, string command)
    {
        return L(
            U4([0]),
            A(processJobId),
            A(command),
            L());
    }

    public static Item S16F27ControlJobCommand(string controlJobId, byte controlJobCommand, byte action = 1)
    {
        return L(
            A(controlJobId),
            U1([controlJobCommand]),
            L(A("ACTION"), U1([action])));
    }

    public static Item S2F41PPSelect(byte loadPortId, string recipeId)
    {
        return S2F41HostCommand(
            "PP-SELECT",
            [
                new KeyValuePair<string, string>("LOADPORT-ID", $"'{loadPortId}'"),
                new KeyValuePair<string, string>("RECIPE-ID", recipeId)
            ]);
    }

    public static Item S2F41PPStart(byte loadPortId)
    {
        return S2F41HostCommand(
            "SLOTMAP-L",
            [new KeyValuePair<string, string>("LOADPORT-ID", $"'{loadPortId}'")]);
    }

    public static Item S2F41HostCommandByParameters(string command, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return S2F41HostCommand(command, parameters);
    }

    public static Item S2F41HostCommandByTypedParameters(string command, IEnumerable<(string Name, string Type, string Value)> parameters)
    {
        return L(
            A(command),
            L(parameters.Select(p => L(A(p.Name), BuildTypedValue(p.Type, p.Value)))));
    }

    public static Item S1F3SelectedEquipmentStatusRequest(IEnumerable<uint> svids)
    {
        var ids = svids as uint[] ?? svids.ToArray();
        return L(ids.Select(id => U4([id])));
    }

    private static Item BuildSlotIdList(IEnumerable<string>? slotIds)
    {
        if (slotIds is null)
        {
            return L();
        }

        var parsedSlotIds = slotIds
            .Select(slotId => slotId.Trim())
            .Where(slotId => byte.TryParse(slotId, out _))
            .Select(byte.Parse)
            .Distinct()
            .Select(slotId => U1([slotId]))
            .ToArray();

        return L(parsedSlotIds);
    }

    private static Item BuildTypedValue(string type, string rawValue)
    {
        var normalizedType = type.Trim().ToUpperInvariant();
        var value = rawValue.Trim().Trim('"', '\'');

        return normalizedType switch
        {
            "B" => B([ParseByte(value)]),
            "BOOLEAN" or "BOOL" => Boolean([ParseBool(value)]),
            "I1" => I1([ParseSByte(value)]),
            "I2" => I2([ParseShort(value)]),
            "I4" => I4([ParseInt(value)]),
            "I8" => I8([ParseLong(value)]),
            "U1" => U1([ParseByte(value)]),
            "U2" => U2([ParseUShort(value)]),
            "U4" => U4([ParseUInt(value)]),
            "U8" => U8([ParseULong(value)]),
            "F4" => F4([ParseFloat(value)]),
            "F8" => F8([ParseDouble(value)]),
            _ => A(value)
        };
    }

    private static byte ParseByte(string value) => byte.TryParse(value, out var parsed) ? parsed : (byte)0;
    private static sbyte ParseSByte(string value) => sbyte.TryParse(value, out var parsed) ? parsed : (sbyte)0;
    private static short ParseShort(string value) => short.TryParse(value, out var parsed) ? parsed : (short)0;
    private static int ParseInt(string value) => int.TryParse(value, out var parsed) ? parsed : 0;
    private static long ParseLong(string value) => long.TryParse(value, out var parsed) ? parsed : 0L;
    private static ushort ParseUShort(string value) => ushort.TryParse(value, out var parsed) ? parsed : (ushort)0;
    private static uint ParseUInt(string value) => uint.TryParse(value, out var parsed) ? parsed : 0u;
    private static ulong ParseULong(string value) => ulong.TryParse(value, out var parsed) ? parsed : 0ul;
    private static float ParseFloat(string value) => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0F;
    private static double ParseDouble(string value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0D;
    private static bool ParseBool(string value) => value is "1" or "TRUE" or "True" or "true" or "Y" or "YES" or "Yes" or "yes";

    private static Item BuildIdQueryList(IEnumerable<uint>? queryIds)
    {
        if (queryIds is null)
        {
            return L();
        }

        var ids = queryIds as uint[] ?? queryIds.ToArray();
        return ids.Length == 0
            ? L()
            : L(ids.Select(id => U4([id])));
    }

    private static bool TryGetScalarString(Item item, out string value)
    {
        try
        {
            value = item.Format switch
            {
                SecsFormat.ASCII => item.GetString(),
                SecsFormat.U1 => item.GetMemory<byte>().Span[0].ToString(),
                SecsFormat.U2 => item.GetMemory<ushort>().Span[0].ToString(),
                SecsFormat.U4 => item.GetMemory<uint>().Span[0].ToString(),
                SecsFormat.U8 => item.GetMemory<ulong>().Span[0].ToString(),
                SecsFormat.I1 => item.GetMemory<sbyte>().Span[0].ToString(),
                SecsFormat.I2 => item.GetMemory<short>().Span[0].ToString(),
                SecsFormat.I4 => item.GetMemory<int>().Span[0].ToString(),
                SecsFormat.I8 => item.GetMemory<long>().Span[0].ToString(),
                _ => string.Empty
            };

            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static void RefreshTrailingAsciiDateTime(Item[] pparms)
    {
        var now = DateTime.Now.ToString("yyyyMMddHHmmss");

        for (var i = pparms.Length - 1; i >= 0; i--)
        {
            var pparm = pparms[i];
            if (pparm.Format != SecsFormat.List || pparm.Count < 2)
            {
                continue;
            }

            var values = pparm[1];
            if (values.Format != SecsFormat.List || values.Count == 0)
            {
                continue;
            }

            var last = values[values.Count - 1];
            if (last.Format != SecsFormat.ASCII)
            {
                continue;
            }

            if (!DateTime.TryParse(last.GetString(), out _))
            {
                continue;
            }

            var codeItem = pparm[0];
            var newValues = new Item[values.Count];
            for (var v = 0; v < values.Count - 1; v++)
            {
                newValues[v] = values[v];
            }
            newValues[^1] = A(now);

            pparms[i] = L(codeItem, L(newValues));
            return;
        }
    }

    private static Item[] NormalizeFormattedPparms(IReadOnlyList<Item> source)
    {
        var normalized = source
            .Take(FormattedRecipeRequiredPparmCount)
            .Select(CloneItem)
            .ToList();

        var seed = 1000;
        while (normalized.Count < FormattedRecipeRequiredPparmCount)
        {
            normalized.Add(L(U2([(ushort)seed]), L(A("0"))));
            seed++;
        }

        return normalized.ToArray();
    }

    private static bool IsLegacyPparmList(Item pparmList)
    {
        if (pparmList.Format != SecsFormat.List)
        {
            return false;
        }

        if (pparmList.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < pparmList.Count; i++)
        {
            var pparm = pparmList[i];
            if (pparm.Format != SecsFormat.List || pparm.Count < 2)
            {
                return false;
            }

            var code = pparm[0];
            var values = pparm[1];
            if ((code.Format is not (SecsFormat.U2 or SecsFormat.U4 or SecsFormat.ASCII)) || values.Format != SecsFormat.List)
            {
                return false;
            }
        }

        return true;
    }

    private static Item CloneItem(Item source)
    {
        return source.Format switch
        {
            SecsFormat.List => L(Enumerable.Range(0, source.Count).Select(i => CloneItem(source[i]))),
            SecsFormat.ASCII => A(source.GetString()),
            SecsFormat.Binary => B(source.GetMemory<byte>().ToArray()),
            SecsFormat.Boolean => Boolean(source.GetMemory<bool>().ToArray()),
            SecsFormat.U1 => U1(source.GetMemory<byte>().ToArray()),
            SecsFormat.U2 => U2(source.GetMemory<ushort>().ToArray()),
            SecsFormat.U4 => U4(source.GetMemory<uint>().ToArray()),
            SecsFormat.U8 => U8(source.GetMemory<ulong>().ToArray()),
            SecsFormat.I1 => I1(source.GetMemory<sbyte>().ToArray()),
            SecsFormat.I2 => I2(source.GetMemory<short>().ToArray()),
            SecsFormat.I4 => I4(source.GetMemory<int>().ToArray()),
            SecsFormat.I8 => I8(source.GetMemory<long>().ToArray()),
            SecsFormat.F4 => F4(source.GetMemory<float>().ToArray()),
            SecsFormat.F8 => F8(source.GetMemory<double>().ToArray()),
            _ => throw new NotSupportedException($"Unsupported SecsFormat for clone: {source.Format}")
        };
    }
}
