using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace SvgPdfGenerator;

public sealed partial class SvgCardRenderer
{
    public IEnumerable<string> ImageSources => svgRootTemplate.DescendantsAndSelf()
        .Where(e => (string?)e.Attribute("data-bind") == "image")
        .Select(e => (string?)e.Attribute("data-field") ?? "")
        .Select(name => fields.TryGetValue(name, out var rule) ? rule.Source ?? name : name);

    public (double Width, double Height) SizePt
    {
        get
        {
            var viewBox = Regex.Split(((string?)svgRootTemplate.Attribute("viewBox") ?? "").Trim(), @"[\s,]+")
                .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN).ToArray();
            double Length(string name, int index)
            {
                string? raw = (string?)svgRootTemplate.Attribute(name);
                if (raw == null && viewBox.Length == 4 && double.IsFinite(viewBox[index]) && viewBox[index] > 0)
                    return viewBox[index] * .75;
                var match = Regex.Match(raw ?? "", @"^\s*(\d+(?:\.\d*)?|\.\d+)(mm|cm|in|pt|pc|px)?\s*$", RegexOptions.IgnoreCase);
                if (!match.Success) throw new InvalidOperationException($"SVG: ungültige absolute {name}: '{raw}'.");
                double factor = match.Groups[2].Value.ToLowerInvariant() switch
                { "mm" => 72 / 25.4, "cm" => 72 / 2.54, "in" => 72, "pt" => 1, "pc" => 12, _ => .75 };
                double result = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * factor;
                if (!double.IsFinite(result) || result <= 0) throw new InvalidOperationException($"SVG: {name} muss positiv sein.");
                return result;
            }
            return (Length("width", 2), Length("height", 3));
        }
    }

    private static void SetDisplay(XElement element, bool visible)
    {
        element.SetAttributeValue("style", Regex.Replace((string?)element.Attribute("style") ?? "", @"(?:^|;)\s*display\s*:[^;]*", "")
            + (visible ? ";display:inline" : ";display:none"));
        element.SetAttributeValue("display", visible ? null : "none");
    }

    private void FillBoundFields(XElement root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var element in root.DescendantsAndSelf().Where(e => e.Attribute("data-bind") != null).ToList())
        {
            string name = (string?)element.Attribute("data-field") ?? (string?)element.Attribute("id") ?? "";
            fields.TryGetValue(name, out var rule);
            string value = values.GetValueOrDefault(rule?.Source ?? name) ?? rule?.DefaultValue ?? "";
            switch ((string?)element.Attribute("data-bind"))
            {
                case "image":
                    if (string.IsNullOrWhiteSpace(value)) { SetDisplay(element, false); break; }
                    if (!File.Exists(value)) throw new FileNotFoundException($"Bild für '{name}' fehlt.", value);
                    string mime = Path.GetExtension(value).ToLowerInvariant() switch
                    { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => throw new InvalidOperationException($"Nicht unterstütztes Bild: {value}") };
                    string uri = $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(value))}";
                    element.SetAttributeValue(XName.Get("href", "http://www.w3.org/1999/xlink"), uri);
                    element.SetAttributeValue("href", uri);
                    break;
                case "dice":
                    var choices = element.Elements().Where(e => e.Attribute("data-value") != null).ToList();
                    string normalized = value.Trim().ToUpperInvariant().Replace('D', 'W');
                    if (normalized.Length > 0 && !choices.Any(c => (string?)c.Attribute("data-value") == normalized))
                        throw new InvalidOperationException($"Unbekannter Würfel '{value}' für '{name}'.");
                    foreach (var choice in choices) SetDisplay(choice, (string?)choice.Attribute("data-value") == normalized);
                    SetDisplay(element, normalized.Length > 0);
                    break;
                case "skill-list":
                    var names = ((string?)element.Attribute("data-fields") ?? "").Split(',');
                    var spans = element.Elements(SvgNs + "tspan").ToList();
                    if (names.Length != spans.Count) throw new InvalidOperationException("Fertigkeiten und SVG-Zeilen stimmen nicht überein.");
                    for (int i = 0; i < names.Length; i++)
                    {
                        spans[i].Value = values.GetValueOrDefault(names[i].Trim()) ?? "";
                    }
                    break;
                case "counter":
                    if (string.IsNullOrWhiteSpace(value)) value = "0";
                    if (!int.TryParse(value, out int count) || count < 0)
                        throw new InvalidOperationException($"Ungültiger Zähler '{name}': '{value}'.");
                    SetDisplay(element, count > 0);
                    var icons = element.Elements(SvgNs + "g").ToList();
                    if (count > 100 || icons.Count < 2) throw new InvalidOperationException("Zähler benötigt mindestens zwei SVG-Symbole und höchstens 100 Punkte.");
                    if (count > 0)
                    {
                        // Keep every point at its original width, extending from the left edge.
                        var first = icons[0];
                        double[] Translation(XElement icon) => Regex.Matches((string?)icon.Attribute("transform") ?? "", @"-?\d+(?:\.\d+)?")
                            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                        var origin = Translation(first);
                        var next = Translation(icons[1]);
                        if (origin.Length != 2 || next.Length != 2) throw new InvalidOperationException("Zähler erwartet translate(x,y)-Symbole.");
                        double step = next[1] - origin[1];
                        element.RemoveNodes();
                        var strip = new XElement(SvgNs + "g");
                        for (int i = 0; i < count; i++)
                        {
                            var icon = new XElement(first);
                            foreach (var child in icon.DescendantsAndSelf()) child.Attribute("id")?.Remove();
                            icon.SetAttributeValue("transform", $"translate({FormatNumber(origin[0])} {FormatNumber(origin[1] + (icons.Count - 1 - i) * step)})");
                            strip.Add(icon);
                        }
                        element.Add(strip);
                    }
                    break;
                case "text":
                    if (rule?.BoxWidth is double width) FitBoundText(element, value, width, rule.BoxHeight);
                    else element.Value = value;
                    break;
                default: throw new InvalidOperationException($"Unbekannte SVG-Bindung: {element.Attribute("data-bind")}");
            }
        }
        ApplyCharacterLayout(root, FindTemplateRoot(svgRootTemplate) ?? svgRootTemplate, values);
    }

    private static void FitBoundText(XElement element, string value, double width, double? height)
    {
        string style = (string?)element.Attribute("style") ?? "";
        var sizeMatch = Regex.Match(style, @"font-size:([\d.]+)");
        double size = sizeMatch.Success ? double.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 3.175;
        var family = Regex.Match(style, @"font-family:([^;]+)").Groups[1].Value.Trim('\'', '"');
        using var typeface = SKTypeface.FromFamilyName(family);
        using var font = new SKFont(typeface, (float)size);
        List<string> Wrap()
        {
            var lines = new List<string>();
            foreach (string paragraph in value.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (font.MeasureText(next) > width && line.Length > 0) { lines.Add(line); line = word; }
                    else line = next;
                }
                lines.Add(line);
            }
            return lines;
        }
        List<string> lines = Wrap();
        if (height.HasValue && lines.Count * size * 1.25 > height.Value)
            throw new InvalidOperationException($"Textüberlauf in '{element.Attribute("data-field")?.Value}': Die Schriftgröße bleibt unverändert.");
        element.RemoveNodes();
        for (int i = 0; i < lines.Count; i++)
        {
            var span = new XElement(SvgNs + "tspan", lines[i]);
            span.SetAttributeValue("x", (string?)element.Attribute("x"));
            if (i > 0) span.SetAttributeValue("dy", FormatNumber(size * 1.25));
            element.Add(span);
        }
    }
}
