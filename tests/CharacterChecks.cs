using SvgPdfGenerator;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

static class CharacterChecks
{
    public static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        using var json = JsonDocument.Parse(File.ReadAllText("data/card_configuration.json"));
        var config = JsonSerializer.Deserialize<CardConfiguration>(json.RootElement.GetProperty("configurations")
            .EnumerateArray().Single(c => c.TryGetProperty("data", out var data) && data.GetString() == "GnM_Charaktere.csv"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var batch = CardBatch.Prepare(config, true, true);
        Check(batch.Cards.Count == 24, "24 character records");
        Check(Math.Abs(batch.CardSizePt.Width * 25.4 / 72 - 126) < .001 && Math.Abs(batch.CardSizePt.Height * 25.4 / 72 - 76) < .001, "SVG dimensions");
        var front = new SvgCardRenderer("templates/" + config.Template, fields: config.Fields);
        var back = new SvgCardRenderer("templates/" + config.Backcard, fields: config.Fields);
        foreach (var row in batch.Cards)
        {
            var f = XDocument.Parse(front.BuildFilledSvg(row));
            var b = XDocument.Parse(back.BuildFilledSvg(row));
            foreach (var (doc, sourcePath) in new[] { (f, config.Template), (b, config.Backcard) })
            {
                var source = XDocument.Load("templates/" + sourcePath);
                foreach (var text in doc.Descendants().Where(e => e.Name.LocalName == "text" && e.Attribute("data-bind") != null))
                {
                    var original = source.Descendants().Single(e => (string?)e.Attribute("id") == (string?)text.Attribute("id"));
                    string Size(XElement e) => Regex.Match((string?)e.Attribute("style") ?? "", @"font-size:[^;]+").Value;
                    Check(Size(text) == Size(original), "Template font size preserved");
                    Check(text.Elements().All(e => !((string?)e.Attribute("style") ?? "").Contains("font-size")), "No per-line font shrinking");
                }
                string background = (string)doc.Descendants().Single(e => (string?)e.Attribute("id") == "rect24").Attribute("style")!;
                Check(background.Contains(row["rang"] == "Fortgeschritten" ? "fill:#ffeeaa" : "fill:#c6e9af"), "Rank background on both sides");
            }
            XElement Field(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute("data-field") == name);
            Check(Field(f, "portraet_datei").Attribute("href")!.Value.StartsWith("data:image/png;base64,"), "Embedded portrait");
            Check(Field(b, "parade").Value == row["parade"], "Individual back values");
            foreach (var dice in b.Descendants().Where(e => (string?)e.Attribute("data-bind") == "dice"))
            {
                string value = row[(string)dice.Attribute("data-field")!];
                var visible = dice.Elements().Where(e => !((string?)e.Attribute("style") ?? "").Contains("display:none")).ToList();
                Check(visible.Count == (value.Length == 0 ? 0 : 1), "Exactly one die per filled slot");
                if (visible.Count > 0) Check((string?)visible[0].Attribute("data-value") == value, "Correct die selected");
            }
            var skills = b.Descendants().Single(e => (string?)e.Attribute("data-bind") == "skill-list");
            Check(skills.Elements().Select(e => e.Value).SequenceEqual(Enumerable.Range(1, 9).Select(i => row[$"fertigkeit_{i:00}_name"])), "Nine ordered skill slots");
            double Y(XElement e) => double.Parse((string)e.Attribute("y")!, CultureInfo.InvariantCulture);
            double lastSkill = skills.Elements().Where(e => e.Value.Length > 0).Max(Y);
            Check(Math.Abs(Y(Field(b, "macht_liste")) - lastSkill - 5) < .001, "Powers below last occupied skill with fixed gap");
            bool shooting = Enumerable.Range(1, 9).Any(i => row[$"fertigkeit_{i:00}_name"].Equals("Schießen", StringComparison.OrdinalIgnoreCase));
            var ammo = b.Descendants().Single(e => (string?)e.Attribute("data-template-role") == "ammo");
            Check((ammo.Attribute("display")?.Value != "none") == shooting, "Ammo only for shooting skill");
            bool hasMagic = int.TryParse(row["machtpunkte"], out int points) && points > 0;
            Check((Field(b, "machtpunkte").Attribute("display")?.Value != "none") == hasMagic, "Magic visibility");
            if (hasMagic)
            {
                var strip = Field(b, "machtpunkte").Elements().Single();
                Check(strip.Elements().Count() == points, "Exact magic counter symbol count");
                Check(strip.Attribute("transform") == null, "No point width scaling");
                var positions = strip.Elements().Select(e => double.Parse(Regex.Matches((string)e.Attribute("transform")!, @"-?\d+(?:\.\d+)?")[1].Value, CultureInfo.InvariantCulture)).ToArray();
                Check(positions.Zip(positions.Skip(1)).All(p => Math.Abs(p.First - p.Second - 5.511867) < .002), "Constant point spacing for 10 and 15 points");
            }
        }
        config.ExpectedCardCount = 23;
        bool rejected = false;
        try { CardBatch.Prepare(config, true, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Expected count enforced without MeinSpiel export");
        string fixture = Path.GetTempFileName();
        try
        {
            foreach (string dimensions in new[] { "width='25.4mm' height='2.54cm'", "width='1in' height='72pt'", "width='6pc' height='96px'", "viewBox='0 0 96 96'" })
            {
                File.WriteAllText(fixture, $"<svg xmlns='http://www.w3.org/2000/svg' {dimensions}/>");
                var size = new SvgCardRenderer(fixture).SizePt;
                Check(Math.Abs(size.Width - 72) < .001 && Math.Abs(size.Height - 72) < .001, "SVG length unit conversion");
            }
            File.WriteAllText(fixture, "<svg xmlns='http://www.w3.org/2000/svg' width='10mm' height='10mm'/>");
            config.ExpectedCardCount = 24;
            config.Backcard = fixture;
            rejected = false;
            try { CardBatch.Prepare(config, true, false); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Mismatched front/back dimensions rejected");
        }
        finally { File.Delete(fixture); }
    }
}
