import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

let isRefreshing = false;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.getToken();

  const cloned = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(cloned).pipe(
    catchError((error) => {
      if (error.status === 401 && !req.url.includes('/api/auth/')) {
        if (!isRefreshing) {
          isRefreshing = true;
          return from(auth.refresh()).pipe(
            switchMap(() => {
              isRefreshing = false;
              const newToken = auth.getToken();
              const retry = newToken
                ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })
                : req;
              return next(retry);
            }),
            catchError((err) => {
              isRefreshing = false;
              auth.logout();
              void router.navigate(['/login']);
              return throwError(() => err);
            })
          );
        }
        auth.logout();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
