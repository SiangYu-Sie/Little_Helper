using System.Xml.Linq;
using NLog;

namespace HostSimTester.App.Secs;

public sealed class SecsTemplateRegistry
{
    private readonly Dictionary<string, SecsMessageTemplate> _byName;
    private readonly Dictionary<(byte Stream, byte Function), List<SecsMessageTemplate>> _bySxfy;

    private SecsTemplateRegistry(IEnumerable<SecsMessageTemplate> templates)
    {
        _byName = templates.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _bySxfy = new Dictionary<(byte Stream, byte Function), List<SecsMessageTemplate>>();

        foreach (var template in templates)
        {
            var key = (template.Stream, template.Function);
            if (!_bySxfy.TryGetValue(key, out var list))
            {
                list = new List<SecsMessageTemplate>();
                _bySxfy[key] = list;
            }

            list.Add(template);
        }
    }

    public int Count => _byName.Count;

    public static SecsTemplateRegistry CreateEmpty() => new(Array.Empty<SecsMessageTemplate>());

    public static SecsTemplateRegistry LoadFromToolIdXmlOrEmpty(Logger logger)
    {
        try
        {
            var path = FindToolIdXmlPath();
            if (path is null)
            {
                logger.Warn("TOOLID.xml not found; continue without template registry.");
                return CreateEmpty();
            }

            var doc = XDocument.Load(path);
            var templates = doc
                .Descendants("Message")
                .Select(x => new SecsMessageTemplate(
                    Name: x.Attribute("Name")?.Value ?? string.Empty,
                    Stream: ParseByte(x.Attribute("Stream")?.Value),
                    Function: ParseByte(x.Attribute("Func")?.Value),
                    ExpectReply: string.Equals(x.Attribute("ExpectReply")?.Value, "Y", StringComparison.OrdinalIgnoreCase),
                    AutoReply: x.Attribute("AutoReply")?.Value ?? string.Empty))
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            logger.Info($"Loaded TOOLID templates: count={templates.Count}, path={path}");
            return new SecsTemplateRegistry(templates);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load TOOLID.xml template registry. Use empty registry.");
            return CreateEmpty();
        }
    }

    public bool TryGetByName(string name, out SecsMessageTemplate template) => _byName.TryGetValue(name, out template!);

    public bool TryGetBySxFy(byte stream, byte function, out IReadOnlyList<SecsMessageTemplate> templates)
    {
        if (_bySxfy.TryGetValue((stream, function), out var list))
        {
            templates = list;
            return true;
        }

        templates = Array.Empty<SecsMessageTemplate>();
        return false;
    }

    private static byte ParseByte(string? value)
    {
        if (byte.TryParse(value, out var result))
        {
            return result;
        }

        return 0;
    }

    private static string? FindToolIdXmlPath()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, "參考資料", "ToolSoftwareTester1.0.0.0", "TOOLID.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }
}
