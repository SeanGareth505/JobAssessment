import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { AUTH_TOKEN_KEY } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const url = req.url;
  const isAuthLogin = url.includes('/api/auth/login');
  let token: string | null = null;
  if (!isAuthLogin) {
    const stored =
      typeof sessionStorage !== 'undefined' ? sessionStorage.getItem(AUTH_TOKEN_KEY) : null;
    token = (stored && stored.trim()) || auth.getToken() || null;
  }
  const cloned = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(cloned).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        auth.logout();
      }
      throw err;
    }),
  );
};
