# Black Friday Rush - difficulty report (deterministic model V1)

Intrinsic = base player (no upgrade). Effective = with the given upgrades. 0-100: <30 easy, <55 moderate, <75 hard, <90 very hard, above = extreme. Unavoidable hit = a sequence where every option (fight, lane, jump, slide) needs more time than available. Model estimates, to be recalibrated with balance-log.csv.

## Summary

| Street | Variant | Duration | Intrinsic | Effective (arrival upgrades) | Unavoidable hits | Medkits | Cheapest accessible upgrades | Predicted attempts |
|---|---|---|---|---|---|---|---|---|
| 1 MALL AVENUE | BALANCED | 00:21 | 21 | 21 (H0 V0 W0 S0) | 0 / 5 | 0 | H0 V0 W0 S0 → 21 | 1.0 |
| 1 MALL AVENUE | HORDES | 00:21 | 25 | 25 (H0 V0 W0 S0) | 0 / 5 | 1 | H0 V0 W0 S0 → 25 | 1.0 |
| 1 MALL AVENUE | GAUNTLET | 00:21 | 25 | 25 (H0 V0 W0 S0) | 0 / 5 | 1 | H0 V0 W0 S0 → 25 | 1.0 |
| 1 MALL AVENUE | LOOTING | 00:21 | 25 | 25 (H0 V0 W0 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 25 | 1.0 |
| 2 MARKET STREET | BALANCED | 00:23 | 48 | 27 (H0 V0 W1 S0) | 0 / 5 | 1 | H0 V0 W0 S0 → 48 | 1.0 |
| 2 MARKET STREET | HORDES | 00:23 | 45 | 35 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 45 | 1.0 |
| 2 MARKET STREET | GAUNTLET | 00:23 | 61 | 33 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 61 | 1.0 |
| 2 MARKET STREET | LOOTING | 00:23 | 54 | 35 (H0 V0 W1 S0) | 0 / 5 | 1 | H0 V0 W0 S0 → 54 | 1.0 |
| 3 STATION ROAD | BALANCED | 00:26 | 54 | 43 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 54 | 1.0 |
| 3 STATION ROAD | HORDES | 00:26 | 44 | 43 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 44 | 1.0 |
| 3 STATION ROAD | GAUNTLET | 00:26 | 38 | 38 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 38 | 1.0 |
| 3 STATION ROAD | LOOTING | 00:26 | 48 | 42 (H0 V0 W1 S0) | 0 / 5 | 2 | H0 V0 W0 S0 → 48 | 1.0 |
| 4 HARBOR BOULEVARD | BALANCED | 00:28 | 58 | 48 (H1 V1 W1 S0) | 0 / 6 | 2 | H0 V0 W0 S0 → 58 | 1.0 |
| 4 HARBOR BOULEVARD | HORDES | 00:30 | 50 | 49 (H1 V1 W1 S0) | 0 / 6 | 2 | H0 V0 W0 S0 → 50 | 1.0 |
| 4 HARBOR BOULEVARD | GAUNTLET | 00:28 | 63 | 31 (H1 V1 W1 S0) | 0 / 6 | 1 | H0 V0 W0 S0 → 63 | 1.0 |
| 4 HARBOR BOULEVARD | LOOTING | 00:30 | 60 | 42 (H1 V1 W1 S0) | 0 / 6 | 1 | H0 V0 W0 S0 → 60 | 1.0 |
| 5 CITY HALL SQUARE | BALANCED | 00:31 | 56 | 43 (H1 V1 W1 S1) | 0 / 6 | 2 | H0 V0 W0 S0 → 56 | 1.0 |
| 5 CITY HALL SQUARE | HORDES | 00:31 | 75 | 54 (H1 V1 W1 S1) | 0 / 6 | 2 | H1 V0 W0 S0 → 63 | 1.0 |
| 5 CITY HALL SQUARE | GAUNTLET | 00:33 | 62 | 52 (H1 V1 W1 S1) | 0 / 6 | 2 | H0 V0 W0 S0 → 62 | 1.0 |
| 5 CITY HALL SQUARE | LOOTING | 00:31 | 77 | 53 (H1 V1 W1 S1) | 0 / 6 | 2 | H1 V0 W0 S0 → 54 | 1.0 |
| 6 BOUTIQUE ROW | BALANCED | 00:24 | 89 | 83 (H1 V1 W1 S1) | 1 / 6 | 2 | H0 V0 W2 S0 → 54 | 4.6 |
| 6 BOUTIQUE ROW | HORDES | 00:24 | 81 | 74 (H1 V1 W1 S1) | 1 / 6 | 2 | H0 V0 W1 S1 → 75 | 1.0 |
| 6 BOUTIQUE ROW | GAUNTLET | 00:24 | 100 | 81 (H1 V1 W1 S1) | 1 / 6 | 2 | H1 V0 W2 S0 → 62 | 4.8 |
| 6 BOUTIQUE ROW | LOOTING | 00:24 | 100 | 82 (H1 V1 W1 S1) | 1 / 6 | 2 | H1 V0 W2 S0 → 58 | 4.4 |
| 7 JEWELRY LANE | BALANCED | 00:25 | 96 | 73 (H1 V1 W2 S1) | 0 / 6 | 2 | H1 V0 W2 S1 → 73 | 1.0 |
| 7 JEWELRY LANE | HORDES | 00:25 | 62 | 48 (H1 V1 W2 S1) | 1 / 6 | 1 | H0 V0 W0 S0 → 62 | 1.0 |
| 7 JEWELRY LANE | GAUNTLET | 00:25 | 87 | 61 (H1 V1 W2 S1) | 1 / 6 | 2 | H1 V0 W1 S1 → 73 | 1.0 |
| 7 JEWELRY LANE | LOOTING | 00:25 | 94 | 65 (H1 V1 W2 S1) | 1 / 6 | 2 | H0 V0 W2 S0 → 67 | 1.0 |
| 8 GRAND ARCADE | BALANCED | 00:30 | 98 | 55 (H1 V1 W2 S2) | 0 / 6 | 2 | H0 V0 W2 S0 → 67 | 1.0 |
| 8 GRAND ARCADE | HORDES | 00:28 | 93 | 62 (H1 V1 W2 S2) | 0 / 6 | 2 | H0 V0 W2 S0 → 74 | 1.0 |
| 8 GRAND ARCADE | GAUNTLET | 00:30 | 96 | 36 (H1 V1 W2 S2) | 0 / 6 | 2 | H0 V0 W2 S0 → 59 | 1.0 |
| 8 GRAND ARCADE | LOOTING | 00:28 | 73 | 41 (H1 V1 W2 S2) | 0 / 6 | 2 | H0 V0 W0 S0 → 73 | 1.0 |

## DOWNTOWN - street 1 MALL AVENUE - variant A BALANCED

- Intrinsic difficulty: **21/100** (easy)
- 12 segments, 00:17 before the boss, 0 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 21/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 18/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 18/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 18/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 18/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 18/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 18/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 17/100 | easy |

Arrival upgrades (H0 V0 W0 S0): **21/100**, easy. Most useful next upgrade: baton +1: 21 → 18.

Critical zones (arrival upgrades):
- 00:09 : 3 enemies - change lane, pressure 0.37
- 00:11 : carts - change lane, pressure 0.95
- 00:12 : boxes - jump (right after the previous one), pressure 0.44
- 00:19 : boss: thrown box - change lane, pressure 0.37
- 00:21 : boss: thrown box - change lane, pressure 0.37
- Medkits: 0 (0.0 per minute, average accessibility -). Boss: 00:17.

## DOWNTOWN - street 1 MALL AVENUE - variant B HORDES

- Intrinsic difficulty: **25/100** (easy)
- 11 segments, 00:17 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 25/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 23/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 16/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 15/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 15/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 14/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 14/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 13/100 | easy |

Arrival upgrades (H0 V0 W0 S0): **25/100**, easy. Most useful next upgrade: squad +1: 25 → 17.

Critical zones (arrival upgrades):
- 00:07 : 2 enemies - change lane, pressure 0.37
- 00:08 : 3 enemies - change lane, pressure 0.44
- 00:09 : 2 enemies - change lane, pressure 0.45
- 00:11 : 1 enemy - fight (1 swings), pressure 0.55
- 00:13 : 3 enemies - fight (1 swings), pressure 0.57
- Medkits: 1 (3.6 per minute, average accessibility 42%). Boss: 00:17.

## DOWNTOWN - street 1 MALL AVENUE - variant C GAUNTLET

- Intrinsic difficulty: **25/100** (easy)
- 12 segments, 00:17 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 25/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 23/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 18/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 17/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 17/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 17/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 17/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 16/100 | easy |

Arrival upgrades (H0 V0 W0 S0): **25/100**, easy. Most useful next upgrade: squad +1: 25 → 20.

Critical zones (arrival upgrades):
- 00:04 : 3 enemies - change lane, pressure 0.37
- 00:08 : boxes - change lane, pressure 0.86
- 00:10 : 3 enemies - fight (1 swings), pressure 0.54
- 00:19 : boss: thrown box - change lane, pressure 0.37
- 00:21 : boss: thrown box - change lane, pressure 0.37
- Medkits: 1 (3.6 per minute, average accessibility 42%). Boss: 00:17.

## DOWNTOWN - street 1 MALL AVENUE - variant D LOOTING

- Intrinsic difficulty: **25/100** (easy)
- 16 segments, 00:17 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 25/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 25/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 22/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 21/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 21/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 19/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 19/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 19/100 | easy |

Arrival upgrades (H0 V0 W0 S0): **25/100**, easy. Most useful next upgrade: squad +1: 25 → 23.

Critical zones (arrival upgrades):
- 00:04 : 4 enemies - change lane, pressure 0.37
- 00:06 : 3 enemies - fight (1 swings) (right after the previous one), pressure 0.61
- 00:08 : 4 enemies - change lane (right after the previous one), pressure 0.91
- 00:10 : 4 enemies - change lane (right after the previous one), pressure 0.95
- 00:21 : boss: thrown box - change lane, pressure 0.37
- Medkits: 2 (7.2 per minute, average accessibility 64%). Boss: 00:17.

## DOWNTOWN - street 2 MARKET STREET - variant A BALANCED

- Intrinsic difficulty: **48/100** (moderate, 2 unavoidable hits)
- 17 segments, 00:19 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 48/100 | moderate, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 27/100 | easy |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 23/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 23/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 23/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 20/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 16/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 16/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **27/100**, easy. Most useful next upgrade: squad +1: 27 → 23.

Critical zones (arrival upgrades):
- 00:07 : 5 enemies - change lane (right after the previous one), pressure 0.43
- 00:08 : 3 enemies - fight (1 swings), pressure 0.32
- 00:10 : SALE signs across all lanes - slide, pressure 0.27
- 00:20 : boss: shockwave - jump, pressure 0.28
- 00:22 : boss: shockwave - jump, pressure 0.28
- Medkits: 1 (3.2 per minute, average accessibility 42%). Boss: 00:19.

## DOWNTOWN - street 2 MARKET STREET - variant B HORDES

- Intrinsic difficulty: **45/100** (moderate)
- 15 segments, 00:19 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 45/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 35/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 28/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 16/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 16/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 16/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **35/100**, moderate. Most useful next upgrade: squad +1: 35 → 28.

Critical zones (arrival upgrades):
- 00:04 : 4 enemies - change lane, pressure 0.37
- 00:06 : 4 enemies - change lane (right after the previous one), pressure 0.40
- 00:07 : 5 enemies - change lane, pressure 0.51
- 00:09 : 4 enemies - change lane (right after the previous one), pressure 0.44
- 00:20 : boss: shockwave - jump, pressure 0.28
- Medkits: 2 (6.4 per minute, average accessibility 42%). Boss: 00:19.

## DOWNTOWN - street 2 MARKET STREET - variant C GAUNTLET

- Intrinsic difficulty: **61/100** (hard, 3 unavoidable hits)
- 24 segments, 00:19 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 61/100 | hard, 3 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 33/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 31/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 31/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 31/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 29/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 25/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 25/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **33/100**, moderate. Most useful next upgrade: squad +1: 33 → 31.

Critical zones (arrival upgrades):
- 00:04 : 6 enemies - change lane, pressure 0.37
- 00:08 : 5 enemies - change lane (right after the previous one), pressure 0.92
- 00:10 : 6 enemies - change lane (right after the previous one), pressure 0.95
- 00:15 : carts - jump (right after the previous one), pressure 0.95
- 00:20 : boss: shockwave - jump, pressure 0.28
- Medkits: 2 (6.4 per minute, average accessibility 45%). Boss: 00:19.

## DOWNTOWN - street 2 MARKET STREET - variant D LOOTING

- Intrinsic difficulty: **54/100** (moderate, 1 unavoidable hit)
- 17 segments, 00:19 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 54/100 | moderate, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 35/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 28/100 | easy |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 20/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 20/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 20/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **35/100**, moderate. Most useful next upgrade: squad +1: 35 → 28.

Critical zones (arrival upgrades):
- 00:04 : 5 enemies - change lane, pressure 0.37
- 00:06 : 4 enemies - change lane (right after the previous one), pressure 0.49
- 00:07 : 3 enemies - fight (1 swings), pressure 0.32
- 00:08 : 4 enemies - change lane, pressure 0.37
- 00:10 : 5 enemies - change lane (right after the previous one), pressure 0.71
- Medkits: 1 (3.2 per minute, average accessibility 42%). Boss: 00:19.

## DOWNTOWN - street 3 STATION ROAD - variant A BALANCED

- Intrinsic difficulty: **54/100** (moderate, 2 unavoidable hits)
- 21 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 54/100 | moderate, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 43/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 38/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 25/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 23/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 23/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **43/100**, moderate. Most useful next upgrade: baton +1: 43 → 30.

Critical zones (arrival upgrades):
- 00:04 : 5 enemies (2 tough) - change lane, pressure 0.37
- 00:06 : 4 enemies (2 tough), other lane - skip, pressure 1.10
- 00:08 : kiosk - jump (right after the previous one), pressure 0.78
- 00:09 : 4 enemies (1 tough) - change lane (right after the previous one), pressure 0.61
- 00:13 : 5 enemies (2 tough) - change lane (right after the previous one), pressure 0.95
- Medkits: 2 (5.9 per minute, average accessibility 60%). Boss: 00:20.

## DOWNTOWN - street 3 STATION ROAD - variant B HORDES

- Intrinsic difficulty: **44/100** (moderate)
- 17 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 44/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 43/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 32/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 26/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 20/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 16/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 10/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 9/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **43/100**, moderate. Most useful next upgrade: baton +1: 43 → 30.

Critical zones (arrival upgrades):
- 00:04 : 5 enemies (1 tough) - change lane, pressure 0.37
- 00:06 : 4 enemies (1 tough) - change lane, pressure 0.78
- 00:10 : thrown box - change lane, pressure 0.92
- 00:13 : 4 enemies (1 tough) - change lane, pressure 0.68
- 00:24 : 3 enemies, boss backup - fight (0.7 swings) (right after the previous one), pressure 0.95
- Medkits: 2 (5.9 per minute, average accessibility 60%). Boss: 00:20.

## DOWNTOWN - street 3 STATION ROAD - variant C GAUNTLET

- Intrinsic difficulty: **38/100** (moderate)
- 17 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 38/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 38/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 33/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 25/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 23/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 18/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 15/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 13/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **38/100**, moderate. Most useful next upgrade: baton +1: 38 → 28.

Critical zones (arrival upgrades):
- 00:09 : 5 enemies (1 tough) - change lane, pressure 0.37
- 00:11 : 5 enemies (3 tough) - change lane, pressure 0.37
- 00:13 : thrown box - change lane (right after the previous one), pressure 0.74
- 00:24 : boss: thrown box - change lane, pressure 0.37
- 00:26 : boss: thrown box - change lane, pressure 0.37
- Medkits: 2 (5.9 per minute, average accessibility 64%). Boss: 00:20.

## DOWNTOWN - street 3 STATION ROAD - variant D LOOTING

- Intrinsic difficulty: **48/100** (moderate)
- 15 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 48/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 42/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 35/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 27/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 26/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 22/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 16/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 15/100 | easy |

Arrival upgrades (H0 V0 W1 S0): **42/100**, moderate. Most useful next upgrade: baton +1: 42 → 32.

Critical zones (arrival upgrades):
- 00:04 : 2 enemies (1 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:08 : 3 enemies (1 tough) - change lane, pressure 0.72
- 00:12 : 5 enemies (1 tough) - change lane, pressure 0.37
- 00:18 : thrown box - change lane, pressure 0.85
- 00:24 : 3 enemies, boss backup - fight (0.7 swings) (right after the previous one), pressure 0.95
- Medkits: 2 (5.9 per minute, average accessibility 36%). Boss: 00:20.

## DOWNTOWN - street 4 HARBOR BOULEVARD - variant A BALANCED

- Intrinsic difficulty: **58/100** (hard, 2 unavoidable hits)
- 18 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 58/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 48/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 41/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 28/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 21/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 17/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 16/100 | easy |

Arrival upgrades (H1 V1 W1 S0): **48/100**, moderate. Most useful next upgrade: baton +1: 48 → 34.

Critical zones (arrival upgrades):
- 00:04 : 6 enemies (5 tough) - change lane, pressure 0.37
- 00:07 : 4 enemies (2 tough), other lane - skip, pressure 1.10
- 00:09 : 6 enemies (3 tough) - change lane, pressure 0.37
- 00:12 : 4 enemies (2 tough) - change lane, pressure 0.67
- 00:13 : 5 enemies (2 tough) - change lane, pressure 0.52
- Medkits: 2 (5.5 per minute, average accessibility 51%). Boss: 00:22.

## DOWNTOWN - street 4 HARBOR BOULEVARD - variant B HORDES

- Intrinsic difficulty: **50/100** (moderate)
- 16 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 50/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 50/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 44/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 25/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 24/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 15/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 14/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 10/100 | easy |

Arrival upgrades (H1 V1 W1 S0): **49/100**, moderate. Most useful next upgrade: baton +1: 49 → 28.

Critical zones (arrival upgrades):
- 00:04 : 5 enemies (3 tough) - change lane, pressure 0.37
- 00:07 : 5 enemies (2 tough) - change lane, pressure 0.62
- 00:09 : 4 enemies (3 tough) - change lane, pressure 0.37
- 00:10 : 3 enemies (1 tough) - change lane, pressure 0.43
- 00:11 : 4 enemies - change lane, pressure 0.44
- Medkits: 2 (5.5 per minute, average accessibility 64%). Boss: 00:22.

## DOWNTOWN - street 4 HARBOR BOULEVARD - variant C GAUNTLET

- Intrinsic difficulty: **63/100** (hard, 1 unavoidable hit)
- 17 segments, 00:22 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 63/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 50/100 | moderate, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 33/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 27/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 20/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 19/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 13/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 13/100 | easy |

Arrival upgrades (H1 V1 W1 S0): **31/100**, moderate. Most useful next upgrade: baton +1: 31 → 22.

Critical zones (arrival upgrades):
- 00:04 : 2 enemies (1 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:09 : 3 enemies (2 tough), other lane - skip, pressure 1.23
- 00:12 : 3 enemies (1 tough) - change lane, pressure 0.49
- 00:24 : boss: charge - change lane, pressure 0.37
- 00:28 : boss: charge - change lane, pressure 0.37
- Medkits: 1 (2.7 per minute, average accessibility 42%). Boss: 00:22.

## DOWNTOWN - street 4 HARBOR BOULEVARD - variant D LOOTING

- Intrinsic difficulty: **60/100** (hard, 2 unavoidable hits)
- 20 segments, 00:22 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 60/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 59/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 55/100 | moderate, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 51/100 | moderate, 2 unavoidable hits |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 25/100 | easy |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 21/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 16/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 15/100 | easy |

Arrival upgrades (H1 V1 W1 S0): **42/100**, moderate. Most useful next upgrade: baton +1: 42 → 30.

Critical zones (arrival upgrades):
- 00:04 : 3 enemies (1 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:06 : 3 enemies (2 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:09 : 4 enemies (1 tough) - change lane, pressure 0.37
- 00:10 : 5 enemies (2 tough) - change lane, pressure 0.52
- 00:30 : boss: charge - change lane, pressure 0.37
- Medkits: 1 (2.7 per minute, average accessibility 42%). Boss: 00:22.

## DOWNTOWN - street 5 CITY HALL SQUARE - variant A BALANCED

- Intrinsic difficulty: **56/100** (hard, 2 unavoidable hits)
- 24 segments, 00:23 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 56/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 50/100 | moderate |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 43/100 | moderate |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 33/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 33/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 28/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 21/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 20/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **43/100**, moderate. Most useful next upgrade: squad +1: 43 → 33.

Critical zones (arrival upgrades):
- 00:05 : 4 enemies (1 tough), other lane - change lane, pressure 0.37
- 00:06 : 5 enemies (2 tough), other lane - fight (1.7 swings), pressure 0.97
- 00:08 : 4 enemies (2 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:10 : 4 enemies (1 tough), other lane - fight (1.7 swings), pressure 0.98
- 00:12 : 7 enemies (5 tough) - change lane (right after the previous one), pressure 0.37
- Medkits: 2 (5.1 per minute, average accessibility 57%). Boss: 00:23.

## DOWNTOWN - street 5 CITY HALL SQUARE - variant B HORDES

- Intrinsic difficulty: **75/100** (very hard, 1 unavoidable hit)
- 17 segments, 00:23 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 75/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 74/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 71/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 63/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 38/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 25/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 17/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 11/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **54/100**, moderate. Most useful next upgrade: squad +1: 54 → 36.

Critical zones (arrival upgrades):
- 00:04 : 7 enemies (2 tough) - change lane, pressure 0.37
- 00:06 : 6 enemies (3 tough) - change lane, pressure 0.37
- 00:08 : 4 enemies (2 tough) - change lane, pressure 0.40
- 00:12 : 5 enemies (4 tough) - change lane, pressure 0.37
- 00:22 : 7 enemies (4 tough) - change lane, pressure 0.91
- Medkits: 2 (5.1 per minute, average accessibility 51%). Boss: 00:23.

## DOWNTOWN - street 5 CITY HALL SQUARE - variant C GAUNTLET

- Intrinsic difficulty: **62/100** (hard)
- 22 segments, 00:23 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 62/100 | hard |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 62/100 | hard |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 57/100 | hard |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 44/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 40/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 26/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 16/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 14/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **52/100**, moderate. Most useful next upgrade: helmet +1: 52 → 33.

Critical zones (arrival upgrades):
- 00:04 : 2 enemies (1 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:05 : 4 enemies (2 tough), other lane - change lane, pressure 0.49
- 00:07 : 3 enemies (2 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:10 : 4 enemies (2 tough), other lane - skip, pressure 1.57
- 00:11 : 6 enemies (3 tough) - change lane, pressure 0.37
- Medkits: 2 (5.1 per minute, average accessibility 57%). Boss: 00:23.

## DOWNTOWN - street 5 CITY HALL SQUARE - variant D LOOTING

- Intrinsic difficulty: **77/100** (very hard, 4 unavoidable hits)
- 21 segments, 00:23 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 77/100 | very hard, 4 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 68/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 68/100 | hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 57/100 | hard, 2 unavoidable hits |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 35/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 26/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 18/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 14/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **53/100**, moderate. Most useful next upgrade: baton +1: 53 → 35.

Critical zones (arrival upgrades):
- 00:04 : 5 enemies (3 tough), other lane - skip, pressure 1.10
- 00:06 : 3 enemies (1 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:08 : 8 enemies (6 tough) - change lane, pressure 0.37
- 00:11 : 4 enemies (3 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:13 : 4 enemies (1 tough) - change lane, pressure 0.38
- Medkits: 2 (5.1 per minute, average accessibility 51%). Boss: 00:23.

## SHOPPING DISTRICT - street 6 BOUTIQUE ROW - variant A BALANCED

- Intrinsic difficulty: **89/100** (very hard, 1 unavoidable hit)
- 20 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 89/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 84/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 83/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 51/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 51/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 37/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 32/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 22/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **83/100**, very hard, 1 unavoidable hit. Most useful next upgrade: baton +1: 83 → 51 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:06 : 5 enemies (2 tough), out of the stores - change lane, pressure 0.99
- 00:07 : 5 enemies (4 tough), out of the stores - change lane (right after the previous one), pressure 0.39
- 00:08 : 4 enemies (2 tough) - change lane, pressure 0.60
- 00:10 : 4 enemies (3 tough) - change lane, pressure 1.13 - **IMPOSSIBLE with these upgrades**
- 00:12 : 4 enemies, out of the stores - change lane, pressure 0.39
- Medkits: 2 (6.1 per minute, average accessibility 57%). Boss: 00:20.

## SHOPPING DISTRICT - street 6 BOUTIQUE ROW - variant B HORDES

- Intrinsic difficulty: **81/100** (very hard, 1 unavoidable hit)
- 18 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 81/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 77/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 75/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 50/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 47/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 34/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 32/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 22/100 | easy |

Arrival upgrades (H1 V1 W1 S1): **74/100**, hard, 1 unavoidable hit. Most useful next upgrade: baton +1: 74 → 47 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:04 : 4 enemies (3 tough), other lane - skip, pressure 1.10
- 00:07 : 7 enemies (5 tough) - change lane, pressure 0.57
- 00:09 : 4 enemies (3 tough), out of the stores - change lane, pressure 1.04 - **IMPOSSIBLE with these upgrades**
- 00:10 : 4 enemies (2 tough) - change lane, pressure 0.38
- 00:13 : 5 enemies (4 tough) - change lane, pressure 0.37
- Medkits: 2 (6.1 per minute, average accessibility 57%). Boss: 00:20.

## SHOPPING DISTRICT - street 6 BOUTIQUE ROW - variant C GAUNTLET

- Intrinsic difficulty: **100/100** (extreme, 4 unavoidable hits)
- 20 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 100/100 | extreme, 4 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 100/100 | extreme, 4 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 100/100 | extreme, 4 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 90/100 | extreme, 3 unavoidable hits |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 59/100 | hard |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 52/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 51/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 40/100 | moderate |

Arrival upgrades (H1 V1 W1 S1): **81/100**, very hard, 1 unavoidable hit. Most useful next upgrade: baton +1: 81 → 59 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:06 : 4 enemies (2 tough), out of the stores - change lane, pressure 0.46
- 00:08 : 4 enemies (1 tough), store raid (optional) - skip, pressure 1.21
- 00:11 : 6 enemies (5 tough) - change lane, pressure 0.73
- 00:13 : 3 enemies (2 tough), ambush, all lanes - fight (1.8 swings), pressure 1.09 - **IMPOSSIBLE with these upgrades**
- 00:14 : 5 enemies (2 tough), out of the stores - change lane, pressure 0.37
- Medkits: 2 (6.1 per minute, average accessibility 85%). Boss: 00:20.

## SHOPPING DISTRICT - street 6 BOUTIQUE ROW - variant D LOOTING

- Intrinsic difficulty: **100/100** (extreme, 2 unavoidable hits)
- 18 segments, 00:20 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 100/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 99/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 99/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 95/100 | extreme, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 57/100 | hard |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 51/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 50/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 41/100 | moderate |

Arrival upgrades (H1 V1 W1 S1): **82/100**, very hard, 1 unavoidable hit. Most useful next upgrade: baton +1: 82 → 57 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:07 : 6 enemies (3 tough) - change lane, pressure 0.37
- 00:08 : 5 enemies (4 tough) - change lane, pressure 0.37
- 00:11 : 7 enemies (1 tough) - change lane, pressure 0.75
- 00:12 : 4 enemies (3 tough) - change lane, pressure 0.58
- 00:14 : 3 enemies (2 tough), out of the stores - change lane, pressure 1.19 - **IMPOSSIBLE with these upgrades**
- Medkits: 2 (6.1 per minute, average accessibility 57%). Boss: 00:20.

## SHOPPING DISTRICT - street 7 JEWELRY LANE - variant A BALANCED

- Intrinsic difficulty: **96/100** (extreme, 2 unavoidable hits)
- 23 segments, 00:21 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 96/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 92/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 91/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 80/100 | very hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 73/100 | hard |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 67/100 | hard |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 67/100 | hard |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 52/100 | moderate |

Arrival upgrades (H1 V1 W2 S1): **73/100**, hard. Most useful next upgrade: squad +1: 73 → 67.

Critical zones (arrival upgrades):
- 00:05 : 3 enemies (2 tough), out of the stores - fight (1.9 swings), pressure 0.93
- 00:06 : 4 enemies (3 tough), other lane - skip, pressure 1.10
- 00:07 : 9 enemies (8 tough) - change lane, pressure 0.65
- 00:08 : 9 enemies (4 tough) - change lane, pressure 0.63
- 00:09 : 6 enemies (5 tough) - change lane, pressure 0.64
- Medkits: 2 (5.7 per minute, average accessibility 45%). Boss: 00:21.

## SHOPPING DISTRICT - street 7 JEWELRY LANE - variant B HORDES

- Intrinsic difficulty: **62/100** (hard, 1 unavoidable hit)
- 14 segments, 00:21 before the boss, 1 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 62/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 60/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 55/100 | moderate, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 48/100 | moderate, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 48/100 | moderate, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 27/100 | easy |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 27/100 | easy |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 14/100 | easy |

Arrival upgrades (H1 V1 W2 S1): **48/100**, moderate, 1 unavoidable hit. Most useful next upgrade: squad +1: 48 → 27 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:08 : 3 enemies (1 tough), out of the stores - change lane, pressure 0.37
- 00:10 : 5 enemies (2 tough) - change lane, pressure 0.37
- 00:13 : 4 enemies (1 tough) - change lane, pressure 0.53
- 00:23 : boss: thrown box - change lane, pressure 0.37
- 00:25 : 5 enemies, boss backup - fight (1.8 swings), pressure 1.23 - **IMPOSSIBLE with these upgrades**
- Medkits: 1 (2.9 per minute, average accessibility 85%). Boss: 00:21.

## SHOPPING DISTRICT - street 7 JEWELRY LANE - variant C GAUNTLET

- Intrinsic difficulty: **87/100** (very hard, 2 unavoidable hits)
- 17 segments, 00:21 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 87/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 83/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 82/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 75/100 | hard, 2 unavoidable hits |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 61/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 53/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 50/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 32/100 | moderate |

Arrival upgrades (H1 V1 W2 S1): **61/100**, hard, 1 unavoidable hit. Most useful next upgrade: squad +1: 61 → 53 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:06 : 4 enemies (2 tough) - change lane, pressure 0.37
- 00:07 : 6 enemies (1 tough) - change lane, pressure 0.43
- 00:10 : 3 enemies (2 tough), other lane - change lane (right after the previous one), pressure 0.71
- 00:11 : 7 enemies (6 tough) - change lane, pressure 0.77
- 00:25 : 5 enemies, boss backup - fight (1.8 swings), pressure 1.23 - **IMPOSSIBLE with these upgrades**
- Medkits: 2 (5.7 per minute, average accessibility 45%). Boss: 00:21.

## SHOPPING DISTRICT - street 7 JEWELRY LANE - variant D LOOTING

- Intrinsic difficulty: **94/100** (extreme, 2 unavoidable hits)
- 22 segments, 00:21 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 94/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 86/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 84/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 65/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 65/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 34/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 33/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 25/100 | easy |

Arrival upgrades (H1 V1 W2 S1): **65/100**, hard, 1 unavoidable hit. Most useful next upgrade: squad +1: 65 → 34 (unavoidable hits 1 → 0).

Critical zones (arrival upgrades):
- 00:03 : 5 enemies (3 tough), out of the stores - change lane, pressure 0.37
- 00:07 : 4 enemies (1 tough), other lane - skip, pressure 1.10
- 00:09 : 4 enemies (1 tough), other lane - skip, pressure 1.10
- 00:11 : 3 enemies (1 tough), ambush, all lanes - fight (1.3 swings), pressure 0.78
- 00:13 : 6 enemies (1 tough) - change lane, pressure 1.43 - **IMPOSSIBLE with these upgrades**
- Medkits: 2 (5.7 per minute, average accessibility 36%). Boss: 00:21.

## SHOPPING DISTRICT - street 8 GRAND ARCADE - variant A BALANCED

- Intrinsic difficulty: **98/100** (extreme, 1 unavoidable hit)
- 24 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 98/100 | extreme, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 87/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 84/100 | very hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 65/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 65/100 | hard, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 55/100 | hard |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 53/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 44/100 | moderate |

Arrival upgrades (H1 V1 W2 S2): **55/100**, hard. Most useful next upgrade: squad +1: 55 → 46.

Critical zones (arrival upgrades):
- 00:05 : 3 enemies (1 tough), ambush, all lanes - fight (1.5 swings), pressure 0.75
- 00:06 : 5 enemies (1 tough), out of the stores - change lane, pressure 0.37
- 00:09 : 6 enemies (2 tough) - change lane, pressure 0.37
- 00:12 : 8 enemies (2 tough) - change lane, pressure 0.81
- 00:13 : 5 enemies (3 tough) - change lane, pressure 0.63
- Medkits: 2 (5.4 per minute, average accessibility 45%). Boss: 00:22.

## SHOPPING DISTRICT - street 8 GRAND ARCADE - variant B HORDES

- Intrinsic difficulty: **93/100** (extreme)
- 22 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 93/100 | extreme |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 88/100 | very hard |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 87/100 | very hard |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 72/100 | hard |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 72/100 | hard |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 62/100 | hard |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 62/100 | hard |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 44/100 | moderate |

Arrival upgrades (H1 V1 W2 S2): **62/100**, hard. Most useful next upgrade: squad +1: 62 → 44.

Critical zones (arrival upgrades):
- 00:06 : 5 enemies (2 tough), other lane - skip, pressure 1.10
- 00:06 : 8 enemies (6 tough) - change lane, pressure 0.60
- 00:11 : 6 enemies (2 tough) - change lane, pressure 0.65
- 00:12 : 8 enemies (7 tough) - change lane, pressure 0.62
- 00:13 : 5 enemies (2 tough) - change lane, pressure 0.63
- Medkits: 2 (5.4 per minute, average accessibility 36%). Boss: 00:22.

## SHOPPING DISTRICT - street 8 GRAND ARCADE - variant C GAUNTLET

- Intrinsic difficulty: **96/100** (extreme, 2 unavoidable hits)
- 17 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 96/100 | extreme, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 81/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 77/100 | very hard, 2 unavoidable hits |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 56/100 | hard, 2 unavoidable hits |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 50/100 | moderate, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 36/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 35/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 25/100 | easy |

Arrival upgrades (H1 V1 W2 S2): **36/100**, moderate. Most useful next upgrade: squad +1: 36 → 26.

Critical zones (arrival upgrades):
- 00:03 : 3 enemies (2 tough), ambush, all lanes - fight (1.6 swings), pressure 0.81
- 00:06 : 5 enemies (4 tough) - change lane, pressure 0.37
- 00:09 : 4 enemies (3 tough) - change lane, pressure 0.37
- 00:10 : 4 enemies (1 tough), out of the stores - change lane, pressure 0.43
- 00:26 : 5 enemies, boss backup - change lane (right after the previous one), pressure 0.95
- Medkits: 2 (5.4 per minute, average accessibility 57%). Boss: 00:22.

## SHOPPING DISTRICT - street 8 GRAND ARCADE - variant D LOOTING

- Intrinsic difficulty: **73/100** (hard, 1 unavoidable hit)
- 18 segments, 00:22 before the boss, 2 medkit(s)

| Upgrades | Effective | Status |
|---|---|---|
| Helmet 0 · Vest 0 · Baton 0 · Squad 0 | 73/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 0 | 66/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 1 · Squad 1 | 64/100 | hard, 1 unavoidable hit |
| Helmet 0 · Vest 0 · Baton 2 · Squad 1 | 52/100 | moderate, 1 unavoidable hit |
| Helmet 1 · Vest 1 · Baton 2 · Squad 1 | 46/100 | moderate |
| Helmet 1 · Vest 1 · Baton 2 · Squad 2 | 41/100 | moderate |
| Helmet 2 · Vest 2 · Baton 3 · Squad 2 | 41/100 | moderate |
| Helmet 3 · Vest 3 · Baton 3 · Squad 3 | 32/100 | moderate |

Arrival upgrades (H1 V1 W2 S2): **41/100**, moderate. Most useful next upgrade: squad +1: 41 → 32.

Critical zones (arrival upgrades):
- 00:03 : 6 enemies (4 tough) - change lane, pressure 0.37
- 00:09 : carts across all lanes - jump, pressure 0.89
- 00:10 : 4 enemies (2 tough) - change lane, pressure 0.38
- 00:11 : 5 enemies (3 tough), store raid (optional) - change lane, pressure 0.48
- 00:13 : 4 enemies (3 tough), out of the stores - change lane, pressure 0.64
- Medkits: 2 (5.4 per minute, average accessibility 85%). Boss: 00:22.

