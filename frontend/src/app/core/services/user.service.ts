import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, Subject, map } from 'rxjs';

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

    return this.http
      .get<unknown>(`${API_URL}/users`, { params })
      .pipe(map((response) => this.normalizeUserListResponse(response)));
  }

  getUserById(id: string): Observable<User> {
    return this.http
      .get<unknown>(`${API_URL}/users/${id}`)
      .pipe(map((response) => this.mapUserResponse(response)));
  }

  createUser(data: CreateUserRequest): Observable<User> {
    return this.http
      .post<unknown>(`${API_URL}/users`, data)
      .pipe(map((response) => this.mapUserResponse(response)));
  }

  updateUser(id: string, data: UpdateUserRequest): Observable<User> {
    return this.http
      .put<unknown>(`${API_URL}/users/${id}`, data)
      .pipe(map((response) => this.mapUserResponse(response)));
  }

  uploadAvatar(id: string, file: File): Observable<User> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http
      .post<unknown>(`${API_URL}/users/${id}/avatar`, formData)
      .pipe(map((response) => this.mapUserResponse(response)));
  }

  deleteAvatar(id: string): Observable<User> {
    return this.http
      .delete<unknown>(`${API_URL}/users/${id}/avatar`)
      .pipe(map((response) => this.mapUserResponse(response)));
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

  private mapUserResponse(raw: unknown): User {
    const normalizedUser = this.normalizeUser(raw);

    if (!normalizedUser) {
      throw new Error('Respuesta de usuario invalida.');
    }

    return normalizedUser;
  }

  private normalizeUserListResponse(raw: unknown): UserListResponse {
    if (!raw || typeof raw !== 'object') {
      return {
        page: 1,
        size: 10,
        totalItems: 0,
        totalPages: 0,
        items: []
      };
    }

    const response = raw as Record<string, unknown>;
    const itemsSource = response['items'] ?? response['Items'];
    const parsedItems = Array.isArray(itemsSource)
      ? itemsSource
          .map((item) => this.normalizeUser(item))
          .filter((item): item is User => item !== null)
      : [];

    return {
      page: this.toNumberValue(response['page'] ?? response['Page']) ?? 1,
      size: this.toNumberValue(response['size'] ?? response['Size']) ?? 10,
      totalItems: this.toNumberValue(response['totalItems'] ?? response['TotalItems']) ?? parsedItems.length,
      totalPages: this.toNumberValue(response['totalPages'] ?? response['TotalPages']) ?? 0,
      items: parsedItems
    };
  }

  private normalizeUser(raw: unknown): User | null {
    if (!raw || typeof raw !== 'object') {
      return null;
    }

    const response = raw as Record<string, unknown>;

    const id = this.toStringValue(response['id'] ?? response['Id']);
    const email = this.toStringValue(response['email'] ?? response['Email']);
    const name = this.toStringValue(response['name'] ?? response['Name']);
    const role = this.toStringValue(response['role'] ?? response['Role'])?.toLowerCase();

    if (!id || !email || !name || (role !== 'admin' && role !== 'user')) {
      return null;
    }

    const isActiveRaw = response['isActive'] ?? response['IsActive'];

    return {
      id,
      email,
      name,
      role,
      avatarUrl: this.toNullableStringValue(response['avatarUrl'] ?? response['AvatarUrl']),
      isActive: typeof isActiveRaw === 'boolean' ? isActiveRaw : true
    };
  }

  private toStringValue(value: unknown): string | null {
    if (typeof value !== 'string') {
      return null;
    }

    const trimmed = value.trim();
    return trimmed ? trimmed : null;
  }

  private toNullableStringValue(value: unknown): string | null {
    if (value == null) {
      return null;
    }

    return this.toStringValue(value);
  }

  private toNumberValue(value: unknown): number | null {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value;
    }

    if (typeof value === 'string') {
      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    }

    return null;
  }
}
