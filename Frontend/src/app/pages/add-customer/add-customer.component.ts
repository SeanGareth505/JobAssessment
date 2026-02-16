import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
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
import type { CreateCustomerRequest } from '../../core/models/api.models';

@Component({
  selector: 'app-add-customer',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './add-customer.component.html',
  styleUrl: './add-customer.component.scss',
})
export class AddCustomerComponent {
  private readonly destroyRef = inject(DestroyRef);
  loading = false;
  message: string | null = null;
  error: string | null = null;
  form: FormGroup;
  readonly countries = SADC_COUNTRIES;

  constructor(
    private readonly fb: FormBuilder,
    private readonly api: ApiService,
    private readonly router: Router,
  ) {
    this.form = this.fb.group({
      name: ['', customerNameValidators()],
      email: ['', emailValidators()],
      countryCode: ['ZA', countryCodeValidators()],
    });
  }

  addCustomer(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request: CreateCustomerRequest = {
      name: (v.name as string).trim(),
      email: (v.email as string)?.trim() || undefined,
      countryCode: (v.countryCode as string).trim(),
    };

    this.loading = true;
    this.error = null;
    this.message = null;

    this.api
      .createCustomer(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loading = false;
          this.message = 'Customer created.';
          this.router.navigate(['/customers']);
        },
        error: (err) => {
          this.error = getApiErrorMessage(err, 'Failed to create customer');
          this.loading = false;
        },
      });
  }
}
