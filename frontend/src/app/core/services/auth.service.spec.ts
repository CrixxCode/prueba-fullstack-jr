import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { API_URL } from '../api.config';
import { AuthResponse } from '../models/auth.model';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should persist a valid session', () => {
    const accessToken = createJwt(3600);

    service.saveSession(buildAuthResponse(accessToken));

    expect(localStorage.getItem('accessToken')).toBe(accessToken);
    expect(localStorage.getItem('refreshToken')).toBe('refresh-token');
    expect(localStorage.getItem('currentUser')).toContain('user@demo.com');
    expect(service.isLoggedIn()).toBeTrue();
  });

  it('should clear session when trying to save an expired token', () => {
    localStorage.setItem('accessToken', 'stale-token');
    localStorage.setItem('refreshToken', 'stale-refresh');
    localStorage.setItem('currentUser', '{"id":"stale"}');

    service.saveSession(buildAuthResponse(createJwt(-60)));

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('currentUser')).toBeNull();
  });

  it('should remove expired token when calling getToken', () => {
    localStorage.setItem('accessToken', createJwt(-30));

    const token = service.getToken();

    expect(token).toBeNull();
    expect(localStorage.getItem('accessToken')).toBeNull();
  });

  it('should share the same refresh request for concurrent calls', () => {
    const newAccessToken = createJwt(1800);
    localStorage.setItem('refreshToken', 'refresh-token');

    let firstResult: string | undefined;
    let secondResult: string | undefined;

    service.refreshAccessToken().subscribe((token) => {
      firstResult = token;
    });

    service.refreshAccessToken().subscribe((token) => {
      secondResult = token;
    });

    const requests = httpMock.match(`${API_URL}/auth/refresh`);
    expect(requests.length).toBe(1);
    expect(requests[0].request.body).toEqual({ refreshToken: 'refresh-token' });

    requests[0].flush(buildAuthResponse(newAccessToken));

    expect(firstResult).toBe(newAccessToken);
    expect(secondResult).toBe(newAccessToken);
    expect(localStorage.getItem('accessToken')).toBe(newAccessToken);
  });

  it('should clear local session when refresh fails', () => {
    service.saveSession(buildAuthResponse(createJwt(1800)));

    let capturedError: unknown;

    service.refreshAccessToken().subscribe({
      next: () => fail('Expected refresh error'),
      error: (error) => {
        capturedError = error;
      }
    });

    const request = httpMock.expectOne(`${API_URL}/auth/refresh`);
    request.flush(
      { message: 'Refresh invalido' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(capturedError).toBeTruthy();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('currentUser')).toBeNull();
  });

  it('should logout and navigate to login by default', () => {
    service.saveSession(buildAuthResponse(createJwt(1800)));

    service.logout();

    const request = httpMock.expectOne(`${API_URL}/auth/logout`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ refreshToken: 'refresh-token' });
    request.flush({});

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});

function buildAuthResponse(accessToken: string): AuthResponse {
  return {
    accessToken,
    refreshToken: 'refresh-token',
    user: {
      id: '00000000-0000-0000-0000-000000000001',
      email: 'user@demo.com',
      name: 'Demo User',
      role: 'user',
      avatarUrl: null,
      isActive: true
    }
  };
}

function createJwt(expiresInSecondsFromNow: number): string {
  const header = toBase64Url('{"alg":"HS256","typ":"JWT"}');
  const payload = toBase64Url(
    JSON.stringify({
      exp: Math.floor(Date.now() / 1000) + expiresInSecondsFromNow,
      sub: '00000000-0000-0000-0000-000000000001',
      email: 'user@demo.com',
      name: 'Demo User',
      role: 'user'
    })
  );

  return `${header}.${payload}.signature`;
}

function toBase64Url(value: string): string {
  return btoa(value)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
}
