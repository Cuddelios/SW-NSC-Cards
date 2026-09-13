Dieses Projekt erstellt mit Hilfe einer SVG-Vorlage und einer CSV-Tabelle eine mehrseitige PDF-Datei.
Es wird das Savage World Regelwerk (20210421) verwendet.

## GnM-Charakterkarten (24 Karten)

Mit `dotnet run --project nsc-cards-gen` die Konfiguration **GnM Charaktere (24 Karten)**
auswählen (derzeit Nummer 7). Sie verarbeitet alle 24 Datensätze aus
`data/GnM_Charaktere.csv` mit `Charakter_Vorlage_vorne.svg` und
`Charakter_Vorlage_rueck.svg`. Relative Bildpfade beziehen sich auf den CSV-Ordner;
die Porträts werden in die Ausgabe eingebettet.

- `output/GnM_Charaktere.pdf`: 48 Einzelseiten im Hochformat (76 × 126 mm), je Charakter
  zuerst vorne, dann hinten. Querformatvorlagen werden samt Inhalt um 90° im Uhrzeigersinn
  gedreht; Schriftgrößen und Proportionen bleiben erhalten.
- `output/GnM_Charaktere.h_mirror.pdf` und `.v_mirror.pdf`: je 12 A4-Querformatseiten
  mit vier Karten pro Seite und passend angeordneten Rückseiten für Duplexdruck.
- Die optionale MeinSpiel-Ausgabe enthält je 24 Vorder- bzw. Rückseiten ohne Titelkarte.

`expectedCardCount: 24` prüft die Anzahl auch ohne MeinSpiel-Export.
`singleCardPages: true` aktiviert zusätzlich das PDF mit einzelnen Kartenseiten.
Breite und Höhe aller Karten werden aus den SVG-Wurzelattributen übernommen
(hier **126 × 76 mm**, einschließlich des in der Vorlage angelegten Randes).
Unterstützt sind mm, cm, in, pt, pc und px; ohne Einheit gelten 96 px pro Zoll.
Fehlt eine Dimension, dient die entsprechende `viewBox`-Dimension als Pixelmaß.
Unterschiedlich große Vorder-, Rück- oder Titelvorlagen innerhalb einer Ausgabe
werden abgewiesen. Die A4-Bögen bleiben A4; Kartenbilder und Einzelseiten verwenden
die Vorlagengröße. Skia rundet die PDF-Seitenbox auf ganze PDF-Punkte.

Die Charaktervorlagen verwenden explizite `data-bind`-Bindungen:
`text`, `image`, `dice` (Auswahl über `data-value`), `skill-list` (neun feste
`data-fields`-Zeilen) und `counter` (Machtpunkte; leer oder 0 blendet sie aus).
Der Zähler wächst von links nach rechts mit unveränderter Symbolbreite und
unverändertem Abstand pro Machtpunkt. Die Würfelgrafiken bleiben unverändert.
`boxWidth` legt den Textumbruch in SVG-Koordinaten fest. Die Schriftgrößen aus
der Vorlage bleiben immer erhalten. Listen dürfen nach unten wachsen;
ein optionales `boxHeight` meldet bei Überschreitung einen Fehler, statt Text
zu verkleinern. Fertigkeiten behalten ihre festen Zeilen und Schriftgrößen.

Die Wurzelattribute `data-layout="character-front"` und
`data-layout="character-back"` aktivieren das Charakterlayout: Handicap- und
Talentlisten beginnen unter der Beschreibung; Rolle und Konzept rücken bei
Bedarf nach unten. Mehrzeilige Namen wachsen im Porträt nach oben. Die Mächte
beginnen fünf SVG-Einheiten unter der letzten belegten Fertigkeitszeile.
Patronen erscheinen nur mit der Fertigkeit `Schießen` (auch `Schiessen`).
Der Rang `Fortgeschritten` setzt den Hintergrund auf beiden Seiten auf `#ffeeaa`.
Mächtelisten und Machtpunkt-Flächen verwenden je nach arkanem Hintergrund
Violett (`#7B2CBF`, Magie), Goldbraun (`#8A5700`, Wunder), Petrol (`#006B73`,
Weird Science) oder Dunkelblau (`#2448A5`, Psionik); die Symbole bleiben weiß.
Bei langen Mächtelisten werden mehr als zehn Punkte auf zwei Reihen verteilt.
In der CSV sind für Jonah, Nyx, Rhea und Adrian jeweils zehn Punkte festgelegt,
für Rhea auf Stufe 7 fünfzehn. Vorhandene Punktzahlen bleiben erhalten.
Diese Bindungen verwenden `source`/`defaultValue` und die Boxmaße; die unten
beschriebenen klassischen Feldregeln gelten für Elemente ohne `data-bind`.

## Konfigurierbare Feldregeln

`fields` wird direkt beim jeweiligen Kartensatz in `data/card_configuration.json` hinterlegt, neben `data`, `template` und `backcard`. Bei kombinierten Ausgaben hat jeder Eintrag in `cardSets` seine eigenen Feldregeln. Bei Einzelkonfigurationen steht `fields` direkt im Eintrag unter `configurations`. So kann derselbe SVG-Feldname je Satz unterschiedlich interpretiert werden. Beispiel innerhalb eines Kartensatzes:

```json
{
  "countField": "anzahl",
  "fields": {
    "name_short": { "source": "Bezeichnung", "type": "text", "wrapLength": 22 },
    "edges": { "source": "Talente", "type": "list", "separators": ";", "lineHeight": 4, "offsetAfter": ["skills_text"] },
    "wc_wound": { "source": "Wildcard", "type": "visibility", "valueMap": { "ja": "true", "nein": "false" }, "defaultValue": "false" }
  }
}
```

| Eigenschaft | Bedeutung |
|---|---|
| `source` | CSV-Spalte; ohne Angabe gilt der SVG-Feldname |
| `aliases` | Alternative Spaltennamen, falls die primäre Spalte fehlt |
| `type` | `auto`, `text`, `list`, `skillLabels`, `skillDice`, `selection`, `visibility`, `fill` |
| `defaultValue` | Ersatz für eine fehlende Spalte; leere Zellen bleiben leer |
| `valueMap` | Zuordnung kompletter Zellwerte vor der Verarbeitung, unabhängig von Groß-/Kleinschreibung |
| `wrapLength` | Positive maximale Textzeilenlänge; Umbruch bevorzugt an Leerzeichen |
| `separators` | Einzelne Trennzeichen für Listen/Fertigkeiten; Standard: Komma und Zeilenumbrüche |
| `lineHeight` | Positive Zeilenhöhe in SVG-Einheiten |
| `lineHeightFrom` | Feld, dessen Zeilenhöhe für Würfelsymbole gilt |
| `offsetAfter` | Referenzierte Feldregeln: jede zusätzliche Textzeile verschiebt dieses Element nach unten |
| `offsetY` | Zusätzliche feste Verschiebung in SVG-Einheiten, auch negativ |

`skillLabels` und `skillDice` interpretieren Einträge wie `Kämpfen d8` (d4, d6, d8, d10, d12). Beide können dieselbe `source` verwenden. `selection` wählt ein direktes SVG-Kindelement anhand seines `data-field`, `visibility` schaltet die Sichtbarkeit und `fill` setzt die Füllfarbe. Die Symbolgrafiken bleiben in der SVG-Vorlage.

Nicht konfigurierte Felder verwenden die automatische SVG-Erkennung (Gruppenauswahl, Sichtbarkeit, Farbe oder Text). Die bisherigen namensabhängigen Sonderregeln stehen jetzt beim jeweiligen Satz in der JSON-Datei. Bei Aufruf mit CSV-/SVG-Pfadargumenten werden Feldregeln und `countField` des ersten Satzes mit passendem CSV- und Vorlagenpfad übernommen, sofern die Konfigurationsdatei im Arbeitsverzeichnis vorhanden ist. `countField` ist pro Kartensatz einstellbar, Standard: `count`.

Feldverarbeitung und alle mitgelieferten CSV-/SVG-Kombinationen prüfen:

```powershell
dotnet run --project tests/GeneratorChecks.csproj
```

## Interaktive Auswahl

### Mehrere Kartensätze in einer Ausgabe

Eine Konfiguration kann statt einzelner CSV-/Vorlagenangaben eine Liste `cardSets`
enthalten. Alle Sätze werden in Listenreihenfolge in gemeinsame PDFs geschrieben.
Jeder Satz hat eigene `data`, `template`, `backcard`, optional `titlecard`,
`countField` und `fields`. Die mitgelieferte Konfiguration definiert Feldregeln pro Satz.
Für bestehende Konfigurationen bleiben gemeinsame Regeln und Regeln auf Ausgabeebene
als Vorgaben unterstützt; die Regel im Kartensatz ersetzt eine gleichnamige Vorgabe vollständig.
`output` und `meinspielCardCount` stehen auf der äußeren Konfiguration.
Verschachtelte `cardSets` sind nicht erlaubt.

```json
{
  "name": "Loot und Barrieren kombiniert",
  "output": "loot_barrier_cards",
  "meinspielCardCount": 110,
  "cardSets": [
    {
      "name": "Loot",
      "data": "loot_settings.csv",
      "template": "loot_template.svg",
      "backcard": "npc_card_back_diamonds.svg",
      "titlecard": "title_loot.svg",
      "fields": {
        "name_short": { "type": "text", "wrapLength": 16 },
        "description": { "type": "text", "wrapLength": 29, "offsetY": 0.5 }
      }
    },
    {
      "name": "Barrieren",
      "data": "barrier_settings.csv",
      "template": "barriers_template.svg",
      "backcard": "npc_card_back_clubs.svg",
      "titlecard": "title_barriers.svg",
      "fields": {
        "name_short": { "type": "text", "wrapLength": 16 },
        "fail_text": { "type": "text", "wrapLength": 20 }
      }
    }
  ]
}
```

Dieses Beispiel ist bereits in der Auswahl enthalten: 54 Loot-Karten plus
54 Barrieren plus zwei Titelkarten ergeben genau 110 Karten im MeinSpiel-Export.
Die Vorderseiten stehen gemeinsam in `output/loot_barrier_cards.meinspiel-front.pdf`,
die zugehörigen Rückseiten in `output/loot_barrier_cards.meinspiel-back.pdf`.
Auch die beiden A4-Duplexvarianten enthalten die zusammengefassten Sätze.

Bei kombinierten MeinSpiel-Exporten ist die positive ganze Zahl
`meinspielCardCount` erforderlich. Sie zählt CSV-Kopien gemäß `countField`
(Standard `count`) **einschließlich einer Titelkarte pro Satz mit `titlecard`**.
Eine Rückseite zählt nicht als zusätzliche Karte. Titelkarten stehen unmittelbar
vor ihrem Satz und sind wie bisher nur im MeinSpiel-Export enthalten.
Ein `count` von 0 überspringt die betreffende Datenkarte; die Titelkarte bleibt enthalten.

Bei Abweichungen bricht das Programm mit Exitcode 1 sowie Soll-/Istzahl und
Aufschlüsselung je Satz ab. Alle ausgewählten Konfigurationen werden vor dem
ersten Schreiben geprüft; vorhandene PDFs bleiben bei einem Zählfehler unberührt.
Bei reiner A4-Ausgabe wird die Gleichheit mit der MeinSpiel-Sollzahl nicht verlangt.
Bestehende Einzelkonfigurationen funktionieren weiterhin; auch dort kann
`meinspielCardCount` optional gesetzt werden.

Ohne Parameter fragt der Generator die Karten-Konfiguration aus `data/card_configuration.json` ab. Neben einzelnen Einträgen kann mit `A` auch **Alle Konfigurationen** gewählt werden; jede wird dann einmal vollständig erzeugt.

Der Name der PDF-Ausgabe orientiert sich immer am Namen der gewaehlten CSV-Datei:

- Daten: `data/GnM Gegner - npc_template.csv`
- Standard-Ausgabe: `output/GnM Gegner - npc_template.pdf`
- MeinSpiel Front-PDF: `output/GnM Gegner - npc_template.meinspiel-front.pdf`
- MeinSpiel Back-PDF: `output/GnM Gegner - npc_template.meinspiel-back.pdf`

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

Optional koennen CSV, Vorderseiten-Template und Rueckseiten-Template als Parameter uebergeben werden. Der Ausgabename wird weiterhin aus der CSV-Datei gebildet:

```powershell
dotnet run --project nsc-cards-gen -- data/char_template_example.csv templates/char_template.svg templates/npc_card_back_clubs.svg
```

Zusaetzlich zur normalen A4-Ausgabe erzeugt der Generator automatisch MeinSpiel-kompatible Fronten- und Rueckseiten-PDFs. In der normalen A4-Ausgabe wechseln sich Vorderseiten- und Rueckseiten-Seiten ab.

- Standard-Ausgabe: `output/<datenname>.pdf`
- MeinSpiel Front-PDF: `output/<datenname>.meinspiel-front.pdf`
- MeinSpiel Back-PDF: `output/<datenname>.meinspiel-back.pdf`

Das MeinSpiel-PDF ist für Spielkarten im Format `59 x 91 mm` aufbereitet und folgt der Dokumentgröße `65 x 97 mm` mit 3 mm Beschnitt, also:

- 1 Karte pro PDF-Seite
- Seitengröße `65 x 97 mm`
- 300 DPI Rasterung
- ohne Druckmarken

Hinweis: Für den Upload bei MeinSpiel werden in der Regel zwei Dateien benötigt:

- ein PDF mit allen Vorderseiten
- ein PDF mit allen Rückseiten

Die Rueckseiten kommen aus `templates/npc_card_back_*.svg`. Pro Deck wird beim Start genau eine Rueckseiten-Vorlage ausgewaehlt und fuer alle Karten verwendet.

Beim Duplexdruck der normalen A4-Ausgabe im Querformat sollte der Drucker ueber die kurze Kante wenden. Die Rueckseiten-Seiten sind dafuer horizontal gespiegelt angeordnet.

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
