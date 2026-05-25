import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_URL } from '../api.config';
import { UserService } from './user.service';

describe('UserService', () => {
  let service: UserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(UserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should request users with trimmed search and normalize mixed API response', () => {
    let response: ReturnType<typeof expectNormalizedUsersResult> | undefined;

    service.getUsers('  demo  ', 2, 5).subscribe((result) => {
      response = expectNormalizedUsersResult(result);
    });

    const request = httpMock.expectOne(`${API_URL}/users?page=2&size=5&search=demo`);
    expect(request.request.method).toBe('GET');

    request.flush({
      Page: 2,
      Size: 5,
      TotalItems: 3,
      TotalPages: 1,
      Items: [
        {
          Id: '00000000-0000-0000-0000-000000000001',
          Email: 'admin@demo.com',
          Name: 'Admin Demo',
          Role: 'admin',
          AvatarUrl: null,
          IsActive: true
        },
        {
          id: '00000000-0000-0000-0000-000000000002',
          email: 'user@demo.com',
          name: 'User Demo',
          role: 'user',
          avatarUrl: null,
          isActive: false
        },
        {
          id: '',
          email: 'invalid@demo.com',
          name: '',
          role: 'guest',
          isActive: true
        }
      ]
    });

    expect(response).toEqual({
      page: 2,
      size: 5,
      totalItems: 3,
      totalPages: 1,
      itemsLength: 2,
      firstUserRole: 'admin',
      secondUserIsActive: false
    });
  });

  it('should throw a friendly error when user response is invalid', () => {
    let capturedError: Error | undefined;

    service.getUserById('bad-response').subscribe({
      next: () => fail('Expected invalid response error'),
      error: (error) => {
        capturedError = error as Error;
      }
    });

    const request = httpMock.expectOne(`${API_URL}/users/bad-response`);
    expect(request.request.method).toBe('GET');
    request.flush({ unexpected: true });

    expect(capturedError).toBeDefined();
    expect(capturedError?.message).toBe('Respuesta de usuario invalida.');
  });

  it('should map created user response', () => {
    let createdUserId = '';

    service.createUser({
      name: 'Nuevo Usuario',
      email: 'nuevo@demo.com',
      password: 'StrongPass1!',
      role: 'user',
      isActive: true
    }).subscribe((user) => {
      createdUserId = user.id;
    });

    const request = httpMock.expectOne(`${API_URL}/users`);
    expect(request.request.method).toBe('POST');

    request.flush({
      Id: '00000000-0000-0000-0000-000000000123',
      Email: 'nuevo@demo.com',
      Name: 'Nuevo Usuario',
      Role: 'user',
      IsActive: true,
      AvatarUrl: null
    });

    expect(createdUserId).toBe('00000000-0000-0000-0000-000000000123');
  });
});

function expectNormalizedUsersResult(result: {
  page: number;
  size: number;
  totalItems: number;
  totalPages: number;
  items: Array<{ role: string; isActive: boolean }>;
}) {
  return {
    page: result.page,
    size: result.size,
    totalItems: result.totalItems,
    totalPages: result.totalPages,
    itemsLength: result.items.length,
    firstUserRole: result.items[0]?.role,
    secondUserIsActive: result.items[1]?.isActive
  };
}
