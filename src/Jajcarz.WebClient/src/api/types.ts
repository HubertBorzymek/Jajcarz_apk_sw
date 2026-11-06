export type ProductState = 'OK' | 'LOW' | 'OUT';

export interface Product {
  id: string;
  name: string;
  category: string | null;
  state: ProductState;
  rev: number;
  changedBy: string;
  changedAt: string;
  addr: string | null;
}

export interface ProductFields {
  name: string;
  category: string | null;
}

export interface Session {
  login: string | null;
  csrfToken: string;
}

export interface StateChangeResult {
  accepted: boolean;
  product: Product;
}

export type Rgb = [number, number, number];

export interface PanelConfig {
  brightness: number;
  colors: Record<ProductState, Rgb>;
}

export interface PanelKeyView {
  addr: string;
  productId: string | null;
  productName: string | null;
  state: ProductState | null;
}

export interface PanelView {
  id: string;
  firmware: string | null;
  connected: boolean;
  lastSeenAt: string | null;
  config: PanelConfig;
  keys: PanelKeyView[];
}
