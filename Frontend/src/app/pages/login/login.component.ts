import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { getApiErrorMessage } from '../../core/utils/api-error';

/** Only allow internal paths (e.g. /orders) to prevent open redirect. */
function getSafeReturnUrl(value: unknown, fallback: string): string {
  if (typeof value !== 'string' || !value.trim()) return fallback;
  const path = value.trim();
  if (path.startsWith('/') && !path.startsWith('//')) return path;
  return fallback;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly destroyRef = inject(DestroyRef);
  loading = false;
  error: string | null = null;
  form: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {
    this.form = this.fb.group({
      userName: ['', [Validators.required]],
      password: [''],
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const user = (this.form.get('userName')?.value as string).trim();
    const pass = (this.form.get('password')?.value as string) ?? '';
    this.loading = true;
    this.error = null;
    this.auth
      .login({ userName: user, password: pass })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
      next: (res) => {
        this.auth.setSession(res);
        this.loading = false;
        const returnUrl = getSafeReturnUrl(
          this.route.snapshot.queryParams['returnUrl'],
          '/customers',
        );
        this.router.navigateByUrl(returnUrl);
      },
      error: (err) => {
        this.error = getApiErrorMessage(err, 'Login failed.');
        this.loading = false;
      },
    });
  }
}
