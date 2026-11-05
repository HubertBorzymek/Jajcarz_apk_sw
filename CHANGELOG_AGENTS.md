# CHANGELOG_AGENTS

Najnowsze na górze. Krótko: co, dlaczego, co dalej.

## 2026-11-05 (v0.1.0, bez taga)

- Założenia zaakceptowane, decyzje w `README_AGENTS.md`.
- Serwer bez interfejsu: domena, operacje, EF Core, `/ws/device`, seed lokalny.
- Zweryfikowano: 57 testów na SQLite, celowe popsucie reguł wykrywają testy,
  handshake, `snapshot` i `ack` na Kestrelu, rozłączenie panelu bez `pong` po 75 s.
- Znaleziono: dwa testy przypięć przechodziły z niewłaściwego powodu, poprawione.
- EF Core 10.0.14, bo 10.0.10 ciągnie SQLite z luką GHSA-2m69-gcr7-jv3q.
- Niezweryfikowane: migracja i testy na Postgresie, prawdziwy panel, `wss`.
- Następny krok: panel na USB, testy z `docs/TESTY_MANUALNE.md`, potem tag i `v0.2.0`.
