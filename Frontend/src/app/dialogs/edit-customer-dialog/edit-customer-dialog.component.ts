import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/utils/api-error';
import { SADC_COUNTRIES } from '../../core/constants/sadc-countries';
import {
  customerNameValidators,
  emailValidators,
  countryCodeValidators,
} from '../../core/constants/validation';
import type { CustomerDto, UpdateCustomerRequest } from '../../core/models/api.models';

@Component({
  selector: 'app-edit-customer-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './edit-customer-dialog.component.html',
  styleUrl: './edit-customer-dialog.component.scss',
})
export class EditCustomerDialogComponent {
  loading = false;
  error: string | null = null;
  form: FormGroup;
  readonly countries = SADC_COUNTRIES;

  constructor(
    private readonly dialogRef: MatDialogRef<EditCustomerDialogComponent, CustomerDto>,
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
    @Inject(MAT_DIALOG_DATA) public data: CustomerDto,
  ) {
    this.form = this.fb.group({
      name: [data.name, customerNameValidators()],
      email: [data.email ?? '', emailValidators()],
      countryCode: [data.countryCode, countryCodeValidators()],
    });
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
    const request: UpdateCustomerRequest = {
      name: (v.name as string).trim(),
      email: (v.email as string)?.trim() || undefined,
      countryCode: (v.countryCode as string).trim(),
    };

    this.loading = true;
    this.error = null;
    this.api.updateCustomer(this.data.id, request).subscribe({
      next: (customer) => {
        this.loading = false;
        this.dialogRef.close(customer);
      },
      error: (err) => {
        this.error = getApiErrorMessage(err, 'Failed to update customer');
        this.loading = false;
      },
    });
  }
}
