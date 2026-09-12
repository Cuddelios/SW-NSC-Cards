using SvgPdfGenerator;
using SvgPdfGenerator.Models;
using System.Text.Json;

internal class Program
{
    private static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception error) when (error is InvalidOperationException or IOException or JsonException or ArgumentException)
        {
            Console.Error.WriteLine($"Fehler: {error.Message}");
            return 1;
        }
    }

    private static void Run(string[] args)
    {
        List<CardConfiguration> selectedConfigurations;

        if (args.Length == 0)
        {
            selectedConfigurations = SelectCardConfigurations(Path.Combine("data", "card_configuration.json"));
        }
        else
        {
            string csvPath = args.Length > 0
                ? args[0]
                : SelectInputFile("data", "*.csv", "Daten");

            string svgTemplatePath = args.Length > 1
                ? args[1]
                : SelectInputFile("templates", "*_template*.svg", "Vorlage");

            string backTemplatePath = args.Length > 2
                ? args[2]
                : SelectInputFile("templates", "npc_card_back_*.svg", "Rueckseiten-Vorlage");

            selectedConfigurations =
            [
                new CardConfiguration
        {
            Name = Path.GetFileNameWithoutExtension(csvPath),
            Data = csvPath,
            Template = svgTemplatePath,
            Backcard = backTemplatePath,
            Output = BuildOutputPathFromData(csvPath)
        }
            ];
            string defaultsPath = Path.Combine("data", "card_configuration.json");
            if (File.Exists(defaultsPath))
            {
                var defaults = JsonSerializer.Deserialize<CardConfigurationFile>(File.ReadAllText(defaultsPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                selectedConfigurations[0].Fields = FieldConfiguration.Merge(defaults?.Fields ?? new(), new());
                // Explicit CSV/template arguments use the matching set's field definitions too.
                foreach (var configuration in defaults?.Configurations ?? [])
                {
                    var matchingSet = (configuration.CardSets ?? [configuration]).FirstOrDefault(set =>
                        string.Equals(Path.GetFullPath(Path.Combine("data", set.Data)), Path.GetFullPath(csvPath), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(Path.GetFullPath(Path.Combine("templates", set.Template)), Path.GetFullPath(svgTemplatePath), StringComparison.OrdinalIgnoreCase));
                    if (matchingSet == null) continue;
                    selectedConfigurations[0].Fields = FieldConfiguration.Merge(
                        FieldConfiguration.Merge(defaults!.Fields, configuration.Fields), matchingSet.Fields);
                    selectedConfigurations[0].CountField = matchingSet.CountField;
                    break;
                }
            }
        }

        var createCmykPdfs = SelectContinue("Sollen die PDF-Dateien als cmyk erzeugt werden?");
        var createMeinspielOutput = SelectContinue("Moechten Sie die MeinSpiel-Ausgabe erzeugen?");

        var batches = selectedConfigurations.Select(configuration => CardBatch.Prepare(configuration, args.Length == 0, createMeinspielOutput)).ToList();
        foreach (CardConfiguration selectedConfiguration in selectedConfigurations)
        {
            Console.WriteLine();
            Console.WriteLine($"Ausfuehrung der Karten-Konfiguration: {selectedConfiguration.Name}");
            GenerateCards(selectedConfiguration, batches[selectedConfigurations.IndexOf(selectedConfiguration)], createCmykPdfs, createMeinspielOutput);
        }

        static void GenerateCards(
            CardConfiguration selectedConfiguration,
            CardBatch batch,
            bool createCmykPdfs,
            bool createMeinspielOutput)
        {
            string outputPdfHorizontalPath = BuildOutputPath(selectedConfiguration.Output, horizontalMirror: true);
            string outputPdfVerticalPath = BuildOutputPath(selectedConfiguration.Output, horizontalMirror: false);
            string outputPathBase = BuildOutputPath(selectedConfiguration.Output);
            var cards = batch.Cards;
            if (cards.Count == 0 && (!createMeinspielOutput || batch.MeinspielCards.Count == 0))
            {
                Console.WriteLine("Die Konfiguration enthält keine Karten.");
                return;
            }

            var layoutOptions = new PdfLayoutOptions
            {
                MarginPt = MmToPt(5),
                GapXPt = MmToPt(3),
                GapYPt = MmToPt(3),
                CardWidthPt = MmToPt(65),
                CardHeightPt = MmToPt(97),
                RenderDpi = 300
            };

            var pdfWriter = new PdfLayoutWriter();
            pdfWriter.WriteCardsWithInterleavedBacks(
                outputPdfHorizontalPath,
                cards,
                batch.RenderFront,
                batch.RenderBack,
                layoutOptions,
                mirrorBackPageHorizontally: true);

            pdfWriter.WriteCardsWithInterleavedBacks(
                outputPdfVerticalPath,
                cards,
                batch.RenderFront,
                batch.RenderBack,
                layoutOptions,
                mirrorBackPageHorizontally: false);

            Console.WriteLine($"DinA4 PDF erzeugt: {Path.GetFullPath(outputPdfHorizontalPath)}");
            Console.WriteLine($"DinA4 PDF erzeugt: {Path.GetFullPath(outputPdfVerticalPath)}");

            if (createCmykPdfs)
            {
                ConvertPdfToCmykIfPossible(outputPdfHorizontalPath);
                ConvertPdfToCmykIfPossible(outputPdfVerticalPath);
            }

            if (!createMeinspielOutput)
            {
                return;
            }

            var meinspielFrontOutputPath = BuildMeinspielFrontOutputPath(outputPathBase);
            var meinspielBackOutputPath = BuildMeinspielBackOutputPath(outputPathBase);

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

            var meinspielCards = batch.MeinspielCards;
            pdfWriter.WriteCards(meinspielFrontOutputPath, meinspielCards, batch.RenderFront, meinspielLayoutOptions);
            pdfWriter.WriteCards(meinspielBackOutputPath, meinspielCards, batch.RenderBack, meinspielLayoutOptions);

            Console.WriteLine($"MeinSpiel Front-PDF erzeugt: {Path.GetFullPath(meinspielFrontOutputPath)}");
            Console.WriteLine($"MeinSpiel Back-PDF erzeugt: {Path.GetFullPath(meinspielBackOutputPath)}");

            ConvertPdfToCmykIfPossible(meinspielFrontOutputPath);
            ConvertPdfToCmykIfPossible(meinspielBackOutputPath);

        }

        static double MmToPt(double millimeters) => millimeters * 72.0 / 25.4;

        static bool SelectContinue(string label)
        {
            Console.WriteLine($"{label} (y/j/n)");
            while (true)
            {
                Console.Write("Bitte wählen (y/j/n): ");
                string? input = Console.ReadLine();

                if (string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "j", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(input, "n", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Console.WriteLine("Ungültige Eingabe. Bitte geben Sie 'y/j' für ja oder 'n' für nein ein.");
            }
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

            if (files.Count == 1)
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

        static List<CardConfiguration> SelectCardConfigurations(string configurationPath)
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

            foreach (var configuration in configurations)
            {
                configuration.Fields = FieldConfiguration.Merge(configurationFile!.Fields, configuration.Fields);
            }

            Console.WriteLine("Karten-Konfiguration auswaehlen:");
            Console.WriteLine("  A. Alle Konfigurationen ausfuehren");
            for (int index = 0; index < configurations.Count; index++)
            {
                Console.WriteLine($"  {index + 1}. {configurations[index].Name}");
            }

            if (configurations.Count == 1)
            {
                Console.WriteLine($"Nur eine Option verfuegbar, automatisch ausgewaehlt: {configurations[0].Name}");
                return [ValidateCardConfiguration(configurations[0], fullConfigurationPath)];
            }

            while (true)
            {
                Console.Write("Nummer oder A fuer alle Karten-Konfigurationen: ");
                string? input = Console.ReadLine();

                if (string.Equals(input, "a", StringComparison.OrdinalIgnoreCase))
                {
                    return configurations
                        .Select(configuration => ValidateCardConfiguration(configuration, fullConfigurationPath))
                        .ToList();
                }

                if (int.TryParse(input, out int selection)
                    && selection >= 1
                    && selection <= configurations.Count)
                {
                    return [ValidateCardConfiguration(configurations[selection - 1], fullConfigurationPath)];
                }

                Console.WriteLine($"Bitte 'A' oder eine Zahl zwischen 1 und {configurations.Count} eingeben.");
            }
        }

        static CardConfiguration ValidateCardConfiguration(
            CardConfiguration configuration,
            string configurationPath)
        {
            RequireConfigurationValue(configuration.Name, "name", configurationPath);
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

        static string BuildOutputPath(string outputName, bool? horizontalMirror = null)
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

            if (horizontalMirror.HasValue)
                fileNameWithoutExtension += horizontalMirror.Value ? ".h_mirror" : ".v_mirror";

            return Path.Combine("output", $"{fileNameWithoutExtension}.pdf");
        }
    }
}


sealed class CardConfigurationFile
{
    public Dictionary<string, FieldConfiguration> Fields { get; set; } = new();
    public List<CardConfiguration>? Configurations { get; set; }
}
