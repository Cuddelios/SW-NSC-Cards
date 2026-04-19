Dieses Projekt erstellt mit Hilfe einer SVG-Vorlage und einer CSV-Tabelle eine mehrseitige PDF-Datei.
Es wird das Savage World Regelwerk (20210421) verwendet.

## Aktuelle Standardpfade

Ohne Parameter verwendet der Generator jetzt:

- CSV: `data/test.csv`
- SVG-Template: `templates/char_template.svg`
- PDF-Ausgabe: `output/output.pdf`

Eine vollständige Beispiel-CSV für das Character-Template liegt unter `data/char_template_example.csv`.

## Beispiel-Daten als Tabelle

| Name | Level | Parade | Robustheit | Wunden | Fertigkeiten | Beschreibung | Talente | Kartenfarbe | Level-Farbe | Geschick | Konstitution | Stärke | Verstand | Willenskraft | Fertigkeitswürfel |
|---|---:|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Roderick Stahl | 3 | 7 | `8,1` | `true` | Kämpfen d10, Athletik d8, Heimlichkeit d6 | Erfahrener Söldner und Fährtensucher aus den Grenzlanden. | Beidhändig, Kräftig | `#d9c27a` | `#b8860b` | `d8` | `d8` | `d10` | `d6` | `d6` | `d10` |
| Elena Voss | 2 | 6 | `7,0` | `false` | Schießen d10, Wahrnehmung d8, Überreden d6 | Ehemalige Offizierin mit ruhiger Hand und scharfem Blick. | Scharfschütze, Kommandant | `#9fc5e8` | `#3d85c6` | `d6` | `d6` | `d6` | `d8` | `d8` | `d10` |
| Professor Aldwyn | 2 | 5 | `6,0` | `false` | Wissen d10, Heilen d8, Nachforschungen d8 | Arkanforscher mit einer Vorliebe für alte Ruinen und verbotene Texte. | Arkaner Hintergrund, Gelehrt | `#cfe2f3` | `#6fa8dc` | `d4` | `d6` | `d4` | `d10` | `d8` | `d8` |
| Mara Kestrel | 3 | 6 | `8,1` | `true` | Einschüchtern d8, Fahren d8, Reparieren d6 | Hartgesottener Schmuggler, der auf jede Gefahr eine schnelle Ausrede hat. | Glück, Zäh | `#d5a6bd` | `#a64d79` | `d8` | `d8` | `d6` | `d6` | `d8` | `d8` |
| Talia Dorn | 1 | 7 | `7,0` | `false` | Überleben d10, Wahrnehmung d10, Reiten d8 | Nomadische Späherin, immer auf der Suche nach der besten Route. | Waldläufer, Schnell | `#b6d7a8` | `#6aa84f` | `d8` | `d6` | `d6` | `d8` | `d8` | `d10` |

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
| Robustheit | `toughness` | Zahl oder kombinierter Text | Hauptwert für Robustheit. Das SVG enthält zusätzlich einen zweiten `tspan`, Das Fromat ist hier `8,9`. |
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

`skills_text` kann als komma-getrennte Liste gepflegt werden, zum Beispiel `Kämpfen d10, Athletik d8, Heimlichkeit d6`. Der Renderer trennt diese Einträge automatisch auf mehrere Zeilen auf, entfernt den Würfelwert aus dem Text und zeigt stattdessen rechts neben jeder Fertigkeit das passende `dices`-Element an.

Für Felder mit vorhandenen `tspan`-Einträgen, insbesondere `toughness`, bleibt die SVG-Struktur erhalten. Mehrteilige Werte werden dafür komma-getrennt übergeben, zum Beispiel `8,1`.

Empfohlene Eingabewerte sind daher genau:

- `d4`
- `d6`
- `d8`
- `d10`
- `d12`

Wenn also zum Beispiel in den Quelldaten für `agility_dices` der Wert `d8` gesetzt ist, dann sollte im Template nur die Untergruppe `data-field="d8"` innerhalb von `data-field="agility_dices"` aktiviert werden.
