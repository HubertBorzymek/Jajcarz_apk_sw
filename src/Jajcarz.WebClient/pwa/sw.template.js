// Service worker. Build podstawia wersję i listę plików (vite.config.ts).
// Trzyma tylko interfejs. Dane produktów i kolejka zmian są w pamięci strony.

const VERSION = '__VERSION__';
const PRECACHE = __PRECACHE__;

const CACHE_NAME = `jajcarz-${VERSION}`;
const APP_PAGE = '/index.html';

/** Słaby zasięg bywa gorszy od braku: po tym czasie pokazujemy wersję z pamięci. */
const NAVIGATION_TIMEOUT_MS = 3000;

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll([APP_PAGE, ...PRECACHE]))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names.filter((name) => name.startsWith('jajcarz-') && name !== CACHE_NAME).map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

async function openApp(request) {
  const cache = await caches.open(CACHE_NAME);
  try {
    const response = await fetch(request, { signal: AbortSignal.timeout(NAVIGATION_TIMEOUT_MS) });
    if (response.ok) {
      await cache.put(APP_PAGE, response.clone());
    }
    return response;
  } catch (error) {
    const cached = await cache.match(APP_PAGE);
    if (cached) {
      return cached;
    }
    throw error;
  }
}

async function openAsset(request) {
  const cached = await caches.match(request);
  return cached ?? fetch(request);
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin) {
    return;
  }
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/ws/')) {
    return;
  }
  event.respondWith(request.mode === 'navigate' ? openApp(request) : openAsset(request));
});
