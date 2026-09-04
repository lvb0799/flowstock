export type Row = Record<string, any>;
export type MasterKind = 'products' | 'customers' | 'suppliers';
export type ToastState = { type: 'success' | 'error'; text: string } | null;
export type EditState = { kind: MasterKind; row: Row } | null;

export interface ActivityHistory {
  Id: number;
  Action: string;
  EntityType: string;
  EntityId?: number;
  EntityLabel?: string;
  Description?: string;
  Actor?: string;
  CreatedAt: string;
}

export type OrderKind = 'sales' | 'purchases';
export interface OrderItemInput {
  productId: number;
  qty: number;
  unitPrice: number;
}
export interface OrderCreatePayload {
  customerId?: number;
  supplierId?: number;
  orderDate?: string;
  note?: string;
  items: OrderItemInput[];
}
export interface OrderDetailResponse {
  header: Row;
  items: Row[];
}
