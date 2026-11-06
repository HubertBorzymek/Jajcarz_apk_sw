# Jajcarz

Aplikacja do panelu na lodówkę. Panel ma klawisze i diody pokazujące stan produktów:
jest, kończy się, nie ma. Zużywając produkt, klikasz klawisz, a w sklepie sprawdzasz
w aplikacji, co trzeba kupić.

To druga połowa systemu. Firmware panelu jest w repozytorium
[Jajcarz_esp_sw](https://github.com/HubertBorzymek/Jajcarz_esp_sw), a wspólny kontrakt
opisuje [docs/PROTOCOL.md](docs/PROTOCOL.md).

## Stan projektu

Działa serwer panelu oraz aplikacja: logowanie, lista produktów, szybka zmiana stanu,
dodawanie, edycja i usuwanie, zmiany na żywo w obie strony, lista zakupów oraz praca
bez zasięgu. Aplikację można dodać do ekranu telefonu.

| Wersja | Zakres |
|---|---|
| `v0.1.0` | Serwer panelu i baza danych |
| `v0.2.0` | Logowanie, API, zmiany na żywo, lista produktów |
| `v0.3.0` | Widok zakupowy, PWA, praca przy słabym zasięgu |
| `v0.4.0` | Stan panelu, przypisywanie klawiszy, kolory i jasność |
| `v0.5.0` | Docker i wdrożenie testowe |
| `v1.0.0` | Wersja produkcyjna |

## Uruchomienie lokalne

Potrzebne są .NET SDK 10 i Node 24. Najpierw zbuduj interfejs, potem uruchom serwer:

```powershell
cd src\Jajcarz.WebClient
npm ci
npm run build
cd ..\Jajcarz.Server
dotnet run --launch-profile Jajcarz.Server
```

Aplikacja działa pod `http://localhost:5080`, a w tej samej sieci także pod adresem
komputera. Lokalnie używa pliku SQLite i przy pierwszym starcie tworzy przykładowe
produkty, panel z tokenem `test-token` oraz konto `test` z hasłem `test-password`,
opisane w `appsettings.Development.json`. Produkcja używa bazy Postgres.

Przy pracy nad interfejsem `npm run dev` w `src\Jajcarz.WebClient` odświeża stronę
na bieżąco i przekazuje `/api` do działającego serwera.

## Telefon i brak zasięgu

Aplikacja instaluje się na telefonie przez „Dodaj do ekranu głównego”. Pamięta ostatnią
listę produktów, a zmiany bez zasięgu czekają w kolejce i wysyłają się same po powrocie
sieci. Instalacja i otwieranie bez sieci działają tylko na `https` i `localhost`. Przy
adresie `http` w sieci lokalnej przeglądarka wyłącza service worker, więc jest to wtedy
zwykła strona, choć kolejka zmian działa nadal.

## Konta

Nie ma rejestracji. Konto zakłada się na serwerze, a hasło podaje na standardowym wejściu:

```powershell
"nowe-haslo" | dotnet Jajcarz.Server.dll user set hubert
```

To samo polecenie zmienia hasło i wylogowuje to konto ze wszystkich urządzeń.

## Testy

```powershell
dotnet test Jajcarz.slnx
cd src\Jajcarz.WebClient
npm test
```

Symulator panelu wysyła dokładnie te same wiadomości co firmware. Testy na prawdziwym
sprzęcie opisuje [docs/TESTY_MANUALNE.md](docs/TESTY_MANUALNE.md).

## Budowa

| Folder | Zawartość |
|---|---|
| `src/Jajcarz.Domain` | Produkty, klawisze i reguły konfliktów |
| `src/Jajcarz.Application` | Operacje aplikacji i konta |
| `src/Jajcarz.Infrastructure` | Baza danych |
| `src/Jajcarz.Server` | Połączenie z panelem, API, logowanie i uruchomienie serwera |
| `src/Jajcarz.WebClient` | Interfejs w React |
| `tests` | Testy reguł, API i symulator panelu |

Stan należy do produktu, a klawisz jest do niego tylko przypięty. Dzięki temu produkt
można przenieść na inny klawisz bez utraty stanu.
