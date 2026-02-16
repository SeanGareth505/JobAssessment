import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/services/auth.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { ORDER_STATUS_LABELS } from '../../core/constants/sadc-countries';
import type { CustomerDto, OrderDto } from '../../core/models/api.models';
import { CreateOrderDialogComponent } from '../../dialogs/create-order-dialog/create-order-dialog.component';
import { EditOrderDialogComponent } from '../../dialogs/edit-order-dialog/edit-order-dialog.component';

@Component({
  selector: 'app-orders-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatIconModule,
    MatDividerModule,
  ],
  templateUrl: './orders-list.component.html',
  styleUrl: './orders-list.component.scss',
})
export class OrdersListComponent implements OnInit {
  loading = signal(false);
  error = signal<string | null>(null);
  customerId = signal<string>('');
  status = signal<string>('');
  sort = signal('createdAt');
  page = signal(1);
  pageSize = signal(20);
  totalCount = signal(0);
  items = signal<OrderDto[]>([]);
  customers = signal<CustomerDto[]>([]);
  updatingOrderId = signal<string | null>(null);
  selectedOrderForMenu = signal<OrderDto | null>(null);

  displayedColumns = [
    'createdAt',
    'customerName',
    'status',
    'totalAmount',
    'currencyCode',
    'actions',
  ];
  readonly statusLabels = ORDER_STATUS_LABELS;

  constructor(
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    private readonly router: Router,
    private readonly snackBar: MatSnackBar,
    public readonly auth: AuthService,
  ) {}

  openNewOrderModal(): void {
    const ref = this.dialog.open(CreateOrderDialogComponent, {
      width: '960px',
      maxWidth: '98vw',
      minHeight: '400px',
      disableClose: false,
    });
    ref.afterClosed().subscribe((result?: OrderDto) => {
      if (result) {
        this.loadPage();
        this.router.navigate(['/orders']);
        this.snackBar.open('Order created', undefined, { duration: 3000 });
      }
    });
  }

  canTransitionToPaid(row: OrderDto): boolean {
    return row.status === 0;
  }

  canTransitionToFulfilled(row: OrderDto): boolean {
    return row.status === 1;
  }

  canTransitionToCancelled(row: OrderDto): boolean {
    return row.status === 0 || row.status === 1;
  }

  hasStatusActions(row: OrderDto): boolean {
    return (
      this.canTransitionToPaid(row) ||
      this.canTransitionToFulfilled(row) ||
      this.canTransitionToCancelled(row)
    );
  }

  canEditOrder(row: OrderDto): boolean {
    return row.status === 0 && this.auth.hasWriteRole();
  }

  statusLabel(s: number): string {
    return this.statusLabels[s] ?? 'Unknown';
  }

  updateStatus(order: OrderDto, newStatus: number): void {
    this.updatingOrderId.set(order.id);
    this.error.set(null);
    this.api.updateOrderStatus(order.id, { status: newStatus }, crypto.randomUUID()).subscribe({
      next: () => {
        this.loadPage();
        this.updatingOrderId.set(null);
        this.snackBar.open('Status updated', undefined, { duration: 2000 });
      },
      error: (err) => {
        this.error.set(getApiErrorMessage(err, 'Failed to update status'));
        this.updatingOrderId.set(null);
      },
    });
  }

  openEditOrderDialog(order: OrderDto): void {
    if (order.status !== 0 || !this.auth.hasWriteRole()) return;
    const ref = this.dialog.open(EditOrderDialogComponent, {
      width: '560px',
      disableClose: false,
      data: order,
    });
    ref.afterClosed().subscribe((result?: OrderDto) => {
      if (result) this.loadPage();
    });
  }

  ngOnInit(): void {
    this.loadCustomers();
    this.loadPage();
  }

  loadCustomers(): void {
    this.api.getCustomersPage(null, 1, 100).subscribe({
      next: (res) => this.customers.set(res.items),
      error: () => {},
    });
  }

  loadPage(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getOrdersPage(
        this.customerId() || null,
        this.status() || null,
        this.page(),
        this.pageSize(),
        this.sort(),
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(getApiErrorMessage(err, 'Failed to load orders'));
          this.loading.set(false);
        },
      });
  }

  onFilter(): void {
    this.page.set(1);
    this.loadPage();
  }

  onPage(e: PageEvent): void {
    this.page.set(e.pageIndex + 1);
    this.pageSize.set(e.pageSize);
    this.loadPage();
  }
}
