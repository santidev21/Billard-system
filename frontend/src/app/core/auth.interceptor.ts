import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

const TOKEN_KEY = 'billiard-admin-token';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem(TOKEN_KEY);
  const cloned = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(cloned).pipe(
    catchError((error) => {
      if (error.status === 401 && req.url.includes('/api/') && !req.url.includes('/api/auth/login')) {
        localStorage.removeItem(TOKEN_KEY);
        const router = inject(Router);
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
