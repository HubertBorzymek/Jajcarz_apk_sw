# Historia zmian

## v0.1.0 (2026-11-05)

### Dodano
- Serwer przyjmujący panel na `/ws/device`, zgodnie z `docs/PROTOCOL.md`.
- Trwałe dane w bazie: produkty, stany, numery zmian, klawisze i panele.
- Reguły konfliktów i bezpieczne ponawianie zmian po utracie potwierdzenia.
- Przypinanie, przenoszenie i odpinanie produktów bez utraty ich stanu.
- Testy reguł i symulator panelu.
- Automatyczne testy w GitHub Actions, także na bazie Postgres.

### Sprawdzono
- Testy z prawdziwym panelem w sieci lokalnej i test certyfikatu `wss`.
