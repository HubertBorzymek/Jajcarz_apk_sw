import { ROUTE_HASHES, type Route } from './route';

interface TabBarProps {
  route: Route;
  toBuy: number;
}

export function TabBar({ route, toBuy }: TabBarProps) {
  return (
    <nav className="tabbar" aria-label="Ekrany">
      <a className="tab" href={ROUTE_HASHES.products} aria-current={route === 'products' ? 'page' : undefined}>
        Produkty
      </a>
      <a className="tab" href={ROUTE_HASHES.shopping} aria-current={route === 'shopping' ? 'page' : undefined}>
        Zakupy
        {toBuy > 0 && (
          <span className="tab-badge" aria-label={`do kupienia: ${toBuy}`}>
            {toBuy}
          </span>
        )}
      </a>
      <a className="tab" href={ROUTE_HASHES.panel} aria-current={route === 'panel' ? 'page' : undefined}>
        Panel
      </a>
    </nav>
  );
}
