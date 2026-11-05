# Testy manualne

Testy wymagające panelu. Wynik zaznacz `[x]`, a problem opisz pod punktem.

## v0.1.0: panel w sieci lokalnej

Przygotowanie:

1. Uruchom serwer poleceniem z `README.md`.
2. `config/secrets.h` w firmware powinien wskazywać `ws://<IP_komputera>:5080/ws/device`
   i token `test-token`. Jeśli adres się zgadza, nie trzeba wgrywać firmware.
3. Identyfikator panelu musi zgadzać się z `DevSeed:DeviceId`
   w `appsettings.Development.json`. Inny identyfikator kończy się zamknięciem połączenia.

Pierwsze trzy klawisze mają przykładowe produkty, a czwarty jest wolny.

- [ ] Panel łączy się, a dioda na płytce świeci stale.
- [ ] Klawisze 0.0 do 0.2 świecą na zielono, a klawisz 0.3 jest zgaszony.
- [ ] Naciśnięcie klawisza 0.0 zmienia kolor, a dioda na płytce krótko gaśnie po potwierdzeniu.
- [ ] Naciśnięcie klawisza 0.3 pokazuje w logu serwera klawisz bez produktu.
- [ ] Po restarcie serwera panel łączy się ponownie i zachowuje stany.
- [ ] Zmiana klawisza przy zatrzymanym serwerze dociera po jego uruchomieniu.
- [ ] Wyłączenie zasilania panelu: po około 75 s log serwera pokazuje przerwane połączenie.

## v0.1.0: certyfikat `wss`

Sprawdza, czy panel ufa obecnemu łańcuchowi Let's Encrypt. Używa adresu Trumpfisha,
bo ma on taki sam certyfikat, jaki dostanie Jajcarz. Wykonuje jedno połączenie
z fałszywym tokenem i niczego nie zmienia na produkcji.

1. Zapisz kopię `config/secrets.h`.
2. Ustaw adres `wss://trumpfish.polandcentral.cloudapp.azure.com/ws/device`
   i dowolny token. Po skopiowaniu pliku odśwież jego `LastWriteTime`.
3. Wgraj firmware i obserwuj log przez około 30 s.
4. Przywróć `config/secrets.h` i wgraj firmware ponownie.

- [ ] Log nie pokazuje błędu weryfikacji certyfikatu.
- [ ] Handshake WebSocket jest odrzucony przez serwer, bo Trumpfish nie ma tej ścieżki.
- [ ] Po przywróceniu konfiguracji panel łączy się z serwerem lokalnym.

Po zaliczeniu obu sekcji: tag `v0.1.0`.
