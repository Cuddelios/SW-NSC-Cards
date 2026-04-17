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
            currentPage.Size = PdfSharpCore.PageSize.A4;

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

            int renderWidthPx = (int)Math.Round(layoutOptions.CardWidthPt * 2);
            int renderHeightPx = (int)Math.Round(layoutOptions.CardHeightPt * 2);

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
}