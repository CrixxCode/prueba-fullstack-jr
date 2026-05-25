import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { API_URL } from '../api.config';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'getToken',
      'refreshAccessToken',
      'logout'
    ]);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: authService
        }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should attach bearer token for non-auth requests', () => {
    authService.getToken.and.returnValue('access-token');

    http.get(`${API_URL}/users`).subscribe();

    const request = httpMock.expectOne(`${API_URL}/users`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({});
  });

  it('should not attach bearer token for auth endpoints', () => {
    authService.getToken.and.returnValue('access-token');

    http.post(`${API_URL}/auth/login`, { email: 'user@demo.com', password: 'StrongPass1!' }).subscribe();

    const request = httpMock.expectOne(`${API_URL}/auth/login`);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('should refresh token and retry request after a 401', () => {
    authService.getToken.and.returnValue('old-token');
    authService.refreshAccessToken.and.returnValue(of('new-token'));

    let response: { ok: boolean } | undefined;

    http.get<{ ok: boolean }>(`${API_URL}/users`).subscribe((result) => {
      response = result;
    });

    const firstRequest = httpMock.expectOne(`${API_URL}/users`);
    expect(firstRequest.request.headers.get('Authorization')).toBe('Bearer old-token');
    firstRequest.flush(
      { message: 'Token expirado' },
      { status: 401, statusText: 'Unauthorized' }
    );

    const retryRequest = httpMock.expectOne(`${API_URL}/users`);
    expect(retryRequest.request.headers.get('Authorization')).toBe('Bearer new-token');
    retryRequest.flush({ ok: true });

    expect(authService.refreshAccessToken).toHaveBeenCalledTimes(1);
    expect(response).toEqual({ ok: true });
  });

  it('should logout when refresh flow fails', () => {
    authService.getToken.and.returnValue('old-token');
    authService.refreshAccessToken.and.returnValue(
      throwError(() => new Error('refresh failed'))
    );

    let capturedError: unknown;

    http.get(`${API_URL}/users`).subscribe({
      next: () => fail('Expected request to fail'),
      error: (error) => {
        capturedError = error;
      }
    });

    const firstRequest = httpMock.expectOne(`${API_URL}/users`);
    firstRequest.flush(
      { message: 'Token expirado' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(authService.logout).toHaveBeenCalledTimes(1);
    expect(capturedError).toEqual(jasmine.any(Error));
  });
});
