# Black Friday Rush — Street Conquest (version A)

Runner mobile en portrait (Unity 6). Deux camps : **GUARDS** (vigiles, bleu) et **THIEVES** (casseurs cagoulés, rouge). Boucle :

**Carte du quartier → choix de rue → course → boss → résultat → récompenses → améliorations → carte mise à jour → course suivante.**

Tout est généré par code (personnages, décor, effets, sons, interface) : aucun asset à importer. Interface en anglais.

## Ouvrir et lancer

1. Unity Hub > ouvrir ce dossier (Unity 6000.6.7f1). La configuration (URP, scène, nom d'app, portrait) se fait toute seule ; sinon menu **Samedi Rouge > Configurer le projet**.
2. Ouvrir `Assets/SamediRouge/Scenes/SamediRouge.unity`, vue Game réglée sur un téléphone (ou Window > General > Device Simulator), **Play**.
3. Android : File > Build Profiles > Android > Run Device > Build And Run.

Au **premier lancement**, le tutoriel jouable démarre directement ; ensuite le jeu s'ouvre sur la carte. Le tutoriel se rejoue avec le bouton **TUTORIAL** de la carte.

## Commandes

| Geste | Action |
|---|---|
| Tap (n'importe où, < 0,35 s, doigt quasi immobile) | attaquer (coup sur tout le groupe devant toi ; tir de pistolet si l'ennemi est loin) |
| Glisser gauche / droite | changer de couloir |
| Glisser haut | sauter |
| Glisser bas | glisser |

Un tap et un glissé sont distingués par la distance parcourue par le doigt (≥ 2,8 % de la hauteur d'écran = glissé). Pour vérifier sur téléphone : DEBUG > **INPUT DEBUG ON**, chaque geste reconnu s'affiche au centre de l'écran.

## Tutoriel jouable (≈ 30 s)

Une consigne à la fois, geste animé, ralenti à l'approche ; l'étape se rejoue tant qu'elle n'est pas réussie (aucun cœur perdu) :

1. **SWIPE TO MOVE** : un kiosque dans ton couloir, des pièces dans le couloir libre.
2. **SWIPE UP TO JUMP** : rangée de caddies.
3. **SWIPE DOWN TO SLIDE** : panneau SALE.
4. **TAP TO ATTACK** : un voleur isolé.
5. **BUILD YOUR CREW** : trois alliés à récupérer.
6. **BEAT THE BOSS** : mini-boss (4 points de vie), puis récompense (+50 pièces la première fois) et passage à la carte.

## Carte, quartiers et rues

Deux quartiers (onglets en haut de la carte) : **DOWNTOWN** (5 rues) puis **SHOPPING DISTRICT** (3 rues), qui s'ouvre quand les 5 rues de Downtown sont conquises. L'équipement acheté est conservé d'un quartier à l'autre.

Downtown : 5 rues, chacune avec sa couleur de sol, son boss et sa difficulté. La rue conquise se remplit de la couleur du camp (animation au retour sur la carte), le compteur affiche `n/5 STREETS` et la prochaine cible clignote. Une rue conquise peut être rejouée (bonus réduit). La progression est séparée par camp.

| Rue | Boss | Nouveautés |
|---|---|---|
| 1 MALL AVENUE | lanceur de cartons | caddies, cartons |
| 2 MARKET STREET | brute (onde de choc) | panneaux SALE, clients à ne pas frapper |
| 3 STATION ROAD | chef de bande | kiosques, lanceurs, adversaires casqués (2 coups) |
| 4 HARBOR BOULEVARD | charge | barrages sur toute la largeur, plus de casqués et de blindés (3 coups) |
| 5 CITY HALL SQUARE | mascotte (onde + cartons) | tout, plus vite, grosses bandes |

### Quartier 2 — SHOPPING DISTRICT (pillage)

Rue commerçante en plein pillage : vitrines fêlées et éclats au sol, rideaux de fer à moitié baissés, gyrophares d'alarme rouges, mannequins et télés renversés, fumée. Nouveautés :

- **Matraque renforcée** (arme de la map, présentée à la première course) : +1 dégât, coup plus large (jusqu'à 6 ennemis), plus de recul, son et impact plus lourds, animation plus ample.
- **Pillards qui sortent des boutiques** et rejoignent la piste en diagonale ; **embuscades** (une bande dans chaque couloir) ; obstacles de pillage (rayonnages renversés, cartons de télés).
- **Magasin attaqué** (gyrophare rouge, pillards devant la vitrine) : tous neutralisés → **STORE SAVED! +10 étoiles** (+5 si l'un d'eux est tombé sur un Perfect Hit), le gyrophare passe au vert ; si un pillard s'échappe → STORE LOOTED.
- **Boss final BIG BAT** (côté émeutiers : INTERVENTION CHIEF), avec escorte : balayage de batte sur 2 couloirs (sauter ou changer de couloir, les alliés touchés tombent), frappe au sol (onde de choc), appel de renforts. Ses coups normaux ne font qu'1 dégât : **les Perfect Hits font les vrais dégâts**, et après chaque frappe au sol il brille en or (ouverture : chaque coup porte).

| Rue | Boss |
|---|---|
| 6 BOUTIQUE ROW | faux Père Noël (deux couloirs visés) |
| 7 JEWELRY LANE | chef de bande |
| 8 GRAND ARCADE | BIG BAT |

### Deux camps, mêmes maps, objectifs opposés

- **Police** (puissance organisée) : défend les magasins attaqués (gyrophare rouge → vert), matraque renforcée sur la map 2.
- **Émeutiers** (mobilité, nombre, chaos) : changement de couloir plus vif, chaque groupe recruté amène un complice de plus ; sur la map 2, **barrages de police** à percer (gyrophare bleu, « BARRAGE BROKEN! +10 ») et **barre de fer** qui casse les obstacles d'un tap (+1 étoile).
- **Grade** automatique selon la map : STREET COP → REINFORCED COP, STREET RIOTER → RIOTER (affiché au départ de la course ; liste dans `Tuning.RankGuard/RankThief`).
- **Perfect Hit** : fenêtre ±100 ms autour de l'atterrissage (`PerfectEarly/PerfectLate`), et 0,45 s de protection après le coup (`PerfectGuard`).

**Victoire** : battre le boss au bout de la rue → rue conquise, étoiles gagnées + bonus de victoire. **Défaite** : jauge de vie à zéro → les étoiles gagnées sont gardées (réglable), cause affichée, conseil adapté, bouton **RETRY** immédiat.

## Lisibilité : codes couleur

Contours lumineux épais et lueurs au sol, appliqués partout :
- **Rouge = danger / ennemi** : contour rouge et zone rouge au sol devant chaque obstacle ; ennemis en blouson rouge (cagoule noire) avec contour et cercle rouges ; onde de choc rouge.
- **Or = à ramasser** : étoiles, et tout l'équipement (casque, gilet, matraque, bouclier, pistolet, mitraillette) doré avec un faisceau lumineux vertical.
- **Vert = soin** : trousse verte, contour, halo et faisceau verts.
- **Bleu = allié** : recrues, escouade et renfort avec contour et cercle bleus.

## Progression entre les niveaux

Victoire qui débloque un niveau : « LEVEL 2 UNLOCKED! » et bouton **CONTINUE - LEVEL 2** ; sur la carte, animation de conquête, bannière « NEW LEVEL UNLOCKED! », puis bascule automatique sur le nouveau niveau. Le bouton principal (jaune) est toujours la suite de la progression (« CONTINUE - LEVEL n » + rue) ; les rues terminées sont grisées et le bouton REPLAY est en retrait ; l'onglet du niveau à jouer clignote « NEW! ». Au retour au menu, la carte s'ouvre sur le niveau en cours.

## Fin de course : écran RETRY = centre de progression

Boucle : course → gain animé → écran RETRY + améliorations → achat éventuel → RETRY → nouvelle course (≈ 0,75 s après la fin, sans repasser par la carte).
- La monnaie s'appelle **STREET CRED** (icône : l'étoile dorée), gardée d'une course à l'autre.
- Gain : « +N STREET CRED! » compté en ≈ 0,7 s ; pendant ce temps le solde et les jauges se remplissent, et une amélioration qui devient payable s'allume (son + vibration).
- Deux axes visibles tout de suite, achetables en un tap : **POWER** (arme : niveau, effet suivant, prix) et **CREW** (nombre d'alliés). Chaque axe a une jauge « solde / prix » ; s'il n'est pas encore payable : « ≈ 2 RUNS » (estimé avec le gain de la course) ou ce qui manque, et un tap rappelle combien il reste. Rien n'est forcé : on peut économiser.
- Échec : gros bouton **RETRY**, puis ALL UPGRADES (casque, gilet) et MAP. Victoire : **NEXT: rue suivante** (directement), ou CONTINUE - LEVEL n si un niveau vient de s'ouvrir ; REPLAY et MAP en dessous.

## Vie, soins, étoiles

- **Jauge de vie** (en haut à gauche, grande barre encadrée avec médaillon cœur, graduations et `PV / max` en chiffres) : 100 PV (+25 par niveau de gilet). Chaque coup reçu enlève 25 PV (kiosque : 50) : la barre descend, une traîne rouge montre la perte, flash rouge. Sous 30 % : battement de cœur, cadre rouge pulsant et vignette rouge. Les soins font briller la barre en vert.
- **Trousses de secours** (boîtes vertes à croix blanche) : +35 PV, halo et chiffre verts. Certaines sont placées en zone risquée (derrière une bande d'ennemis, juste après un obstacle). Le boss vaincu rend aussi de la vie.
- **Étoiles de police** (la monnaie) : étoiles dorées sur la piste, +1 par adversaire assommé (+2 côté voleur), +5 par Perfect Hit, +3 par palier de combo. Compteur en haut à droite (vraie étoile de police à 6 branches), qui saute à chaque gain.
- **Bilan de fin de course** : compteur d'étoiles animé, détail (adversaires, étoiles ramassées, Perfect Hits, combos, bonus), solde total, et les 3 améliorations les moins chères **achetables directement** avant de rejouer.
- Codes couleur : rouge = danger/dégâts, vert = soin, bleu = alliés/protection, or = étoiles/récompenses.

## Équipement permanent (écran UPGRADES)

| | Niveau 1 | Niveau 2 | Niveau 3 |
|---|---|---|---|
| **Helmet** (résistance) | bloque les cartons lancés | + panneaux SALE | + caisses du boss |
| **Vest** (points de vie) | 125 PV | 150 PV | 175 PV |
| **Baton** (dégâts) | matraque (large) | matraque lourde (2 dégâts) | matraque électrique (3 dégâts) |
| **Squad** (alliés) | 1 allié au départ | 2 alliés | 3 alliés |

Chaque niveau se voit sur le personnage (visière, crête dorée, épaulières, liserés, matraque plus grosse puis électrique), qui grandit aussi un peu avec sa puissance. Les **pickups en course restent temporaires** et utiles : plaque d'armure (encaisse un coup), bouclier (on traverse les adversaires), pistolet, mitraillette, café (+1 cœur), recrues.

## Équilibrage : modèle de difficulté déterministe (`DifficultyModel.cs`, `Balance.cs`, `BalanceConfig.cs`)

**Plans de parcours** (`Balance.cs`) : 4 variantes par rue (BALANCED, HORDES, GAUNTLET, LOOTING) à graine fixe, même décor ; la variante change à chaque tentative (« LAYOUT: … » au départ). Le jeu fait apparaître exactement le plan (vie de chaque adversaire, trousses).

**Modèle** (`DifficultyModel.cs`, C# pur, pas de simulation aléatoire) :
1. *Ligne de temps* : chaque élément devient un instant de contact (vitesse réelle `v = min(vmax, v0 + d/60)`, + vitesse des adversaires qui chargent).
2. *Demandes* : frapper (coups nécessaires = vie / dégâts, rang par rang, moins la part des alliés), sauter (0,66 s), glisser (0,72 s), changer de couloir (0,2 s).
3. *Enchaînement* sur trois canaux (bras, corps, couloir) : pression = temps nécessaire (réaction comprise) / temps disponible. Pression > 1 pour toutes les options = **séquence impossible avec cette puissance** (« unavoidable hit »). Une décision trop proche de la précédente ajoute de la pression.
4. *Score d'élément* : `d = b × (1 + k × pression^γ)` (+ pénalité si impossible). Bonus (trousse, gilet) : `b < 0` × accessibilité `α = Π(1 − r)` (couloir à changer, position risquée, danger juste après). Un renfort n'a pas de score : il change le nombre d'alliés pour les combats suivants.
5. *Agrégation non additive* : fenêtres de 2 s, `charge(w) = Σd + λ·charge(w−1)`, `D = (Σ charge^p)^(1/p)` (p = 3 : trois obstacles serrés pèsent ~9× plus que trois espacés), `score = 100 × (1 − e^(−D/D0))`.
6. *Intrinsèque* (sans amélioration) / *effectif* (équipement donné, monotone : une amélioration ne rend jamais un parcours plus dur). La vie restante n'entre jamais dans le score ; la vie max sert seulement à dire si les coups inévitables bloquent.
7. *Diagnostic* : équipement le moins cher qui rend la rue accessible, amélioration la plus utile, tentatives prévues (étoiles manquantes / étoiles par course), zones critiques horodatées.

**Calibrage** : pour chaque variante, l'intensité (densité, taille des bandes, casqués/blindés) est choisie pour que le score effectif avec l'équipement d'arrivée (`refLoadout`) soit proche de `targetScore`, avec au plus `allowedImpossible` coups inévitables, et que `accessLoadout` rende la rue accessible. Trousses : placées après les passages les plus chargés (au plus 2 par rue, aucune au début ni pendant le boss), sans simuler la vie.

**Outils** : Samedi Rouge > Balance > *Export balance.json* (tous les coefficients, modifiables sans code) et *Difficulty report* (`Balance/difficulty-report.md`). Panneau DEBUG : prévision pour la rue sélectionnée avec tes améliorations. **Vraies parties** : `balance-log.csv` (une ligne par course : variante, tentative, équipement, résultat, durée, vie à 25/50/75 %, devant le boss, trousses, étoiles, Perfect Hits) et `balance-hits.csv` (chaque coup reçu : instant, distance, cause) dans le dossier de données de l'appli, pour recaler les coefficients.

## Réglages (`Scripts/Tuning.cs`)

- Quartiers : `MapNames`, `MapFirst`, `MapCount`, `MapTheme` (décor), `MapWeaponTier` (arme). **Ajouter une map = ajouter une entrée à ces tableaux et les rues correspondantes** (la carte, les onglets et le déblocage suivent).
- Pillage : `StoreExitShare`, `AmbushShare`, `StoreAttackShare`, `StoreSaveStars`, `StoreSavePerfectBonus`, `Heavy*` (matraque renforcée).
- Rues : noms, longueur avant le boss, boss, PV du boss, vitesse de départ et plafond, taille des bandes, part d'adversaires casqués/blindés, rue d'apparition de chaque obstacle, couleurs.
- Récompenses : bonus de victoire par rue, facteur de rejeu, part conservée en défaite, récompense du tutoriel, pièces par adversaire.
- Équipement : coûts (3 × 3), textes d'effet, cœurs de base, dégâts par niveau d'arme.
- Tutoriel : vitesse, ralenti, PV du mini-boss.

Sauvegarde locale uniquement (`Scripts/Progress.cs`, PlayerPrefs).

## Debug

Bouton **DEBUG** en bas à droite de la carte : +500 pièces, remise à zéro, niveaux d'équipement, conquérir la prochaine rue / tout libérer, décalage de difficulté, lancer le tutoriel à une étape donnée, INPUT DEBUG (gestes reconnus), vibrations on/off, BOSS RUSH (boss au bout de 30 m), marquer le tutoriel fait/non fait.

## Architecture

- `Game.cs` : la course (runner, escouade, boss, effets). `GameCampaign.cs` : boucle Street Conquest (carte, résultats, améliorations, debug). `GameTutorial.cs` : tutoriel jouable.
- `Hud.cs` : interface de course. `HudMeta.cs` : écrans de méta (carte, améliorations, résultats, debug).
- `Tuning.cs`, `Progress.cs`, `Haptics.cs` : réglages, sauvegarde, vibrations courtes.

Pour tester la version B (City Wars), remplacer `GameCampaign.cs` / `HudMeta.cs` par une autre méta qui appelle les mêmes points d'entrée (`StartStreet`, `RunWon`, `ShowDefeat`).
