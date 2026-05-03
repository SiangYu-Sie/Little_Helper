using Secs4Net;
using static Secs4Net.Item;

namespace HostSimTester.App.Secs;

public static class SecsMessageFactory
{
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
        return S16F15ProcessJobCreate(processJobId, recipeId, carrierId, null);
    }

    public static Item S16F15ProcessJobCreate(string processJobId, string recipeId, string carrierId, IEnumerable<string>? slotIds)
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
                    Boolean([true]),
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
        return L(
            A(ppid),
            A("HostSimTester"),
            A("1.0.0"),
            L(
                L(
                    U2([12]),
                    L(A("0")))));
    }

    public static Item S7F23UnformattedProcessProgramSend(string ppid)
    {
        return L(A(ppid), B(DefaultRecipeBody));
    }

    public static Item S7F3UnformattedProcessProgramSend(string ppid)
    {
        return L(A(ppid), B(DefaultRecipeBody));
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
}
