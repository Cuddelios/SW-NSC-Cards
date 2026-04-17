namespace SvgPdfGenerator.Models;

public sealed class PdfLayoutOptions
{
    public double MarginPt { get; set; } = 30;
    public double GapXPt { get; set; } = 12;
    public double GapYPt { get; set; } = 12;
    public double CardWidthPt { get; set; } = 220;
    public double CardHeightPt { get; set; } = 90;
}