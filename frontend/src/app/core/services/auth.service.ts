import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import {
  Observable,
  catchError,
  finalize,
  map,
  shareReplay,
  tap,
  throwError
} from 'rxjs';

import { API_URL } from '../api.config';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.model';
import { User } from '../models/user.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly tokenKey = 'accessToken';
  private readonly refreshTokenKey = 'refreshToken';
  private readonly userKey = 'currentUser';
  private readonly tokenLeewaySeconds = 5;
  private refreshRequest$: Observable<string> | null = null;

  login(data: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_URL}/auth/login`, data).pipe(
      tap((response) => this.saveSession(response))
    );
  }

  register(data: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_URL}/auth/register`, data).pipe(
      tap((response) => this.saveSession(response))
    );
  }

  logout(options?: { redirectToLogin?: boolean }): void {
    const refreshToken = this.getRefreshToken();
    this.clearSession();

    if (refreshToken) {
      this.http.post(`${API_URL}/auth/logout`, { refreshToken }).subscribe({
        error: () => {
          // Best effort: la sesion local ya fue cerrada.
        }
      });
    }

    if (options?.redirectToLogin === false) {
      return;
    }

    this.router.navigate(['/login']);
  }

  saveSession(response: AuthResponse): void {
    if (
      !response?.accessToken ||
      !response?.refreshToken ||
      this.isTokenExpired(response.accessToken)
    ) {
      this.clearSession();
      return;
    }

    localStorage.setItem(this.tokenKey, response.accessToken);
    localStorage.setItem(this.refreshTokenKey, response.refreshToken);
    localStorage.setItem(this.userKey, JSON.stringify(response.user));
  }

  getToken(): string | null {
    const token = localStorage.getItem(this.tokenKey);

    if (!token) {
      return null;
    }

    if (this.isTokenExpired(token)) {
      localStorage.removeItem(this.tokenKey);
      return null;
    }

    return token;
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshTokenKey);
  }

  refreshAccessToken(): Observable<string> {
    if (this.refreshRequest$) {
      return this.refreshRequest$;
    }

    const refreshToken = this.getRefreshToken();

    if (!refreshToken) {
      return throwError(() => new Error('No refresh token available'));
    }

    this.refreshRequest$ = this.http.post<AuthResponse>(`${API_URL}/auth/refresh`, {
      refreshToken
    }).pipe(
      tap((response) => this.saveSession(response)),
      map((response) => response.accessToken),
      catchError((error) => {
        this.clearSession();
        return throwError(() => error);
      }),
      finalize(() => {
        this.refreshRequest$ = null;
      }),
      shareReplay(1)
    );

    return this.refreshRequest$;
  }

  getCurrentUser(): User | null {
    const user = localStorage.getItem(this.userKey);

    if (!user) {
      return null;
    }

    try {
      return JSON.parse(user) as User;
    } catch {
      localStorage.removeItem(this.userKey);
      return null;
    }
  }

  isLoggedIn(): boolean {
    return (
      !!this.getCurrentUser() &&
      (!!this.getToken() || !!this.getRefreshToken())
    );
  }

  isAdmin(): boolean {
    return this.getCurrentUser()?.role === 'admin';
  }

  getCurrentUserId(): string | null {
    return this.getCurrentUser()?.id ?? null;
  }

  updateCurrentUser(user: User): void {
    localStorage.setItem(this.userKey, JSON.stringify(user));
  }

  private clearSession(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshTokenKey);
    localStorage.removeItem(this.userKey);
  }

  private isTokenExpired(token: string): boolean {
    const payload = this.parseJwtPayload(token);

    if (!payload?.exp) {
      return true;
    }

    const nowInSeconds = Math.floor(Date.now() / 1000);
    return payload.exp <= nowInSeconds + this.tokenLeewaySeconds;
  }

  private parseJwtPayload(token: string): { exp?: number } | null {
    const tokenParts = token.split('.');

    if (tokenParts.length !== 3) {
      return null;
    }

    try {
      const payloadJson = this.decodeBase64Url(tokenParts[1]);
      return JSON.parse(payloadJson) as { exp?: number };
    } catch {
      return null;
    }
  }

  private decodeBase64Url(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    const padding = '='.repeat((4 - (normalized.length % 4)) % 4);
    return atob(normalized + padding);
  }
}
