Dieses Projekt erstellt mit Hilfe einer SVG-Vorlage und einer CSV-Tabelle eine mehrseitige PDF-Datei.

## Aktuelle Standardpfade

Ohne Parameter verwendet der Generator jetzt:

- CSV: `data/test.csv`
- SVG-Template: `templates/char_template.svg`
- PDF-Ausgabe: `output/output.pdf`

Starten:

```powershell
dotnet run --project nsc-cards-gen
```

Optional können CSV, Template und Ausgabedatei als Parameter übergeben werden:

```powershell
dotnet run --project nsc-cards-gen -- data/test.csv templates/char_template.svg output/output.pdf
```

Für `char_template.svg` berücksichtigt der Code zusätzlich:

- Textfelder über `data-field`
- Farbwerte für SVG-Elemente wie Kartenhintergrund und Level-Kreis
- Sichtbarkeit von Gruppen, z. B. `wc_wound`
- Würfelgruppen wie `agility_dices`, `skills_dices` usw. über Werte wie `d4`, `d6`, `d8`, `d10`, `d12`
- den Karten-Ausschnitt `0 0 64 96`, damit nicht die komplette A4-SVG, sondern die eigentliche Karte gerendert wird

## Verwendete `data-field`-Felder im `char_template.svg`

Die folgende Liste beschreibt alle `data-field`-Werte, die aktuell in [char_template.svg](./templates/char_template.svg) verwendet werden.

## Einfache Felder

| Feld | `data-field` | Typ | Beschreibung |
|---|---|---|---|
| Kartenhintergrund | `card_back_color` | Farbe | Hintergrundfarbe der Karte. Laut Kommentar im SVG typischerweise aus dem Level abgeleitet. |
| Level-Kreis | `level_color` | Farbe | Füllfarbe des Kreises hinter der Level-Zahl. |
| Wundabzüge anzeigen | `wc_wound` | Boolean / Sichtbarkeit | Aktiviert die zusätzliche Wundabzugs-Gruppe `-2` und `-3`. |
| Fertigkeiten | `skills_text` | Text | Fließtext oder Liste der Fertigkeiten. |
| Beschreibung | `description` | Text | Beschreibung des Charakters. |
| Talente | `edges` | Text | Talente / Edges des Charakters. |
| Parade | `parry` | Zahl | Anzeigewert für Parade. |
| Robustheit | `toughness` | Zahl oder kombinierter Text | Hauptwert für Robustheit. Das SVG enthält zusätzlich einen zweiten `tspan`, daher sind Formate wie `8` oder `8 (1)` denkbar, abhängig von eurer Ersetzungslogik. |
| Level | `level` | Zahl / kurzer Text | Sichtbarer Level-Wert im farbigen Kreis. |
| Name | `name` | Text | Name des Charakters. |

## Würfelgruppen

Diese Felder referenzieren jeweils eine Gruppe mit mehreren Untergruppen. Innerhalb der Gruppe gibt es die möglichen Würfel-Untergruppen:

- `d4`
- `d6`
- `d8`
- `d10`
- `d12`

Die Logik ist dabei:

1. Der Wert des äußeren Feldes bestimmt, welche Untergruppe aktiviert wird.
2. Nur die passende Untergruppe innerhalb der jeweiligen `dices`-Gruppe soll sichtbar sein.
3. Alle anderen Untergruppen bleiben verborgen.

Beispiel:

- Wert `d4` aktiviert die Untergruppe `d4`
- Wert `d8` aktiviert die Untergruppe `d8`
- Wert `d12` aktiviert die Untergruppe `d12`

## Attribute und Würfelgruppen

| Feld | `data-field` | Typ | Aktiviert Untergruppe in der Gruppe |
|---|---|---|---|
| Geschicklichkeit | `agility_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |
| Konstitution | `vigor_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |
| Stärke | `st_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |
| Verstand | `smarts_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |
| Willenskraft | `spirit_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |
| Fertigkeitswürfel | `skills_dices` | Würfelgruppe | `d4`, `d6`, `d8`, `d10`, `d12` |

## Hinweis zur Besonderheit der `dices`-Gruppen

Die `dices`-Felder sind keine einfachen Textfelder. Stattdessen zeigt der Wert an, welche Untergruppe innerhalb der SVG-Gruppe sichtbar werden soll.

Empfohlene Eingabewerte sind daher genau:

- `d4`
- `d6`
- `d8`
- `d10`
- `d12`

Wenn also zum Beispiel in den Quelldaten für `agility_dices` der Wert `d8` gesetzt ist, dann sollte im Template nur die Untergruppe `data-field="d8"` innerhalb von `data-field="agility_dices"` aktiviert werden.
