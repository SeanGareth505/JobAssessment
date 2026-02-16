import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/services/auth.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { ORDER_STATUS_LABELS } from '../../core/constants/sadc-countries';
import { EditOrderDialogComponent } from '../../dialogs/edit-order-dialog/edit-order-dialog.component';
import type { OrderDto } from '../../core/models/api.models';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatToolbarModule,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatIconModule,
    MatDividerModule,
  ],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss',
})
export class OrderDetailComponent implements OnInit {
  order = signal<OrderDto | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  updating = signal(false);
  readonly statusLabels = ORDER_STATUS_LABELS;

  canTransitionToPaid = computed(() => this.order()?.status === 0);
  canTransitionToFulfilled = computed(() => this.order()?.status === 1);
  canTransitionToCancelled = computed(() => {
    const s = this.order()?.status;
    return s === 0 || s === 1;
  });

  canEditOrder = computed(() => {
    const o = this.order();
    return o?.status === 0 && this.auth.hasWriteRole();
  });

  get hasOrderActions(): boolean {
    return this.hasStatusActions() || this.canEditOrder();
  }

  hasStatusActions(): boolean {
    return (
      this.canTransitionToPaid() ||
      this.canTransitionToFulfilled() ||
      this.canTransitionToCancelled()
    );
  }

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: ApiService,
    public readonly auth: AuthService,
    private readonly dialog: MatDialog,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadOrder(id);
    else this.loading.set(false);
  }

  loadOrder(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getOrderById(id).subscribe({
      next: (o) => {
        this.order.set(o);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(getApiErrorMessage(err, 'Failed to load order'));
        this.loading.set(false);
      },
    });
  }

  statusLabel(s: number): string {
    return this.statusLabels[s] ?? 'Unknown';
  }

  openEditOrderModal(): void {
    const o = this.order();
    if (!o || o.status !== 0) return;
    const ref = this.dialog.open(EditOrderDialogComponent, {
      width: '560px',
      disableClose: false,
      data: o,
    });
    ref.afterClosed().subscribe((result?: OrderDto) => {
      if (result) {
        this.order.set(result);
        this.error.set(null);
      }
    });
  }

  transitionTo(status: number): void {
    const o = this.order();
    if (!o) return;
    this.updating.set(true);
    this.error.set(null);
    const idempotencyKey = crypto.randomUUID();
    this.api.updateOrderStatus(o.id, { status }, idempotencyKey).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.updating.set(false);
      },
      error: (err) => {
        this.error.set(getApiErrorMessage(err, 'Failed to update status'));
        this.updating.set(false);
      },
    });
  }
}
