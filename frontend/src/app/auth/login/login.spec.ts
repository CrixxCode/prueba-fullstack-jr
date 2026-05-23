import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthResponse } from '../../core/models/auth.model';
import { AuthService } from '../../core/services/auth.service';
import { Login } from './login';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let router: Router;
  let authServiceMock: jasmine.SpyObj<AuthService>;

  const authResponse: AuthResponse = {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    user: {
      id: '00000000-0000-0000-0000-000000000001',
      email: 'user@demo.com',
      name: 'Demo User',
      role: 'user',
      isActive: true,
      avatarUrl: null
    }
  };

  beforeEach(async () => {
    authServiceMock = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'isAdmin']);
    authServiceMock.isAdmin.and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: authServiceMock
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture.detectChanges();
  });

  it('should validate form and prevent submit when invalid', () => {
    component.form.setValue({
      email: 'correo-invalido',
      password: ''
    });

    component.submit();

    expect(component.form.invalid).toBeTrue();
    expect(component.form.controls.email.touched).toBeTrue();
    expect(component.form.controls.password.touched).toBeTrue();
    expect(authServiceMock.login).not.toHaveBeenCalled();
  });

  it('should submit successfully and navigate to profile for non-admin users', () => {
    authServiceMock.login.and.returnValue(of(authResponse));

    component.form.setValue({
      email: 'user@demo.com',
      password: 'Secret123!'
    });

    component.submit();

    expect(authServiceMock.login).toHaveBeenCalledWith({
      email: 'user@demo.com',
      password: 'Secret123!'
    });
    expect(authServiceMock.isAdmin).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/profile']);
    expect(component.loading).toBeFalse();
    expect(component.errorMessage).toBe('');
  });

  it('should show a friendly error message when login fails without backend message', () => {
    authServiceMock.login.and.returnValue(throwError(() => ({ error: {} })));

    component.form.setValue({
      email: 'user@demo.com',
      password: 'Secret123!'
    });

    component.submit();
    fixture.detectChanges();

    expect(component.loading).toBeFalse();
    expect(component.errorMessage).toBe('No se pudo iniciar sesion.');

    const alertElement = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null;
    expect(alertElement).not.toBeNull();
    expect(alertElement?.textContent).toContain('No se pudo iniciar sesion.');
  });
});
