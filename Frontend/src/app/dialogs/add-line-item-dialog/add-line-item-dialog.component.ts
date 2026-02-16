import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { quantityValidators, unitPriceValidators } from '../../core/constants/validation';
import type { CreateOrderLineItemRequest, ProductDto } from '../../core/models/api.models';

export interface AddLineItemDialogData {
  products: ProductDto[];
}

@Component({
  selector: 'app-add-line-item-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './add-line-item-dialog.component.html',
  styleUrl: './add-line-item-dialog.component.scss',
})
export class AddLineItemDialogComponent {
  form: FormGroup;

  constructor(
    private readonly dialogRef: MatDialogRef<
      AddLineItemDialogComponent,
      CreateOrderLineItemRequest
    >,
    private readonly fb: FormBuilder,
    @Inject(MAT_DIALOG_DATA) public data: AddLineItemDialogData,
  ) {
    this.form = this.fb.group({
      productId: ['', Validators.required],
      quantity: [1, quantityValidators()],
      unitPrice: [0, unitPriceValidators()],
    });
  }

  get products(): ProductDto[] {
    return this.data?.products ?? [];
  }

  cancel(): void {
    this.dialogRef.close();
  }

  add(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      productId: v.productId as string,
      quantity: v.quantity as number,
      unitPrice: v.unitPrice as number,
    });
  }
}
