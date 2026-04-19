using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace SvgPdfGenerator;

public sealed class SvgCardRenderer
{
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";
    private static readonly Regex SkillDicePattern = new(
        @"^(?<label>.*?)(?:\s+(?<dice>d(?:4|6|8|10|12)))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> ShapeElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "rect", "circle", "ellipse", "path", "polygon", "line", "polyline"
    };

    private readonly XElement svgRootTemplate;
    private readonly string? templateGroupId;
    private readonly string? templateViewBox;

    public SvgCardRenderer(
        string svgTemplatePath,
        string? templateGroupId = null,
        string? templateViewBox = null)
    {
        if (string.IsNullOrWhiteSpace(svgTemplatePath))
        {
            throw new ArgumentException("SVG template path must not be empty.", nameof(svgTemplatePath));
        }

        if (!File.Exists(svgTemplatePath))
        {
            throw new FileNotFoundException("SVG template file was not found.", svgTemplatePath);
        }

        XDocument document = XDocument.Load(svgTemplatePath);
        this.svgRootTemplate = document.Root
            ?? throw new InvalidOperationException("SVG root element was not found.");

        this.templateGroupId = templateGroupId;
        this.templateViewBox = templateViewBox;

        EnsureTemplateExists();
    }

    public byte[] RenderCardAsPng(
        IReadOnlyDictionary<string, string> values,
        int targetWidthPx,
        int targetHeightPx)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (targetWidthPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWidthPx));
        }

        if (targetHeightPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHeightPx));
        }

        string svgContent = BuildFilledSvg(values);
        return RenderSvgToPng(svgContent, targetWidthPx, targetHeightPx);
    }

    private void EnsureTemplateExists()
    {
        XElement? template = FindTemplateRoot(new XElement(this.svgRootTemplate));
        if (template == null)
        {
            throw new InvalidOperationException(
                this.templateGroupId == null
                    ? "SVG template root was not found."
                    : $"Template group with id='{this.templateGroupId}' was not found.");
        }
    }

    private string BuildFilledSvg(IReadOnlyDictionary<string, string> values)
    {
        XElement svgRootClone = new XElement(this.svgRootTemplate);
        ApplyViewBoxOverride(svgRootClone, this.templateViewBox);

        XElement template = FindTemplateRoot(svgRootClone)
            ?? throw new InvalidOperationException("Template group was not found in SVG clone.");

        FillTemplateFields(template, values);

        return svgRootClone.ToString(SaveOptions.DisableFormatting);
    }

    private XElement? FindTemplateRoot(XElement root)
    {
        if (string.IsNullOrWhiteSpace(this.templateGroupId))
        {
            return root;
        }

        return root
            .Descendants(SvgNs + "g")
            .FirstOrDefault(e => string.Equals(
                (string?)e.Attribute("id"),
                this.templateGroupId,
                StringComparison.Ordinal));
    }


    public enum TemplateFieldType
    {
        skills_text,
        skills_dices,
        description,
        edges,
    }

    private static void FillTemplateFields(
        XElement templateClone,
        IReadOnlyDictionary<string, string> values)
    {
        IEnumerable<XElement> elementsWithField = templateClone
            .DescendantsAndSelf()
            .Where(e => e.Attribute("data-field") != null);

        // Sondertypen zuerst ermitteln, damit die Daten bei den Elementen bereits verfügbar sind, wenn die Felder angewendet werden
        List<SkillEntry> skillEntries = ParseSkillEntries(values.TryGetValue(nameof(TemplateFieldType.skills_text), out string? value) ? value : string.Empty);
        double skillsLineHeight = 0;

        foreach (XElement element in elementsWithField)
        {
            string? fieldName = (string?)element.Attribute("data-field");
            if (string.IsNullOrWhiteSpace(fieldName) || !values.TryGetValue(fieldName, out value))
            {
                continue;
            }

            if(Enum.TryParse<TemplateFieldType>(fieldName, true, out TemplateFieldType fieldType))
            {
                switch (fieldType)
                {
                    case TemplateFieldType.skills_text:
                        var displayText = skillEntries.Count > 0
                                    ? string.Join('\n', skillEntries.Select(entry => entry.Label))
                                    : value ?? string.Empty;
                        skillsLineHeight = GetLineHeight(element);
                        ApplyTextValue(element, displayText);
                        continue;

                    case TemplateFieldType.skills_dices:
                        ApplySkillDiceIcons(element, skillEntries, skillsLineHeight);
                        continue;

                    case TemplateFieldType.description:
                        displayText = string.Join('\n', SplitTextLength(value, 29));
                        ApplyTextValue(element, displayText);
                        continue;
    
                    case TemplateFieldType.edges:  
                        displayText = string.Join('\n', SplitTextParts(value)); 
                        ApplyTextValue(element, displayText);
                        continue;
                    default:
                        break;
                }
            }

            ApplyFieldValue(element, value ?? string.Empty);
        }
    }

    private static void ApplyFieldValue(XElement element, string value)
    {
        if (TryApplyGroupSelection(element, value))
        {
            return;
        }

        if (TryApplyVisibility(element, value))
        {
            return;
        }

        if (TryApplyFillColor(element, value))
        {
            return;
        }

        ApplyTextValue(element, value);
    }

    private static bool TryApplyGroupSelection(XElement element, string value)
    {
        List<XElement> directChildGroups = element
            .Elements(SvgNs + "g")
            .Where(child => child.Attribute("data-field") != null)
            .ToList();

        if (directChildGroups.Count == 0)
        {
            return false;
        }

        XElement? matchingGroup = directChildGroups.FirstOrDefault(child => string.Equals(
            (string?)child.Attribute("data-field"),
            value,
            StringComparison.OrdinalIgnoreCase));

        if (matchingGroup == null)
        {
            return false;
        }

        foreach (XElement childGroup in directChildGroups)
        {
            bool isMatch = ReferenceEquals(childGroup, matchingGroup);
            childGroup.SetAttributeValue("display", isMatch ? null : "none");
        }

        return true;
    }

    private static bool TryApplyVisibility(XElement element, string value)
    {
        if (!string.Equals(element.Name.LocalName, "g", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseBooleanLike(value, out bool isVisible))
        {
            return false;
        }

        element.SetAttributeValue("display", isVisible ? null : "none");
        return true;
    }

    private static bool TryApplyFillColor(XElement element, string value)
    {
        if (!ShapeElementNames.Contains(element.Name.LocalName))
        {
            return false;
        }

        if (element.Attribute("fill") == null)
        {
            return false;
        }

        element.SetAttributeValue("fill", value);
        return true;
    }

    private static void ApplyTextValue(XElement element, string value)
    {
        if (!string.Equals(element.Name.LocalName, "text", StringComparison.OrdinalIgnoreCase))
        {
            element.Value = value;
            return;
        }

        List<XElement> existingTspans = element.Elements(SvgNs + "tspan").ToList();
        if (existingTspans.Count > 1)
        {
            string[] parts = SplitTextParts(value);

            for (int index = 0; index < existingTspans.Count; index++)
            {
                existingTspans[index].Value = index < parts.Length ? parts[index] : string.Empty;
            }

            if (parts.Length > existingTspans.Count)
            {
                existingTspans[^1].Value = string.Join(", ", parts.Skip(existingTspans.Count - 1));
            }

            return;
        }

        string[] lines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        if (lines.Length <= 1)
        {
            element.Value = value;
            return;
        }

        string? textX = (string?)element.Attribute("x");
        string? inheritedX = existingTspans
            .Select(tspan => (string?)tspan.Attribute("x"))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        string? lineX = textX ?? inheritedX;

        double lineHeight = GetLineHeight(element);

        element.Nodes().Remove();

        for (int index = 0; index < lines.Length; index++)
        {
            var tspan = new XElement(SvgNs + "tspan", lines[index]);

            if (!string.IsNullOrWhiteSpace(lineX))
            {
                tspan.SetAttributeValue("x", lineX);
            }

            if (index > 0)
            {
                tspan.SetAttributeValue("dy", $"{lineHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}px");
            }

            element.Add(tspan);
        }
    }

    private static double GetLineHeight(XElement textElement)
    {
        const double fallbackFontSize = 3.18;
        const double lineHeightFactor = 1.2;

        string? fontSizeText = (string?)textElement.Attribute("font-size");
        if (string.IsNullOrWhiteSpace(fontSizeText))
        {
            return fallbackFontSize * lineHeightFactor;
        }

        string normalized = fontSizeText.Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (!double.TryParse(
                normalized,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double fontSize))
        {
            return fallbackFontSize * lineHeightFactor;
        }

        return fontSize * lineHeightFactor;
    }

    private static string[] SplitTextParts(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split([',', '\n'], StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<string> SplitTextLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        string remaining = value.Trim();
        while (remaining.Length > maxLength)
        {
            int splitIndex = remaining.LastIndexOf(' ', maxLength);
            if (splitIndex <= 0)
            {
                splitIndex = maxLength;
            }

            string line = remaining[..splitIndex].Trim();
            if (line.Length > 0)
            {
                yield return line;
            }

            remaining = remaining[splitIndex..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }

    }


    private static void ApplySkillsField(
        XElement? skillsTextElement,       
        string skillsText)
    {
        bool hasSkillsText = !string.IsNullOrEmpty(skillsText);

        if (!hasSkillsText)
        {
            return;
        }

        List<SkillEntry> skillEntries = ParseSkillEntries(skillsText);

        if (skillsTextElement != null)
        {
            string displayText = skillEntries.Count > 0
                ? string.Join('\n', skillEntries.Select(entry => entry.Label))
                : skillsText;

            ApplyTextValue(skillsTextElement, displayText);
        }
    }

    private static List<SkillEntry> ParseSkillEntries(string rawSkillsText)
    {
        return rawSkillsText
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseSkillEntry)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Label))
            .ToList();
    }

    private static SkillEntry ParseSkillEntry(string rawEntry)
    {
        Match match = SkillDicePattern.Match(rawEntry.Trim());
        if (!match.Success)
        {
            return new SkillEntry(rawEntry.Trim(), null);
        }

        string label = match.Groups["label"].Value.Trim();
        string? dice = match.Groups["dice"].Success
            ? match.Groups["dice"].Value.ToLowerInvariant()
            : null;

        return new SkillEntry(label, dice);
    }

    private static void ApplySkillDiceIcons(
        XElement skillsDiceElement,
        IReadOnlyList<SkillEntry> skillEntries,
        double lineHeight)
    {
        List<XElement> diceTemplates = skillsDiceElement
            .Elements(SvgNs + "g")
            .Where(child => child.Attribute("data-field") != null)
            .ToList();

        var diceTemplatesByField = diceTemplates.ToDictionary(
            child => ((string?)child.Attribute("data-field") ?? string.Empty).ToLowerInvariant(),
            child => child,
            StringComparer.OrdinalIgnoreCase);

        skillsDiceElement.Elements().Remove();
        skillsDiceElement.SetAttributeValue("display", null);

        for (int index = 0; index < skillEntries.Count; index++)
        {
            string? dice = skillEntries[index].Dice;
            if (string.IsNullOrWhiteSpace(dice))
            {
                continue;
            }

            if (!diceTemplatesByField.TryGetValue(dice, out XElement? templateGroup))
            {
                continue;
            }

            XElement clone = new(templateGroup);
            clone.SetAttributeValue("display", null);

            string extraTranslate = $"translate({FormatNumber((index%2)*2.5)} {FormatNumber(lineHeight * index)})";
            string? existingTransform = (string?)clone.Attribute("transform");
            clone.SetAttributeValue("transform", CombineTransforms(existingTransform, extraTranslate));

            skillsDiceElement.Add(clone);
        }
    }

    private static string CombineTransforms(string? first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        return $"{first} {second}";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryParseBooleanLike(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "ja":
            case "y":
            case "on":
                result = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "nein":
            case "n":
            case "off":
            case "":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static void ApplyViewBoxOverride(XElement root, string? templateViewBox)
    {
        if (string.IsNullOrWhiteSpace(templateViewBox))
        {
            return;
        }

        root.SetAttributeValue("viewBox", templateViewBox);

        string[] parts = templateViewBox
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 4)
        {
            return;
        }

        root.SetAttributeValue("width", parts[2]);
        root.SetAttributeValue("height", parts[3]);
    }

    private sealed record SkillEntry(string Label, string? Dice);

    private static byte[] RenderSvgToPng(string svgContent, int widthPx, int heightPx)
    {
        var svg = new SKSvg();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
        SKPicture? picture = svg.Load(stream);

        if (picture == null)
        {
            throw new InvalidOperationException("SVG could not be rendered.");
        }

        SKRect bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("SVG has invalid bounds.");
        }

        float scaleX = widthPx / bounds.Width;
        float scaleY = heightPx / bounds.Height;
        float scale = Math.Min(scaleX, scaleY);

        using var bitmap = new SKBitmap(widthPx, heightPx);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.White);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
