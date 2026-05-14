using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SvgPdfGenerator.Models;

namespace SvgPdfGenerator;

public sealed class PdfLayoutWriter
{
    public delegate byte[] CardRenderDelegate(
        IReadOnlyDictionary<string, string> row,
        int targetWidthPx,
        int targetHeightPx);

    public void WriteCards(
        string outputPdfPath,
        IReadOnlyList<Dictionary<string, string>> rows,
        SvgCardRenderer cardRenderer,
        PdfLayoutOptions layoutOptions)
    {
        if (cardRenderer == null)
        {
            throw new ArgumentNullException(nameof(cardRenderer));
        }

        WriteCards(
            outputPdfPath,
            rows,
            cardRenderer.RenderCardAsPng,
            layoutOptions);
    }

    public void WriteCards(
        string outputPdfPath,
        IReadOnlyList<Dictionary<string, string>> rows,
        CardRenderDelegate cardRenderer,
        PdfLayoutOptions layoutOptions)
    {
        WriteCardsInternal(
            outputPdfPath,
            rows,
            cardRenderer,
            backRenderer: null,
            layoutOptions);
    }

    public void WriteCardsWithInterleavedBacks(
        string outputPdfPath,
        IReadOnlyList<Dictionary<string, string>> rows,
        CardRenderDelegate frontRenderer,
        CardRenderDelegate backRenderer,
        PdfLayoutOptions layoutOptions,
        bool mirrorBackPageHorizontally = false)
    {
        if (backRenderer == null)
        {
            throw new ArgumentNullException(nameof(backRenderer));
        }

        WriteCardsInternal(
            outputPdfPath,
            rows,
            frontRenderer,
            backRenderer,
            layoutOptions,
            mirrorBackPageHorizontally);
    }

    private static void WriteCardsInternal(
        string outputPdfPath,
        IReadOnlyList<Dictionary<string, string>> rows,
        CardRenderDelegate frontRenderer,
        CardRenderDelegate? backRenderer,
        PdfLayoutOptions layoutOptions,
        bool mirrorBackPageHorizontally = false)
    {
        if (string.IsNullOrWhiteSpace(outputPdfPath))
        {
            throw new ArgumentException("Output PDF path must not be empty.", nameof(outputPdfPath));
        }

        if (rows == null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        if (frontRenderer == null)
        {
            throw new ArgumentNullException(nameof(frontRenderer));
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

        for (int pageStartIndex = 0; pageStartIndex < rows.Count; pageStartIndex += itemsPerPage)
        {
            if (pageStartIndex > 0)
            {
                StartNewPage();
            }

            int cardsOnPage = Math.Min(itemsPerPage, rows.Count - pageStartIndex);
            DrawCardPage(
                rows,
                pageStartIndex,
                cardsOnPage,
                columnsPerPage,
                frontRenderer,
                graphics!,
                layoutOptions,
                currentPage!.Width.Point,
                mirrorHorizontally: false);

            if (backRenderer != null)
            {
                StartNewPage();
                DrawCardPage(
                    rows,
                    pageStartIndex,
                    cardsOnPage,
                    columnsPerPage,
                    backRenderer,
                    graphics!,
                    layoutOptions,
                    currentPage!.Width.Point,
                    mirrorHorizontally: mirrorBackPageHorizontally);
            }
        }

        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPdfPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        pdf.Save(outputPdfPath);
    }

    private static void DrawCardPage(
        IReadOnlyList<Dictionary<string, string>> rows,
        int pageStartIndex,
        int cardsOnPage,
        int columnsPerPage,
        CardRenderDelegate renderer,
        XGraphics graphics,
        PdfLayoutOptions layoutOptions,
        double pageWidthPt,
        bool mirrorHorizontally)
    {
        for (int pageIndex = 0; pageIndex < cardsOnPage; pageIndex++)
        {
            int column = pageIndex % columnsPerPage;
            int row = pageIndex / columnsPerPage;

            double x = layoutOptions.MarginPt + column * (layoutOptions.CardWidthPt + layoutOptions.GapXPt);
            double y = layoutOptions.MarginPt + row * (layoutOptions.CardHeightPt + layoutOptions.GapYPt);

            if (mirrorHorizontally)
            {
                x = pageWidthPt - x - layoutOptions.CardWidthPt;
            }

            int renderWidthPx = ConvertPointsToPixels(layoutOptions.CardWidthPt, layoutOptions.RenderDpi);
            int renderHeightPx = ConvertPointsToPixels(layoutOptions.CardHeightPt, layoutOptions.RenderDpi);

            byte[] pngBytes = renderer(
                rows[pageStartIndex + pageIndex],
                renderWidthPx,
                renderHeightPx);

            using var imageStream = new MemoryStream(pngBytes);
            using XImage image = XImage.FromStream(() => imageStream);

            graphics.DrawImage(
                image,
                x,
                y,
                layoutOptions.CardWidthPt,
                layoutOptions.CardHeightPt);
        }
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
