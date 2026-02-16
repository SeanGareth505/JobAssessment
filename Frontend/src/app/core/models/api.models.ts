export interface CustomerDto {
  id: string;
  name: string;
  email: string;
  countryCode: string;
  createdAt: string;
  /** Set when returned from the customers list endpoint. */
  orderCount?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type OrderStatusDto = 0 | 1 | 2 | 3;

export interface OrderLineItemDto {
  id: string;
  orderId: string;
  productId?: string | null;
  productSku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface OrderDto {
  id: string;
  customerId: string;
  status: number;
  createdAt: string;
  currencyCode: string;
  totalAmount: number;
  customerName?: string | null;
  lineItems: OrderLineItemDto[];
  etag?: string | null;
}

export interface CreateCustomerRequest {
  name: string;
  email?: string;
  countryCode: string;
}

export interface UpdateCustomerRequest {
  name: string;
  email?: string;
  countryCode: string;
}

export interface CreateOrderLineItemRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  customerId: string;
  currencyCode: string;
  lineItems: CreateOrderLineItemRequest[];
}

export interface UpdateOrderRequest {
  lineItems: CreateOrderLineItemRequest[];
}

export interface UpdateOrderStatusRequest {
  status: number;
}

export interface ProductDto {
  id: string;
  sku: string;
  name: string;
  createdAt: string;
}

export interface CreateProductRequest {
  sku: string;
  name: string;
}
