using SvgPdfGenerator;
using SvgPdfGenerator.Models;

string csvPath = args.Length > 0 ? args[0] : Path.Combine("data", "char_template_example.csv");
//string svgTemplatePath = args.Length > 1 ? args[1] : Path.Combine("templates", "char_template.svg");
string svgTemplatePath = args.Length > 1 ? args[1] : Path.Combine("templates", "CharTemplate_Dice_opt.svg");
string outputPdfPath = args.Length > 2 ? args[2] : Path.Combine("output", "output.pdf");
bool usesCharacterTemplate = true;
// bool usesCharacterTemplate = string.Equals(
//Path.GetFileName(svgTemplatePath),
//    "char_template.svg",
//    StringComparison.OrdinalIgnoreCase);

var csvReader = new CsvReaderService();
List<Dictionary<string, string>> rows = csvReader.Read(csvPath, ',');
List<Dictionary<string, string>> cards = ExpandRowsByCount(rows, "count");

if (cards.Count == 0)
{
    Console.WriteLine("Die CSV enthält keine Daten.");
    return;
}

var cardRenderer = new SvgCardRenderer(
    svgTemplatePath,
    templateViewBox: usesCharacterTemplate ? "0 0 64 96" : null);

var layoutOptions = new PdfLayoutOptions
{
    MarginPt = 20,
    GapXPt = 10,
    GapYPt = 10,
    CardWidthPt = usesCharacterTemplate ? MmToPt(64) : 220,
    CardHeightPt = usesCharacterTemplate ? MmToPt(96) : 90,
    RenderDpi = 300
};

var pdfWriter = new PdfLayoutWriter();
pdfWriter.WriteCards(outputPdfPath, cards, cardRenderer, layoutOptions);

var meinspielFrontOutputPath = BuildMeinspielFrontOutputPath(outputPdfPath);
var meinspielLayoutOptions = new PdfLayoutOptions
{
    MarginPt = 0,
    GapXPt = 0,
    GapYPt = 0,
    CardWidthPt = MmToPt(65),
    CardHeightPt = MmToPt(97),
    RenderDpi = 300,
    PageWidthPt = MmToPt(65),
    PageHeightPt = MmToPt(97)
};

pdfWriter.WriteCards(meinspielFrontOutputPath, cards, cardRenderer, meinspielLayoutOptions);

Console.WriteLine($"PDF erzeugt: {Path.GetFullPath(outputPdfPath)}");
Console.WriteLine($"MeinSpiel Front-PDF erzeugt: {Path.GetFullPath(meinspielFrontOutputPath)}");

static double MmToPt(double millimeters) => millimeters * 72.0 / 25.4;

static List<Dictionary<string, string>> ExpandRowsByCount(
    IReadOnlyList<Dictionary<string, string>> rows,
    string countFieldName)
{
    var expandedRows = new List<Dictionary<string, string>>();

    foreach (Dictionary<string, string> row in rows)
    {
        int count = GetCardCount(row, countFieldName);

        for (int copyIndex = 0; copyIndex < count; copyIndex++)
        {
            expandedRows.Add(row);
        }
    }

    return expandedRows;
}

static int GetCardCount(
    IReadOnlyDictionary<string, string> row,
    string countFieldName)
{
    if (!row.TryGetValue(countFieldName, out string? rawCount)
        || string.IsNullOrWhiteSpace(rawCount))
    {
        return 1;
    }

    if (!int.TryParse(rawCount, out int count))
    {
        throw new InvalidOperationException(
            $"Der Wert in der Spalte '{countFieldName}' muss eine ganze Zahl sein: '{rawCount}'.");
    }

    if (count < 0)
    {
        throw new InvalidOperationException(
            $"Der Wert in der Spalte '{countFieldName}' darf nicht negativ sein: '{rawCount}'.");
    }

    return count;
}

static string BuildMeinspielFrontOutputPath(string outputPdfPath)
{
    string fullPath = Path.GetFullPath(outputPdfPath);
    string directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

    return Path.Combine(directory, $"{fileNameWithoutExtension}.meinspiel-front.pdf");
}
