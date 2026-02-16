import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'customers' },
  {
    path: 'customers',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/customers-list/customers-list.component').then(
        (m) => m.CustomersListComponent,
      ),
  },
  { path: 'customers/new', redirectTo: 'customers', pathMatch: 'full' },
  {
    path: 'orders',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/orders-list/orders-list.component').then((m) => m.OrdersListComponent),
  },
  {
    path: 'orders/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/order-create/order-create.component').then((m) => m.OrderCreateComponent),
  },
  {
    path: 'orders/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/order-detail/order-detail.component').then((m) => m.OrderDetailComponent),
  },
  {
    path: 'products',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/products-list/products-list.component').then((m) => m.ProductsListComponent),
  },
  { path: '**', redirectTo: 'customers' },
];
