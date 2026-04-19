using SvgPdfGenerator;
using SvgPdfGenerator.Models;

string csvPath = args.Length > 0 ? args[0] : Path.Combine("data", "test.csv");
string svgTemplatePath = args.Length > 1 ? args[1] : Path.Combine("templates", "char_template.svg");
string outputPdfPath = args.Length > 2 ? args[2] : Path.Combine("output", "output.pdf");
bool usesCharacterTemplate = string.Equals(
    Path.GetFileName(svgTemplatePath),
    "char_template.svg",
    StringComparison.OrdinalIgnoreCase);

var csvReader = new CsvReaderService();
List<Dictionary<string, string>> rows = csvReader.Read(csvPath, ';');

if (rows.Count == 0)
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
    CardHeightPt = usesCharacterTemplate ? MmToPt(96) : 90
};

var pdfWriter = new PdfLayoutWriter();
pdfWriter.WriteCards(outputPdfPath, rows, cardRenderer, layoutOptions);

Console.WriteLine($"PDF erzeugt: {Path.GetFullPath(outputPdfPath)}");

static double MmToPt(double millimeters) => millimeters * 72.0 / 25.4;
