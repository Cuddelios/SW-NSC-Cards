using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace SvgPdfGenerator;

public sealed class SvgCardRenderer
{
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";
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

    private static void FillTemplateFields(
        XElement templateClone,
        IReadOnlyDictionary<string, string> values)
    {
        IEnumerable<XElement> elementsWithField = templateClone
            .DescendantsAndSelf()
            .Where(e => e.Attribute("data-field") != null);

        foreach (XElement element in elementsWithField)
        {
            string? fieldName = (string?)element.Attribute("data-field");
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }

            if (values.TryGetValue(fieldName, out string? value))
            {
                ApplyFieldValue(element, value ?? string.Empty);
            }
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

        List<XElement> tspans = element.Elements(SvgNs + "tspan").ToList();
        if (tspans.Count == 0)
        {
            element.Value = value;
            return;
        }

        string[] lines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (int index = 0; index < tspans.Count; index++)
        {
            tspans[index].Value = index < lines.Length ? lines[index] : string.Empty;
        }

        if (lines.Length > tspans.Count)
        {
            tspans[^1].Value = string.Join(Environment.NewLine, lines.Skip(tspans.Count - 1));
        }
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
