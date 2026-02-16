import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { API_PATHS } from '../constants/api-paths';
import type { LoginRequest, LoginResponse, UserInfo } from '../models/auth.models';

export const AUTH_TOKEN_KEY = 'order_mgmt_token';
const USER_KEY = 'order_mgmt_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenSignal = signal<string | null>(this.getStoredToken());
  private userSignal = signal<UserInfo | null>(this.getStoredUser());

  token = this.tokenSignal.asReadonly();
  currentUser = this.userSignal.asReadonly();
  isAuthenticated = computed(() => !!this.tokenSignal());

  hasWriteRole(): boolean {
    const roles = this.userSignal()?.roles ?? [];
    return roles.includes('Orders.Write') || roles.includes('Orders.Admin');
  }

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {}

  login(request: LoginRequest) {
    return this.http.post<LoginResponse>(`${API_PATHS.auth}/login`, request);
  }

  setSession(response: LoginResponse): void {
    this.tokenSignal.set(response.accessToken);
    this.userSignal.set(response.user);
    sessionStorage.setItem(AUTH_TOKEN_KEY, response.accessToken);
    sessionStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }

  logout(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    sessionStorage.removeItem(AUTH_TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.tokenSignal() ?? this.getStoredToken();
  }

  private getStoredToken(): string | null {
    return sessionStorage.getItem(AUTH_TOKEN_KEY);
  }

  private getStoredUser(): UserInfo | null {
    try {
      const raw = sessionStorage.getItem(USER_KEY);
      if (!raw) return null;
      return JSON.parse(raw) as UserInfo;
    } catch {
      return null;
    }
  }
}
