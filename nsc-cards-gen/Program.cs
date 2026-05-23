using SvgPdfGenerator;
using SvgPdfGenerator.Models;
using System.Text.Json;

string csvPath;
string svgTemplatePath;
string backTemplatePath;
string outputPdfPath;

if (args.Length == 0)
{
    CardConfiguration selectedConfiguration = SelectCardConfiguration(Path.Combine("data", "card_configuration.json"));
    csvPath = Path.Combine("data", selectedConfiguration.Data);
    svgTemplatePath = Path.Combine("templates", selectedConfiguration.Template);
    backTemplatePath = Path.Combine("templates", selectedConfiguration.Backcard);
    outputPdfPath = BuildOutputPath(selectedConfiguration.Output);
}
else
{
    csvPath = args.Length > 0
        ? args[0]
        : SelectInputFile("data", "*.csv", "Daten");

    svgTemplatePath = args.Length > 1
        ? args[1]
        : SelectInputFile("templates", "*_template*.svg", "Vorlage");

    backTemplatePath = args.Length > 2
        ? args[2]
        : SelectInputFile("templates", "npc_card_back_*.svg", "Rueckseiten-Vorlage");

    outputPdfPath = BuildOutputPathFromData(csvPath);
}

EnsureFileExists(csvPath, "Daten");
EnsureFileExists(svgTemplatePath, "Vorlage");
EnsureFileExists(backTemplatePath, "Rueckseiten-Vorlage");
bool usesCharacterTemplate = true;
// bool usesCharacterTemplate = string.Equals(
//Path.GetFileName(svgTemplatePath),
//    "char_template.svg",
//    StringComparison.OrdinalIgnoreCase);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPdfPath)) ?? "output");

Console.WriteLine();
if (args.Length > 2)
{
    Console.WriteLine("Hinweis: Das dritte Argument wird als Rueckseiten-Vorlage verwendet; ein Ausgabepfad wird weiterhin ignoriert.");
}

Console.WriteLine($"Daten: {Path.GetFullPath(csvPath)}");
Console.WriteLine($"Vorlage: {Path.GetFullPath(svgTemplatePath)}");
Console.WriteLine($"Rueckseite: {Path.GetFullPath(backTemplatePath)}");
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
    svgTemplatePath);
var cardBackRenderer = new SvgCardRenderer(backTemplatePath);

var layoutOptions = new PdfLayoutOptions
{
    MarginPt = MmToPt(5),
    GapXPt = MmToPt(3),
    GapYPt = MmToPt(3),
    CardWidthPt = usesCharacterTemplate ? MmToPt(65) : 220,
    CardHeightPt = usesCharacterTemplate ? MmToPt(97) : 90,
    RenderDpi = 300
};

var pdfWriter = new PdfLayoutWriter();
pdfWriter.WriteCardsWithInterleavedBacks(
    outputPdfPath,
    cards,
    cardRenderer.RenderCardAsPng,
    RenderCardBackAsPng,
    layoutOptions,
    mirrorBackPageHorizontally: true);

var meinspielFrontOutputPath = BuildMeinspielFrontOutputPath(outputPdfPath);
var meinspielBackOutputPath = BuildMeinspielBackOutputPath(outputPdfPath);
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
pdfWriter.WriteCards(meinspielBackOutputPath, cards, RenderCardBackAsPng, meinspielLayoutOptions);

Console.WriteLine($"PDF erzeugt: {Path.GetFullPath(outputPdfPath)}");
Console.WriteLine($"MeinSpiel Front-PDF erzeugt: {Path.GetFullPath(meinspielFrontOutputPath)}");
Console.WriteLine($"MeinSpiel Back-PDF erzeugt: {Path.GetFullPath(meinspielBackOutputPath)}");

ConvertPdfToCmykIfPossible(outputPdfPath);
ConvertPdfToCmykIfPossible(meinspielFrontOutputPath);
ConvertPdfToCmykIfPossible(meinspielBackOutputPath);

byte[] RenderCardBackAsPng(
    IReadOnlyDictionary<string, string> row,
    int targetWidthPx,
    int targetHeightPx)
{
    return cardBackRenderer.RenderCardAsPng(
        row,
        targetWidthPx,
        targetHeightPx);
}

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

    if(files.Count == 1)
    {
        Console.WriteLine($"Nur eine Option verfuegbar, automatisch ausgewaehlt: {Path.GetFileName(files[0])}");
        return files[0];
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

static CardConfiguration SelectCardConfiguration(string configurationPath)
{
    string fullConfigurationPath = Path.GetFullPath(configurationPath);
    if (!File.Exists(fullConfigurationPath))
    {
        throw new FileNotFoundException("Die Karten-Konfiguration wurde nicht gefunden.", fullConfigurationPath);
    }

    string configurationJson = File.ReadAllText(fullConfigurationPath);
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    CardConfigurationFile? configurationFile = JsonSerializer.Deserialize<CardConfigurationFile>(
        configurationJson,
        jsonOptions);

    List<CardConfiguration> configurations = configurationFile?.Configurations?
        .Where(configuration => !string.IsNullOrWhiteSpace(configuration.Name))
        .ToList()
        ?? [];

    if (configurations.Count == 0)
    {
        throw new InvalidOperationException($"Keine Karten-Konfigurationen in {fullConfigurationPath} gefunden.");
    }

    Console.WriteLine("Karten-Konfiguration auswaehlen:");
    for (int index = 0; index < configurations.Count; index++)
    {
        Console.WriteLine($"  {index + 1}. {configurations[index].Name}");
    }

    if (configurations.Count == 1)
    {
        Console.WriteLine($"Nur eine Option verfuegbar, automatisch ausgewaehlt: {configurations[0].Name}");
        return ValidateCardConfiguration(configurations[0], fullConfigurationPath);
    }

    while (true)
    {
        Console.Write("Nummer fuer Karten-Konfiguration: ");
        string? input = Console.ReadLine();

        if (int.TryParse(input, out int selection)
            && selection >= 1
            && selection <= configurations.Count)
        {
            return ValidateCardConfiguration(configurations[selection - 1], fullConfigurationPath);
        }

        Console.WriteLine($"Bitte eine Zahl zwischen 1 und {configurations.Count} eingeben.");
    }
}

static CardConfiguration ValidateCardConfiguration(
    CardConfiguration configuration,
    string configurationPath)
{
    RequireConfigurationValue(configuration.Name, "name", configurationPath);
    RequireConfigurationValue(configuration.Template, "template", configurationPath);
    RequireConfigurationValue(configuration.Backcard, "backcard", configurationPath);
    RequireConfigurationValue(configuration.Data, "data", configurationPath);
    RequireConfigurationValue(configuration.Output, "output", configurationPath);

    return configuration;
}

static void RequireConfigurationValue(
    string value,
    string fieldName,
    string configurationPath)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Das Feld '{fieldName}' darf in {configurationPath} nicht leer sein.");
    }
}

static void EnsureFileExists(string filePath, string label)
{
    string fullPath = Path.GetFullPath(filePath);
    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"{label}-Datei wurde nicht gefunden.", fullPath);
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

static string BuildMeinspielBackOutputPath(string outputPdfPath)
{
    string fullPath = Path.GetFullPath(outputPdfPath);
    string directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

    return Path.Combine(directory, $"{fileNameWithoutExtension}.meinspiel-back.pdf");
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

static string BuildOutputPath(string outputName)
{
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputName);

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

sealed class CardConfigurationFile
{
    public List<CardConfiguration>? Configurations { get; set; }
}

sealed class CardConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string Template { get; set; } = string.Empty;

    public string Backcard { get; set; } = string.Empty;

    public string Titlecard { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;
}
