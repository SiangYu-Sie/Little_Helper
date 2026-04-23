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

    public static Item S3F17ProceedWithSlotMap(string carrierId, byte portId)
    {
        return L(
            U4([0]),
            A("ProceedWithCarrier"),
            A(carrierId),
            U1([portId]),
            L(
                L(
                    A("ContentMap"),
                    L(
                        L(A("'1'"), A("'1'"), A(string.Empty), A(string.Empty), A(string.Empty), A(string.Empty))))));
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
        return L(
            A(processJobId),
            A(recipeId),
            L(A(carrierId)),
            L());
    }

    public static Item S14F9ControlJobCreate(string controlJobId, string carrierId)
    {
        return L(
            A(controlJobId),
            L(A(carrierId)));
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

    public static Item S1F3SelectedEquipmentStatusRequest(IEnumerable<uint> svids)
    {
        var ids = svids as uint[] ?? svids.ToArray();
        return L(ids.Select(id => U4([id])));
    }

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
