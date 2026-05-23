import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';

import { API_URL } from '../api.config';
import {
  CreateUserRequest,
  UpdateUserRequest,
  User,
  UserListResponse
} from '../models/user.model';

export interface UserActionEvent {
  type: 'created' | 'updated' | 'deleted';
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly usersChangedSubject = new Subject<void>();
  private readonly userActionSubject = new Subject<UserActionEvent>();

  readonly usersChanged$ = this.usersChangedSubject.asObservable();
  readonly userAction$ = this.userActionSubject.asObservable();

  getUsers(search = '', page = 1, size = 10): Observable<UserListResponse> {
    let params = new HttpParams()
      .set('page', page)
      .set('size', size);

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<UserListResponse>(`${API_URL}/users`, { params });
  }

  getUserById(id: string): Observable<User> {
    return this.http.get<User>(`${API_URL}/users/${id}`);
  }

  createUser(data: CreateUserRequest): Observable<User> {
    return this.http.post<User>(`${API_URL}/users`, data);
  }

  updateUser(id: string, data: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${API_URL}/users/${id}`, data);
  }

  deleteUser(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${API_URL}/users/${id}`);
  }

  notifyUsersChanged(): void {
    this.usersChangedSubject.next();
  }

  notifyUserAction(event: UserActionEvent): void {
    this.userActionSubject.next(event);
  }
}
