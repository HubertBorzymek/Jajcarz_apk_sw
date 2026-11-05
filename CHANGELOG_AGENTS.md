# CHANGELOG_AGENTS

Najnowsze na górze. Krótko: co, dlaczego, co dalej.

## 2026-11-06 (v0.2.0, bez taga)

- Konta: PBKDF2, cookie na rok, `SessionStamp`, CSRF, limit logowań, polecenie `user set`.
- REST `/api/products`, SSE `/api/events`, `ProductEvents` publikowane przed zwolnieniem zapisu.
- Interfejs w `src/Jajcarz.WebClient`, budowany do `Jajcarz.Server/wwwroot`.
- Parametry `ProductService` w endpointach muszą mieć `[FromServices]`, bo jego
  metoda `BindAsync` myli wiązanie Minimal API.
- Zweryfikowano: 93 testy .NET, 18 testów interfejsu, 8 celowych błędów wykrytych,
  w przeglądarce logowanie, zmiany stanu, edycja, usuwanie, dwie karty, restart serwera.
- Niezweryfikowane: nowa migracja na Postgresie, klawisze panelu widoczne w aplikacji.
- Następny krok: test z panelem i telefonem z `docs/TESTY_MANUALNE.md`, potem `v0.3.0`.

## 2026-11-05 (v0.1.0, testy na panelu)

- CI przeszło, także migracja i testy na Postgresie.
- Testy z `docs/TESTY_MANUALNE.md` zaliczone w sieci lokalnej i na `wss`.
- `secrets.h` wskazuje teraz `192.168.68.58`, bo komputer dostał nowy adres.
- SW2 (`0.1`, GPIO33) chwilowo nie zgłaszał naciśnięć: brak wpisu w logu, `seq` bez zmian.
  Potem zadziałał bez restartu i wgrywania. Podejrzany styk, nie zmierzono.
- Seria 15 szybkich zmian i 3 naciśnięć `0.3` w 6 s przeszła bez utraty numerów `seq`.
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
