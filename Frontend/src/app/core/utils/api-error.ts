import { HttpErrorResponse } from '@angular/common/http';

export function getApiErrorMessage(err: unknown, fallback = 'Something went wrong'): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body && typeof body === 'object') {
      if (typeof body.message === 'string') return body.message;
      if (typeof body.title === 'string') return body.title;
      if (body.detail && typeof body.detail === 'string') return body.detail;
      if (body.errors && typeof body.errors === 'object') {
        const first = Object.values(body.errors as Record<string, string[]>)[0];
        if (Array.isArray(first) && first[0]) return first[0];
      }
    }
    if (err.message) return err.message;
    if (err.statusText) return err.statusText;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
