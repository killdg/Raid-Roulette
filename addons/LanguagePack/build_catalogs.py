"""Build bundled catalogs from reviewed, human-readable translation rows.

Each row is an event title followed by its short player-facing briefing. Row
order is checked against the canonical event manifest; IDs never get translated.
No external translation service or network access is used by this script.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
english_events = json.loads((ROOT / 'EnglishEvents.json').read_text(encoding='utf-8'))
ids = list(english_events)
names = [event['Name'] for event in english_events.values()]
descriptions = [event['Description'] for event in english_events.values()]
assert len(ids) == len(names) == 44

def write(code, rows, text):
    lines = rows.strip().splitlines()
    assert len(lines) == len(ids), (code, len(lines))
    events = {}
    for event_id, line in zip(ids, lines):
        title, description = line.split('|', 1)
        events[event_id] = {'Name': title.strip(), 'Description': description.strip()}
    catalog = {'Language': code, 'Events': events, 'Text': text}
    (ROOT / 'Locales' / (code + '.json')).write_text(
        json.dumps(catalog, ensure_ascii=False, indent=4) + '\n', encoding='utf-8')

write('en', '\n'.join(a + '|' + b for a, b in zip(names, descriptions)), {})

# Order: notices, Fence objective/dialogue, then commonly used player controls.
TEXT_KEYS = '''Raid Event : {0}
It's arranged.
We're done here.
Another time, then.
I will take that assignment.
The raid is over.
Clear every wave.
Play one raid.
Enable Raid Roulette
Trader Event Activation
Raid preset
Chance status
Normal raid chance (%)
Custom event mix
Enable all
Disable all
Equal weights
End run
Show raid event before raid
Show raid event after raid
Mostly Normal
Half And Half
Custom
Event
Did you hear that?
....you're not dead
you win'''.splitlines()

def common(rows):
    values = rows.strip().splitlines()
    assert len(values) == len(TEXT_KEYS), len(values)
    return dict(zip(TEXT_KEYS, values))

write('fr', '''Traque du boss|Un boss vous traque et tente de vous rejoindre.
Convention des boss|Plusieurs boss et leurs gardes se retrouvent sur la carte.
Butin sous clé|Chaque pièce verrouillée à clé cache un à trois objets précieux.
Assaut des Raiders|Les Raiders remplacent les adversaires habituels.
Invasion des cultistes|Les cultistes envahissent le raid. Méfiez-vous de leurs couteaux.
Rassemblement de Scavs|Les Scavs se déplacent en groupes.
Ratissage des PMC|Des groupes USEC et BEAR remplacent les adversaires habituels.
À portée de pistolet|Les Scavs et PMC portent des pistolets et des grenades aveuglantes.
Coup de pompe|Les Scavs et PMC portent des fusils à pompe et des grenades aveuglantes.
Tireur d'élite|Les Scavs et PMC portent des fusils de précision.
Scavs explosifs|Les Scavs portent des pistolets et des lance-grenades.
Épidémie|Des infectés arrivent tout au long du raid.
Suréquipés|Les ennemis portent des armes puissantes, des armures et des munitions redoutables.
Sous bonne garde|Les gardes des boss remplacent les adversaires habituels.
Chargeurs démesurés|Les Scavs et PMC disposent de chargeurs de grande capacité.
Bug de la matrice|Des groupes de combattants portent les mêmes vêtements.
Nulle part à l'abri|Tous les combattants, boss compris, traquent votre position.
Imposteurs partout|Des combattants ordinaires portent l'équipement de Killa.
Faux culte|Les adversaires portent des tenues de cultistes, mais gardent leur façon de combattre.
Submergés|Des renforts remplacent les pertes lorsque la population diminue.
Guerre des clones|Les PMC copient votre apparence et votre équipement de départ.
Le voleur|Un voleur au bonnet de Noël transporte des provisions, de l'argent et parfois des bitcoins.
Salon des fumeurs|Les adversaires lancent des fumigènes tout au long du raid.
Distorsion temporelle|Le jour et la nuit défilent rapidement. Le compte à rebours reste normal.
Schizophrénie|Voix, silhouettes et visions impossibles troublent le raid.
Course aux armes|Chaque élimination valide débloque l'arme suivante. Terminez pour un bitcoin ; vos armes initiales reviennent par courrier.
Course aux armes [Réapparitions]|Après une défaite, repartez en pleine santé avec un pistolet. La progression et le bonus au couteau sont réinitialisés.
Marée d'orage|Il fait nuit en cinq minutes. La pluie intense couvre les autres bruits et les éclairs illuminent le ciel.
AK50|Tout le monde porte un AK-50, vous compris. Vos armes initiales reviennent par courrier.
Grenadier|Les Scavs et PMC lancent de nombreuses grenades explosives.
Journée au labo|Des employés non armés circulent entre les bureaux. Les salles sont ouvertes ; tuer trois employés attire des Raiders et coûte de l'expérience.
Jour bleu|Une sirène annonce des bombardements de mortier imprévisibles. Abritez-vous sous un toit.
Épreuve Boule de neige|Affrontez les boss un par un en une seule vie. Temps de raid doublé et défi caché.
Épreuves|Dix vagues de cinq chasseurs de plus en plus forts. Un bitcoin dans la dernière vague ; temps doublé.
Épreuves extrêmes|Dix vagues de cinq boss identiques. Un membre de chaque vague porte dix bitcoins ; temps doublé.
Épreuve des gardes|Affrontez des escouades de cinq gardes, puis un groupe mélangé. Temps doublé.
Épreuve totale|Vingt vagues de cinq chasseurs. Éliminez chaque groupe pour continuer ; temps doublé.
Printemps|Décor printanier pour ce raid uniquement.
Été|Décor estival pour ce raid uniquement.
Automne|Couleurs d'automne pour ce raid uniquement.
Hiver|Neige hivernale sur les cartes compatibles, pour ce raid uniquement.
Mauvaise porte|Franchir une porte ouverte peut vous téléporter vers une autre porte de la carte.
Infrastructure en déclin|Le réseau électrique vacille. Les coupures désactivent temporairement les boutons et interrupteurs électriques.
Normal|Un raid normal sans événement Raid Roulette.''', common('''Événement de raid : {0}
C'est arrangé.
Nous en avons terminé.
Une autre fois, alors.
J'accepte cette mission.
Le raid est terminé.
Éliminez toutes les vagues.
Participez à un raid.
Activer Raid Roulette
Activation par le marchand
Préréglage des raids
Probabilités actuelles
Chance de raid normal (%)
Mélange d'événements personnalisé
Tout activer
Tout désactiver
Poids égaux
Terminer la partie
Afficher l'événement avant le raid
Afficher l'événement après le raid
Surtout normal
Moitié-moitié
Personnalisé
Événement
Vous avez entendu ça ?
....vous n'êtes pas mort
vous avez gagné'''))

write('ge', '''Boss auf der Jagd|Ein zufälliger Boss jagt dich und versucht, dich zu erreichen.
Bossversammlung|Mehrere Bosse und ihre Wachen sind auf derselben Karte.
Beute hinter Schloss und Riegel|Jeder mit Schlüssel verschlossene Raum enthält ein bis drei wertvolle Gegenstände.
Raider-Angriff|Raider ersetzen die gewöhnlichen Gegner.
Kultisteninvasion|Kultisten übernehmen den Raid. Hüte dich vor ihren Messern.
Scav-Ansammlung|Scavs ziehen in Gruppen umher.
PMC-Razzia|USEC- und BEAR-Gruppen ersetzen die gewöhnlichen Gegner.
Pistolenparty|Scavs und PMCs tragen Pistolen und Blendgranaten.
Schrotflintenzeit|Scavs und PMCs tragen Schrotflinten und Blendgranaten.
Scharfschütze|Scavs und PMCs tragen Scharfschützengewehre.
Explosive Scavs|Scavs tragen Pistolen und Granatwerfer.
Ausbruch|Während des Raids treffen immer mehr Infizierte ein.
Schwer ausgerüstet|Gegner tragen starke Waffen, Rüstung und gefährliche Munition.
Bewacht|Bosswachen ersetzen die gewöhnlichen Gegner.
Überladen|Scavs und PMCs haben Magazine mit hoher Kapazität.
Matrixfehler|Kämpfergruppen tragen dieselbe Kleidung.
Kein sicherer Ort|Alle Kämpfer, auch Bosse, verfolgen deine Position.
Überall Hochstapler|Gewöhnliche Kämpfer tragen Killas Ausrüstung.
Falscher Kult|Gegner sehen wie Kultisten aus, behalten aber ihre übliche Kampfweise.
Überrannt|Verstärkung ersetzt Verluste, wenn zu wenige Kämpfer übrig sind.
Klonkriege|PMCs kopieren dein Aussehen und deine Startausrüstung.
Der Dieb|Ein Dieb mit Weihnachtsmütze trägt Vorräte, Geld und manchmal Bitcoins.
Raucherlobby|Gegner werfen während des gesamten Raids Rauchgranaten.
Zeitsprung|Tag und Nacht vergehen rasend schnell. Der Raid-Timer läuft normal.
Schizophrenie|Stimmen, Gestalten und unmögliche Bilder stören den Raid.
Waffenleiter|Jeder gültige Abschuss bringt eine neue Waffe. Gewinne einen Bitcoin; deine ursprünglichen Waffen kommen per Post zurück.
Waffenleiter [Wiedereinstieg]|Nach einer Niederlage startest du gesund mit einer Pistole neu. Leiter und Messerbonus werden zurückgesetzt.
Sturmflut|Nach fünf Minuten ist es dunkel. Starkregen überdeckt Geräusche und Blitze erhellen die Nacht.
AK50|Alle erhalten eine AK-50, auch du. Deine ursprünglichen Waffen kommen per Post zurück.
Grenadier|Scavs und PMCs werfen viele Sprenggranaten.
Arbeitstag im Labor|Unbewaffnete Mitarbeiter bewegen sich zwischen Büros. Räume sind offen; drei getötete Mitarbeiter rufen Raider und kosten Erfahrung.
Blauer Tag|Eine Sirene warnt vor unvorhersehbarem Mörserbeschuss. Suche ein Dach über dem Kopf.
Schneeballprüfung|Bosse jagen dich nacheinander. Nur ein Leben, doppelte Raid-Zeit und eine versteckte Herausforderung.
Prüfungen|Zehn Wellen mit je fünf stärkeren Jägern. Die letzte Welle trägt einen Bitcoin; doppelte Zeit.
Extreme Prüfungen|Zehn Wellen mit je fünf gleichen Bossen. Ein Gegner jeder Welle trägt zehn Bitcoins; doppelte Zeit.
Wachenprüfung|Kämpfe gegen Fünfergruppen von Wachen und zuletzt eine gemischte Gruppe. Doppelte Zeit.
Gesamtprüfung|Zwanzig Wellen mit je fünf Jägern. Töte die ganze Gruppe, um fortzufahren; doppelte Zeit.
Frühling|Frühlingslandschaft nur für diesen Raid.
Sommer|Sommerlandschaft nur für diesen Raid.
Herbst|Herbstfarben nur für diesen Raid.
Winter|Winterlicher Schnee auf unterstützten Karten, nur für diesen Raid.
Falsche Tür|Eine offene Tür kann dich zu einer anderen Tür auf der Karte teleportieren.
Verfallene Infrastruktur|Das Stromnetz flackert. Stromausfälle legen elektrische Knöpfe und Schalter vorübergehend still.
Normal|Ein normaler Raid ohne Raid-Roulette-Ereignis.''', common('''Raid-Ereignis: {0}
Alles geregelt.
Wir sind fertig.
Dann ein anderes Mal.
Ich übernehme den Auftrag.
Der Raid ist vorbei.
Besiege alle Wellen.
Spiele einen Raid.
Raid Roulette aktivieren
Ereignisaktivierung beim Händler
Raid-Voreinstellung
Aktuelle Chancen
Chance auf normalen Raid (%)
Eigene Ereignismischung
Alle aktivieren
Alle deaktivieren
Gleiche Gewichtung
Durchlauf beenden
Ereignis vor dem Raid anzeigen
Ereignis nach dem Raid anzeigen
Meist normal
Halb und halb
Benutzerdefiniert
Ereignis
Hast du das gehört?
....du bist nicht tot
du gewinnst'''))
spanish = '''Cacería del jefe|Un jefe aleatorio te persigue para matarte.
Convención de jefes|Varios jefes y sus guardias se reúnen en el mapa.
Botín bajo llave|Cada habitación cerrada con llave contiene entre uno y tres objetos valiosos.
Asalto de Raiders|Los Raiders sustituyen a los enemigos habituales.
Invasión de sectarios|Los sectarios dominan la incursión. Cuidado con sus cuchillos.
Concentración de Scavs|Los Scavs recorren el mapa en grupos.
Barrido de PMC|Grupos USEC y BEAR sustituyen a los enemigos habituales.
A punta de pistola|Los Scavs y PMC llevan pistolas y granadas cegadoras.
Escopetazo|Los Scavs y PMC llevan escopetas y granadas cegadoras.
Tirador|Los Scavs y PMC llevan fusiles de francotirador.
Scavs explosivos|Los Scavs llevan pistolas y lanzagranadas.
Brote|Siguen llegando infectados durante la incursión.
Bien equipados|Los enemigos llevan armas potentes, blindaje y munición peligrosa.
Custodiado|Los guardias de los jefes sustituyen a los enemigos habituales.
Sobrecargados|Los Scavs y PMC llevan cargadores de gran capacidad.
Fallo en Matrix|Grupos de combatientes llevan la misma ropa.
Sin lugar seguro|Todos los combatientes, incluidos los jefes, rastrean tu posición.
Impostores por todas partes|Combatientes normales llevan el equipo de Killa.
Secta falsa|Los enemigos visten como sectarios, pero conservan su estilo de combate.
Desbordados|Llegan refuerzos cuando disminuye la población de combatientes.
Guerra de clones|Los PMC copian tu aspecto y tu equipo inicial.
El ladrón|Un ladrón con gorro navideño lleva suministros, dinero y a veces bitcoins.
Sala de fumadores|Los enemigos lanzan humo durante toda la incursión.
Distorsión temporal|El día y la noche pasan rápidamente. El temporizador sigue normal.
Esquizofrenia|Voces, figuras y visiones imposibles alteran la incursión.
Juego de armas|Cada baja válida da una nueva arma. Gana un bitcoin; tus armas originales vuelven por correo.
Juego de armas [Reapariciones]|Tras caer, reapareces sano con pistola. Se reinician la progresión y el bonus de cuchillo.
Marea de tormenta|Oscurece en cinco minutos. La lluvia intensa oculta sonidos y los rayos iluminan la noche.
AK50|Todos llevan un AK-50, incluido tú. Tus armas originales vuelven por correo.
Granadero|Los Scavs y PMC lanzan muchas granadas explosivas.
Jornada de laboratorio|Trabajadores desarmados recorren las oficinas. Salas abiertas; matar a tres trabajadores atrae Raiders y cuesta experiencia.
Día azul|Una sirena avisa de bombardeos de mortero imprevisibles. Busca un techo.
Prueba Bola de nieve|Los jefes te cazan uno por uno. Una vida, tiempo doble y un desafío oculto.
Pruebas|Diez oleadas de cinco cazadores cada vez más fuertes. La última lleva un bitcoin; tiempo doble.
Pruebas extremas|Diez oleadas de cinco jefes idénticos. Un miembro de cada oleada lleva diez bitcoins; tiempo doble.
Prueba de guardias|Combate contra grupos de cinco guardias y un grupo mixto final. Tiempo doble.
Prueba total|Veinte oleadas de cinco cazadores. Elimina cada grupo para avanzar; tiempo doble.
Primavera|Paisaje primaveral solo para esta incursión.
Verano|Paisaje veraniego solo para esta incursión.
Otoño|Colores otoñales solo para esta incursión.
Invierno|Nieve en los mapas compatibles, solo para esta incursión.
Puerta equivocada|Cruzar una puerta abierta puede teletransportarte a otra puerta del mapa.
Infraestructura en ruinas|La red eléctrica falla. Los apagones bloquean temporalmente botones e interruptores eléctricos.
Normal|Una incursión normal sin evento de Raid Roulette.'''
spanish_text = common('''Evento de incursión: {0}
Está arreglado.
Hemos terminado.
En otra ocasión.
Acepto el encargo.
La incursión terminó.
Supera todas las oleadas.
Juega una incursión.
Activar Raid Roulette
Activación por comerciante
Preajuste de incursiones
Probabilidades actuales
Probabilidad de incursión normal (%)
Mezcla personalizada de eventos
Activar todos
Desactivar todos
Pesos iguales
Terminar partida
Mostrar evento antes de la incursión
Mostrar evento después de la incursión
Mayormente normal
Mitad y mitad
Personalizado
Evento
¿Has oído eso?
....no estás muerto
ganaste''')
write('es', spanish, spanish_text)
# Shared Spanish terminology, with regional wording for the Latin American bank.
write('es-mx', spanish.replace('fusiles', 'rifles').replace('gorro', 'gorro'),
      {key: value.replace('¿Has oído eso?', '¿Oíste eso?') for key, value in spanish_text.items()})

write('it', '''Caccia del boss|Un boss casuale ti dà la caccia.
Raduno dei boss|Vari boss e le loro guardie si riuniscono sulla mappa.
Bottino sotto chiave|Ogni stanza chiusa a chiave contiene da uno a tre oggetti preziosi.
Assalto dei Raider|I Raider sostituiscono i nemici normali.
Invasione dei cultisti|I cultisti invadono il raid. Attento ai loro coltelli.
Raduno di Scav|Gli Scav si spostano in gruppi.
Rastrellamento PMC|Gruppi USEC e BEAR sostituiscono i nemici normali.
Mano alla pistola|Scav e PMC portano pistole e granate accecanti.
Colpo di fucile|Scav e PMC portano fucili a pompa e granate accecanti.
Tiratore scelto|Scav e PMC portano fucili di precisione.
Scav esplosivi|Gli Scav portano pistole e lanciagranate.
Epidemia|Durante il raid continuano ad arrivare infetti.
Ben equipaggiati|I nemici portano armi potenti, corazze e munizioni pericolose.
Sorvegliato|Le guardie dei boss sostituiscono i nemici normali.
Sovraccarichi|Scav e PMC hanno caricatori ad alta capacità.
Errore nella matrice|Gruppi di combattenti indossano gli stessi vestiti.
Nessun posto sicuro|Tutti i combattenti, boss compresi, inseguono la tua posizione.
Impostori ovunque|Combattenti normali indossano l'equipaggiamento di Killa.
Falso culto|I nemici vestono da cultisti, ma mantengono il loro stile di combattimento.
Sopraffatti|I rinforzi sostituiscono le perdite quando restano pochi combattenti.
Guerre dei cloni|I PMC copiano il tuo aspetto e l'equipaggiamento iniziale.
Il ladro|Un ladro con cappello natalizio porta provviste, denaro e talvolta bitcoin.
Sala fumatori|I nemici lanciano fumogeni per tutto il raid.
Distorsione temporale|Giorno e notte scorrono velocemente. Il timer resta normale.
Schizofrenia|Voci, figure e visioni impossibili turbano il raid.
Gara d'armi|Ogni uccisione valida dà una nuova arma. Vinci un bitcoin; le armi originali tornano per posta.
Gara d'armi [Rinascite]|Dopo la sconfitta riparti sano con una pistola. Progressione e bonus coltello si azzerano.
Marea di tempesta|Fa buio in cinque minuti. La pioggia copre i suoni e i fulmini illuminano la notte.
AK50|Tutti ricevono un AK-50, anche tu. Le tue armi originali tornano per posta.
Granatiere|Scav e PMC lanciano molte granate esplosive.
Giornata in laboratorio|Personale disarmato si sposta fra gli uffici. Stanze aperte; tre dipendenti uccisi richiamano Raider e costano esperienza.
Giorno blu|Una sirena annuncia bombardamenti di mortaio imprevedibili. Cerca un tetto.
Prova Palla di neve|I boss ti cacciano uno alla volta. Una vita, tempo doppio e una sfida nascosta.
Prove|Dieci ondate di cinque cacciatori sempre più forti. L'ultima porta un bitcoin; tempo doppio.
Prove estreme|Dieci ondate di cinque boss identici. Un membro di ogni ondata porta dieci bitcoin; tempo doppio.
Prova delle guardie|Affronta gruppi di cinque guardie e un gruppo misto finale. Tempo doppio.
Prova totale|Venti ondate di cinque cacciatori. Elimina ogni gruppo per avanzare; tempo doppio.
Primavera|Paesaggio primaverile solo per questo raid.
Estate|Paesaggio estivo solo per questo raid.
Autunno|Colori autunnali solo per questo raid.
Inverno|Neve sulle mappe compatibili, solo per questo raid.
Porta sbagliata|Attraversare una porta aperta può teletrasportarti a un'altra porta sulla mappa.
Infrastrutture in declino|La rete elettrica cede. I blackout bloccano temporaneamente pulsanti e interruttori elettrici.
Normale|Un raid normale senza evento Raid Roulette.''', common('''Evento del raid: {0}
È tutto organizzato.
Abbiamo finito.
Un'altra volta, allora.
Accetto l'incarico.
Il raid è finito.
Completa tutte le ondate.
Gioca un raid.
Attiva Raid Roulette
Attivazione dal commerciante
Preimpostazione raid
Probabilità attuali
Probabilità raid normale (%)
Combinazione eventi personalizzata
Attiva tutti
Disattiva tutti
Pesi uguali
Termina partita
Mostra evento prima del raid
Mostra evento dopo il raid
Quasi sempre normale
Metà e metà
Personalizzato
Evento
Hai sentito?
....non sei morto
hai vinto'''))

write('po', '''Caçada do chefe|Um chefe aleatório está caçando você.
Reunião de chefes|Vários chefes e seus guardas se reúnem no mapa.
Saque trancado|Cada sala trancada com chave contém de um a três itens valiosos.
Ataque dos Raiders|Raiders substituem os inimigos habituais.
Invasão de cultistas|Cultistas invadem a incursão. Cuidado com suas facas.
Concentração de Scavs|Scavs percorrem o mapa em grupos.
Varredura de PMCs|Grupos USEC e BEAR substituem os inimigos habituais.
Na base da pistola|Scavs e PMCs usam pistolas e granadas de luz.
Hora da escopeta|Scavs e PMCs usam escopetas e granadas de luz.
Atirador|Scavs e PMCs usam rifles de precisão.
Scavs explosivos|Scavs usam pistolas e lança-granadas.
Surto|Mais infectados chegam durante a incursão.
Bem equipados|Os inimigos usam armas fortes, blindagem e munição perigosa.
Sob guarda|Guardas dos chefes substituem os inimigos habituais.
Sobrecarregados|Scavs e PMCs usam carregadores de alta capacidade.
Falha na Matrix|Grupos de combatentes usam roupas idênticas.
Nenhum lugar seguro|Todos os combatentes, incluindo chefes, seguem sua posição.
Impostores por toda parte|Combatentes comuns usam o equipamento de Killa.
Culto falso|Inimigos se vestem como cultistas, mas mantêm seu estilo de combate.
Invadidos|Reforços substituem perdas quando restam poucos combatentes.
Guerra dos clones|PMCs copiam sua aparência e seu equipamento inicial.
O ladrão|Um ladrão de gorro natalino carrega suprimentos, dinheiro e às vezes bitcoins.
Sala dos fumantes|Inimigos lançam fumaça durante toda a incursão.
Distorção temporal|Dia e noite passam rapidamente. O cronômetro continua normal.
Esquizofrenia|Vozes, figuras e visões impossíveis perturbam a incursão.
Jogo de armas|Cada eliminação válida dá uma nova arma. Vença por um bitcoin; suas armas originais voltam pelo correio.
Jogo de armas [Reaparecimentos]|Após a derrota, volte com saúde cheia e pistola. A progressão e o bônus da faca são reiniciados.
Maré de tempestade|Escurece em cinco minutos. A chuva forte abafa sons e os relâmpagos iluminam a noite.
AK50|Todos recebem um AK-50, incluindo você. Suas armas originais voltam pelo correio.
Granadeiro|Scavs e PMCs lançam muitas granadas explosivas.
Expediente no laboratório|Funcionários desarmados circulam entre escritórios. Salas abertas; matar três funcionários atrai Raiders e custa experiência.
Dia azul|Uma sirene avisa sobre morteiros imprevisíveis. Procure um teto.
Desafio Bola de neve|Chefes caçam você um por vez. Uma vida, tempo dobrado e um desafio escondido.
Desafios|Dez ondas de cinco caçadores cada vez mais fortes. A última leva um bitcoin; tempo dobrado.
Desafios extremos|Dez ondas de cinco chefes idênticos. Um membro de cada onda leva dez bitcoins; tempo dobrado.
Desafio dos guardas|Enfrente grupos de cinco guardas e uma equipe mista final. Tempo dobrado.
Desafio total|Vinte ondas de cinco caçadores. Elimine cada grupo para avançar; tempo dobrado.
Primavera|Cenário de primavera apenas nesta incursão.
Verão|Cenário de verão apenas nesta incursão.
Outono|Cores de outono apenas nesta incursão.
Inverno|Neve nos mapas compatíveis, apenas nesta incursão.
Porta errada|Atravessar uma porta aberta pode teleportar você para outra porta no mapa.
Infraestrutura em decadência|A rede elétrica falha. Apagões bloqueiam temporariamente botões e interruptores elétricos.
Normal|Uma incursão normal sem evento de Raid Roulette.''', common('''Evento da incursão: {0}
Está combinado.
Terminamos por aqui.
Outra hora, então.
Aceito a missão.
A incursão acabou.
Conclua todas as ondas.
Jogue uma incursão.
Ativar Raid Roulette
Ativação pelo comerciante
Predefinição de incursões
Probabilidades atuais
Chance de incursão normal (%)
Mistura personalizada de eventos
Ativar todos
Desativar todos
Pesos iguais
Encerrar partida
Mostrar evento antes da incursão
Mostrar evento depois da incursão
Quase sempre normal
Meio a meio
Personalizado
Evento
Você ouviu isso?
....você não morreu
você venceu'''))
write('ru', '''Охота босса|Случайный босс преследует вас.
Сходка боссов|На одной карте собираются несколько боссов с охраной.
Добыча под замком|За каждой дверью с ключом лежит от одного до трёх ценных предметов.
Штурм рейдеров|Рейдеры заменяют обычных противников.
Вторжение сектантов|Рейд захватили сектанты. Остерегайтесь их ножей.
Сбор Диких|Дикие перемещаются группами.
Зачистка ЧВК|Отряды USEC и BEAR заменяют обычных противников.
Пистолетный бой|Дикие и ЧВК носят пистолеты и светошумовые гранаты.
Дробовой залп|Дикие и ЧВК носят дробовики и светошумовые гранаты.
Снайпер|Дикие и ЧВК носят снайперские винтовки.
Взрывные Дикие|Дикие носят пистолеты и гранатомёты.
Вспышка|Заражённые продолжают прибывать в течение рейда.
Экипированы|Противники носят мощное оружие, броню и опасные патроны.
Под охраной|Охранники боссов заменяют обычных противников.
Перегруз|Дикие и ЧВК используют магазины большой ёмкости.
Сбой Матрицы|Группы бойцов одеты одинаково.
Нигде не безопасно|Все бойцы, включая боссов, преследуют вас.
Кругом подражатели|Обычные бойцы носят снаряжение Киллы.
Ложный культ|Противники одеты как сектанты, но сохраняют обычную манеру боя.
Натиск|При уменьшении числа бойцов прибывают подкрепления.
Война клонов|ЧВК копируют вашу внешность и начальное снаряжение.
Вор|Вор в новогодней шапке носит припасы, деньги и иногда биткоины.
Курилка|Противники бросают дымовые гранаты весь рейд.
Искажение времени|День и ночь быстро сменяются. Таймер рейда идёт как обычно.
Шизофрения|Голоса, фигуры и невозможные видения нарушают ход рейда.
Гонка вооружений|Каждое подходящее убийство даёт новое оружие. Победа приносит биткоин; исходное оружие вернётся почтой.
Гонка вооружений [Возрождения]|После поражения вы возвращаетесь здоровым с пистолетом. Прогресс и ножевой бонус сбрасываются.
Штормовой прилив|Через пять минут темнеет. Ливень заглушает звуки, молнии освещают ночь.
AK50|Все получают AK-50, включая вас. Исходное оружие вернётся почтой.
Гренадер|Дикие и ЧВК часто бросают боевые гранаты.
Рабочий день в лаборатории|Безоружные сотрудники ходят между офисами. Комнаты открыты; убийство трёх сотрудников вызывает рейдеров и отнимает опыт.
Синий день|Сирена предупреждает о непредсказуемых миномётных обстрелах. Ищите укрытие с крышей.
Испытание «Снежный ком»|Боссы охотятся на вас по одному. Одна жизнь, двойное время и скрытое испытание.
Испытания|Десять волн по пять всё более сильных охотников. В последней есть биткоин; время удвоено.
Экстремальные испытания|Десять волн по пять одинаковых боссов. У одного бойца каждой волны десять биткоинов; время удвоено.
Испытание охраны|Сражайтесь с пятёрками охранников и смешанной последней группой. Время удвоено.
Полное испытание|Двадцать волн по пять охотников. Для продолжения убейте всю группу; время удвоено.
Весна|Весенний пейзаж только на этот рейд.
Лето|Летний пейзаж только на этот рейд.
Осень|Осенние краски только на этот рейд.
Зима|Снег на поддерживаемых картах только на этот рейд.
Не та дверь|Проход через открытую дверь может перенести вас к другой двери на карте.
Износ инфраструктуры|Электросеть выходит из строя. Отключения временно блокируют электрические кнопки и выключатели.
Обычный|Обычный рейд без события Raid Roulette.''', common('''Событие рейда: {0}
Договорились.
Здесь мы закончили.
Тогда в другой раз.
Беру это задание.
Рейд окончен.
Пройдите все волны.
Сыграйте один рейд.
Включить Raid Roulette
Активация через торговца
Набор настроек рейда
Текущие вероятности
Шанс обычного рейда (%)
Своя смесь событий
Включить все
Выключить все
Равные веса
Завершить забег
Показать событие до рейда
Показать событие после рейда
В основном обычные
Поровну
Свои настройки
Событие
Вы это слышали?
....вы не мертвы
вы победили'''))

write('pl', '''Polowanie bossa|Losowy boss ściga cię i próbuje do ciebie dotrzeć.
Zjazd bossów|Kilku bossów i ich strażników spotyka się na mapie.
Łup pod kluczem|Każdy pokój zamknięty na klucz zawiera od jednego do trzech cennych przedmiotów.
Szturm Raiderów|Raiderzy zastępują zwykłych przeciwników.
Inwazja kultystów|Kultyści opanowali rajd. Uważaj na ich noże.
Zbiórka Scavów|Scavowie przemieszczają się grupami.
Przeczesanie PMC|Grupy USEC i BEAR zastępują zwykłych przeciwników.
Pora na pistolety|Scavowie i PMC mają pistolety oraz granaty błyskowe.
Strzał ze strzelby|Scavowie i PMC mają strzelby oraz granaty błyskowe.
Strzelec wyborowy|Scavowie i PMC mają karabiny snajperskie.
Wybuchowi Scavowie|Scavowie mają pistolety i granatniki.
Epidemia|Zarażeni przybywają przez cały rajd.
Dobrze wyposażeni|Przeciwnicy mają potężną broń, pancerze i groźną amunicję.
Pod strażą|Strażnicy bossów zastępują zwykłych przeciwników.
Przeładowani|Scavowie i PMC mają magazynki dużej pojemności.
Błąd Matrixa|Grupy bojowników noszą takie same ubrania.
Nigdzie bezpiecznie|Wszyscy bojownicy, w tym bossowie, śledzą twoją pozycję.
Wszędzie pozerzy|Zwykli bojownicy noszą wyposażenie Killi.
Fałszywy kult|Przeciwnicy wyglądają jak kultyści, ale walczą po swojemu.
Naparcie|Posiłki zastępują straty, gdy liczba bojowników spada.
Wojny klonów|PMC kopiują twój wygląd i początkowe wyposażenie.
Złodziej|Złodziej w świątecznej czapce niesie zapasy, pieniądze i czasem bitcoiny.
Palarnia|Przeciwnicy rzucają granaty dymne przez cały rajd.
Zakrzywienie czasu|Dzień i noc mijają szybko. Licznik rajdu działa normalnie.
Schizofrenia|Głosy, postacie i niemożliwe widoki zakłócają rajd.
Wyścig zbrojeń|Każde zaliczone zabójstwo daje nową broń. Wygraj bitcoin; pierwotna broń wróci pocztą.
Wyścig zbrojeń [Odrodzenia]|Po porażce wracasz zdrowy z pistoletem. Postęp i bonus noża są zerowane.
Sztormowa fala|Po pięciu minutach robi się ciemno. Ulewa tłumi dźwięki, a błyskawice rozświetlają noc.
AK50|Wszyscy dostają AK-50, także ty. Pierwotna broń wróci pocztą.
Grenadier|Scavowie i PMC często rzucają granaty wybuchowe.
Dzień pracy w laboratorium|Nieuzbrojeni pracownicy krążą między biurami. Pokoje są otwarte; zabicie trzech pracowników wzywa Raiderów i kosztuje doświadczenie.
Błękitny dzień|Syrena ostrzega przed nieprzewidywalnym ostrzałem moździerzowym. Szukaj dachu.
Próba Śnieżnej kuli|Bossowie ścigają cię pojedynczo. Jedno życie, podwójny czas i ukryte wyzwanie.
Próby|Dziesięć fal po pięciu coraz silniejszych łowców. Ostatnia niesie bitcoin; podwójny czas.
Ekstremalne próby|Dziesięć fal po pięciu identycznych bossów. Jeden członek każdej fali niesie dziesięć bitcoinów; podwójny czas.
Próba strażników|Walcz z piątkami strażników i ostatnią grupą mieszaną. Podwójny czas.
Pełna próba|Dwadzieścia fal po pięciu łowców. Zabij całą grupę, by przejść dalej; podwójny czas.
Wiosna|Wiosenny krajobraz tylko na ten rajd.
Lato|Letni krajobraz tylko na ten rajd.
Jesień|Jesienne kolory tylko na ten rajd.
Zima|Śnieg na obsługiwanych mapach tylko na ten rajd.
Złe drzwi|Przejście przez otwarte drzwi może przenieść cię do innych drzwi na mapie.
Rozpad infrastruktury|Sieć elektryczna zawodzi. Awarie czasowo blokują elektryczne przyciski i przełączniki.
Normalny|Zwykły rajd bez wydarzenia Raid Roulette.''', common('''Wydarzenie rajdu: {0}
Załatwione.
Skończyliśmy tutaj.
To innym razem.
Biorę to zlecenie.
Rajd się skończył.
Pokonaj wszystkie fale.
Rozegraj jeden rajd.
Włącz Raid Roulette
Aktywacja u handlarza
Zestaw ustawień rajdu
Aktualne szanse
Szansa normalnego rajdu (%)
Własna mieszanka wydarzeń
Włącz wszystkie
Wyłącz wszystkie
Równe wagi
Zakończ rozgrywkę
Pokaż wydarzenie przed rajdem
Pokaż wydarzenie po rajdzie
Głównie normalne
Pół na pół
Własne
Wydarzenie
Słyszałeś to?
....nie jesteś martwy
wygrywasz'''))
write('ch', '''首领追猎|一名随机首领会追踪并猎杀你。
首领集会|多名首领及其护卫聚集在同一张地图。
锁后珍宝|每个需要钥匙的房间内都有一至三件高价值物品。
掠夺者突袭|掠夺者取代普通敌人。
邪教徒入侵|邪教徒占据战局，小心他们的刀。
Scav集结|Scav会结群巡游。
PMC扫荡|USEC和BEAR小队取代普通敌人。
手枪时刻|Scav和PMC携带手枪及闪光弹。
霰弹出击|Scav和PMC携带霰弹枪及闪光弹。
神射手|Scav和PMC携带狙击步枪。
爆破Scav|Scav携带手枪和榴弹发射器。
疫情爆发|感染者会在战局中持续增援。
全副武装|敌人配备强力武器、护甲和危险弹药。
护卫当道|首领护卫取代普通敌人。
超大弹匣|Scav和PMC使用大容量弹匣。
矩阵故障|一群群战斗人员穿着相同服装。
无处安全|包括首领在内的所有战斗人员都会追踪你。
遍地冒牌货|普通战斗人员穿着Killa的装备。
伪装邪教|敌人穿着邪教徒服装，但保持原有战斗方式。
援军不绝|战斗人员减少时，增援小队会补充损失。
克隆战争|PMC复制你的外貌和初始装备。
窃贼|一个戴圣诞帽的窃贼携带物资、现金，有时还有比特币。
烟雾大厅|敌人在整场战局中不断投掷烟雾弹。
时间扭曲|昼夜飞速交替，战局倒计时保持正常。
精神分裂|声音、人影和不可能的景象扰乱战局。
枪械竞赛|每次有效击杀解锁下一把武器。完成可获比特币；原有武器通过邮件返还。
枪械竞赛[重生]|战败后以满状态和手枪重生，武器进度及刀杀奖励进度重置。
风暴潮|五分钟内天色变暗。暴雨掩盖声音，闪电照亮黑夜。
AK50|所有人包括你都获得AK-50；原有武器通过邮件返还。
掷弹兵|Scav和PMC频繁投掷大量爆炸手榴弹。
实验室工作日|无武装员工在办公室间活动。房门开放；杀死三名员工会引来掠夺者并扣除经验。
蓝色之日|警报预示无法预测的迫击炮轰击。寻找有屋顶的掩体。
雪球试炼|首领逐一追杀你。仅有一条命，战局时长翻倍，还有隐藏挑战。
试炼|十波各五名越来越强的追猎者。最后一波携带一枚比特币；时长翻倍。
极限试炼|十波各五名相同首领。每波至少一人携带十枚比特币；时长翻倍。
护卫试炼|迎战五人护卫队，最后一波为混合队伍。时长翻倍。
终极试炼|二十波，每波五名追猎者。全歼当前队伍后进入下一波；时长翻倍。
春季|仅在本场战局使用春季景观。
夏季|仅在本场战局使用夏季景观。
秋季|仅在本场战局使用秋季景观。
冬季|仅在本场战局在支持的地图上呈现积雪。
错门|穿过敞开的门可能将你传送到地图上的另一扇门。
基建衰败|电网出现故障。停电期间，电动按钮和开关暂时无法使用。
普通|没有Raid Roulette事件的普通战局。''', common('''战局事件：{0}
安排好了。
我们办完了。
那就下次吧。
我接受这个委托。
战局结束了。
清除全部波次。
进行一场战局。
启用Raid Roulette
商人事件激活
战局预设
当前概率
普通战局概率（%）
自定义事件组合
全部启用
全部禁用
均等权重
结束挑战
战局前显示事件
战局后显示事件
以普通为主
各占一半
自定义
事件
你听到了吗？
....你还没死
你赢了'''))

write('jp', '''ボスの追跡|ランダムなボスがあなたを追いかける。
ボス集会|複数のボスと護衛が同じマップに集まる。
鍵の向こうの戦利品|鍵が必要な各部屋に貴重品が1～3個ある。
レイダー襲撃|通常の敵がレイダーに置き換わる。
カルト襲来|カルトがレイドを占拠する。ナイフに注意。
Scav集結|Scavが集団で巡回する。
PMC掃討|通常の敵がUSECとBEARの部隊に置き換わる。
ハンドガン戦|ScavとPMCがハンドガンと閃光弾を携帯する。
散弾戦|ScavとPMCがショットガンと閃光弾を携帯する。
狙撃手|ScavとPMCが狙撃銃を携帯する。
爆発Scav|Scavがハンドガンとグレネードランチャーを携帯する。
感染拡大|レイド中、感染者が次々と現れる。
重装備|敵が強力な武器、防具、危険な弾薬を装備する。
護衛隊|通常の敵がボスの護衛に置き換わる。
大容量|ScavとPMCが大容量マガジンを使用する。
マトリックスの異常|戦闘員の集団が同じ服を着ている。
安全地帯なし|ボスを含むすべての戦闘員があなたを追跡する。
偽物だらけ|普通の戦闘員がKillaの装備を身につける。
偽カルト|敵はカルトの服を着るが、通常の戦い方を維持する。
増援の波|戦闘員が減ると増援が補充される。
クローン戦争|PMCがあなたの外見と初期装備をコピーする。
泥棒|サンタ帽の泥棒が物資や現金、ときにはビットコインを持ち歩く。
喫煙ロビー|敵がレイド中ずっと発煙弾を投げる。
時間の歪み|昼夜が高速で巡る。レイドの残り時間は通常どおり。
統合失調症|声、人影、ありえない光景がレイドに現れる。
ガンゲーム|有効なキルごとに次の武器を得る。完走報酬はビットコイン。元の武器は郵便で戻る。
ガンゲーム[リスポーン]|敗北後は全快し、拳銃で再出発。武器の進行とナイフボーナスはリセットされる。
嵐の潮|5分で暗くなる。豪雨が音をかき消し、稲妻が夜を照らす。
AK50|あなたを含む全員がAK-50を得る。元の武器は郵便で戻る。
擲弾兵|ScavとPMCが大量の爆発手榴弾を頻繁に投げる。
ラボ勤務日|非武装の職員がオフィス間を移動する。部屋は開放。職員を3人倒すとレイダーが来て経験値を失う。
青い日|サイレンが予測不能な迫撃砲撃を知らせる。屋根のある場所へ避難しよう。
スノーボール試練|ボスが1人ずつ追ってくる。命は1つ、制限時間は2倍。隠れた挑戦もある。
試練|強さを増す5人組が10波襲来。最終波にビットコインがある。時間は2倍。
極限試練|同じボス5人が10波襲来。各波の少なくとも1人がビットコイン10枚を持つ。時間は2倍。
護衛の試練|護衛5人組と戦い、最後は混成部隊。時間は2倍。
総合試練|5人組が20波襲来。全滅させると次の波に進む。時間は2倍。
春|このレイドだけ春の景色になる。
夏|このレイドだけ夏の景色になる。
秋|このレイドだけ秋の景色になる。
冬|対応マップでは、このレイドだけ雪景色になる。
違うドア|開いたドアを通ると、マップ上の別のドアに転送されることがある。
インフラの衰退|電力網が不安定になる。停電中は電動ボタンやスイッチが一時的に使えない。
通常|Raid Rouletteのイベントがない通常のレイド。''', common('''レイドイベント：{0}
手配は済んだ。
これで終わりだ。
またの機会にな。
その依頼を受ける。
レイドは終わった。
すべての波を倒す。
レイドを1回プレイする。
Raid Rouletteを有効化
トレーダーからイベントを開始
レイドのプリセット
現在の確率
通常レイドの確率（%）
イベント構成をカスタマイズ
すべて有効
すべて無効
重みを均等に
挑戦を終了
レイド前にイベントを表示
レイド後にイベントを表示
通常中心
半分ずつ
カスタム
イベント
今の聞こえた？
....まだ死んでいない
勝利'''))

write('kr', '''보스의 추격|무작위 보스가 당신을 추적합니다.
보스 집회|여러 보스와 경호원이 같은 지도에 모입니다.
잠긴 전리품|열쇠가 필요한 각 방에 고가품 1~3개가 있습니다.
레이더 습격|레이더가 일반 적을 대체합니다.
광신도 침공|광신도가 레이드를 점령합니다. 칼을 조심하세요.
Scav 집결|Scav가 무리 지어 돌아다닙니다.
PMC 소탕|USEC와 BEAR 부대가 일반 적을 대체합니다.
권총전|Scav와 PMC가 권총과 섬광탄을 소지합니다.
산탄총전|Scav와 PMC가 산탄총과 섬광탄을 소지합니다.
명사수|Scav와 PMC가 저격소총을 소지합니다.
폭발성 Scav|Scav가 권총과 유탄발사기를 소지합니다.
감염 확산|레이드 내내 감염자가 계속 나타납니다.
중무장|적이 강력한 무기, 방어구와 위험한 탄약을 갖춥니다.
경호 구역|보스 경호원이 일반 적을 대체합니다.
대용량|Scav와 PMC가 대용량 탄창을 사용합니다.
매트릭스 오류|전투원 무리가 같은 옷을 입습니다.
안전지대 없음|보스를 포함한 모든 전투원이 당신을 추적합니다.
사칭꾼 천지|일반 전투원이 Killa의 장비를 착용합니다.
가짜 광신도|적은 광신도 옷을 입지만 기존 전투 방식을 유지합니다.
밀려오는 증원|전투원 수가 줄면 증원 부대가 손실을 보충합니다.
클론 전쟁|PMC가 당신의 외형과 시작 장비를 복제합니다.
도둑|산타 모자를 쓴 도둑이 물자, 돈과 가끔 비트코인을 들고 다닙니다.
흡연 로비|적이 레이드 내내 연막탄을 던집니다.
시간 왜곡|낮과 밤이 빠르게 바뀝니다. 레이드 제한 시간은 그대로입니다.
조현병|목소리, 형체와 불가능한 광경이 레이드를 뒤흔듭니다.
건 게임|유효한 처치마다 새 무기를 받습니다. 완주 보상은 비트코인입니다. 원래 무기는 우편으로 돌아옵니다.
건 게임 [부활]|패배하면 완전히 회복한 상태로 권총을 들고 부활합니다. 무기 진행과 칼 보너스가 초기화됩니다.
폭풍 해일|5분 안에 어두워집니다. 폭우가 다른 소리를 덮고 번개가 밤을 밝힙니다.
AK50|당신을 포함해 모두 AK-50을 받습니다. 원래 무기는 우편으로 돌아옵니다.
척탄병|Scav와 PMC가 폭발 수류탄을 자주 던집니다.
연구소 근무일|비무장 직원이 사무실 사이를 이동합니다. 방은 열려 있습니다. 직원 3명을 죽이면 레이더가 오고 경험치를 잃습니다.
푸른 날|사이렌이 예측 불가능한 박격포 공격을 알립니다. 지붕 아래로 피하세요.
스노볼 시련|보스가 한 명씩 추격합니다. 목숨은 하나, 시간은 두 배이며 숨겨진 도전도 있습니다.
시련|점점 강해지는 5인조가 10차례 공격합니다. 마지막 부대에 비트코인이 있습니다. 시간은 두 배입니다.
극한 시련|동일한 보스 5명이 10차례 공격합니다. 매 부대의 최소 한 명이 비트코인 10개를 소지합니다. 시간은 두 배입니다.
경호원 시련|경호원 5인조와 싸우며 마지막은 혼합 부대입니다. 시간은 두 배입니다.
종합 시련|5인조가 20차례 공격합니다. 모두 처치하면 다음 부대가 옵니다. 시간은 두 배입니다.
봄|이번 레이드만 봄 풍경을 적용합니다.
여름|이번 레이드만 여름 풍경을 적용합니다.
가을|이번 레이드만 가을 풍경을 적용합니다.
겨울|지원되는 지도에서 이번 레이드만 눈 풍경을 적용합니다.
잘못된 문|열린 문을 통과하면 지도상의 다른 문으로 순간이동할 수 있습니다.
기반 시설 노후화|전력망이 불안정해집니다. 정전 중에는 전기 버튼과 스위치를 일시적으로 사용할 수 없습니다.
일반|Raid Roulette 이벤트가 없는 일반 레이드입니다.''', common('''레이드 이벤트: {0}
준비됐어.
이제 끝났군.
그럼 다음에.
그 의뢰를 받겠습니다.
레이드가 끝났습니다.
모든 부대를 처치하세요.
레이드를 한 번 진행하세요.
Raid Roulette 활성화
상인을 통한 이벤트 활성화
레이드 사전 설정
현재 확률
일반 레이드 확률 (%)
이벤트 조합 사용자 지정
모두 활성화
모두 비활성화
동일 가중치
도전 종료
레이드 전에 이벤트 표시
레이드 후에 이벤트 표시
대부분 일반
반반
사용자 지정
이벤트
방금 들었어?
....아직 안 죽었어
승리'''))
write('cz', '''Lov bosse|Náhodný boss vás pronásleduje.
Sraz bossů|Na mapě se sejde několik bossů a jejich stráže.
Kořist pod zámkem|Každá místnost vyžadující klíč obsahuje jeden až tři cenné předměty.
Útok Raiderů|Raideři nahradí běžné nepřátele.
Invaze kultistů|Nájezd ovládli kultisté. Pozor na jejich nože.
Shromáždění Scavů|Scavové se pohybují ve skupinách.
Zátah PMC|Skupiny USEC a BEAR nahradí běžné nepřátele.
Pistolníci|Scavové a PMC mají pistole a oslepující granáty.
Broková rána|Scavové a PMC mají brokovnice a oslepující granáty.
Odstřelovač|Scavové a PMC mají odstřelovací pušky.
Výbušní Scavové|Scavové mají pistole a granátomety.
Nákaza|Během nájezdu stále přicházejí nakažení.
Dobře vybavení|Nepřátelé mají silné zbraně, pancíře a nebezpečnou munici.
Pod stráží|Stráže bossů nahradí běžné nepřátele.
Přetížení|Scavové a PMC mají velkokapacitní zásobníky.
Chyba Matrixu|Skupiny bojovníků nosí stejné oblečení.
Nikde není bezpečno|Všichni bojovníci včetně bossů sledují vaši polohu.
Samí napodobitelé|Běžní bojovníci nosí Killovu výstroj.
Falešný kult|Nepřátelé vypadají jako kultisté, ale bojují obvyklým způsobem.
Přesila|Když počet bojovníků klesne, ztráty doplní posily.
Válka klonů|PMC kopírují váš vzhled a počáteční výstroj.
Zloděj|Zloděj ve vánoční čepici nese zásoby, peníze a někdy bitcoiny.
Kuřácká lobby|Nepřátelé po celý nájezd házejí kouřové granáty.
Časová deformace|Den a noc rychle ubíhají. Odpočet nájezdu zůstává normální.
Schizofrenie|Nájezd narušují hlasy, postavy a nemožné výjevy.
Závod ve zbraních|Každé platné zabití přinese další zbraň. Vyhrajte bitcoin; původní zbraně se vrátí poštou.
Závod ve zbraních [Oživení]|Po porážce se vrátíte zdraví s pistolí. Postup i bonus za nůž se vynulují.
Bouřlivý příliv|Do pěti minut se setmí. Liják přehluší zvuky a blesky osvítí noc.
AK50|Všichni včetně vás dostanou AK-50. Původní zbraně se vrátí poštou.
Granátník|Scavové a PMC často házejí výbušné granáty.
Pracovní den v laboratoři|Neozbrojení zaměstnanci se pohybují mezi kancelářemi. Místnosti jsou otevřené; zabití tří zaměstnanců přivolá Raidery a stojí zkušenosti.
Modrý den|Siréna varuje před nepředvídatelnou minometnou palbou. Najděte střechu.
Sněhová zkouška|Bossové vás loví jeden po druhém. Jeden život, dvojnásobný čas a skrytá výzva.
Zkoušky|Deset vln pěti stále silnějších lovců. Poslední vlna má bitcoin; dvojnásobný čas.
Extrémní zkoušky|Deset vln pěti stejných bossů. Jeden člen každé vlny má deset bitcoinů; dvojnásobný čas.
Zkouška stráží|Bojujte s pětičlennými skupinami stráží a závěrečnou smíšenou skupinou. Dvojnásobný čas.
Úplná zkouška|Dvacet vln pěti lovců. Pro postup zabijte celou skupinu; dvojnásobný čas.
Jaro|Jarní krajina pouze pro tento nájezd.
Léto|Letní krajina pouze pro tento nájezd.
Podzim|Podzimní barvy pouze pro tento nájezd.
Zima|Sníh na podporovaných mapách pouze pro tento nájezd.
Špatné dveře|Průchod otevřenými dveřmi vás může teleportovat k jiným dveřím na mapě.
Úpadek infrastruktury|Elektrická síť selhává. Výpadky dočasně blokují elektrická tlačítka a vypínače.
Normální|Běžný nájezd bez události Raid Roulette.''', common('''Událost nájezdu: {0}
Je to zařízené.
Tady jsme skončili.
Tak někdy jindy.
Beru tu zakázku.
Nájezd skončil.
Dokončete všechny vlny.
Odehrajte jeden nájezd.
Zapnout Raid Roulette
Aktivace u obchodníka
Předvolba nájezdů
Aktuální šance
Šance na normální nájezd (%)
Vlastní směs událostí
Zapnout vše
Vypnout vše
Stejné váhy
Ukončit pokus
Zobrazit událost před nájezdem
Zobrazit událost po nájezdu
Většinou normální
Půl na půl
Vlastní
Událost
Slyšeli jste to?
....nejste mrtví
vítězství'''))

write('sk', '''Lov bossa|Náhodný boss vás prenasleduje.
Zraz bossov|Na mape sa stretne niekoľko bossov a ich stráže.
Korisť pod zámkom|Každá miestnosť vyžadujúca kľúč obsahuje jeden až tri cenné predmety.
Útok Raiderov|Raideri nahradia bežných nepriateľov.
Invázia kultistov|Nájazd ovládli kultisti. Pozor na ich nože.
Zhromaždenie Scavov|Scavovia sa pohybujú v skupinách.
Záťah PMC|Skupiny USEC a BEAR nahradia bežných nepriateľov.
Pištoliari|Scavovia a PMC majú pištole a oslepujúce granáty.
Brokový výstrel|Scavovia a PMC majú brokovnice a oslepujúce granáty.
Ostreľovač|Scavovia a PMC majú ostreľovacie pušky.
Výbušní Scavovia|Scavovia majú pištole a granátomety.
Nákaza|Počas nájazdu stále prichádzajú nakazení.
Dobre vybavení|Nepriatelia majú silné zbrane, panciere a nebezpečnú muníciu.
Pod strážou|Stráže bossov nahradia bežných nepriateľov.
Preťažení|Scavovia a PMC majú veľkokapacitné zásobníky.
Chyba Matrixu|Skupiny bojovníkov nosia rovnaké oblečenie.
Nikde nie je bezpečne|Všetci bojovníci vrátane bossov sledujú vašu polohu.
Samí napodobňovatelia|Bežní bojovníci nosia Killovu výstroj.
Falošný kult|Nepriatelia vyzerajú ako kultisti, ale bojujú obvyklým spôsobom.
Presila|Keď počet bojovníkov klesne, straty doplnia posily.
Vojna klonov|PMC kopírujú váš vzhľad a počiatočnú výstroj.
Zlodej|Zlodej vo vianočnej čiapke nesie zásoby, peniaze a niekedy bitcoiny.
Fajčiarska lobby|Nepriatelia počas celého nájazdu hádžu dymové granáty.
Časová deformácia|Deň a noc rýchlo ubiehajú. Odpočet nájazdu zostáva normálny.
Schizofrénia|Nájazd narúšajú hlasy, postavy a nemožné výjavy.
Preteky v zbraniach|Každé platné zabitie prinesie ďalšiu zbraň. Vyhrajte bitcoin; pôvodné zbrane sa vrátia poštou.
Preteky v zbraniach [Oživenie]|Po porážke sa vrátite zdraví s pištoľou. Postup aj bonus za nôž sa vynulujú.
Búrlivý príliv|Do piatich minút sa zotmie. Lejak prehluší zvuky a blesky osvietia noc.
AK50|Všetci vrátane vás dostanú AK-50. Pôvodné zbrane sa vrátia poštou.
Granátnik|Scavovia a PMC často hádžu výbušné granáty.
Pracovný deň v laboratóriu|Neozbrojení zamestnanci sa pohybujú medzi kanceláriami. Miestnosti sú otvorené; zabitie troch zamestnancov privolá Raiderov a stojí skúsenosti.
Modrý deň|Siréna varuje pred nepredvídateľnou mínometnou paľbou. Nájdite strechu.
Snehová skúška|Bossovia vás lovia jeden po druhom. Jeden život, dvojnásobný čas a skrytá výzva.
Skúšky|Desať vĺn piatich čoraz silnejších lovcov. Posledná vlna má bitcoin; dvojnásobný čas.
Extrémne skúšky|Desať vĺn piatich rovnakých bossov. Jeden člen každej vlny má desať bitcoinov; dvojnásobný čas.
Skúška stráží|Bojujte s päťčlennými skupinami stráží a záverečnou zmiešanou skupinou. Dvojnásobný čas.
Úplná skúška|Dvadsať vĺn piatich lovcov. Na postup zabite celú skupinu; dvojnásobný čas.
Jar|Jarná krajina iba pre tento nájazd.
Leto|Letná krajina iba pre tento nájazd.
Jeseň|Jesenné farby iba pre tento nájazd.
Zima|Sneh na podporovaných mapách iba pre tento nájazd.
Nesprávne dvere|Prechod otvorenými dverami vás môže teleportovať k iným dverám na mape.
Úpadok infraštruktúry|Elektrická sieť zlyháva. Výpadky dočasne blokujú elektrické tlačidlá a vypínače.
Normálny|Bežný nájazd bez udalosti Raid Roulette.''', common('''Udalosť nájazdu: {0}
Je to vybavené.
Tu sme skončili.
Tak niekedy inokedy.
Beriem tú zákazku.
Nájazd sa skončil.
Dokončite všetky vlny.
Odohrajte jeden nájazd.
Zapnúť Raid Roulette
Aktivácia u obchodníka
Predvoľba nájazdov
Aktuálne šance
Šanca na normálny nájazd (%)
Vlastná zmes udalostí
Zapnúť všetko
Vypnúť všetko
Rovnaké váhy
Ukončiť pokus
Zobraziť udalosť pred nájazdom
Zobraziť udalosť po nájazde
Väčšinou normálne
Pol na pol
Vlastné
Udalosť
Počuli ste to?
....nie ste mŕtvi
víťazstvo'''))
write('hu', '''Főnökvadászat|Egy véletlenszerű főnök üldöz téged.
Főnöktalálkozó|Több főnök és őreik gyűlnek össze a pályán.
Zárt zsákmány|Minden kulccsal zárt szobában egy–három értékes tárgy található.
Raider-roham|Raiderek váltják fel a szokásos ellenfeleket.
Kultistainvázió|Kultisták lepik el a rajtaütést. Vigyázz a késeikre.
Scav-gyülekező|A Scavek csoportosan járőröznek.
PMC-tisztogatás|USEC- és BEAR-csoportok váltják fel a szokásos ellenfeleket.
Pisztolyra fel|A Scavek és PMC-k pisztolyt és villanógránátot visznek.
Sörétes idő|A Scavek és PMC-k sörétest és villanógránátot visznek.
Mesterlövész|A Scavek és PMC-k mesterlövészpuskát visznek.
Robbanékony Scavek|A Scavek pisztolyt és gránátvetőt visznek.
Járvány|A rajtaütés során újabb fertőzöttek érkeznek.
Jól felszerelve|Az ellenfelek erős fegyvert, páncélt és veszélyes lőszert kapnak.
Őrizet alatt|Főnökőrök váltják fel a szokásos ellenfeleket.
Túltöltve|A Scavek és PMC-k nagy kapacitású tárakat használnak.
Mátrixhiba|Harcoscsoportok ugyanolyan ruhát viselnek.
Nincs biztonságos hely|Minden harcos, a főnökök is, követi a helyzetedet.
Mindenütt utánzók|Közönséges harcosok viselik Killa felszerelését.
Hamis kultusz|Az ellenfelek kultistának öltöznek, de a megszokott módon harcolnak.
Túlerő|Ha csökken a harcosok száma, erősítés pótolja a veszteségeket.
Klónháború|A PMC-k lemásolják a külsődet és kezdőfelszerelésedet.
A tolvaj|Egy mikulássapkás tolvaj készleteket, pénzt és néha bitcoint cipel.
Dohányzólobbi|Az ellenfelek a rajtaütés végéig füstgránátokat dobálnak.
Időtorzulás|Gyorsan váltakozik nappal és éjszaka. A visszaszámlálás normális marad.
Skizofrénia|Hangok, alakok és lehetetlen látomások zavarják meg a rajtaütést.
Fegyververseny|Minden érvényes ölés új fegyvert ad. A győzelem bitcoin; eredeti fegyvereid postán visszajönnek.
Fegyververseny [Újjáéledés]|Vereség után teljes életerővel és pisztollyal térsz vissza. A haladás és a késbónusz nullázódik.
Viharhullám|Öt percen belül besötétedik. A zápor elnyomja a hangokat, villámok világítanak.
AK50|Mindenki AK-50-et kap, te is. Eredeti fegyvereid postán visszajönnek.
Gránátos|A Scavek és PMC-k sok robbanógránátot dobnak.
Laboratóriumi munkanap|Fegyvertelen dolgozók járnak az irodák között. A szobák nyitva; három dolgozó megölése Raidert hív és tapasztalatba kerül.
Kék nap|Sziréna jelzi a kiszámíthatatlan aknavetőtüzet. Keress tetőt.
Hógolyópróba|A főnökök egyenként vadásznak rád. Egy élet, dupla idő és rejtett kihívás.
Próbák|Tíz hullám, egyenként öt erősödő vadásszal. Az utolsó hullámban bitcoin van; dupla idő.
Extrém próbák|Tíz hullám, egyenként öt azonos főnökkel. Minden hullám egy tagjánál tíz bitcoin van; dupla idő.
Őrpróba|Ötfős őrcsoportok és egy végső vegyes csapat ellen harcolsz. Dupla idő.
Teljes próba|Húsz hullám, egyenként öt vadásszal. A következőhöz mindenkit ölj meg; dupla idő.
Tavasz|Tavaszi táj csak erre a rajtaütésre.
Nyár|Nyári táj csak erre a rajtaütésre.
Ősz|Őszi színek csak erre a rajtaütésre.
Tél|Havas táj a támogatott pályákon, csak erre a rajtaütésre.
Rossz ajtó|Egy nyitott ajtón áthaladva a pálya másik ajtajához teleportálhatsz.
Infrastruktúra-hanyatlás|Az elektromos hálózat akadozik. Áramszünetkor a villamos gombok és kapcsolók átmenetileg nem működnek.
Normál|Normál rajtaütés Raid Roulette-esemény nélkül.''', common('''Rajtaütési esemény: {0}
Elintézve.
Itt végeztünk.
Akkor máskor.
Elvállalom a megbízást.
A rajtaütés véget ért.
Teljesíts minden hullámot.
Játssz egy rajtaütést.
Raid Roulette engedélyezése
Kereskedői eseményindítás
Rajtaütési előbeállítás
Aktuális esélyek
Normál rajtaütés esélye (%)
Egyéni eseménykeverék
Mind engedélyezése
Mind letiltása
Egyenlő súlyok
Menet befejezése
Esemény megjelenítése rajtaütés előtt
Esemény megjelenítése rajtaütés után
Többnyire normál
Fele-fele
Egyéni
Esemény
Hallottad ezt?
....nem vagy halott
győztél'''))

write('ro', '''Vânătoarea șefului|Un șef aleatoriu te urmărește.
Convenția șefilor|Mai mulți șefi și gărzile lor se adună pe hartă.
Pradă sub cheie|Fiecare cameră încuiată cu cheie conține între unu și trei obiecte valoroase.
Asaltul Raiderilor|Raiderii înlocuiesc adversarii obișnuiți.
Invazia cultiștilor|Cultiștii invadează raidul. Ferește-te de cuțitele lor.
Adunarea Scavilor|Scavii se deplasează în grupuri.
Curățenie PMC|Grupuri USEC și BEAR înlocuiesc adversarii obișnuiți.
La pistol|Scavii și PMC-urile au pistoale și grenade orbitoare.
Foc de alice|Scavii și PMC-urile au puști cu alice și grenade orbitoare.
Lunetist|Scavii și PMC-urile au puști cu lunetă.
Scavi explozivi|Scavii au pistoale și lansatoare de grenade.
Epidemie|Infectații continuă să sosească pe tot parcursul raidului.
Bine echipați|Adversarii au arme puternice, armură și muniție periculoasă.
Sub pază|Gărzile șefilor înlocuiesc adversarii obișnuiți.
Supraîncărcați|Scavii și PMC-urile au încărcătoare de mare capacitate.
Eroare în Matrix|Grupuri de luptători poartă aceleași haine.
Niciun loc sigur|Toți luptătorii, inclusiv șefii, îți urmăresc poziția.
Impostori peste tot|Luptători obișnuiți poartă echipamentul lui Killa.
Cult fals|Adversarii se îmbracă precum cultiștii, dar își păstrează stilul de luptă.
Copleșit|Întăririle înlocuiesc pierderile când scade numărul luptătorilor.
Războiul clonelor|PMC-urile îți copiază aspectul și echipamentul inițial.
Hoțul|Un hoț cu căciulă de Crăciun transportă provizii, bani și uneori bitcoini.
Salonul fumătorilor|Adversarii aruncă grenade fumigene pe tot parcursul raidului.
Deformarea timpului|Ziua și noaptea trec rapid. Cronometrul raidului rămâne normal.
Schizofrenie|Voci, siluete și viziuni imposibile tulbură raidul.
Cursa armelor|Fiecare eliminare validă oferă altă armă. Câștigă un bitcoin; armele originale revin prin poștă.
Cursa armelor [Reînvieri]|După înfrângere revii sănătos, cu pistol. Progresul și bonusul cuțitului se resetează.
Maree de furtună|Se întunecă în cinci minute. Ploaia puternică acoperă sunetele, iar fulgerele luminează noaptea.
AK50|Toată lumea primește un AK-50, inclusiv tu. Armele originale revin prin poștă.
Grenadier|Scavii și PMC-urile aruncă frecvent grenade explozive.
Zi de lucru în laborator|Angajați neînarmați circulă între birouri. Camere deschise; uciderea a trei angajați atrage Raideri și costă experiență.
Zi albastră|O sirenă anunță bombardamente imprevizibile de mortier. Caută un acoperiș.
Proba Bulgărelui|Șefii te vânează pe rând. O viață, timp dublu și o provocare ascunsă.
Probe|Zece valuri de câte cinci vânători tot mai puternici. Ultimul are un bitcoin; timp dublu.
Probe extreme|Zece valuri de câte cinci șefi identici. Un membru al fiecărui val are zece bitcoini; timp dublu.
Proba gărzilor|Înfruntă grupuri de cinci gărzi și un grup mixt final. Timp dublu.
Proba totală|Douăzeci de valuri de câte cinci vânători. Elimină tot grupul pentru a continua; timp dublu.
Primăvară|Peisaj de primăvară doar pentru acest raid.
Vară|Peisaj de vară doar pentru acest raid.
Toamnă|Culori de toamnă doar pentru acest raid.
Iarnă|Zăpadă pe hărțile compatibile doar pentru acest raid.
Ușa greșită|Trecerea printr-o ușă deschisă te poate teleporta la altă ușă de pe hartă.
Degradarea infrastructurii|Rețeaua electrică cedează. Penele blochează temporar butoanele și întrerupătoarele electrice.
Normal|Un raid normal fără eveniment Raid Roulette.''', common('''Eveniment de raid: {0}
S-a aranjat.
Am terminat aici.
Atunci altă dată.
Accept misiunea.
Raidul s-a terminat.
Învinge toate valurile.
Joacă un raid.
Activează Raid Roulette
Activare prin comerciant
Presetare raid
Probabilități actuale
Șansa unui raid normal (%)
Combinație personalizată de evenimente
Activează toate
Dezactivează toate
Ponderi egale
Încheie partida
Arată evenimentul înainte de raid
Arată evenimentul după raid
În mare parte normal
Jumătate-jumătate
Personalizat
Eveniment
Ai auzit asta?
....nu ești mort
ai câștigat'''))

write('tu', '''Boss Takibi|Rastgele bir boss seni avlar.
Boss Toplantısı|Birden fazla boss ve muhafızları aynı haritada toplanır.
Kilitli Ganimet|Anahtar gerektiren her odada bir ila üç değerli eşya bulunur.
Raider Saldırısı|Raider'lar normal düşmanların yerini alır.
Tarikatçı İstilası|Tarikatçılar baskını ele geçirir. Bıçaklarına dikkat et.
Scav Toplanması|Scav'ler gruplar halinde dolaşır.
PMC Taraması|USEC ve BEAR grupları normal düşmanların yerini alır.
Tabanca Zamanı|Scav ve PMC'ler tabanca ve flaş bombası taşır.
Saçma Yağmuru|Scav ve PMC'ler pompalı tüfek ve flaş bombası taşır.
Keskin Nişancı|Scav ve PMC'ler keskin nişancı tüfeği taşır.
Patlayıcı Scav'ler|Scav'ler tabanca ve bombaatar taşır.
Salgın|Baskın boyunca enfekte düşmanlar gelmeye devam eder.
Donanımlı|Düşmanlar güçlü silah, zırh ve tehlikeli mühimmat taşır.
Muhafızlı|Boss muhafızları normal düşmanların yerini alır.
Aşırı Dolu|Scav ve PMC'ler yüksek kapasiteli şarjörler kullanır.
Matrix Hatası|Savaşçı grupları aynı kıyafetleri giyer.
Güvenli Yer Yok|Bosslar dahil tüm savaşçılar konumunu takip eder.
Her Yerde Taklitçi|Normal savaşçılar Killa'nın teçhizatını giyer.
Sahte Tarikat|Düşmanlar tarikatçı gibi giyinir ama normal savaş biçimlerini korur.
İstila|Savaşçı sayısı düşünce takviye ekipleri kayıpları tamamlar.
Klon Savaşları|PMC'ler görünüşünü ve başlangıç teçhizatını kopyalar.
Hırsız|Noel şapkalı bir hırsız erzak, para ve bazen bitcoin taşır.
Sigara Lobisi|Düşmanlar baskın boyunca sis bombası atar.
Zaman Bükülmesi|Gece ve gündüz hızla geçer. Baskın sayacı normal kalır.
Şizofreni|Sesler, siluetler ve imkânsız görüntüler baskını bozar.
Silah Oyunu|Her geçerli öldürme yeni silah verir. Kazanınca bitcoin alırsın; asıl silahların postayla döner.
Silah Oyunu [Yeniden Doğma]|Yenilgiden sonra tam sağlıkla ve tabancayla dönersin. İlerleme ve bıçak bonusu sıfırlanır.
Fırtına Dalgası|Beş dakika içinde kararır. Şiddetli yağmur sesleri örter, şimşekler geceyi aydınlatır.
AK50|Sen dahil herkes AK-50 alır. Asıl silahların postayla döner.
Bombacı|Scav ve PMC'ler sık sık patlayıcı el bombası atar.
Laboratuvarda İş Günü|Silahsız çalışanlar ofisler arasında dolaşır. Odalar açıktır; üç çalışanı öldürmek Raider çağırır ve deneyim kaybettirir.
Mavi Gün|Siren, öngörülemeyen havan saldırılarını haber verir. Çatı altına sığın.
Kartopu Sınavı|Bosslar seni sırayla avlar. Tek can, iki kat süre ve gizli bir meydan okuma.
Sınavlar|Gittikçe güçlenen beşer avcıdan on dalga. Son dalgada bitcoin vardır; süre iki katıdır.
Ekstrem Sınavlar|Beşer aynı bosstan on dalga. Her dalganın en az bir üyesinde on bitcoin vardır; süre iki katıdır.
Muhafız Sınavı|Beş kişilik muhafız ekipleri ve son karma ekiple savaş. Süre iki katıdır.
Tam Sınav|Beşer avcıdan yirmi dalga. İlerlemek için tüm ekibi öldür; süre iki katıdır.
İlkbahar|Yalnızca bu baskın için ilkbahar manzarası.
Yaz|Yalnızca bu baskın için yaz manzarası.
Sonbahar|Yalnızca bu baskın için sonbahar renkleri.
Kış|Desteklenen haritalarda yalnızca bu baskın için kar manzarası.
Yanlış Kapı|Açık bir kapıdan geçmek seni haritadaki başka bir kapıya ışınlayabilir.
Altyapı Çöküşü|Elektrik şebekesi aksar. Kesintiler elektrikli düğme ve anahtarları geçici olarak devre dışı bırakır.
Normal|Raid Roulette etkinliği olmayan normal bir baskın.''', common('''Baskın Etkinliği: {0}
Ayarlandı.
Burada işimiz bitti.
O zaman başka sefere.
Bu görevi alıyorum.
Baskın bitti.
Tüm dalgaları temizle.
Bir baskın oyna.
Raid Roulette'i etkinleştir
Tüccardan Etkinlik Başlatma
Baskın ön ayarı
Güncel olasılıklar
Normal baskın şansı (%)
Özel etkinlik karışımı
Tümünü etkinleştir
Tümünü devre dışı bırak
Eşit ağırlıklar
Koşuyu bitir
Baskından önce etkinliği göster
Baskından sonra etkinliği göster
Çoğunlukla Normal
Yarı Yarıya
Özel
Etkinlik
Bunu duydun mu?
....ölmedin
kazandın'''))

assert {path.stem for path in (ROOT / 'Locales').glob('*.json')} == {
    'en', 'ch', 'cz', 'es', 'es-mx', 'fr', 'ge', 'hu', 'it', 'jp', 'kr', 'pl', 'po', 'ro', 'ru', 'sk', 'tu'
}
print('Built 17 offline catalogs, 44 event entries in each.')
# Additional menu terms. The numbered section prefixes are presentation only.
EXTRA_KEYS = ['Random', 'Weight', 'Search events', '0. General', '2. Raid chances', '6. Raid summary',
              'Enable events • Weight controls relative frequency']
extras = {
    'fr': ['Aléatoire', 'Poids', 'Rechercher des événements', '0. Général', '2. Probabilités des raids', '6. Résumé du raid', 'Activer des événements • Le poids règle la fréquence relative'],
    'ge': ['Zufällig', 'Gewicht', 'Ereignisse suchen', '0. Allgemein', '2. Raid-Chancen', '6. Raid-Zusammenfassung', 'Ereignisse aktivieren • Gewicht bestimmt die relative Häufigkeit'],
    'es': ['Aleatorio', 'Peso', 'Buscar eventos', '0. General', '2. Probabilidades de incursión', '6. Resumen de incursión', 'Activa eventos • El peso controla la frecuencia relativa'],
    'es-mx': ['Aleatorio', 'Peso', 'Buscar eventos', '0. General', '2. Probabilidades de incursión', '6. Resumen de incursión', 'Activa eventos • El peso controla la frecuencia relativa'],
    'it': ['Casuale', 'Peso', 'Cerca eventi', '0. Generale', '2. Probabilità raid', '6. Riepilogo raid', 'Attiva eventi • Il peso controlla la frequenza relativa'],
    'po': ['Aleatório', 'Peso', 'Buscar eventos', '0. Geral', '2. Chances de incursão', '6. Resumo da incursão', 'Ative eventos • O peso controla a frequência relativa'],
    'ru': ['Случайное', 'Вес', 'Поиск событий', '0. Общие', '2. Вероятности рейдов', '6. Итоги рейда', 'Включите события • Вес задаёт относительную частоту'],
    'pl': ['Losowe', 'Waga', 'Szukaj wydarzeń', '0. Ogólne', '2. Szanse rajdów', '6. Podsumowanie rajdu', 'Włącz wydarzenia • Waga określa względną częstotliwość'],
    'ch': ['随机', '权重', '搜索事件', '0. 常规', '2. 战局概率', '6. 战局总结', '启用事件 • 权重控制相对出现频率'],
    'jp': ['ランダム', '重み', 'イベントを検索', '0. 一般', '2. レイドの確率', '6. レイドの概要', 'イベントを有効化 • 重みで相対的な頻度を調整'],
    'kr': ['무작위', '가중치', '이벤트 검색', '0. 일반', '2. 레이드 확률', '6. 레이드 요약', '이벤트 활성화 • 가중치는 상대 빈도를 조절합니다'],
    'cz': ['Náhodné', 'Váha', 'Hledat události', '0. Obecné', '2. Šance nájezdů', '6. Shrnutí nájezdu', 'Zapněte události • Váha řídí relativní četnost'],
    'sk': ['Náhodné', 'Váha', 'Hľadať udalosti', '0. Všeobecné', '2. Šance nájazdov', '6. Zhrnutie nájazdu', 'Zapnite udalosti • Váha riadi relatívnu početnosť'],
    'hu': ['Véletlen', 'Súly', 'Események keresése', '0. Általános', '2. Rajtaütési esélyek', '6. Rajtaütési összegzés', 'Események engedélyezése • A súly a relatív gyakoriságot szabályozza'],
    'ro': ['Aleatoriu', 'Pondere', 'Caută evenimente', '0. General', '2. Probabilități raid', '6. Rezumat raid', 'Activează evenimente • Ponderea controlează frecvența relativă'],
    'tu': ['Rastgele', 'Ağırlık', 'Etkinlik ara', '0. Genel', '2. Baskın olasılıkları', '6. Baskın özeti', 'Etkinlikleri aç • Ağırlık göreli sıklığı belirler']
}
for code, values in extras.items():
    path = ROOT / 'Locales' / (code + '.json')
    catalog = json.loads(path.read_text(encoding='utf-8'))
    catalog['Text'].update(zip(EXTRA_KEYS, values))
    catalog['Text']['Mostly_Normal'] = catalog['Text']['Mostly Normal']
    catalog['Text']['Half_And_Half'] = catalog['Text']['Half And Half']
    catalog['Text']['Guard Trials'] = catalog['Events']['guard_trials']['Name']
    # Existing native enum labels differ in capitalization from Fence's titles.
    catalog['Text']['I Have A Shot'] = catalog['Events']['i_have_a_shot']['Name']
    catalog['Text']['Snowball Trials'] = catalog['Events']['snowball']['Name']
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=4) + '\n', encoding='utf-8')
