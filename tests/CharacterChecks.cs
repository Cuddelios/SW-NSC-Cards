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
        var frontTemplate = XDocument.Load("templates/" + config.Template);
        var backTemplate = XDocument.Load("templates/" + config.Backcard);
        XElement Field(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute("data-field") == name);
        double Y(XElement e) => double.Parse((string)e.Attribute("y")!, CultureInfo.InvariantCulture);
        double FontSize(XElement e) => double.Parse(Regex.Match((string?)e.Attribute("style") ?? "", @"font-size:([\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        double Bottom(XElement e) => Y(e) + Math.Max(0, e.Elements().Count() - 1) * FontSize(e) * 1.25;
        var layoutErrors = new StringWriter();
        TextWriter originalError = Console.Error;
        Console.SetError(layoutErrors);
        try
        {
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
            Check(Field(f, "portraet_datei").Attribute("href")!.Value.StartsWith("data:image/png;base64,"), "Embedded portrait");
            Check(Y(Field(f, "handicap_liste")) + .001 >= Y(Field(frontTemplate, "handicap_liste")), "Handicaps preserve template minimum y position");
            Check(Y(Field(f, "talent_liste")) + .001 >= Y(Field(frontTemplate, "talent_liste")), "Edges preserve template minimum y position");
            Check(Y(Field(f, "rolle")) + .001 >= Y(Field(frontTemplate, "rolle")), "Role preserves template minimum y position");
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
            double lastSkillY = skills.Elements().Where(e => !string.IsNullOrWhiteSpace(e.Value)).Max(Y);
            Check(Math.Abs(Y(Field(b, "macht_liste")) - Bottom(Field(b, "ausruestung_liste")) - FontSize(Field(b, "macht_liste")) * 1.25 * 1.5) < .002,
                "First power list starts one and a half lines below equipment");
            Check(Math.Abs(Y(Field(b, "macht_liste_2")) - lastSkillY - FontSize(Field(b, "macht_liste_2")) * 1.25 * 1.5) < .002,
                "Continued power list starts one and a half lines below skills");
            Check((string?)Field(b, "macht_liste").Attribute("x") == (string?)Field(backTemplate, "macht_liste").Attribute("x"),
                "Power list preserves template x position");
            Check((string?)Field(b, "macht_liste_2").Attribute("x") == (string?)Field(backTemplate, "macht_liste_2").Attribute("x"),
                "Continued power list preserves template x position");
            bool shooting = Enumerable.Range(1, 9).Any(i => row[$"fertigkeit_{i:00}_name"].Equals("Schießen", StringComparison.OrdinalIgnoreCase));
            var ammo = b.Descendants().Single(e => (string?)e.Attribute("data-template-role") == "ammo");
            Check((ammo.Attribute("display")?.Value != "none") == shooting, "Ammo only for shooting skill");
            bool hasMagic = int.TryParse(row["machtpunkte"], out int points) && points > 0;
            Check((Field(b, "machtpunkte").Attribute("display")?.Value != "none") == hasMagic, "Magic visibility");
            if (hasMagic)
            {
                string color = row["talent_liste"].Contains("(Magie)") ? "#7B2CBF"
                    : row["talent_liste"].Contains("(Wunder)") ? "#8A5700"
                    : row["talent_liste"].Contains("(Weird Science)") ? "#006B73" : "#2448A5";
                Check(((string?)Field(b, "macht_liste").Attribute("style"))?.Contains("fill:" + color) == true, "Arcane power text color");
                Check(Field(b, "machtpunkte").Descendants().Where(e => e.Name.LocalName == "rect")
                    .All(e => ((string?)e.Attribute("style"))?.Contains("fill:" + color) == true), "Arcane counter color");
                var strip = Field(b, "machtpunkte").Elements().Single();
                Check(strip.Elements().Count() == points, "Exact magic counter symbol count");
                Check(strip.Attribute("transform") == null, "No point width scaling");
                var pointRows = strip.Elements().Select(e => Regex.Matches((string)e.Attribute("transform")!, @"-?\d+(?:\.\d+)?")
                    .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray()).GroupBy(p => p[0]).ToList();
                Check(pointRows.Count == 1, "Power points remain in one row");
                foreach (var pointRow in pointRows)
                {
                    var positions = pointRow.Select(p => p[1]).OrderDescending().ToArray();
                    Check(positions.Zip(positions.Skip(1)).All(p => Math.Abs(p.First - p.Second - 5.511867) < .002), "Constant point spacing for 10 and 15 points");
                }
            }
            if (row["talent_liste"].Contains("Arkane Hintergrund")) Check(hasMagic, "All arcane characters have power points");
        }
        var continuationRow = batch.Cards.First(row => int.TryParse(row["machtpunkte"], out int points) && points > 10)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        continuationRow["ausruestung_liste"] = "";
        continuationRow["macht_liste"] = "Macht A\nMacht B\nMacht C\nMacht D\nMacht E\nMacht F\nMacht G\nMacht H";
        for (int i = 2; i <= 9; i++) continuationRow[$"fertigkeit_{i:00}_name"] = "";
        var continuedBack = XDocument.Parse(back.BuildFilledSvg(continuationRow));
        Check(Field(continuedBack, "macht_liste").Value.Contains("Macht A")
            && Field(continuedBack, "macht_liste").Value.Contains("Macht F"), "First power-list field keeps the fitting prefix");
        Check(Field(continuedBack, "macht_liste_2").Value.Contains("Macht H"), "Overflowing power lists continue in the _2 field");
        var tooLongPowerList = continuationRow.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        tooLongPowerList["macht_liste"] = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"Macht {i}"));
        _ = XDocument.Parse(back.BuildFilledSvg(tooLongPowerList));
        int errorLengthBeforeTenPoints = layoutErrors.GetStringBuilder().Length;
        tooLongPowerList["machtpunkte"] = "10";
        var tenPointBack = XDocument.Parse(back.BuildFilledSvg(tooLongPowerList));
        Check(layoutErrors.GetStringBuilder().Length == errorLengthBeforeTenPoints
            && string.IsNullOrWhiteSpace(Field(tenPointBack, "macht_liste_2").Value),
            "Ten or fewer power points cannot trigger power-point overlap handling");
        }
        finally
        {
            Console.SetError(originalError);
        }
        string layoutErrorText = layoutErrors.ToString();
        Check(layoutErrorText.Contains("Fehler: Layoutwarnung bei '")
            && layoutErrorText.Contains("'macht_liste_2'")
            && layoutErrorText.Contains("'machtpunkte'"),
            "Overlapping power lists identify both potentially colliding fields without aborting rendering");
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
