namespace SvgPdfGenerator;

public sealed class CardConfiguration
{
    public Dictionary<string, FieldConfiguration> Fields { get; set; } = new();
    public string CountField { get; set; } = "count";
    public string Name { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Backcard { get; set; } = string.Empty;
    public string Titlecard { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public List<CardConfiguration>? CardSets { get; set; }
    public int? MeinspielCardCount { get; set; }
    public int? ExpectedCardCount { get; set; }
    public bool SingleCardPages { get; set; }
}

/// <summary>Prepares every card before any PDF is opened; renderer bindings are not CSV metadata.</summary>
public sealed class CardBatch
{
    public List<Dictionary<string, string>> Cards { get; } = [];
    public List<Dictionary<string, string>> MeinspielCards { get; } = [];
    public (double Width, double Height) CardSizePt { get; private set; }
    private readonly Dictionary<IReadOnlyDictionary<string, string>, (SvgCardRenderer Front, SvgCardRenderer Back)> renderers = new(ReferenceEqualityComparer.Instance);

    public byte[] RenderFront(IReadOnlyDictionary<string, string> row, int width, int height)
        => renderers[row].Front.RenderCardAsPng(row, width, height);

    public byte[] RenderBack(IReadOnlyDictionary<string, string> row, int width, int height)
        => renderers[row].Back.RenderCardAsPng(row, width, height);

    public static CardBatch Prepare(CardConfiguration configuration, bool useConfigurationDirectories, bool exportMeinspiel)
    {
        if (configuration.MeinspielCardCount is <= 0)
            throw new InvalidOperationException($"'{configuration.Name}': meinspielCardCount muss positiv sein.");
        if (configuration.CardSets is { Count: 0 })
            throw new InvalidOperationException($"'{configuration.Name}': cardSets darf nicht leer sein.");
        if (configuration.CardSets != null && (!string.IsNullOrWhiteSpace(configuration.Data)
            || !string.IsNullOrWhiteSpace(configuration.Template) || !string.IsNullOrWhiteSpace(configuration.Backcard)
            || !string.IsNullOrWhiteSpace(configuration.Titlecard)))
            throw new InvalidOperationException($"'{configuration.Name}': cardSets darf nicht mit einzelnen CSV-/Vorlagenangaben kombiniert werden.");
        if (exportMeinspiel && configuration.CardSets != null && configuration.MeinspielCardCount == null)
            throw new InvalidOperationException($"'{configuration.Name}': Für kombinierte MeinSpiel-Ausgaben ist meinspielCardCount erforderlich.");

        var batch = new CardBatch();
        var counts = new List<string>();
        foreach (var set in configuration.CardSets ?? [configuration])
        {
            if (set == null || (configuration.CardSets != null && (set.CardSets != null || set.MeinspielCardCount != null)))
                throw new InvalidOperationException("Kartensätze dürfen keine weiteren cardSets oder eigene meinspielCardCount enthalten.");
            foreach (var (name, value) in new[] { ("data", set.Data), ("template", set.Template), ("backcard", set.Backcard), ("countField", set.CountField) })
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Kartensatz '{set.Name}': {name} darf nicht leer sein.");

            string Resolve(string directory, string path) => useConfigurationDirectories ? Path.Combine(directory, path) : path;
            var fields = FieldConfiguration.Merge(configuration.Fields, set.Fields);
            var front = new SvgCardRenderer(Resolve("templates", set.Template), fields: fields);
            var back = new SvgCardRenderer(Resolve("templates", set.Backcard), fields: fields);
            void CheckSize(SvgCardRenderer renderer)
            {
                var size = renderer.SizePt;
                if (batch.CardSizePt == default) batch.CardSizePt = size;
                if (Math.Abs(size.Width - batch.CardSizePt.Width) > .01 || Math.Abs(size.Height - batch.CardSizePt.Height) > .01)
                    throw new InvalidOperationException($"Kartensatz '{set.Name}': Alle Vorlagen müssen dieselbe Größe haben.");
            }
            CheckSize(front);
            CheckSize(back);
            int countBefore = batch.MeinspielCards.Count;
            if (!string.IsNullOrWhiteSpace(set.Titlecard))
            {
                var title = new SvgCardRenderer(Resolve("templates", set.Titlecard), fields: fields);
                CheckSize(title);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Titelkarte" };
                batch.MeinspielCards.Add(row);
                batch.renderers.Add(row, (title, back));
            }
            string csvPath = Resolve("data", set.Data);
            string header = File.ReadLines(csvPath).FirstOrDefault() ?? "";
            char delimiter = header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
            foreach (var row in new CsvReaderService().Read(csvPath, delimiter))
            {
                foreach (var key in front.ImageSources.Concat(back.ImageSources).Distinct(StringComparer.OrdinalIgnoreCase))
                    if (row.TryGetValue(key, out var image) && !string.IsNullOrWhiteSpace(image))
                        row[key] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(csvPath))!, image));
                int count = 1;
                if (row.TryGetValue(set.CountField, out var raw) && !string.IsNullOrWhiteSpace(raw)
                    && (!int.TryParse(raw, out count) || count < 0))
                    throw new InvalidOperationException($"Kartensatz '{set.Name}': Spalte '{set.CountField}' muss eine nichtnegative ganze Zahl enthalten: '{raw}'.");
                batch.renderers.Add(row, (front, back));
                for (int i = 0; i < count; i++)
                {
                    batch.Cards.Add(row);
                    batch.MeinspielCards.Add(row);
                }
            }
            counts.Add($"{set.Name}: {batch.MeinspielCards.Count - countBefore}");
        }
        if (configuration.ExpectedCardCount is int expectedCards && (expectedCards <= 0 || batch.Cards.Count != expectedCards))
            throw new InvalidOperationException($"Kartenanzahl für '{configuration.Name}': erwartet {expectedCards}, tatsächlich {batch.Cards.Count}.");
        if (exportMeinspiel && configuration.MeinspielCardCount is int expected && batch.MeinspielCards.Count != expected)
            throw new InvalidOperationException($"MeinSpiel-Kartenanzahl für '{configuration.Name}' stimmt nicht: erwartet {expected}, tatsächlich {batch.MeinspielCards.Count} (inklusive Titelkarten; {string.Join(", ", counts)}). Es wurden keine PDFs erzeugt.");
        return batch;
    }
}
