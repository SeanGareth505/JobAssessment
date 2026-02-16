import { Component, DestroyRef, Inject, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { ORDER_STATUS_LABELS } from '../../core/constants/order-status';
import type { CustomerDto, OrderDto } from '../../core/models/api.models';

@Component({
  selector: 'app-customer-orders-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatTableModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './customer-orders-dialog.component.html',
  styleUrl: './customer-orders-dialog.component.scss',
})
export class CustomerOrdersDialogComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  loading = signal(true);
  error = signal<string | null>(null);
  orders = signal<OrderDto[]>([]);
  readonly statusLabels = ORDER_STATUS_LABELS;
  readonly displayedColumns = ['createdAt', 'status', 'totalAmount', 'currencyCode', 'actions'];

  constructor(
    private readonly dialogRef: MatDialogRef<CustomerOrdersDialogComponent>,
    private readonly api: ApiService,
    private readonly router: Router,
    @Inject(MAT_DIALOG_DATA) public data: CustomerDto,
  ) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getOrdersPage(this.data.id, null, 1, 100, '-createdAt')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.orders.set(res.items);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(getApiErrorMessage(err, 'Failed to load orders'));
          this.loading.set(false);
        },
      });
  }

  statusLabel(s: number): string {
    return this.statusLabels[s] ?? 'Unknown';
  }

  viewOrder(orderId: string): void {
    this.dialogRef.close();
    this.router.navigate(['/orders', orderId]);
  }

  close(): void {
    this.dialogRef.close();
  }
}
