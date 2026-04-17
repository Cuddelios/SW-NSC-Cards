using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace SvgPdfGenerator;

public sealed class SvgCardRenderer
{
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

    private readonly XElement svgRootTemplate;
    private readonly string templateGroupId;

    public SvgCardRenderer(string svgTemplatePath, string templateGroupId = "template")
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
        XElement? template = FindTemplateGroup(new XElement(this.svgRootTemplate));
        if (template == null)
        {
            throw new InvalidOperationException(
                $"Template group with id='{this.templateGroupId}' was not found.");
        }
    }

    private string BuildFilledSvg(IReadOnlyDictionary<string, string> values)
    {
        XElement svgRootClone = new XElement(this.svgRootTemplate);

        XElement template = FindTemplateGroup(svgRootClone)
            ?? throw new InvalidOperationException("Template group was not found in SVG clone.");

        FillTemplateFields(template, values);

        return svgRootClone.ToString(SaveOptions.DisableFormatting);
    }

    private XElement? FindTemplateGroup(XElement root)
    {
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
            .Descendants()
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
                element.Value = value ?? string.Empty;
            }
        }
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