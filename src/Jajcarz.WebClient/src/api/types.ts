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
