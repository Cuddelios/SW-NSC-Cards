using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SvgPdfGenerator.Models;

namespace SvgPdfGenerator;

public sealed class PdfLayoutWriter
{
    public void WriteCards(
        string outputPdfPath,
        IReadOnlyList<Dictionary<string, string>> rows,
        SvgCardRenderer cardRenderer,
        PdfLayoutOptions layoutOptions)
    {
        if (string.IsNullOrWhiteSpace(outputPdfPath))
        {
            throw new ArgumentException("Output PDF path must not be empty.", nameof(outputPdfPath));
        }

        if (rows == null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        if (cardRenderer == null)
        {
            throw new ArgumentNullException(nameof(cardRenderer));
        }

        if (layoutOptions == null)
        {
            throw new ArgumentNullException(nameof(layoutOptions));
        }

        using var pdf = new PdfDocument();

        PdfPage? currentPage = null;
        XGraphics? graphics = null;

        int columnsPerPage = 0;
        int rowsPerPage = 0;
        int itemsPerPage = 0;

        void StartNewPage()
        {
            currentPage = pdf.AddPage();
            ApplyPageSize(currentPage, layoutOptions);

            graphics = XGraphics.FromPdfPage(currentPage);

            columnsPerPage = Math.Max(
                1,
                (int)((currentPage.Width.Point - 2 * layoutOptions.MarginPt + layoutOptions.GapXPt)
                    / (layoutOptions.CardWidthPt + layoutOptions.GapXPt)));

            rowsPerPage = Math.Max(
                1,
                (int)((currentPage.Height.Point - 2 * layoutOptions.MarginPt + layoutOptions.GapYPt)
                    / (layoutOptions.CardHeightPt + layoutOptions.GapYPt)));

            itemsPerPage = columnsPerPage * rowsPerPage;
        }

        StartNewPage();

        for (int index = 0; index < rows.Count; index++)
        {
            if (index > 0 && index % itemsPerPage == 0)
            {
                StartNewPage();
            }

            int pageIndex = index % itemsPerPage;
            int column = pageIndex % columnsPerPage;
            int row = pageIndex / columnsPerPage;

            double x = layoutOptions.MarginPt + column * (layoutOptions.CardWidthPt + layoutOptions.GapXPt);
            double y = layoutOptions.MarginPt + row * (layoutOptions.CardHeightPt + layoutOptions.GapYPt);

            int renderWidthPx = ConvertPointsToPixels(layoutOptions.CardWidthPt, layoutOptions.RenderDpi);
            int renderHeightPx = ConvertPointsToPixels(layoutOptions.CardHeightPt, layoutOptions.RenderDpi);

            byte[] pngBytes = cardRenderer.RenderCardAsPng(
                rows[index],
                renderWidthPx,
                renderHeightPx);

            using var imageStream = new MemoryStream(pngBytes);
            using XImage image = XImage.FromStream(() => imageStream);

            graphics!.DrawImage(
                image,
                x,
                y,
                layoutOptions.CardWidthPt,
                layoutOptions.CardHeightPt);
        }

        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPdfPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        pdf.Save(outputPdfPath);
    }

    private static void ApplyPageSize(PdfPage page, PdfLayoutOptions layoutOptions)
    {
        if (layoutOptions.PageWidthPt is > 0 && layoutOptions.PageHeightPt is > 0)
        {
            page.Width = PdfSharpCore.Drawing.XUnit.FromPoint(layoutOptions.PageWidthPt.Value);
            page.Height = PdfSharpCore.Drawing.XUnit.FromPoint(layoutOptions.PageHeightPt.Value);
            return;
        }

        page.Size = PdfSharpCore.PageSize.A4;
        page.Orientation = PdfSharpCore.PageOrientation.Landscape;
    }

    private static int ConvertPointsToPixels(double points, double dpi)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points));
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi));
        }

        double inches = points / 72.0;
        return Math.Max(1, (int)Math.Ceiling(inches * dpi));
    }
}
