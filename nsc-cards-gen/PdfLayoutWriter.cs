using SkiaSharp;
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
        bool? mirrorBackPageHorizontally = null)
    {
        if (string.IsNullOrWhiteSpace(outputPdfPath))
        {
            throw new ArgumentException("Output PDF path must not be empty.", nameof(outputPdfPath));
        }

        var fileName = Path.GetFileName(outputPdfPath);

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

        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPdfPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using SKDocument pdf = SKDocument.CreatePdf(outputPdfPath);

        Console.Write($"Creating PDF {fileName} ");

        SKCanvas? canvas = null;
        double pageWidthPt = 0;
        double pageHeightPt = 0;

        int columnsPerPage = 0;
        int rowsPerPage = 0;
        int itemsPerPage = 0;

        void StartNewPage()
        {
            if (canvas != null)
            {
                pdf.EndPage();
            }

            (pageWidthPt, pageHeightPt) = GetPageSize(layoutOptions);
            canvas = pdf.BeginPage((float)pageWidthPt, (float)pageHeightPt);

            columnsPerPage = Math.Max(
                1,
                (int)((pageWidthPt - 2 * layoutOptions.MarginPt + layoutOptions.GapXPt)
                    / (layoutOptions.CardWidthPt + layoutOptions.GapXPt)));

            rowsPerPage = Math.Max(
                1,
                (int)((pageHeightPt - 2 * layoutOptions.MarginPt + layoutOptions.GapYPt)
                    / (layoutOptions.CardHeightPt + layoutOptions.GapYPt)));

            itemsPerPage = columnsPerPage * rowsPerPage;
        }

        StartNewPage();

        for (int pageStartIndex = 0; pageStartIndex < rows.Count; pageStartIndex += itemsPerPage)
        {
            Console.Write(".");

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
                canvas!,
                layoutOptions,
                (pageWidthPt, pageHeightPt));

            if (backRenderer != null)
            {
                StartNewPage();
                DrawCardPage(
                    rows,
                    pageStartIndex,
                    cardsOnPage,
                    columnsPerPage,
                    backRenderer,
                    canvas!,
                    layoutOptions,
                    (pageWidthPt, pageHeightPt),
                    mirrorHorizontally: mirrorBackPageHorizontally);
            }
        }

        if (canvas != null)
        {
            pdf.EndPage();
        }

        pdf.Close();

        Console.WriteLine(" done");
        Console.WriteLine($"with {rows.Count} cards and {(rows.Count + itemsPerPage - 1) / itemsPerPage} pages.");
    }

    private static void DrawCardPage(
        IReadOnlyList<Dictionary<string, string>> rows,
        int pageStartIndex,
        int cardsOnPage,
        int columnsPerPage,
        CardRenderDelegate renderer,
        SKCanvas canvas,
        PdfLayoutOptions layoutOptions,
        (double Width, double Height) pageSizePt,
        bool? mirrorHorizontally = null)
    {
        for (int pageIndex = 0; pageIndex < cardsOnPage; pageIndex++)
        {
            int column = pageIndex % columnsPerPage;
            int row = pageIndex / columnsPerPage;

            double x = layoutOptions.MarginPt + column * (layoutOptions.CardWidthPt + layoutOptions.GapXPt);
            double y = layoutOptions.MarginPt + row * (layoutOptions.CardHeightPt + layoutOptions.GapYPt);

            if (mirrorHorizontally!= null)
            {
                if (mirrorHorizontally.Value)
                    x = pageSizePt.Width - x - layoutOptions.CardWidthPt;
                else
                    y = pageSizePt.Height - y - layoutOptions.CardHeightPt;                
            }

            int renderWidthPx = ConvertPointsToPixels(layoutOptions.CardWidthPt, layoutOptions.RenderDpi);
            int renderHeightPx = ConvertPointsToPixels(layoutOptions.CardHeightPt, layoutOptions.RenderDpi);

            byte[] pngBytes = renderer(
                rows[pageStartIndex + pageIndex],
                renderWidthPx,
                renderHeightPx);

            using SKData imageData = SKData.CreateCopy(pngBytes);
            using SKImage image = SKImage.FromEncodedData(imageData)
                ?? throw new InvalidOperationException("Rendered card PNG could not be decoded.");

            var destination = new SKRect(
                (float)x,
                (float)y,
                (float)(x + layoutOptions.CardWidthPt),
                (float)(y + layoutOptions.CardHeightPt));

            canvas.DrawImage(
                image,
                destination);
        }
    }

    private static (double WidthPt, double HeightPt) GetPageSize(PdfLayoutOptions layoutOptions)
    {
        if (layoutOptions.PageWidthPt is > 0 && layoutOptions.PageHeightPt is > 0)
        {
            return (layoutOptions.PageWidthPt.Value, layoutOptions.PageHeightPt.Value);
        }

        return (MmToPt(297), MmToPt(210));
    }

    private static double MmToPt(double millimeters) => millimeters * 72.0 / 25.4;

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
