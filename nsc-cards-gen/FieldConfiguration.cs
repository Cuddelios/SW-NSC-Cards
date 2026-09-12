namespace SvgPdfGenerator;

/// <summary>A binding from a CSV column to an SVG data-field.</summary>
public sealed class FieldConfiguration
{
    public string? Source { get; set; }
    public string[] Aliases { get; set; } = [];
    public string Type { get; set; } = "auto";
    public int? WrapLength { get; set; }
    public string Separators { get; set; } = ",\r\n";
    public Dictionary<string, string> ValueMap { get; set; } = new();
    public string? DefaultValue { get; set; }
    public double? LineHeight { get; set; }
    public string? LineHeightFrom { get; set; }
    public string[] OffsetAfter { get; set; } = [];
    public double OffsetY { get; set; }

    public static Dictionary<string, FieldConfiguration> Merge(
        Dictionary<string, FieldConfiguration> defaults,
        Dictionary<string, FieldConfiguration> overrides)
    {
        var result = new Dictionary<string, FieldConfiguration>(defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides) result[pair.Key] = pair.Value;
        Validate(result);
        return result;
    }

    public static void Validate(IReadOnlyDictionary<string, FieldConfiguration> fields)
    {
        string[] types = ["auto", "text", "list", "skillLabels", "skillDice", "selection", "visibility", "fill"];
        foreach (var (name, rule) in fields)
        {
            if (string.IsNullOrWhiteSpace(name) || rule == null)
                throw new InvalidOperationException("Feldregeln benötigen einen Namen und ein Regelobjekt.");
            if (!types.Contains(rule.Type) || rule.WrapLength is <= 0 || rule.LineHeight is <= 0
                || !double.IsFinite(rule.OffsetY) || (rule.LineHeight.HasValue && !double.IsFinite(rule.LineHeight.Value))
                || rule.Aliases == null || rule.OffsetAfter == null || rule.ValueMap == null || string.IsNullOrEmpty(rule.Separators))
                throw new InvalidOperationException($"Ungültige Feldregel für '{name}' (Typ, Umbruch, Trennzeichen oder Zeilenhöhe).");
            foreach (string dependency in rule.OffsetAfter.Concat(rule.LineHeightFrom == null ? [] : new[] { rule.LineHeightFrom }))
                if (!fields.ContainsKey(dependency))
                    throw new InvalidOperationException($"Feld '{name}' verweist auf unbekanntes Feld '{dependency}'.");
        }
    }
}
