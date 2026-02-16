import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_PATHS } from './constants/api-paths';
import type {
  CustomerDto,
  OrderDto,
  PagedResult,
  ProductDto,
  CreateCustomerRequest,
  CreateOrderRequest,
  CreateProductRequest,
  UpdateCustomerRequest,
  UpdateOrderRequest,
  UpdateOrderStatusRequest,
} from './models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  getCustomersPage(
    search: string | null,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<CustomerDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<CustomerDto>>(API_PATHS.customers, { params });
  }

  getCustomerById(id: string): Observable<CustomerDto> {
    return this.http.get<CustomerDto>(`${API_PATHS.customers}/${id}`);
  }

  createCustomer(request: CreateCustomerRequest): Observable<CustomerDto> {
    return this.http.post<CustomerDto>(API_PATHS.customers, request);
  }

  updateCustomer(id: string, request: UpdateCustomerRequest): Observable<CustomerDto> {
    return this.http.put<CustomerDto>(`${API_PATHS.customers}/${id}`, request);
  }

  getOrdersPage(
    customerId: string | null,
    status: string | null,
    page: number,
    pageSize: number,
    sort: string | null,
  ): Observable<PagedResult<OrderDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (customerId) params = params.set('customerId', customerId);
    if (status) params = params.set('status', status);
    if (sort?.trim()) params = params.set('sort', sort.trim());
    return this.http.get<PagedResult<OrderDto>>(API_PATHS.orders, { params });
  }

  getOrderById(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${API_PATHS.orders}/${id}`);
  }

  createOrder(request: CreateOrderRequest): Observable<OrderDto> {
    return this.http.post<OrderDto>(API_PATHS.orders, request);
  }

  updateOrder(id: string, request: UpdateOrderRequest): Observable<OrderDto> {
    return this.http.put<OrderDto>(`${API_PATHS.orders}/${id}`, request);
  }

  updateOrderStatus(
    id: string,
    request: UpdateOrderStatusRequest,
    idempotencyKey: string,
  ): Observable<OrderDto> {
    return this.http.put<OrderDto>(`${API_PATHS.orders}/${id}/status`, request, {
      headers: { 'Idempotency-Key': idempotencyKey },
    });
  }

  getProductsPage(
    search: string | null,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<ProductDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<ProductDto>>(API_PATHS.products, { params });
  }

  createProduct(request: CreateProductRequest): Observable<ProductDto> {
    return this.http.post<ProductDto>(API_PATHS.products, request);
  }
}
