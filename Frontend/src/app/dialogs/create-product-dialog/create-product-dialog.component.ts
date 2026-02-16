import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { productSkuValidators, productNameValidators } from '../../core/constants/validation';
import type { ProductDto, CreateProductRequest } from '../../core/models/api.models';

@Component({
  selector: 'app-create-product-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './create-product-dialog.component.html',
  styleUrl: './create-product-dialog.component.scss',
})
export class CreateProductDialogComponent {
  loading = false;
  error: string | null = null;
  form: FormGroup;

  constructor(
    private readonly dialogRef: MatDialogRef<CreateProductDialogComponent, ProductDto>,
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
  ) {
    this.form = this.fb.group({
      sku: ['', productSkuValidators()],
      name: ['', productNameValidators()],
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request: CreateProductRequest = {
      sku: (v.sku as string).trim(),
      name: (v.name as string).trim(),
    };

    this.loading = true;
    this.error = null;
    this.api.createProduct(request).subscribe({
      next: (product) => {
        this.dialogRef.close(product);
      },
      error: (err) => {
        this.error = getApiErrorMessage(err, 'Failed to create product');
        this.loading = false;
      },
    });
  }
}
