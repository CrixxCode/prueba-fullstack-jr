import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./auth/login/login').then((m) => m.Login)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./auth/register/register').then((m) => m.Register)
  },
  {
    path: '403',
    loadComponent: () =>
      import('./errors/forbidden/forbidden').then((m) => m.Forbidden)
  },
  {
    path: '404',
    loadComponent: () =>
      import('./errors/not-found/not-found').then((m) => m.NotFound)
  },
  {
    path: '',
    loadComponent: () =>
      import('./shared/admin-layout/admin-layout').then((m) => m.AdminLayout),
    canActivate: [authGuard],
    children: [
      {
        path: 'profile',
        loadComponent: () =>
          import('./users/profile/profile').then((m) => m.Profile)
      },
      {
        path: 'users',
        loadComponent: () =>
          import('./users/user-list/user-list').then((m) => m.UserList),
        canActivate: [adminGuard],
        children: [
          {
            path: 'new',
            loadComponent: () =>
              import('./users/user-form/user-form').then((m) => m.UserForm)
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./users/user-form/user-form').then((m) => m.UserForm)
          }
        ]
      },
      {
        path: 'audit/auth',
        loadComponent: () =>
          import('./audit/auth-logs/auth-logs').then((m) => m.AuthLogs),
        canActivate: [adminGuard]
      }
    ]
  },
  {
    path: '**',
    redirectTo: '404'
  }
];
