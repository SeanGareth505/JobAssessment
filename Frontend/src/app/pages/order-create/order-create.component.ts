import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { ApiService } from '../../core/api.service';
import { CreateProductDialogComponent } from '../../dialogs/create-product-dialog/create-product-dialog.component';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { getAllowedCurrenciesForCountry } from '../../core/constants/sadc-countries';
import {
  currencyCodeValidators,
  quantityValidators,
  unitPriceValidators,
} from '../../core/constants/validation';
import type {
  CustomerDto,
  CreateOrderRequest,
  CreateOrderLineItemRequest,
  ProductDto,
} from '../../core/models/api.models';

@Component({
  selector: 'app-order-create',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './order-create.component.html',
  styleUrl: './order-create.component.scss',
})
export class OrderCreateComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  loading = false;
  error: string | null = null;
  customers: CustomerDto[] = [];
  catalogProducts: ProductDto[] = [];
  selectedCatalogProductId = '';
  form: FormGroup;

  get allowedCurrencies(): string[] {
    const cid = this.form?.get('customerId')?.value;
    if (!cid) return [];
    const customer = this.customers.find((c) => c.id === cid);
    return customer ? getAllowedCurrenciesForCountry(customer.countryCode) : [];
  }

  constructor(
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
    private readonly router: Router,
    private readonly snackBar: MatSnackBar,
    private readonly dialog: MatDialog,
  ) {
    this.form = this.fb.group({
      customerId: ['', Validators.required],
      currencyCode: [{ value: '', disabled: true }, currencyCodeValidators()],
      lineItems: this.fb.array([]),
    });
  }

  get lineItems(): FormArray {
    return this.form.get('lineItems') as FormArray;
  }

  getProductForLine(productId: string): ProductDto | undefined {
    return this.catalogProducts.find((p) => p.id === productId);
  }

  private createLineItemGroup(productId: string, quantity = 1, unitPrice = 0): FormGroup {
    return this.fb.group({
      productId: [productId, Validators.required],
      quantity: [quantity, quantityValidators()],
      unitPrice: [unitPrice, unitPriceValidators()],
    });
  }

  ngOnInit(): void {
    this.api
      .getCustomersPage(null, 1, 500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => (this.customers = res.items),
        error: (err) => (this.error = getApiErrorMessage(err, 'Failed to load customers')),
      });
    this.api
      .getProductsPage(null, 1, 500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => (this.catalogProducts = res.items),
      });
    this.form
      .get('customerId')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const cid = this.form.get('customerId')?.value;
        const currencyControl = this.form.get('currencyCode');
        if (!cid) {
          currencyControl?.disable({ emitEvent: false });
          this.form.patchValue({ currencyCode: '' }, { emitEvent: false });
        } else {
          currencyControl?.enable({ emitEvent: false });
          const cur = getAllowedCurrenciesForCountry(
            this.customers.find((c) => c.id === cid)?.countryCode ?? '',
          );
          if (cur.length > 0 && !cur.includes(this.form.get('currencyCode')?.value)) {
            this.form.patchValue({ currencyCode: cur[0] }, { emitEvent: false });
          }
        }
      });
  }

  removeLine(i: number): void {
    if (this.lineItems.length > 1) this.lineItems.removeAt(i);
  }

  onCatalogProductSelected(productId: string): void {
    if (!productId) return;
    const product = this.catalogProducts.find((p) => p.id === productId);
    if (!product) return;
    this.lineItems.push(this.createLineItemGroup(product.id, 1, 0));
    this.selectedCatalogProductId = '';
  }

  openCreateProductModal(): void {
    const ref = this.dialog.open(CreateProductDialogComponent, {
      width: '420px',
      disableClose: false,
    });
    ref
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result?: ProductDto) => {
        if (result) {
          this.catalogProducts = [...this.catalogProducts, result];
          this.lineItems.push(this.createLineItemGroup(result.id, 1, 0));
        }
      });
  }

  createOrder(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.lineItems.length === 0) {
        this.error =
          'Add at least one product line: select a product from the catalog or create a new product.';
      }
      return;
    }
    const v = this.form.getRawValue();
    const lineItems: CreateOrderLineItemRequest[] = (
      v.lineItems as Array<{ productId: string; quantity: number; unitPrice: number }>
    )
      .filter((r) => r.productId && (r.quantity ?? 0) >= 1 && (r.unitPrice ?? 0) >= 0)
      .map((r) => ({
        productId: r.productId,
        quantity: r.quantity,
        unitPrice: r.unitPrice,
      }));
    if (lineItems.length === 0) {
      this.error = 'Add at least one product line: each line must be a product from the catalog.';
      return;
    }
    this.error = null;
    const request: CreateOrderRequest = {
      customerId: v.customerId,
      currencyCode: v.currencyCode,
      lineItems,
    };
    this.loading = true;
    this.api
      .createOrder(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loading = false;
          this.router.navigate(['/orders']);
          this.snackBar.open('Order created', undefined, { duration: 3000 });
        },
        error: (err) => {
          this.error = getApiErrorMessage(err, 'Failed to create order');
          this.loading = false;
        },
      });
  }
}
