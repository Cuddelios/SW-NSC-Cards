using SvgPdfGenerator;
using SvgPdfGenerator.Models;

string csvPath = "daten.csv";
string svgTemplatePath = "template.svg";
string outputPdfPath = "output/output.pdf";

var csvReader = new CsvReaderService();
List<Dictionary<string, string>> rows = csvReader.Read(csvPath, ';');

if (rows.Count == 0)
{
    Console.WriteLine("Die CSV enthält keine Daten.");
    return;
}

var cardRenderer = new SvgCardRenderer(svgTemplatePath);

var layoutOptions = new PdfLayoutOptions
{
    MarginPt = 30,
    GapXPt = 12,
    GapYPt = 12,
    CardWidthPt = 220,
    CardHeightPt = 90
};

var pdfWriter = new PdfLayoutWriter();
pdfWriter.WriteCards(outputPdfPath, rows, cardRenderer, layoutOptions);

Console.WriteLine($"PDF erzeugt: {Path.GetFullPath(outputPdfPath)}");
