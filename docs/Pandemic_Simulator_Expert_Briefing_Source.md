# Pandemic Simulator – Expert Briefing

Deutschsprachige Gesprächsgrundlage für ein Fachgespräch mit einem Epidemiologen

Projektstand: Auswertung der aktuellen Codebasis des Mods im Repository Pandamic_Simulator

Stichtag: 15.04.2026

Zweck dieses Dokuments: belastbare, technisch genaue und fachlich ehrliche Vorbereitung auf ein Expertengespräch. Das Dokument ist bewusst code-grounded. Es erklärt nicht nur, was die Benutzeroberfläche suggeriert, sondern was die Implementierung aktuell tatsächlich tut.

{{PAGEBREAK}}

## Executive Briefing

Der Pandemic Simulator ist kein klassisches mathematisches Kompartimentmodell auf Aggregatebene, sondern ein agentenbasiertes, stadtintegriertes Ausbruchssystem auf Basis von Cities: Skylines. Das Grundspiel liefert eine synthetische Stadt mit Bürgern, Gebäuden, Arbeitsorten, Schulen, Wegen, Fahrzeugen, Krankenhäusern, Ambulanzen, ÖPNV und Pathfinding. Die RealTime-Mod ergänzt darauf realistischere Tagesabläufe, Öffnungszeiten und Scheduling-Logik. Der Pandemic-Layer legt darüber eine Krankheitslogik mit Zuständen, Transmission, Testen, Tracing, Quarantäne, Lockdown, Visualisierung und Export.

Für das Gespräch ist die wichtigste fachliche Einordnung: Das System eignet sich als mechanistischer Szenario- und Interventionssimulator. Es ist gut darin, plausible Kontaktgelegenheiten entlang realer Stadtmobilität zu erzeugen und Maßnahmen als Veränderung von Kontaktgelegenheiten oder Übertragungswahrscheinlichkeiten sichtbar zu machen. Es ist nicht als kalibriertes Vorhersagemodell für reale Inzidenzen oder Mortalität zu verstehen.

Die zentrale Klasse ist `PandemicManager` in `src/RealTime/Pandemic/PandemicManager.cs`. Dort werden der Pandemie-Lifecycle, der Seed, die Krankheitszustände, die Transmission, die Quarantäne-Entscheidung, der Krankenhauspfad, der Public-Transport-Lockdown, der Observer, die X-Ray-Datenquellen und die Live-Snapshots orchestriert. Unterstützende Logik liegt in `MaskManager.cs`, `TestManager.cs`, `ContactManager.cs`, `QuarantineManager.cs` und `PandemicObserver.cs`.

Die epidemiologisch wichtigste Stärke des Systems ist die Kopplung an echte, im Spiel vorhandene Wege und Orte: Bürger haben ein Zuhause, fahren Bus oder Auto, gehen arbeiten, besuchen Gebäude und erzeugen dadurch setting-spezifische Transmission. Die epidemiologisch wichtigste Schwäche ist die Vereinfachung des Krankheitsverlaufs und der Erkennung: Es gibt kein explizites `Exposed`-Kompartiment, die Testlogik ist derzeit perfekt, die Wohnungslogik ist nur familienbasiert approximiert, und es existiert ein zusätzlicher Quarantäne-Fate-Mechanismus, der modelltheoretisch kritisch ist.

Wenn im Gespräch wenig Zeit ist, reichen diese vier Sätze:

- Das Modell ist agentenbasiert und an reale Spielmobilität gekoppelt, nicht an eine feste Kontaktmatrix.
- Transmission entsteht in drei Kontexten: draußen, Fahrzeuge und Gebäude; Haushalte werden über Familien approximiert.
- Interventionen verändern entweder Übertragungswahrscheinlichkeiten oder Kontaktgelegenheiten.
- Das Modell ist explorativ und policy-orientiert, aber noch nicht epidemiologisch validiert.

## 1. Systemarchitektur: Grundspiel, RealTime und Pandemie-Layer

Die sauberste Art, die Mod zu erklären, ist eine Dreiteilung.

| Ebene | Technische Rolle | Epidemiologische Bedeutung |
|---|---|---|
| Grundspiel Cities: Skylines | Liefert Bürger, Gebäude, Haushalte, Arbeitsorte, Schulen, Fahrzeuge, ÖPNV, Krankenhäuser, Ambulanzen, Pathfinding und Positionen in der Stadt. | Liefert die räumliche und infrastrukturelle Kontaktumgebung. |
| RealTime-Mod | Verändert Tagesrhythmus, Arbeits- und Freizeitzeiten, Öffnungszeiten, Scheduling, Travel-Time-Schätzung und Teile der Verhaltens-KI. | Verändert, wann und wie oft Kontakte überhaupt möglich werden. |
| Pandemic-Layer | Legt Krankheitszustände, Transmission, Testen, Tracing, Quarantäne, Lockdown, Analytics und Heatmaps darüber. | Führt die eigentliche epidemiologische Dynamik ein. |

### 1.1 Was das Grundspiel tatsächlich liefert

Cities: Skylines liefert keine epidemiologische Simulation, aber es liefert bereits ein synthetisches Agentensystem. Jeder Bürger hat mindestens:

- eine Identität im Citizen-Buffer
- in der Regel ein Wohngebäude
- optional Arbeit oder Schule
- einen aktuellen Ort und Location-Typ wie `Home`, `Work`, `Visit`, `Moving`
- eine aktuelle oder geplante Fortbewegung über Fahrzeuge und Pfade

Für die Pandemie ist genau das entscheidend. Die Mod erfindet die Stadtstruktur nicht künstlich, sondern liest sie aus dem Spielzustand aus. Gebäude haben Services und SubServices, Fahrzeuge haben AIs und Typen, Krankenhäuser und Depots existieren wirklich in der Stadt, und Public Transport nutzt die Originalsysteme des Spiels.

### 1.2 Was RealTime verändert

Der RealTime-Teil verschiebt das Spiel weg von der stark komprimierten Vanilla-Zeit hin zu einer detaillierteren Tageslogik. Relevant sind vor allem:

- `SimulationHandler` in `src/RealTime/Simulation/SimulationHandler.cs`
- `CitizenProcessor` in `src/RealTime/Simulation/CitizenProcessor.cs`
- `TravelBehavior` in `src/RealTime/CustomAI/TravelBehavior.cs`
- `RealTimeResidentAI` in `src/RealTime/CustomAI/RealTimeResidentAI.cs`

Diese Komponenten beeinflussen, wann Bürger aufstehen, arbeiten, essen, Freizeit haben, wann Gebäude offen sind und wie Reisezeiten in Terminentscheidungen einfließen. Epidemiologisch ist das wichtig, weil dadurch das Kontaktnetz nicht zufällig, sondern tageszeitlich strukturiert ist.

### 1.3 Was der Pandemie-Layer ergänzt

Der Pandemie-Layer ergänzt fünf Dinge:

- Krankheitszustände pro Bürger
- setting-spezifische Transmission
- Erkennungs- und Interventionslogik
- Beobachtung und Datenexport
- Policy-UI und Visualisierung

Die Primärklasse `PandemicManager` hält dafür u. a. folgende zentrale Zustände:

| Datenstruktur / Feld | Bedeutung |
|---|---|
| `activeInfections` | Map Bürger-ID -> Infektionszeitpunkt in Millisekunden. Das ist die eigentliche Zeitbasis der Krankheit. |
| `infectedCitizens` | Menge aller aktuell infizierten Bürger. |
| `infectedCitizensWithSymptoms` | Teilmenge der Infizierten, die beim Infektionszeitpunkt als symptomatisch ausgelost wurden. |
| `initialPopulationHealthy / Sick / Recovered / Dead` | Laufende S/I/R/D-Partition der Ausgangspopulation. |
| `infectionOrigins` | Zuletzt bekannter Infektionsursprung pro infiziertem Bürger. |
| `deathRecords` | Todesereignisse für Dead-X-Ray und Analyse. |
| `Observer.GeneralObservation.Infections` | Infektionsgraph: wer wen wann und wo infiziert hat. |

## 2. Zeitbasis und Hauptloop

Ein zentrales Detail für jede fachliche Diskussion ist: Die Pandemie läuft auf der Simulationszeit des Spiels, nicht auf Echtzeit.

Der `PandemicManager` liest `SimulationManager.instance.m_currentGameTime` und arbeitet in diskreten Pandemie-Schritten. Die relevante Update-Logik läuft derzeit ungefähr alle fünf In-Game-Minuten. Das ergibt sich aus `UPDATE_INTERVAL_MINUTES = 5` in `PandemicManager.cs`.

Die Pandemie-Iteration ist grob:

| Reihenfolge | Technischer Schritt | Fachliche Bedeutung |
|---|---|---|
| 1 | aktuelle Simulationszeit lesen | gemeinsame Zeitbasis für Krankheit, Tests und Beobachtung |
| 2 | Maskenwahrscheinlichkeiten an Schrittlänge anpassen | gleiche Logik auch bei anderer Schrittgröße |
| 3 | Todessimulation | symptomatische Todesfälle pro Schritt |
| 4 | Symptom- und Sick-Flags synchronisieren | Kopplung an Grundspiel- und Krankenhauslogik |
| 5 | Quarantäne-Fates verarbeiten | zusätzlicher Outcome-Pfad für quarantänisierte Infizierte |
| 6 | `spread()` ausführen | Testen, Recovery, Tracing, Kontaktbildung, neue Infektionen |
| 7 | Visualisierung und Building-Sets aktualisieren | Overlays, Heatmaps, Hotspots, Hubs |
| 8 | Observer und CSV fortschreiben | Auswertung und Export |

Technisch setzt der Manager während seines Update-Blocks kurz `ForcedSimulationPaused`, um inkonsistente Zwischenzustände zu vermeiden. Für das Gespräch reicht die Aussage: Die Pandemie aktualisiert in atomaren Simulationsschritten, nicht parallel unsauber zum restlichen Spiel.

## 3. Start der Pandemie und Seed-Logik

Die Pandemie startet nicht automatisch beim Laden des Savegames. Sie hat einen eigenen Lifecycle:

- `Dormant`
- `Running`
- `Finished`

Die Startpunkte sind `StartPandemic()` und `RestartSimulation()` in `PandemicManager.cs`. Beide rufen intern `BootstrapSimulation(...)` auf.

### 3.1 Aufbau der Ausgangspopulation

Beim Bootstrap wird aus allen Bürgern eine Startpopulation gebildet, sofern sie:

- nicht leer sind
- nicht tot sind
- ein Home Building besitzen

Diese Bürger landen in `initialPopulation`. Parallel wird eine Healthy-Liste aufgebaut. Bereits vorhandene Sick-Flags des Grundspiels werden beim Bootstrap zurückgesetzt, damit die Pandemie ihren eigenen Zustand sauber initialisiert.

### 3.2 Auswahl der Seed-Fälle

Die Zahl der initial Infizierten wird aus `DiseaseStartInfectionRatio` berechnet. Standardmäßig sind das `33%` der Ausgangspopulation, mit Mindestwert `1`.

Wichtig ist die Auswahlstrategie:

- Zuerst werden bevorzugt Bürger gewählt, die sich gerade `Home` befinden.
- Erst wenn diese Kandidaten nicht reichen, wird aus der restlichen Healthy-Population gesampelt.

Das ist epidemiologisch relevant, weil der Seed nicht völlig zufällig über alle Mobilitätskontexte verteilt ist.

### 3.3 Rückdatierter Seed

Ein sehr wichtiger, oft übersehener Punkt: Seed-Fälle starten nicht zwingend mit Krankheitsalter `0`.

Im Bootstrap wird ein negativer Offset erzeugt:

- `infectionOffsetRange = max(StartInfection - 1, 0) * 24h`
- danach `InfectCitizen(..., -offset)`

Das bedeutet: Wenn `StartInfection > 1`, können Seed-Fälle bereits vor dem sichtbaren Start der Pandemie intern einige Krankheitstage alt sein. Fachlich musst du das offen sagen, weil dadurch Symptome, Hospitalisierung oder Recovery früher auftreten können als ein Laie erwarten würde.

## 4. Zustandsmodell der Krankheit

Das Modell ist am treffendsten als agentenbasiertes `SIRD` mit impliziter Latenz zu bezeichnen.

### 4.1 Warum nicht sauberes SEIR?

Es gibt kein separates `Exposed`-Kompartiment als eigene gespeicherte Klasse. Stattdessen gilt:

- Ein Bürger wird zu einem definierten Zeitpunkt infiziert.
- Er ist aber erst infektiös, wenn `StartInfection` erreicht ist.

Formal ist damit eine latente Phase vorhanden, aber nicht als eigener persistenter Zustand oder eigener Zähler im UI.

### 4.2 Zentralparameter des Krankheitsverlaufs

| Parameter | Default | Implementierte Bedeutung |
|---|---|---|
| `DiseaseDuration` | `14` | Gesamtdauer bis reguläre Recovery |
| `StartInfection` | `1` | ab diesem Tag infektiös |
| `EndInfection` | `10` | bis zu diesem Tag infektiös |
| `StartSymptoms` | `3` | ab diesem Tag symptomrelevant |
| `EndSymptoms` | `14` | nominelles Ende der Symptomphase |
| `SymptomProbability` | `60%` | Anteil der Fälle, die beim Infektionszeitpunkt als symptomatisch markiert werden |

### 4.3 Infektiös vs. symptomatisch vs. erkannt

Diese drei Dinge sind im Code getrennt:

- `infiziert`: Bürger ist in `activeInfections`
- `infektiös`: `StartInfection <= days_since_infection <= EndInfection`
- `symptomatisch`: Bürger wurde beim Infektionszeitpunkt per Zufall in `infectedCitizensWithSymptoms` aufgenommen
- `known sick`: symptomatisch und intern bereits jenseits von `StartSymptoms`

Das ist fachlich wichtig, weil der Mod nicht dieselbe Variable für alle vier Zustände benutzt.

### 4.4 Symptomatik ist statisch ausgelost

Die Symptomatik wird beim Zeitpunkt der Infektion einmalig gezogen. Es gibt also derzeit kein Progressionsmodell, in dem ein zuvor asymptomatischer Fall später noch symptomatisch wird. Das ist eine klare Vereinfachung.

### 4.5 Recovery

Recovery erfolgt regulär, wenn:

- `infectedTimeInDays > DiseaseDuration`

Dann wird der Bürger aus `activeInfections` entfernt und in `Recovered` verschoben.

### 4.6 Tod

Es gibt zwei Todespfade:

- den regulären symptomorientierten Todesschritt
- den zusätzlichen Quarantäne-Fate-Pfad

Der reguläre Todesschritt transformiert die altersabhängigen Gesamtwahrscheinlichkeiten pro Simulationsschritt in eine Schrittwahrscheinlichkeit. Dafür werden `DeathChild`, `DeathTeen`, `DeathYoung`, `DeathAdult`, `DeathSenior` über die Länge der Symptomphase auf das aktuelle Updateintervall umgerechnet.

Die Altersdefaults sind:

| Altersgruppe | Default-Sterbewahrscheinlichkeit |
|---|---|
| Child | `0.1%` |
| Teen | `0.1%` |
| Young | `0.2%` |
| Adult | `0.5%` |
| Senior | `2.0%` |

## 5. Transmission: Wie Ansteckung entsteht

Der Kern liegt in `spread()` im `PandemicManager`. Der Ablauf ist:

- Tests verarbeiten
- Recoveries verarbeiten
- Contact-Tracing-Kandidaten verarbeiten
- Bürger-Tickzustände indizieren
- Outdoor-Transmission
- Vehicle-Transmission
- Building-Transmission

### 5.1 Bürger-Tickzustand

Vor der eigentlichen Transmission wird pro relevantem Bürger ein Laufzeitzustand aufgebaut:

- Home Building
- Current Building
- Vehicle
- Position
- Location
- Alter
- infiziert
- infektiös
- known sick
- sollte quarantänisiert sein
- zu Hause ja/nein
- Shared-Area-Fenster ja/nein

Das ist epidemiologisch wichtig, weil das Modell seine Kontakte aus dem aktuellen Raumzustand ableitet und nicht aus abstrakten Mischungsmatrizen.

### 5.2 Outdoor-Transmission

Für draußen werden Bürger in ein räumliches Grid gebucktet. Dann prüft das Modell pro infektiösem Bürger nur die eigene und benachbarte Zellen. Eine Transmission ist nur möglich, wenn der euklidische Abstand innerhalb der konfigurierten `DiseaseTransmissionRange` liegt.

Default:

- `OutdoorDiseaseTransmissionProbability = 0.3`
- `DiseaseTransmissionRange = 1.5`

Interpretation: Draußen ist die Basistransmission geringer und zusätzlich an physische Nähe gebunden.

### 5.3 Transmission in Fahrzeugen

In Fahrzeugen gilt Co-Occupancy:

- gleicher Fahrzeugraum
- keine explizite Distanzmetrik
- eigene Fahrzeugwahrscheinlichkeit

Technisch nutzt der Code für Fahrzeugkontakte die Indoor-Basis mit maskenabhängiger Fahrzeugmodifikation.

### 5.4 Transmission in Gebäuden

Gebäude sind der wichtigste Sonderfall, weil dort drei Unterfälle existieren:

| Fall | Implementierte Logik |
|---|---|
| beide zuhause und gleiche Familie | `Household`-Pfad |
| beide zuhause, aber unterschiedliche Familien | nur im Shared-Area-Fenster |
| gemeinsames Gebäude, aber nicht beide zuhause | normaler Indoor-Pfad |

### 5.5 Haushalte und Mehrfamilienhäuser

Das ist einer der wichtigsten Punkte für Fragen zur Plausibilität.

Der Mod kennt keine einzelnen Wohnungen. Stattdessen gilt:

- Familie = Proxy für Haushalt / Wohnung
- gleiches Wohngebäude allein reicht nicht für vollen Haushaltskontakt

Wenn zwei Bürger zwar beide zuhause sind, aber nicht derselben Familie angehören, gibt es nur dann eine Kontaktchance, wenn beide gleichzeitig in einem kurzen Shared-Area-Fenster liegen. Dieses Fenster ist aktuell auf `15` Minuten gesetzt und approximiert Begegnungen auf Flur, Treppenhaus, Eingang oder Aufzug.

Wichtig: Der `Household`-Pfad nutzt aktuell dieselbe Wahrscheinlichkeit wie normaler Indoor-Kontakt. Es gibt also im Code keinen eigenen, separat kalibrierten Haushaltsparameter.

### 5.6 No-Contact-Indoor

Für fremde Haushalte im selben Wohngebäude wird `GetIndoorInfectionProbabilityNoContact(...)` verwendet. Diese Wahrscheinlichkeit ist die Indoor-Basis geteilt durch `96`, zusätzlich maskenmodifiziert. Das ist die technische Approximation eines sehr kleinen Restrisikos in gemeinsamen Hausbereichen.

## 6. Maskenlogik

Die Maskenlogik lebt in `src/RealTime/Pandemic/MaskManager.cs`.

### 6.1 Bürgerindividuelles Maskenverhalten

Jeder Bürger erhält beim ersten Kontakt mit dem System genau einen von drei Maskentypen:

- ignoriert Masken
- Maske schützt andere
- Maske schützt den Träger selbst

Die Verteilung erfolgt probabilistisch über:

- `RatioIgnoreMasks`
- `RatioOtherProtectionMask`
- `RatioOwnProtectionMask`

Defaults:

| Parameter | Default |
|---|---|
| Ignore | `30` |
| Other protection | `50` |
| Own protection | `50` |

Diese drei Werte werden als relative Gewichte behandelt, nicht als hart normierte Prozentwerte.

### 6.2 Maskenmodi

`MaskBehavior` hat die Werte:

- `None`
- `Building`
- `Vehicle`
- `Full`

Semantik:

- `None`: keine Kontextreduktion
- `Building`: Reduktion in Gebäuden
- `Vehicle`: Reduktion in Gebäuden und Fahrzeugen
- `Full`: Reduktion in Gebäuden, Fahrzeugen und draußen

### 6.3 Reduktionsmechanismus

Die Mod benutzt keinen simplen linearen Abschlag, sondern einen Multiplikator über `TransmissionProbabilityReduction`. Default ist `2`. Je nachdem, ob der Infizierende andere schützt und ob der Empfänger Eigenschutz trägt, wird der Reduktionsfaktor potenziert.

Das Ergebnis ist:

- eine Schutzseite: ungefähr Halbierung der Basistransmission
- zwei Schutzseiten: ungefähr Viertelung

Die tatsächliche Schrittwahrscheinlichkeit wird dann wieder auf die Länge des aktuellen Pandemie-Schritts umgerechnet.

## 7. Testlogik

Die Testlogik liegt in `src/RealTime/Pandemic/TestManager.cs`.

### 7.1 Kapazität

Die Gesamttestkapazität wird aus der Bevölkerung und zwei Parametern gebildet:

- `RelativeTestCapacity`
- `PercentageOfTestsReservedForSickCitizens`

Defaults:

| Parameter | Default |
|---|---|
| RelativeTestCapacity | `40%` |
| PercentageOfTestsReservedForSickCitizens | `50%` |
| MinimumTestDuration | `1` Tag |
| MaximumTestDuration | `3` Tage |

Die Tests werden nicht sofort ausgeführt, sondern über einen 7-Tage-Horizont verteilt. Dadurch entsteht ein Scheduling-Modell statt eines instantanen Testens.

### 7.2 Wer wird getestet?

Der Manager unterscheidet zwischen:

- `known sick`
- nicht bekannt krank

Bekannt kranke Bürger nutzen den für Kranke reservierten Anteil der Testkapazität. Andere Bürger werden über die restliche Kapazität eingeplant.

### 7.3 Wichtige Limitierung

Aktuell gilt:

- `falsePositiveRate = 0`
- `falseNegativeRate = 0`

Das Testsystem ist damit diagnostisch idealisiert. Es modelliert Kapazitäts- und Zeitverzug, aber keine Messfehler.

### 7.4 Positive Tests und Blockierung

Ein positiver Test wird nicht sofort wirksam, sondern ab:

- `plannedDate + MinimumTestDuration`

Ein positiver Status bleibt für `DiseaseDuration` Tage aktiv. Positive Bürger gelten im System als blockiert und können dadurch Quarantäne- und Tracing-Logik auslösen.

## 8. Contact Tracing

Die Tracing-Logik liegt in `src/RealTime/Pandemic/ContactManager.cs`.

### 8.1 Grundprinzip

Kontakte werden als gerichtete Beziehung gespeichert:

- Bürger A
- Kontakt B
- letzter Kontaktzeitpunkt

Wichtig ist: Ein Kontakt wird nur gespeichert, wenn beide beteiligten Bürger im jeweiligen Tracing-System als Teilnehmer gelten.

### 8.2 Zwei Tracing-Modi in der Praxis

Obwohl es historisch ein `ContactTracingBehavior`-Enum gab, wird dieses im aktuellen Code nicht als aktive Steuergröße benutzt. Stattdessen arbeitet das System mit zwei Wahrscheinlichkeiten:

- `BuildingContactTracingProbability`
- `AppBasedContactTracingProbability`

Defaults:

| Parameter | Default |
|---|---|
| BuildingContactTracingProbability | `30%` |
| AppBasedContactTracingProbability | `20%` |

Gebäudebasiertes Tracing zählt nur für `inBuilding = true`. App-basiertes Tracing kann auch außerhalb von Gebäuden greifen.

### 8.3 Wann werden Kontakte in Maßnahmen übersetzt?

Kontakte werden nicht sofort bei bloßem Kontakt quarantänisiert. Zunächst muss der Ausgangsfall entweder:

- positiv getestet sein
- oder als `known sick` gelten, sofern `OnlyTestedCitizensToQuarantine = false`

Dann werden je nach Quarantänemodus Kontakte oder Familienmitglieder prophylaktisch quarantänisiert.

## 9. Quarantäne

Die operative Verwaltung liegt in `src/RealTime/Pandemic/QuarantineManager.cs`, die Entscheidung in `PandemicManager.cs`.

### 9.1 Quarantänemodi

`QuarantineBehavior` hat:

- `None`
- `Self`
- `Family`
- `Contacts`

Semantik:

- `Self`: nur der Fall selbst
- `Family`: Fall selbst plus Familienmitglieder
- `Contacts`: Fall selbst plus traced contacts

### 9.2 Wann gilt ein Bürger als quarantänepflichtig?

Die Logik `ShouldBeInQuarantine(...)` ist härter als eine reine Testlogik. Ein infizierter Bürger wird quarantänisiert, wenn:

- er bereits in Quarantäne ist
- oder `known sick` und nicht nur getestete Bürger quarantänisiert werden sollen
- oder positiv/blockiert ist
- oder bereits mindestens zwei Tage infiziert ist

Gerade der letzte Punkt ist für ein Expertengespräch wichtig, weil das eine generische Sicherheitsregel ist und keine explizite klinische oder organisatorische Detection-Logik.

### 9.3 Dauer

Die Quarantänedauer im `QuarantineManager` ist aktuell fest auf `10` Tage gesetzt, sowohl für direkte als auch prophylaktische Quarantäne.

### 9.4 Wichtige Modellkritik: Quarantäne-Fate

Zusätzlich zur normalen Recovery- und Death-Logik existiert ein Sonderpfad:

- Beim Beginn einer Quarantäne wird ein zufälliger Auflösungszeitpunkt zwischen Tag `1` und `14` gewählt.
- Gleichzeitig wird auf Basis der altersabhängigen Sterbewahrscheinlichkeit vorgelost, ob der Bürger am Ende dieses Quarantäne-Fates stirbt oder genest.

Das ist technisch implementiert, epidemiologisch aber problematisch. Es koppelt Outcome direkt an Quarantänebeginn und nicht ausschließlich an den sonstigen Krankheitsverlauf.

Wenn der Epidemiologe eine Stelle sucht, die man prioritär überarbeiten sollte, ist das eine der ersten.

## 10. Krankenhaus- und Ambulanzlogik

Die Pandemie nutzt die vorhandene Healthcare-Infrastruktur des Spiels, statt eine eigene Krankenhauswelt zu simulieren.

### 10.1 Wann versucht ein Bürger ein Krankenhaus zu erreichen?

`ShouldSeekHospital(...)` verlangt:

- Pandemie läuft
- Bürger ist infiziert
- Bürger hat Symptome
- Tage seit Infektion >= `StartSymptoms`
- Bürger wurde noch nicht als hospital-handled markiert
- Bürger ist nicht bereits in Quarantäne

### 10.2 Was passiert bei erfolgreichem Krankenhauskontakt?

Wenn ein Bürger ein Healthcare-Gebäude besucht, wird:

- `symptomHospitalHandledCitizens` gesetzt
- Quarantäne begonnen
- Quarantäne-Fate terminiert
- der sichtbare Sick-Flag des Grundspiels gelöscht

Das bedeutet: Das Krankenhaus modelliert hier weniger klinische Behandlung als den Übergang von unerkannter Krankheit zu isoliertem Fall.

### 10.3 Was passiert bei fehlendem Krankenhaus?

Wenn kein Krankenhauspfad gefunden wird, wird der Bürger dennoch als hospital-handled markiert und in Quarantäne gesetzt. Es gibt also einen Home-Isolation-Fallback.

### 10.4 Healthcare-Load

Im Dashboard werden zusätzlich citywide Metriken für:

- Krankenhausauslastung
- Ambulanzauslastung

gesammelt. Diese Werte sind Telemetrie, keine Eingriffe in die Krankheitslogik.

## 11. Lockdown und Public Transport

Lockdown ist in dieser Mod primär eine Kontaktgelegenheitsintervention.

### 11.1 Gebäudefamilien

Der Lockdown arbeitet nicht pro Einzelgebäude, sondern über Familien:

- Education
- PublicTransport
- Commercial
- LeisureTourismParks
- Office
- IndustryPlayerIndustry
- GovernmentOtherPublic
- EssentialServices
- Healthcare

Healthcare ist als geschützte Familie vorgesehen und bleibt offen.

### 11.2 Manuell und threshold-basiert

Eine Familie kann geschlossen werden durch:

- manuelles Schließen
- automatisches Erreichen eines Infizierten-Schwellenwertes

Die Defaults im Config-Reset zeigen, dass nicht alle Familien standardmäßig sofort geschlossen werden. Einige sind manuell an, andere nur threshold-basiert oder offen.

### 11.3 Public-Transport-Shutdown

Die ÖPNV-Logik ist weiter ausgebaut als ein bloßes “Gebäude zu”. Es gibt einen Runtime-Zustand:

- `Open`
- `Draining`
- `Closed`

Beim Schließen werden:

- Linienzustände erfasst
- Depots erfasst
- laufende Fahrzeuge erfasst
- der Dienst kontrolliert aus dem Netz gefahren

Beim Wiederöffnen werden Depots und Linienzustände wiederhergestellt.

Für das Expertengespräch ist das relevant, weil `PublicTransport` im Modell nicht einfach ein Verbot ist, sondern eine strukturelle Änderung des Mobilitätsnetzwerks.

## 12. Dashboard, X-Ray und Datenausgabe

Die Mod liefert nicht nur eine Simulation, sondern auch umfangreiche Beobachtung.

### 12.1 Live-Snapshot

`PandemicLiveSnapshot` hält u. a.:

- Lifecycle
- Pandemie-Tag
- Healthy, Sick, Recovered, Dead
- Delten
- Quarantäne
- Tests
- Kontakte
- Gesamte Transmissionen
- Origins
- Superspreaders
- Lockdownfamilien
- Chartpunkte
- Policy-Marker
- Healthcare-Load

### 12.2 Observer

`PandemicObserver` ist die analytische Kernkomponente. Er speichert:

- Zeitreihe `healthy / sick / recovered / dead`
- Infektionsgraphen `wer infizierte wen`
- Infektionszeit
- Setting/Origin
- Position
- Building- und Vehicle-Metadaten

Am Ende einer Runde entstehen mindestens:

- `data.csv`
- `contacts.csv`

### 12.3 Origin-Kategorien

Die Infektionsherkunft wird über eine feste Taxonomie kategorisiert:

| Kategorie | Bedeutung |
|---|---|
| Initial seed | initial gesetzte Infektion |
| Residential/Home | Zuhause / Wohngebäude |
| Workplace/Office/Industry | Arbeit / Büro / Industrie |
| School/University | Schule / Bildung |
| Healthcare | Gesundheitskontext |
| Commercial/Leisure/Tourism | Handel, Freizeit, Tourismus |
| Bus / Tram / Metro / Train / Ship-Ferry / Plane / Taxi / Car-Other vehicle | Fahrzeugkontexte |
| Outdoor/Street | draußen |
| Stop/Platform | Haltestellen- oder PT-Building-Kontexte |
| Other/Unknown | Restkategorie |

### 12.4 X-Ray

Das X-Ray kann aktuell nach zwei Achsen unterscheiden:

- Metrik: `Infected`, `Recovered`, `Dead`
- Raumbezug: `LivePositions`, `HomeLocations`

Interpretation:

- `Infected`: aktuell infizierte Bürger
- `Recovered`: aktuell genesene, lebende Bürger
- `Dead`: kumulierte Todesfälle der laufenden Runde

Für `Dead` wird ein eigener Todesdatensatz mit Todesort und Home Building gespeichert.

### 12.5 Superspreaders

Superspreaders werden aus `Observer.GeneralObservation.Infections` abgeleitet, also direkt aus der aufgezeichneten Infektionskette. Das ist analytisch deutlich stärker als eine bloße Heuristik auf Basis momentaner Hotspots.

## 13. Epidemiologische Einordnung

### 13.1 Was das Modell gut kann

- setting-spezifische Transmission entlang einer echten Stadtstruktur
- Policy-Vergleiche zwischen Masken, Quarantäne, Lockdown und Testing
- Sichtbarmachen von räumlichen Hotspots und Herkunftskontexten
- Exploration von Infrastrukturabhängigkeiten, z. B. ÖPNV oder Healthcare-Zugang

### 13.2 Was das Modell nur grob kann

- klinischer Verlauf einzelner Fälle
- diagnostische Unsicherheit
- echte Wohnungs- und Haushaltsgeometrie
- Dauer und Intensität von Innenraumexposition
- individuelle Compliance-Dynamik über die Zeit
- realistische soziale Netzwerke jenseits der Spielstruktur

### 13.3 Was das Modell derzeit nicht leisten sollte

- quantitative Vorhersage realer Inzidenzen
- direkte Ableitung eines belastbaren `R_t`
- externe epidemiologische Validitätsansprüche ohne Kalibrierung
- realweltliche Krankenhausprognosen

## 14. Kritische Schwächen und offene Modellfragen

Dieser Abschnitt ist absichtlich offen formuliert. Er soll dich davor schützen, im Gespräch zu viel zu behaupten.

| Thema | Aktueller Stand | Warum kritisch |
|---|---|---|
| Kein explizites `E`-Kompartiment | Latenz ist nur implizit über `StartInfection` modelliert. | Fachlich weniger sauber als SEIR-artige Modelle. |
| Perfekte Tests | keine false positives / false negatives | überschätzt Erkennung und Tracing-Wirksamkeit |
| `DetectionTime` ungenutzt | Parameter existiert, ist aber nicht an die Simulationslogik angeschlossen | UI und Modellsemantik weichen auseinander |
| Haushalte nur über Familien | keine echte Wohnungseinheit | Mehrfamilienhäuser bleiben Approximation |
| Household-Transmission = Indoor-Transmission | kein eigener Haushaltsparameter | household secondary attack rate kaum sauber kalibrierbar |
| Quarantäne-Fate | Outcome wird zusätzlich beim Quarantänebeginn vorgelost | modelltheoretisch inkonsistent zum übrigen Krankheitsverlauf |
| Contact-Tracing-Verhalten nicht als separates Enum aktiv | Steuerung praktisch nur über Wahrscheinlichkeiten | fachliche UI-Semantik potenziell missverständlich |
| Grundspielpopulation synthetisch | keine realen Bevölkerungsdaten | externe Validität begrenzt |

## 15. Was ich dem Experten offen sagen sollte

- Dieses System ist derzeit ein mechanistischer Szenario-Simulator und kein validiertes Prognosemodell.
- Die starke Seite ist die Kopplung von Transmission an reale Stadtmobilität und Infrastruktur.
- Die schwache Seite ist die Vereinfachung des Krankheitsverlaufs und der Erkennung.
- Besonders kritisch sehe ich aktuell den Quarantäne-Fate-Mechanismus, perfekte Tests und die nur familienbasierte Haushaltsmodellierung.
- Wenn wir die fachliche Qualität erhöhen wollen, wären ein expliziter Exposed-Zustand, realistischere Testfehler, sauberere Detection-Logik und eine Überarbeitung des Outcome-Pfads die wichtigsten nächsten Schritte.

## 16. Typische Expertenfragen mit belastbaren Antwortvorschlägen

### 16.1 Ist das Modell SEIR?

Antwortvorschlag:

Nein, nicht sauber. Am treffendsten ist agentenbasiertes SIRD mit impliziter Latenz. Der Exposed-Zustand ist nicht als eigenes persistentes Kompartiment modelliert, sondern nur indirekt über den Unterschied zwischen Infektionszeitpunkt und Beginn der Infektiosität.

### 16.2 Wie entstehen Kontakte?

Antwortvorschlag:

Nicht über eine feste Kontaktmatrix, sondern über den tatsächlichen Aufenthaltsort der Cims im Spiel. Die Mod prüft Kontakte in drei Kontexten: draußen, Fahrzeuge, Gebäude. Das Kontaktnetz entsteht also aus Stadtstruktur, Mobilität, Gebäudenutzung und Tagesabläufen.

### 16.3 Wie realistisch sind Haushalte?

Antwortvorschlag:

Haushalte sind aktuell über die Familienstruktur des Spiels approximiert. Verschiedene Haushalte im selben Mehrfamilienhaus werden nicht als voller Haushaltskontakt behandelt, sondern nur in einem kurzen Shared-Area-Fenster. Das ist realistischer als reiner Gebäudekontakt, aber keine Wohnungssimulation.

### 16.4 Sind Tests realistisch?

Antwortvorschlag:

Teilweise. Kapazität und zeitlicher Verzug sind modelliert. Diagnostische Fehler sind aktuell jedoch nicht modelliert, weil false positives und false negatives im Code auf null stehen.

### 16.5 Wie wird Symptomatik modelliert?

Antwortvorschlag:

Beim Zeitpunkt der Infektion wird mit einer festen Wahrscheinlichkeit entschieden, ob der Fall symptomatisch sein wird. Das ist eine statische Ziehung, kein dynamisches Progressionsmodell.

### 16.6 Warum können Maßnahmen spät wirken?

Antwortvorschlag:

Weil Erkennung, Quarantäne und Contact Tracing verzögert sind und Haushaltskontakte weiterlaufen. Außerdem sind bei Aktivierung von Maßnahmen oft bereits viele Infektionen im System angelegt.

### 16.7 Wie funktioniert Lockdown?

Antwortvorschlag:

Lockdown reduziert primär Kontaktgelegenheiten, indem bestimmte Gebäudefamilien geschlossen und Teile der Mobilität eingeschränkt werden. Er verändert nicht primär die Biologie, sondern die Begegnungsstruktur.

### 16.8 Wie funktioniert Contact Tracing?

Antwortvorschlag:

Probabilistisch und unvollständig. Bürger nutzen das Tracing-System nur mit bestimmten Wahrscheinlichkeiten. Erst wenn ein Fall positiv oder bekannt krank ist, werden gespeicherte Kontakte in prophylaktische Quarantäne übersetzt.

### 16.9 Was ist die größte fachliche Schwäche?

Antwortvorschlag:

Wenn ich eine priorisieren müsste: die Kombination aus perfekter Testlogik, fehlendem explizitem Exposed-Zustand und dem separaten Quarantäne-Fate-Pfad.

### 16.10 Wofür ist das Modell gut genug?

Antwortvorschlag:

Für qualitative und semiquantitative Policy-Exploration: Welche Settings treiben die Transmission? Wie verschiebt sich die Herkunft von Infektionen? Was passiert, wenn Public Transport geschlossen wird? Wo entstehen Hotspots? Für harte Realweltprognosen ist es derzeit nicht ausreichend validiert.

## 17. Fragen an den Epidemiologen

### 17.1 Fragen zur Modellstruktur

- Würden Sie für dieses System zwingend ein explizites `Exposed`-Stadium empfehlen?
- Welche drei Kontaktkontexte sind aus Ihrer Sicht unverzichtbar: Haushalt, Arbeit, Schule, Transport, Freizeit?
- Ist die Aufteilung in draußen / Gebäude / Fahrzeuge fachlich sinnvoll genug für ein erstes policy-orientiertes Modell?

### 17.2 Fragen zu Haushalten und Gebäuden

- Wie würden Sie Kontakte in Mehrfamilienhäusern modellieren, wenn keine Wohnungsgeometrie verfügbar ist?
- Ist ein sehr kleines Shared-Area-Fenster als Flur- oder Treppenhaus-Approximation vertretbar?
- Braucht ein Haushalt einen eigenen, separat kalibrierbaren Transmissionsparameter?

### 17.3 Fragen zu Detektion und Maßnahmen

- Welche Detection- oder Symptomlogik wäre für ein Modell dieser Komplexität minimal sinnvoll?
- Wie würden Sie Testing realistischer machen, ohne das Modell massiv zu verkomplizieren?
- Was wäre aus Ihrer Sicht eine sinnvolle Minimalform von Contact Tracing in so einer urbanen Agentensimulation?

### 17.4 Fragen zu Outcome und Klinik

- Ist die aktuelle Trennung zwischen Recovery, symptomabhängigem Tod und zusätzlichem Quarantäne-Fate fachlich unvertretbar oder nur unschön?
- Würden Sie Hospitalisierung eher als Zustandsübergang, Ressourcenfilter oder Outcome-Modifikator modellieren?
- Welche Rolle sollte Krankenhausüberlastung im Krankheitsverlauf spielen?

### 17.5 Fragen zu Validierung

- Welche Form von Validierung wäre für ein solches System angemessen: Face Validity, Vergleich relativer Trends, Setting-Anteile, Secondary Attack Rates?
- Welche beobachtbaren Kennzahlen würden Sie als erste Validierungsziele wählen?
- Ab welchem Punkt würden Sie sagen: Das Modell ist als exploratives Werkzeug fachlich brauchbar?

## 18. Technischer Anhang: wichtigste Klassen

| Datei | Hauptrolle |
|---|---|
| `src/RealTime/Pandemic/PandemicManager.cs` | zentrale Orchestrierung der Pandemie, Zustände, Transmission, Quarantäne, Lockdown, Snapshot |
| `src/RealTime/Pandemic/MaskManager.cs` | kontextspezifische Übertragungswahrscheinlichkeiten unter Maskenannahmen |
| `src/RealTime/Pandemic/TestManager.cs` | Testplanung, positive Tests, Blockierung |
| `src/RealTime/Pandemic/ContactManager.cs` | Kontaktaufzeichnung und Tracing-Teilnahme |
| `src/RealTime/Pandemic/QuarantineManager.cs` | Quarantäne- und prophylaktische Quarantänezustände |
| `src/RealTime/Pandemic/PandemicObserver.cs` | Zeitreihe, Infektionsgraph und CSV-Export |
| `src/RealTime/Pandemic/PandemicLiveSnapshot.cs` | UI-Datentransfer für Dashboard und Charts |
| `src/RealTime/CustomAI/RealTimeResidentAI.cs` | Kopplung der Bürger-KI an Krankheit, Quarantäne und Schedule |
| `src/RealTime/CustomAI/RealTimeResidentAI.Common.cs` | Krankenhaussuche, Healthcare-Besuch, Sick-Handling |
| `src/RealTime/Simulation/SimulationHandler.cs` | Taktgeber für RealTime-Simulationslogik |

## 19. Technischer Anhang: zentrale Konfigurationsparameter

### 19.1 Krankheitsparameter

| Parameter | Default | Kommentar |
|---|---|---|
| `DiseaseDuration` | `14` | Recovery nach regulärem Pfad |
| `DetectionTime` | `2` | aktuell im Kernmodell nicht angeschlossen |
| `StartSymptoms` | `3` | Beginn symptomrelevanter Phase |
| `EndSymptoms` | `14` | Ende symptomrelevanter Phase |
| `StartInfection` | `1` | Beginn der Infektiosität |
| `EndInfection` | `10` | Ende der Infektiosität |
| `IndoorDiseaseTransmissionProbability` | `1.5` | Indoor-Basis pro Modellschritt umgerechnet |
| `OutdoorDiseaseTransmissionProbability` | `0.3` | Outdoor-Basis |
| `DiseaseTransmissionRange` | `1.5` | Distanzgrenze draußen |
| `DiseaseStartInfectionRatio` | `33` | Anteil Seed-Fälle |
| `SymptomProbability` | `60` | statische Ziehung pro neuem Fall |

### 19.2 Interventionsparameter

| Parameter | Default | Kommentar |
|---|---|---|
| `QuarantineBehavior` | `None` | Self / Family / Contacts optional |
| `OnlyTestedCitizensToQuarantine` | `false` | wenn `true`, stärkere Bindung an Teststatus |
| `TransmissionProbabilityReduction` | `2` | Masken-Reduktionsfaktor |
| `MaskBehavior` | `None` | None / Building / Vehicle / Full |
| `BuildingContactTracingProbability` | `30` | Teilnahme für building tracing |
| `AppBasedContactTracingProbability` | `20` | Teilnahme für app tracing |
| `RelativeTestCapacity` | `40` | relative Testkapazität |
| `PercentageOfTestsReservedForSickCitizens` | `50` | Anteil für bekannte Kranke |
| `MinimumTestDuration` | `1` | frühestes positives Ergebnis |
| `MaximumTestDuration` | `3` | spätester akzeptierter Planungshorizont |

## 20. Schlussfolgerung für das Gespräch

Wenn du die Mod in einem Satz fachlich sauber zusammenfassen willst, nimm diesen:

Der Pandemic Simulator ist eine agentenbasierte, in die synthetische Stadtmobilität von Cities: Skylines eingebettete Ausbruchssimulation, die Maßnahmen nicht nur als Parameteränderung, sondern als Veränderung von Kontaktgelegenheiten, Erkennungspfaden und Infrastrukturzuständen modelliert, dabei aber mehrere epidemiologisch relevante Vereinfachungen und offene Validierungsfragen aufweist.

Wenn du in zwei Sätzen antworten willst, ergänze:

Die größte Stärke ist die räumlich und infrastrukturell verankerte Kontaktentstehung. Die größte Schwäche ist die noch vereinfachte Krankheits- und Detektionslogik, insbesondere fehlendes `Exposed`-Stadium, perfekte Tests und der separate Quarantäne-Fate-Mechanismus.
