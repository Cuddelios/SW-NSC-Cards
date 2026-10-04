using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SvgPdfGenerator;

public sealed partial class SvgCardRenderer
{
    private void ApplyCharacterLayout(
        XElement root,
        XElement templateRoot,
        IReadOnlyDictionary<string, string> values)
    {
        string? layout = (string?)root.Attribute("data-layout");
        if (layout is not ("character-front" or "character-back")) return;
        XElement Field(string name) => root.Descendants().Single(e => (string?)e.Attribute("data-field") == name);
        XElement TemplateField(string name) => templateRoot.Descendants().Single(e => (string?)e.Attribute("data-field") == name);
        XElement? OptionalField(string name) => root.Descendants().FirstOrDefault(e => (string?)e.Attribute("data-field") == name);
        XElement? OptionalTemplateField(string name) => templateRoot.Descendants().FirstOrDefault(e => (string?)e.Attribute("data-field") == name);
        double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);
        double FontSize(XElement e) => double.Parse(Regex.Match((string?)e.Attribute("style") ?? "", @"font-size:([\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        double Bottom(XElement e) => Number(e, "y") + Math.Max(0, e.Elements(SvgNs + "tspan").Count() - 1) * FontSize(e) * 1.25;
        void Move(XElement e, double y, double? x = null)
        {
            e.SetAttributeValue("y", FormatNumber(y));
            if (x.HasValue)
            {
                e.SetAttributeValue("x", FormatNumber(x.Value));
                foreach (var span in e.Elements(SvgNs + "tspan")) span.SetAttributeValue("x", FormatNumber(x.Value));
            }
        }
        if (string.Equals(values.GetValueOrDefault("rang"), "Fortgeschritten", StringComparison.OrdinalIgnoreCase))
        {
            var background = root.Descendants().Single(e => (string?)e.Attribute("id") == "rect24");
            background.SetAttributeValue("style", Regex.Replace((string?)background.Attribute("style") ?? "", @"(?<![-\w])fill:[^;]+", "fill:#ffeeaa"));
        }
        if (layout == "character-front")
        {
            var name = Field("name");
            Move(name, Number(name, "y") - (Bottom(name) - Number(name, "y")));
            double listY = Math.Max(
                Math.Max(Number(TemplateField("handicap_liste"), "y"), Number(TemplateField("talent_liste"), "y")),
                Bottom(Field("beschreibung")) + 5);
            Move(Field("handicap_liste"), listY);
            Move(Field("talent_liste"), listY);
            double roleY = Math.Max(
                Number(TemplateField("rolle"), "y"),
                Math.Max(Bottom(Field("handicap_liste")), Bottom(Field("talent_liste"))) + 5);
            Move(Field("rolle"), roleY);
            double conceptGap = Number(TemplateField("konzept"), "y") - Number(TemplateField("rolle"), "y");
            Move(Field("konzept"), Bottom(Field("rolle")) + conceptGap);
        }
        else
        {
            bool shooting = values.Any(p => Regex.IsMatch(p.Key, @"^fertigkeit_\d+_name$")
                && string.Equals(p.Value.Trim().Replace("ß", "ss"), "Schiessen", StringComparison.OrdinalIgnoreCase));
            foreach (var ammo in root.Descendants().Where(e => ((string?)e.Attribute("data-template-role"))?.StartsWith("ammo") == true))
                if (!shooting) SetDisplay(ammo, false);
            var skills = root.Descendants().Single(e => (string?)e.Attribute("data-bind") == "skill-list");
            double lastSkillY = skills.Elements(SvgNs + "tspan").Where(e => !string.IsNullOrWhiteSpace(e.Value))
                .Select(e => Number(e, "y")).DefaultIfEmpty(Number(skills, "y")).Max();
            var powers = Field("macht_liste");
            var templatePowers = TemplateField("macht_liste");
            var equipment = Field("ausruestung_liste");
            double PowerListGap(XElement element) => FontSize(element) * 1.25 * 1.5;
            Move(powers, Bottom(equipment) + PowerListGap(powers));

            var powers2 = OptionalField("macht_liste_2");
            var templatePowers2 = OptionalTemplateField("macht_liste_2");
            if (powers2 != null && templatePowers2 != null)
            {
                Move(powers2, lastSkillY + PowerListGap(powers2));
            }

            var magic = Field("machtpunkte");
            double ReservedBottom(XElement templateElement)
            {
                var explicitLineYs = templateElement.Elements(SvgNs + "tspan")
                    .Where(e => e.Attribute("y") != null).Select(e => Number(e, "y")).ToList();
                return explicitLineYs.Count > 0 ? explicitLineYs.Max() : Bottom(templateElement);
            }
            double reservedPowersBottom = ReservedBottom(templatePowers);
            const double coordinateTolerance = .001;
            bool powerPointsCanOverlap = int.TryParse(values.GetValueOrDefault("machtpunkte"), out int powerPointCount)
                && powerPointCount > 10;
            void ReportPotentialOverlap(string field, double actualBottom, double reservedBottom, string? detail = null)
            {
                string card = values.GetValueOrDefault("name") ?? values.GetValueOrDefault("id") ?? "unbekannte Karte";
                Console.Error.WriteLine(
                    $"Fehler: Layoutwarnung bei '{card}': SVG-Feld '{field}' überschreitet seinen Vorlagenbereich " +
                    $"(Unterkante y={FormatNumber(actualBottom)}, Grenze y={FormatNumber(reservedBottom)}). " +
                    $"Mögliche Überschneidung mit SVG-Feld 'machtpunkte'.{(detail == null ? "" : " " + detail)} Die PDF-Erstellung wird fortgesetzt.");
            }
            if (Bottom(Field("macht_liste")) > reservedPowersBottom + coordinateTolerance && powerPointsCanOverlap)
            {
                if (powers2 == null || templatePowers2 == null)
                {
                    ReportPotentialOverlap("macht_liste", Bottom(powers), reservedPowersBottom,
                        "Das Fortsetzungsfeld 'macht_liste_2' fehlt.");
                }
                else
                {
                    string[] entries = (values.GetValueOrDefault("macht_liste") ?? "")
                        .Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    fields.TryGetValue("macht_liste", out var powerRule);
                    fields.TryGetValue("macht_liste_2", out var powerRule2);
                    double? firstWidth = powerRule?.BoxWidth;
                    double? secondWidth = powerRule2?.BoxWidth ?? firstWidth;
                    void FillPowerList(XElement element, IEnumerable<string> items, double? width)
                    {
                        string text = string.Join('\n', items);
                        if (width.HasValue) FitBoundText(element, text, width.Value, null);
                        else element.Value = text;
                    }

                    int firstCount = entries.Length - 1;
                    for (; firstCount >= 0; firstCount--)
                    {
                        FillPowerList(powers, entries.Take(firstCount), firstWidth);
                        if (Bottom(powers) <= reservedPowersBottom + coordinateTolerance) break;
                    }
                    if (firstCount < 0)
                    {
                        FillPowerList(powers, entries, firstWidth);
                        ReportPotentialOverlap("macht_liste", Bottom(powers), reservedPowersBottom,
                            $"SVG-Feld 'ausruestung_liste' reicht bis y={FormatNumber(Bottom(equipment))} und lässt keinen Platz für die Mächteliste.");
                    }
                    else
                    {
                        FillPowerList(powers2, entries.Skip(firstCount), secondWidth);
                        double reservedPowers2Bottom = ReservedBottom(templatePowers2);
                        if (Bottom(powers2) > reservedPowers2Bottom + coordinateTolerance)
                            ReportPotentialOverlap("macht_liste_2", Bottom(powers2), reservedPowers2Bottom,
                                "Beide Mächtelisten zusammen reichen für den Inhalt nicht aus.");
                    }
                }
            }
            var backgroundType = Regex.Match(values.GetValueOrDefault("talent_liste") ?? "",
                @"Arkane[rms]? Hintergrund\s*\(([^)]+)\)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            string? arcaneColor = backgroundType.ToLowerInvariant() switch
            {
                "magie" => "#7B2CBF",
                "wunder" => "#8A5700",
                "weird science" => "#006B73",
                "psionik" => "#2448A5",
                _ => null
            };
            if (arcaneColor != null)
            {
                void Color(XElement element, string color) => element.SetAttributeValue("style",
                    Regex.Replace((string?)element.Attribute("style") ?? "", @"(?<![-\w])fill:[^;]+", "fill:" + color));
                Color(Field("macht_liste"), arcaneColor);
                if (powers2 != null) Color(powers2, arcaneColor);
                foreach (var rectangle in magic.Descendants(SvgNs + "rect")) Color(rectangle, arcaneColor);
                foreach (var symbol in magic.Descendants(SvgNs + "path")) Color(symbol, "#ffffff");
            }
        }
    }
}
