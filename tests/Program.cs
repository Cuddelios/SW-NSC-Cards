using SvgPdfGenerator;
using System.Xml.Linq;
using System.Text.Json;

string template = Path.GetTempFileName();
try
{
    File.WriteAllText(template, """
    <svg xmlns="http://www.w3.org/2000/svg">
      <text data-field="abilities" x="0"/>
      <g data-field="icons"><g data-field="d8"><path d="M0 0"/></g></g>
      <text data-field="notes" x="0"/>
      <g data-field="shown"><path/></g>
      <rect data-field="color" fill="black"/>
      <text data-field="alias"/>
    </svg>
    """);
    var rules = FieldConfiguration.Merge(new(), new()
    {
        ["abilities"] = new() { Source = "custom", Type = "skillLabels", LineHeight = 9 },
        ["icons"] = new() { Source = "custom", Type = "skillDice", LineHeightFrom = "abilities" },
        ["notes"] = new() { Type = "text", WrapLength = 5, OffsetAfter = ["abilities"], OffsetY = 1 },
        ["shown"] = new() { Type = "visibility", ValueMap = new() { ["ja"] = "true" } },
        ["color"] = new() { Type = "fill", DefaultValue = "#abcdef" },
        ["alias"] = new() { Aliases = ["legacy"], Type = "text" }
    });
    var renderer = new SvgCardRenderer(template, fields: rules);
    var svg = XDocument.Parse(renderer.BuildFilledSvg(new Dictionary<string, string>
    {
        ["custom"] = "First d8, Second d8", ["notes"] = "hello world",
        ["shown"] = "ja", ["legacy"] = "works"
    }));
    XElement Field(string name) => svg.Descendants().First(e => (string?)e.Attribute("data-field") == name);
    void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    Check(Field("abilities").Value == "FirstSecond", "Skill labels");
    Check(Field("abilities").Elements().Last().Attribute("dy")?.Value == "9px", "Configured line height");
    Check(Field("icons").Elements().Count() == 2, "Dice icons");
    Check(Field("notes").Attribute("transform")?.Value == "translate(0 10)", "Dependent offset");
    Check(Field("notes").Elements().Count() == 2, "Wrapping");
    Check(Field("shown").Attribute("display") == null, "Mapped visibility");
    Check(Field("color").Attribute("fill")?.Value == "#abcdef", "Default fill");
    Check(Field("alias").Value == "works", "Alias");
    foreach (var invalid in new[] { new FieldConfiguration { Type = "typo" }, new FieldConfiguration { WrapLength = 0 }, new FieldConfiguration { OffsetAfter = ["missing"] } })
    {
        bool rejected = false;
        try { FieldConfiguration.Merge(new(), new() { ["bad"] = invalid }); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid configuration was accepted");
    }
    using var json = JsonDocument.Parse(File.ReadAllText("data/card_configuration.json"));
    var shipped = JsonSerializer.Deserialize<Dictionary<string, FieldConfiguration>>(json.RootElement.GetProperty("fields"),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    foreach (var deck in json.RootElement.GetProperty("configurations").EnumerateArray())
    {
        var configuration = JsonSerializer.Deserialize<CardConfiguration>(deck,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        configuration.Fields = FieldConfiguration.Merge(shipped, configuration.Fields);
        CardBatch.Prepare(configuration, true, true);
        if (configuration.CardSets != null) continue;
        var cardRenderer = new SvgCardRenderer(Path.Combine("templates", deck.GetProperty("template").GetString()!), fields: shipped);
        var rows = new CsvReaderService().Read(Path.Combine("data", deck.GetProperty("data").GetString()!), ',');
        foreach (var row in rows) XDocument.Parse(cardRenderer.BuildFilledSvg(row));
    }
    BatchChecks.Run();
    Console.WriteLine("All generator checks passed.");
}
finally { File.Delete(template); }
