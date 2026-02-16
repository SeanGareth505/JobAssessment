import { Component, Inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { quantityValidators, unitPriceValidators } from '../../core/constants/validation';
import type {
  OrderDto,
  UpdateOrderRequest,
  CreateOrderLineItemRequest,
  ProductDto,
} from '../../core/models/api.models';

@Component({
  selector: 'app-edit-order-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './edit-order-dialog.component.html',
  styleUrl: './edit-order-dialog.component.scss',
})
export class EditOrderDialogComponent implements OnInit {
  loading = false;
  error: string | null = null;
  form: FormGroup;
  catalogProducts: ProductDto[] = [];

  constructor(
    private readonly dialogRef: MatDialogRef<EditOrderDialogComponent, OrderDto>,
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
    private readonly cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: OrderDto,
  ) {
    this.form = this.buildForm();
  }

  get lineItems(): FormArray {
    return this.form.get('lineItems') as FormArray;
  }

  ngOnInit(): void {
    this.api.getProductsPage(null, 1, 500).subscribe({
      next: (res) => {
        setTimeout(() => {
          this.catalogProducts = res.items;
          this.cdr.detectChanges();
        }, 0);
      },
    });
  }

  private buildForm(): FormGroup {
    const items = this.data?.lineItems?.length ? this.data.lineItems : [];
    const lineItemsArray = this.fb.array(
      items.map((li) => {
        const productId = li.productId ?? '';
        return this.fb.group({
          productId: [productId, Validators.required],
          quantity: [li.quantity, quantityValidators()],
          unitPrice: [li.unitPrice, unitPriceValidators()],
        });
      }),
    );
    if (lineItemsArray.length === 0) {
      lineItemsArray.push(this.createLineItemGroup());
    }
    return this.fb.group({ lineItems: lineItemsArray });
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

  addLine(): void {
    this.lineItems.push(this.createLineItemGroup());
  }

  removeLine(i: number): void {
    if (this.lineItems.length <= 1) return;
    setTimeout(() => {
      this.lineItems.removeAt(i);
      this.cdr.detectChanges();
    }, 0);
  }

  cancel(): void {
    this.dialogRef.close();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
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
      this.error =
        'At least one product line is required. Every line must have a product selected from the catalog.';
      return;
    }
    const request: UpdateOrderRequest = { lineItems };
    this.loading = true;
    this.error = null;
    this.api.updateOrder(this.data.id, request).subscribe({
      next: (order) => {
        this.loading = false;
        this.dialogRef.close(order);
      },
      error: (err) => {
        this.error = getApiErrorMessage(err, 'Failed to update order');
        this.loading = false;
      },
    });
  }
}
