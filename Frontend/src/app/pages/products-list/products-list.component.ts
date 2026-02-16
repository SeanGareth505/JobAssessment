import { Component, DestroyRef, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/services/auth.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import type { ProductDto } from '../../core/models/api.models';
import { CreateProductDialogComponent } from '../../dialogs/create-product-dialog/create-product-dialog.component';

export type ProductSortOption = 'sku-asc' | 'sku-desc' | 'created-desc' | 'created-asc';

@Component({
  selector: 'app-products-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatIconModule,
  ],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss',
})
export class ProductsListComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  loading = signal(false);
  error = signal<string | null>(null);
  search = signal('');
  page = signal(1);
  pageSize = signal(20);
  totalCount = signal(0);
  rawItems = signal<ProductDto[]>([]);
  sortOption = signal<ProductSortOption>('sku-asc');

  displayedColumns = ['sku', 'name', 'createdAt'];

  items = computed(() => {
    const list = [...this.rawItems()];
    const opt = this.sortOption();
    if (opt === 'sku-asc') return list.sort((a, b) => a.sku.localeCompare(b.sku));
    if (opt === 'sku-desc') return list.sort((a, b) => b.sku.localeCompare(a.sku));
    if (opt === 'created-desc')
      return list.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    if (opt === 'created-asc')
      return list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    return list;
  });

  isEmpty = computed(() => !this.loading() && this.totalCount() === 0);

  constructor(
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    public readonly auth: AuthService,
  ) {}

  openCreateProductModal(): void {
    const ref = this.dialog.open(CreateProductDialogComponent, {
      width: '420px',
      disableClose: false,
    });
    ref
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result?: ProductDto) => {
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
      .getProductsPage(this.search() || null, this.page(), this.pageSize())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.rawItems.set(res.items);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(getApiErrorMessage(err, 'Failed to load products'));
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

  onSortChange(opt: ProductSortOption): void {
    this.sortOption.set(opt);
  }
}
