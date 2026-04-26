# Spielkartenwert

Jede Spielkarte hat einen Wert, welcher sich oben rechts neben dem Namensfeld befindet.

Die Farbe der Spielkarte richtet sich nach diesem Wert.

## Berechnung

Der Wert wird aktuell mit der nachfollgenden Formel bestimmt:

$$
ROUND \left( \left( \frac{\sum Eigenschaften}{2}-10 + Fertigkeitengewichtung + Talentegewichtung + Mächtegewichtung + Ausrüstunggewichtung + (Parade - 2) + (Robustheit - 2) + Rüstung \right) * \frac{1 + wildcard * 0,5}{10} \right)
$$

Alternative:

$$
ROUND \left( \left( \frac{\sum Eigenschaften}{2} + Fertigkeitengewichtung + Talentegewichtung + Mächtegewichtung + Ausrüstunggewichtung + Parade + Robustheit + Rüstung - Offset \right) * \frac{1 + wildcard * 0,5}{Divisor} \right)
$$

mit $Offset = 20$ und $Divisor=5,5$ erreicht man Werte zwischen 1 und 10 für die Spielkarten.

## Im Spiel

### Bildkarte

Beim Betreten einer Spielkarte wird zuerst ermittelt, wie viele Karten gezogen werden müssen.

$Spielkartenwert+ \sum Spielerlevel$

Würfeln mit dem W4: Ergibt die Anzahl der Wildcard-Gegner.
Anschliessend werden die restlichen Gegner von dem andern Stapel gezogen, bis oben ermittelte Wert erreicht oder überschritten wurde.

### Zahlenkarten

Bei mehreren aufeinander folgenden Zahlenkarten, wird der Wert der Karten summiert und dan von dem Gegnerstapel so lange gezogen, bis der Wert erreicht oder überschritten wurde.

