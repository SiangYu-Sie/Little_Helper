using NLog;

namespace HostSimTester.App;

public static class AppSession
{
    public static string TimeStamp { get; private set; } = string.Empty;
    public static string L1InitialTemplatePath { get; private set; } = string.Empty;
    public static bool IsL1InitialExcelImported { get; private set; }
    public static bool IsL1InitialDefineEventCompleted { get; private set; }
    public static bool IsL1InitialCompleted => IsL1InitialExcelImported && IsL1InitialDefineEventCompleted;

    public static IReadOnlyList<uint> L1InitialCeids { get; private set; } = Array.Empty<uint>();
    public static IReadOnlyList<uint> L1InitialDvids { get; private set; } = Array.Empty<uint>();
    public static IReadOnlyList<uint> L1InitialRptids { get; private set; } = Array.Empty<uint>();

    public static void Initialize()
    {
        TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        GlobalDiagnosticsContext.Set("TimeStamp", TimeStamp);
        L1InitialTemplatePath = string.Empty;
        IsL1InitialExcelImported = false;
        IsL1InitialDefineEventCompleted = false;
        L1InitialCeids = Array.Empty<uint>();
        L1InitialDvids = Array.Empty<uint>();
        L1InitialRptids = Array.Empty<uint>();
    }

    public static void MarkL1InitialExcelImported(string templatePath)
    {
        L1InitialTemplatePath = templatePath;
        IsL1InitialExcelImported = !string.IsNullOrWhiteSpace(templatePath);
    }

    public static void SetL1InitialTemplateContent(IReadOnlyList<uint> ceids, IReadOnlyList<uint> dvids, IReadOnlyList<uint> rptids)
    {
        L1InitialCeids = ceids ?? Array.Empty<uint>();
        L1InitialDvids = dvids ?? Array.Empty<uint>();
        L1InitialRptids = rptids ?? Array.Empty<uint>();
    }

    public static void MarkL1InitialDefineEventCompleted()
    {
        IsL1InitialDefineEventCompleted = true;
    }
}
