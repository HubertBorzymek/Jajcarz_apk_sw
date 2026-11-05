# CHANGELOG_AGENTS

Najnowsze na górze. Krótko: co, dlaczego, co dalej.

## 2026-11-05 (v0.1.0, testy na panelu)

- CI przeszło, także migracja i testy na Postgresie.
- Testy z `docs/TESTY_MANUALNE.md` zaliczone w sieci lokalnej i na `wss`.
- `secrets.h` wskazuje teraz `192.168.68.58`, bo komputer dostał nowy adres.
- SW2 (`0.1`, GPIO33) nie zgłasza naciśnięć. Log nie ma wpisu, a `seq` nie rośnie.
  Pin nie ma innego użycia w kodzie, więc podejrzany jest sprzęt. Nie zmierzono.
- `LastSeenAt` nie reaguje na `unassigned_press`. Do poprawy w `v0.4.0`.

## 2026-11-05 (v0.1.0, bez taga)

- Założenia zaakceptowane, decyzje w `README_AGENTS.md`.
- Serwer bez interfejsu: domena, operacje, EF Core, `/ws/device`, seed lokalny.
- Zweryfikowano: 57 testów na SQLite, celowe popsucie reguł wykrywają testy,
  handshake, `snapshot` i `ack` na Kestrelu, rozłączenie panelu bez `pong` po 75 s.
- Znaleziono: dwa testy przypięć przechodziły z niewłaściwego powodu, poprawione.
- EF Core 10.0.14, bo 10.0.10 ciągnie SQLite z luką GHSA-2m69-gcr7-jv3q.
- Niezweryfikowane: migracja i testy na Postgresie, prawdziwy panel, `wss`.
- Następny krok: panel na USB, testy z `docs/TESTY_MANUALNE.md`, potem tag i `v0.2.0`.
