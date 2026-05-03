Dieses Projekt erstellt mit Hilfe einer SVG-Vorlage und einer CSV-Tabelle eine mehrseitige PDF-Datei.
Es wird das Savage World Regelwerk (20210421) verwendet.

## Interaktive Auswahl

Ohne Parameter fragt der Generator jetzt in der Kommandozeile zuerst die CSV-Datei aus `data/` und danach die SVG-Vorlage aus `templates/` ab.

Der Name der PDF-Ausgabe orientiert sich immer am Namen der gewaehlten CSV-Datei:

- Daten: `data/GnM Gegner - npc_template.csv`
- Standard-Ausgabe: `output/GnM Gegner - npc_template.pdf`
- MeinSpiel Front-PDF: `output/GnM Gegner - npc_template.meinspiel-front.pdf`

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

Optional koennen CSV und Template als Parameter uebergeben werden. Der Ausgabename wird weiterhin aus der CSV-Datei gebildet:

```powershell
dotnet run --project nsc-cards-gen -- data/char_template_example.csv templates/char_template.svg
```

Zusätzlich zur normalen A4-Ausgabe erzeugt der Generator automatisch ein MeinSpiel-kompatibles Fronten-PDF:

- Standard-Ausgabe: `output/<datenname>.pdf`
- MeinSpiel Front-PDF: `output/<datenname>.meinspiel-front.pdf`

Das MeinSpiel-PDF ist für Spielkarten im Format `59 x 91 mm` aufbereitet und folgt der Dokumentgröße `65 x 97 mm` mit 3 mm Beschnitt, also:

- 1 Karte pro PDF-Seite
- Seitengröße `65 x 97 mm`
- 300 DPI Rasterung
- ohne Druckmarken

Hinweis: Für den Upload bei MeinSpiel werden in der Regel zwei Dateien benötigt:

- ein PDF mit allen Vorderseiten
- ein PDF mit allen Rückseiten

Aktuell erzeugt das Projekt automatisch das Fronten-PDF. Falls du auch die Rückseiten automatisiert generieren möchtest, braucht das Projekt dafür zusätzlich eine Rückseiten-Vorlage.

Für `npc_template_enemy.svg` berücksichtigt der Code zusätzlich:

- Textfelder über `data-field`
- Farbwerte für SVG-Elemente wie Kartenhintergrund und Level-Kreis
- Sichtbarkeit von Gruppen, z. B. `wc_wound`
- Würfelgruppen wie `agility_dices`, `skills_dices` usw. über Werte wie `d4`, `d6`, `d8`, `d10`, `d12`
- den Karten-Ausschnitt `0 0 64 96`, damit nicht die komplette A4-SVG, sondern die eigentliche Karte gerendert wird

## Aktuell verwendete Felder im `npc_template_enemy.svg`

Die folgende Liste beschreibt die CSV-Spalten aus [npc_template_enemies.csv](./data/npc_template_enemies.csv) und die dazu passenden `data-field`-Werte in [npc_template_enemy.svg](./templates/npc_template_enemy.svg).

## Aktuelle CSV-Spalten

| CSV-Spalte | Inhalt | Rendering |
|---|---|---|
| `card_back_color` | Hex-Farbe fuer den Kartenhintergrund, z. B. `#f6b26b`. | Fuellt das SVG-Element `data-field="card_back_color"`. |
| `level_color` | Hex-Farbe fuer die Level-Markierung. | Fuellt das SVG-Element `data-field="level_color"`. |
| `wc_wound` | Boolean-Wert wie `TRUE` oder `FALSE`. | Blendet die Wildcard-Wundabzuege ein oder aus. |
| `skills_text` | Komma-getrennte Fertigkeiten, optional mit Wuerfelwert, z. B. `Kaempfen d10, Athletik d8`. | Wird in Zeilen zerlegt; Wuerfelwerte werden als Icons ueber `skills_dices` gesetzt. |
| `description` | Kurzer Beschreibungstext fuer Verhalten, Taktik oder Besonderheit. | Wird auf kurze Zeilen umgebrochen. |
| `edges` | Komma-getrennte Talente, Aktionen oder Sonderregeln. | Wird unter den Fertigkeiten platziert. |
| `weapons` | Komma-getrennte Waffen oder Angriffe. | Datenfeld ist vorhanden; wird nur sichtbar, wenn die Vorlage ein `data-field="weapons"` enthaelt. |
| `parry` | Parade-Wert. | Textfeld `data-field="parry"`. |
| `toughness` | Robustheit-Wert. | Textfeld `data-field="toughness"`. |
| `armor` | Ruestungsbonus, z. B. `+2`. | Textfeld `data-field="armor"`. |
| `level` | Level oder Rangwert; ein- oder zweistellig moeglich. | Zentriertes Textfeld `data-field="level"`. |
| `name` | Name des Gegners. | Textfeld `data-field="name"`. |
| `type` | Typus / Kategorie als Text, z. B. `Halbork`. | Textfeld `data-field="type"`. |
| `type_image` | Auswahlwert fuer die Silhouette. | Aktiviert eine Untergruppe in `data-field="type_image"`. |
| `agility_dices` | Geschicklichkeits-Wuerfel: `d4`, `d6`, `d8`, `d10` oder `d12`. | Aktiviert die passende Wuerfel-Untergruppe. |
| `vigor_dices` | Konstitutions-Wuerfel: `d4`, `d6`, `d8`, `d10` oder `d12`. | Aktiviert die passende Wuerfel-Untergruppe. |
| `st_dices` | Staerke-Wuerfel: `d4`, `d6`, `d8`, `d10` oder `d12`. | Aktiviert die passende Wuerfel-Untergruppe. |
| `smarts_dices` | Verstands-Wuerfel: `d4`, `d6`, `d8`, `d10` oder `d12`. | Aktiviert die passende Wuerfel-Untergruppe. |
| `spirit_dices` | Willenskraft-Wuerfel: `d4`, `d6`, `d8`, `d10` oder `d12`. | Aktiviert die passende Wuerfel-Untergruppe. |
| `count` | Anzahl der zu erzeugenden Kopien. Leer bedeutet `1`. | Kein SVG-Feld; wird vor dem Rendern ausgewertet. |
| `speed` | Bewegungswert; ein- oder zweistellig moeglich. | Zentriertes Textfeld `data-field="speed"`. |
| `consumption` | Auswahlwert fuer Verbrauch / Ressource. | Aktiviert eine Untergruppe in `data-field="consumption"`. |

## Auswahlwerte

| Gruppe | Erlaubte Werte in der CSV |
|---|---|
| Wuerfelgruppen | `d4`, `d6`, `d8`, `d10`, `d12` |
| `consumption` | `ammo`, `ammo_2`, `ammo_4`, `ammo_8`, `magic`, `magic_ammo` |
| `type_image` | `Assasine`, `Bandit`, `Chimäre`, `Goblin`, `Golem`, `Halb-Goblin`, `Halbork`, `Hund`, `Irrwicht`, `Kobold`, `Kultist`, `Magier`, `Militz`, `Mönch`, `Pixie`, `Rabe`, `Söldner`, `Schmuggler`, `Titan`, `Wolf` |

## Einfache Felder

Die vollstaendige aktuelle CSV-Liste steht oben. Die folgenden Tabellen beschreiben die technische `data-field`-Behandlung des Renderers.

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
