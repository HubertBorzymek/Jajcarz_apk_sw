# Historia zmian

## Niewydane

Nic.

## v0.5.0 (2026-11-06)

### Dodano
- Obraz Dockera, pliki wdrożenia, instrukcja i kopie zapasowe.
- Adres `/healthz` do sprawdzania stanu aplikacji i bazy.
- CI buduje obraz, uruchamia go na czystej bazie i sprawdza zależności.
- CI przechodzi całe wdrożenie: aktualizację, Caddy, kopię zapasową i jej przywrócenie.

### Naprawiono
- Kopię zapasową można przywrócić do bazy, w której są już tabele.
- Przerwany zrzut nie zostaje na dysku jako kopia.

## v0.4.0 (2026-11-06)

### Dodano
- Ekran panelu: stan połączenia, wersja firmware i lista klawiszy, na żywo.
- Przypisywanie produktu do klawisza, także przez naciśnięcie wolnego klawisza.
- Mignięcie wybranym klawiszem, żeby znaleźć go na panelu.
- Kolory stanów i jasność panelu ustawiane w aplikacji.
- Polecenie `panel set` do rejestracji panelu i wymiany tokenu.

## v0.3.0 (2026-11-06)

### Dodano
- Lista zakupów: najpierw braki, „Kupione” przekreśla pozycję, a „Cofnij” przywraca stan.
- Dolna nawigacja z licznikiem produktów do kupienia.
- Praca bez zasięgu: ostatnia lista zostaje na telefonie, a zmiany czekają w kolejce.
- PWA: instalacja na telefonie, ikony, otwieranie aplikacji bez sieci.

## v0.2.0 (2026-11-06)

### Dodano
- Logowanie: konta bez rejestracji, sesja na rok i wylogowanie urządzeń po zmianie hasła.
- API produktów: lista, dodawanie, edycja, usuwanie i zmiana stanu.
- Zmiany na żywo między panelem, telefonem i komputerem.
- Aplikacja na telefon i komputer: lista produktów i szybka zmiana stanu jedną ręką.

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
