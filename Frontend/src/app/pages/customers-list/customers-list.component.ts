import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/services/auth.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import type { CustomerDto } from '../../core/models/api.models';
import { AddCustomerDialogComponent } from '../../dialogs/add-customer-dialog/add-customer-dialog.component';
import { CustomerOrdersDialogComponent } from '../../dialogs/customer-orders-dialog/customer-orders-dialog.component';
import { EditCustomerDialogComponent } from '../../dialogs/edit-customer-dialog/edit-customer-dialog.component';

@Component({
  selector: 'app-customers-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './customers-list.component.html',
  styleUrl: './customers-list.component.scss',
})
export class CustomersListComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  loading = signal(false);
  error = signal<string | null>(null);
  search = signal('');
  page = signal(1);
  pageSize = signal(20);
  totalCount = signal(0);
  items = signal<CustomerDto[]>([]);

  displayedColumns = ['name', 'email', 'countryCode', 'orderCount', 'createdAt', 'actions'];

  constructor(
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    public readonly auth: AuthService,
  ) {}

  openAddCustomerModal(): void {
    const ref = this.dialog.open(AddCustomerDialogComponent, {
      width: '420px',
      disableClose: false,
    });
    ref
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result?: CustomerDto) => {
        if (result) this.loadPage();
      });
  }

  openCustomerOrdersModal(customer: CustomerDto): void {
    this.dialog.open(CustomerOrdersDialogComponent, {
      width: '640px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      disableClose: false,
      data: customer,
    });
  }

  openEditCustomerModal(customer: CustomerDto): void {
    const ref = this.dialog.open(EditCustomerDialogComponent, {
      width: '420px',
      disableClose: false,
      data: customer,
    });
    ref
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result?: CustomerDto) => {
        if (result) this.loadPage();
      });
  }

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getCustomersPage(this.search() || null, this.page(), this.pageSize())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
      next: (res) => {
        this.items.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(getApiErrorMessage(err, 'Failed to load customers'));
        this.loading.set(false);
      },
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadPage();
  }

  onPage(e: PageEvent): void {
    this.page.set(e.pageIndex + 1);
    this.pageSize.set(e.pageSize);
    this.loadPage();
  }
}
