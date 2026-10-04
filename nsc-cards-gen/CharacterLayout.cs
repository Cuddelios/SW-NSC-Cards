using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SvgPdfGenerator;

public sealed partial class SvgCardRenderer
{
    private static void ApplyCharacterLayout(XElement root, IReadOnlyDictionary<string, string> values)
    {
        string? layout = (string?)root.Attribute("data-layout");
        if (layout is not ("character-front" or "character-back")) return;
        XElement Field(string name) => root.Descendants().Single(e => (string?)e.Attribute("data-field") == name);
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
            double listY = Math.Max(36, Bottom(Field("beschreibung")) + 5);
            Move(Field("handicap_liste"), listY);
            Move(Field("talent_liste"), listY);
            double roleY = Math.Max(58.304237, Math.Max(Bottom(Field("handicap_liste")), Bottom(Field("talent_liste"))) + 5);
            Move(Field("rolle"), roleY);
            Move(Field("konzept"), Bottom(Field("rolle")) + 5);
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
            Move(Field("macht_liste"), lastSkillY + 5, 65);
            var magic = Field("machtpunkte");
            if (Bottom(Field("macht_liste")) > 66 && magic.Attribute("display")?.Value != "none")
            {
                var strip = magic.Elements().Single();
                var points = strip.Elements().ToList();
                // A second row preserves point widths while clearing long power lists.
                foreach (var point in points.Skip(10))
                {
                    var coordinates = Regex.Matches((string)point.Attribute("transform")!, @"-?\d+(?:\.\d+)?")
                        .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                    point.SetAttributeValue("transform", $"translate({FormatNumber(coordinates[0] - 6 / 1.0222987)} {FormatNumber(coordinates[1] + 10 * 5.511867)})");
                }
                // Draw the upper row first, so its stems cannot cover the lower symbols.
                strip.ReplaceNodes(points.Skip(10).Concat(points.Take(10)));
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
                foreach (var rectangle in magic.Descendants(SvgNs + "rect")) Color(rectangle, arcaneColor);
                foreach (var symbol in magic.Descendants(SvgNs + "path")) Color(symbol, "#ffffff");
            }
        }
    }
}
