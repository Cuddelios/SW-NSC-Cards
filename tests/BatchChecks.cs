using SvgPdfGenerator;
using SkiaSharp;

static class BatchChecks
{
    public static void Run()
    {
        var files = new List<string>();
        string Temp(string content)
        {
            string path = Path.GetTempFileName();
            files.Add(path);
            File.WriteAllText(path, content);
            return path;
        }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        try
        {
            string Svg(string color) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"{color}\"/></svg>";
            string red = Temp(Svg("red")), blue = Temp(Svg("blue")), green = Temp(Svg("lime"));
            var config = new CardConfiguration
            {
                Name = "Combined", MeinspielCardCount = 4,
                CardSets = [
                    new() { Name = "First", Data = Temp("name,count\nA,2\nSkipped,0\n"), Template = red, Backcard = blue, Titlecard = green },
                    new() { Name = "Second", Data = Temp("name;copies\nB;1\n"), CountField = "copies", Template = blue, Backcard = red }
                ]
            };
            var batch = CardBatch.Prepare(config, false, true);
            Check(batch.Cards.Count == 3 && batch.MeinspielCards.Count == 4, "Copies and title count");
            Check(batch.MeinspielCards.Select(r => r["name"]).SequenceEqual(new[] { "Titelkarte", "A", "A", "B" }), "Combined order");
            SKColor Color(byte[] png) { using var bitmap = SKBitmap.Decode(png); return bitmap.GetPixel(5, 5); }
            Check(Color(batch.RenderFront(batch.MeinspielCards[0], 10, 10)) == SKColors.Lime, "Title renderer");
            Check(Color(batch.RenderFront(batch.Cards[0], 10, 10)) == SKColors.Red, "First front");
            Check(Color(batch.RenderBack(batch.Cards[0], 10, 10)) == SKColors.Blue, "First back");
            Check(Color(batch.RenderFront(batch.Cards[2], 10, 10)) == SKColors.Blue, "Second front");
            Check(Color(batch.RenderBack(batch.Cards[2], 10, 10)) == SKColors.Red, "Second back");
            string sharedTemplate = Temp(Svg("red").Replace("<rect ", "<rect data-field=\"tint\" "));
            var independent = new CardConfiguration
            {
                CardSets = [
                    new() { Data = config.CardSets[1].Data, Template = sharedTemplate, Backcard = red,
                        Fields = new() { ["tint"] = new() { Type = "fill", DefaultValue = "blue" } } },
                    new() { Data = config.CardSets[1].Data, Template = sharedTemplate, Backcard = red,
                        Fields = new() { ["tint"] = new() { Type = "fill", DefaultValue = "lime" } } }
                ]
            };
            var independentBatch = CardBatch.Prepare(independent, false, false);
            Check(Color(independentBatch.RenderFront(independentBatch.Cards[0], 10, 10)) == SKColors.Blue
                && Color(independentBatch.RenderFront(independentBatch.Cards[1], 10, 10)) == SKColors.Lime,
                "Field definitions must remain independent per set with the same template");
            foreach (int? expected in new int?[] { 3, 5, 0, null })
            {
                config.MeinspielCardCount = expected;
                bool rejected = false;
                try { CardBatch.Prepare(config, false, true); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, $"Invalid target {expected} accepted");
            }
            config.MeinspielCardCount = 99;
            Check(CardBatch.Prepare(config, false, false).Cards.Count == 3, "A4 should not require MeinSpiel target equality");
            Check(CardBatch.Prepare(config.CardSets[0], false, true).MeinspielCards.Count == 3, "Legacy single set");
            config.MeinspielCardCount = 4;
            string pdfPath = Temp("");
            var options = new SvgPdfGenerator.Models.PdfLayoutOptions { CardWidthPt = 10, CardHeightPt = 10, PageWidthPt = 10, PageHeightPt = 10, MarginPt = 0, GapXPt = 0, GapYPt = 0, RenderDpi = 72 };
            new PdfLayoutWriter().WriteCards(pdfPath, batch.MeinspielCards, batch.RenderFront, options);
            string pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
            Check(System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type\s*/Page\b").Count == 4, "Combined PDF page count");
            // Run the real CLI with a valid first job and invalid second job: neither may write a PDF.
            string work = Path.Combine(Path.GetTempPath(), "card-batch-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(work, "data"));
            string configurationPath = Path.Combine(work, "data", "card_configuration.json");
            try
            {
                config.Output = "invalid";
                config.MeinspielCardCount = 5;
                var single = config.CardSets[0];
                single.Output = "valid";
                File.WriteAllText(configurationPath, System.Text.Json.JsonSerializer.Serialize(new { configurations = new[] { single, config } }));
                var start = new System.Diagnostics.ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = work, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
                };
                start.ArgumentList.Add("exec");
                start.ArgumentList.Add("--runtimeconfig");
                start.ArgumentList.Add(Path.ChangeExtension(typeof(BatchChecks).Assembly.Location, ".runtimeconfig.json"));
                start.ArgumentList.Add(typeof(CardBatch).Assembly.Location);
                using var process = System.Diagnostics.Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.StandardInput.WriteLine("a");
                process.StandardInput.WriteLine("n");
                process.StandardInput.WriteLine("j");
                process.StandardInput.Close();
                if (!process.WaitForExit(20000)) { process.Kill(true); throw new Exception("CLI preflight timed out"); }
                Check(process.ExitCode == 1 && stderr.Result.Contains("erwartet 5,") && stderr.Result.Contains("First: 3, Second: 1"), "CLI count error: " + stderr.Result + stdout.Result);
                Check(!Directory.Exists(Path.Combine(work, "output")), "CLI wrote output before validating all jobs");
            }
            finally
            {
                File.Delete(configurationPath);
                Directory.Delete(Path.Combine(work, "data"));
                Directory.Delete(work);
            }
            config.MeinspielCardCount = 4;
            File.WriteAllText(config.CardSets[1].Data, "name;copies\nB;-1\n");
            bool negativeRejected = false;
            try { CardBatch.Prepare(config, false, true); }
            catch (InvalidOperationException) { negativeRejected = true; }
            Check(negativeRejected, "Negative copies accepted");
            Console.WriteLine("All batch checks passed.");
        }
        finally { foreach (string file in files) File.Delete(file); }
    }
}
