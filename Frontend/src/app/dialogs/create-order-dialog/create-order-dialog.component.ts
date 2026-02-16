import { Component, DestroyRef, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { getAllowedCurrenciesForCountry } from '../../core/constants/sadc-countries';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
} from '@angular/forms';
import {
  currencyCodeValidators,
  quantityValidators,
  unitPriceValidators,
} from '../../core/constants/validation';
import type {
  CustomerDto,
  OrderDto,
  CreateOrderRequest,
  CreateOrderLineItemRequest,
  ProductDto,
} from '../../core/models/api.models';
import { CreateProductDialogComponent } from '../create-product-dialog/create-product-dialog.component';

@Component({
  selector: 'app-create-order-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './create-order-dialog.component.html',
  styleUrl: './create-order-dialog.component.scss',
})
export class CreateOrderDialogComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  loading = false;
  error: string | null = null;
  customers: CustomerDto[] = [];
  catalogProducts: ProductDto[] = [];
  form: FormGroup;

  get allowedCurrencies(): string[] {
    const cid = this.form?.get('customerId')?.value;
    if (!cid) return [];
    const customer = this.customers.find((c) => c.id === cid);
    return customer ? getAllowedCurrenciesForCountry(customer.countryCode) : [];
  }

  get lineItems(): FormArray {
    return this.form.get('lineItems') as FormArray;
  }

  constructor(
    private readonly dialogRef: MatDialogRef<CreateOrderDialogComponent, OrderDto>,
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    private readonly cdr: ChangeDetectorRef,
  ) {
    this.form = this.fb.group({
      customerId: ['', Validators.required],
      currencyCode: [{ value: '', disabled: true }, currencyCodeValidators()],
      lineItems: this.fb.array([this.createLineItemGroup('', 1, 0)]),
    });
  }

  private createLineItemGroup(productId = '', quantity = 1, unitPrice = 0): FormGroup {
    return this.fb.group({
      productId: [productId, Validators.required],
      quantity: [quantity, quantityValidators()],
      unitPrice: [unitPrice, unitPriceValidators()],
    });
  }

  getProductForLine(productId: string): ProductDto | undefined {
    return this.catalogProducts.find((p) => p.id === productId);
  }

  ngOnInit(): void {
    this.api
      .getCustomersPage(null, 1, 500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          setTimeout(() => {
            this.customers = res.items;
            this.cdr.detectChanges();
          }, 0);
        },
        error: (err) => {
          setTimeout(() => {
            this.error = getApiErrorMessage(err, 'Failed to load customers');
            this.cdr.detectChanges();
          }, 0);
        },
      });
    this.api
      .getProductsPage(null, 1, 500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          setTimeout(() => {
            this.catalogProducts = res.items;
            this.cdr.detectChanges();
          }, 0);
        },
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
          const cur = this.allowedCurrencies;
          if (cur.length > 0 && !cur.includes(this.form.get('currencyCode')?.value)) {
            this.form.patchValue({ currencyCode: cur[0] }, { emitEvent: false });
          }
        }
      });
  }

  addLine(): void {
    this.lineItems.push(this.createLineItemGroup('', 1, 0));
  }

  removeLine(i: number): void {
    if (this.lineItems.length <= 1) return;
    this.lineItems.removeAt(i);
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
          const product = result;
          setTimeout(() => {
            this.catalogProducts = [...this.catalogProducts, product];
            this.lineItems.push(this.createLineItemGroup(product.id, 1, 0));
            this.cdr.detectChanges();
          }, 0);
        }
      });
  }

  createOrder(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.lineItems.length === 0) {
        this.error =
          'Add at least one product line. All fields (product, quantity, unit price) are required.';
      }
      return;
    }
    const v = this.form.getRawValue();
    const lineItems: CreateOrderLineItemRequest[] = (
      v.lineItems as Array<{ productId: string; quantity: number; unitPrice: number }>
    )
      .filter((r) => r.productId && (r.quantity ?? 0) >= 1 && (r.unitPrice ?? 0) >= 0)
      .map((r) => ({ productId: r.productId, quantity: r.quantity, unitPrice: r.unitPrice }));
    if (lineItems.length === 0) {
      this.error =
        'Add at least one product line. Each line must have a product, quantity ≥ 1, and unit price ≥ 0.';
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
        next: (order) => {
          this.loading = false;
          this.dialogRef.close(order);
        },
        error: (err) => {
          this.error = getApiErrorMessage(err, 'Failed to create order');
          this.loading = false;
        },
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
