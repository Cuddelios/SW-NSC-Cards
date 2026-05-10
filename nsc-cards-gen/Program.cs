using SvgPdfGenerator;
using SvgPdfGenerator.Models;

string csvPath = args.Length > 0
    ? args[0]
    : SelectInputFile("data", "*.csv", "Daten");

string svgTemplatePath = args.Length > 1
    ? args[1]
    : SelectInputFile("templates", "*.svg", "Vorlage");

string outputPdfPath = BuildOutputPathFromData(csvPath);
bool usesCharacterTemplate = true;
// bool usesCharacterTemplate = string.Equals(
//Path.GetFileName(svgTemplatePath),
//    "char_template.svg",
//    StringComparison.OrdinalIgnoreCase);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPdfPath)) ?? "output");

Console.WriteLine();
if (args.Length > 2)
{
    Console.WriteLine("Hinweis: Ein uebergebener Ausgabepfad wird ignoriert; die Ausgabe wird aus dem Datennamen gebildet.");
}

Console.WriteLine($"Daten: {Path.GetFullPath(csvPath)}");
Console.WriteLine($"Vorlage: {Path.GetFullPath(svgTemplatePath)}");
Console.WriteLine($"Ausgabe: {Path.GetFullPath(outputPdfPath)}");
Console.WriteLine();

var csvReader = new CsvReaderService();
List<Dictionary<string, string>> rows = csvReader.Read(csvPath, DetectDelimiter(csvPath));
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

ConvertPdfToCmykIfPossible(outputPdfPath);
ConvertPdfToCmykIfPossible(meinspielFrontOutputPath);

static double MmToPt(double millimeters) => millimeters * 72.0 / 25.4;

static char DetectDelimiter(string csvPath)
{
    string? header = File.ReadLines(csvPath).FirstOrDefault();
    if (string.IsNullOrWhiteSpace(header))
    {
        return ',';
    }

    int semicolonCount = header.Count(character => character == ';');
    int commaCount = header.Count(character => character == ',');

    return semicolonCount > commaCount ? ';' : ',';
}

static string SelectInputFile(string directoryPath, string searchPattern, string label)
{
    string fullDirectoryPath = Path.GetFullPath(directoryPath);
    if (!Directory.Exists(fullDirectoryPath))
    {
        throw new DirectoryNotFoundException($"Der Ordner fuer die {label} wurde nicht gefunden: {fullDirectoryPath}");
    }

    List<string> files = Directory
        .GetFiles(fullDirectoryPath, searchPattern)
        .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    if (files.Count == 0)
    {
        throw new InvalidOperationException($"Keine {label}-Dateien in {fullDirectoryPath} gefunden.");
    }

    Console.WriteLine($"{label} auswaehlen:");
    for (int index = 0; index < files.Count; index++)
    {
        Console.WriteLine($"  {index + 1}. {Path.GetFileName(files[index])}");
    }

    while (true)
    {
        Console.Write($"Nummer fuer {label}: ");
        string? input = Console.ReadLine();

        if (int.TryParse(input, out int selection)
            && selection >= 1
            && selection <= files.Count)
        {
            return files[selection - 1];
        }

        Console.WriteLine($"Bitte eine Zahl zwischen 1 und {files.Count} eingeben.");
    }
}

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

static void ConvertPdfToCmykIfPossible(string inputPdfPath)
{
    string cmykOutputPath = BuildCmykOutputPath(inputPdfPath);
    string iccProfilePath = Path.Combine("profiles", "ISOcoated_v2_300_eci.icc");

    var converter = new PdfColorConverter();
    PdfColorConversionResult result = converter.ConvertToCmyk(
        inputPdfPath,
        cmykOutputPath,
        iccProfilePath);

    switch (result.Status)
    {
        case PdfColorConversionStatus.Converted:
            Console.WriteLine(result.Message);
            break;

        case PdfColorConversionStatus.Skipped:
            Console.WriteLine($"CMYK-Konvertierung uebersprungen: {result.Message}");
            break;

        case PdfColorConversionStatus.Failed:
            Console.WriteLine($"CMYK-Konvertierung fehlgeschlagen: {result.Message}");
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Console.WriteLine("Ghostscript-Ausgabe:");
                Console.WriteLine(result.StandardOutput.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Console.WriteLine("Ghostscript-Fehlerausgabe:");
                Console.WriteLine(result.StandardError.Trim());
            }
            break;
    }
}

static string BuildCmykOutputPath(string inputPdfPath)
{
    string fullPath = Path.GetFullPath(inputPdfPath);
    string directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

    return Path.Combine(directory, $"{fileNameWithoutExtension}.cmyk.pdf");
}

static string BuildOutputPathFromData(string csvPath)
{
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(csvPath);

    if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
    {
        fileNameWithoutExtension = "output";
    }

    foreach (char invalidChar in Path.GetInvalidFileNameChars())
    {
        fileNameWithoutExtension = fileNameWithoutExtension.Replace(invalidChar, '_');
    }

    return Path.Combine("output", $"{fileNameWithoutExtension}.pdf");
}
