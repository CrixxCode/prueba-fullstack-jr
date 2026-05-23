import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

import { API_URL } from '../api.config';
import {
  AuthAuditLog,
  AuthAuditLogListResponse,
  AuthAuditLogQuery
} from '../models/audit.model';

@Injectable({
  providedIn: 'root'
})
export class AuditService {
  private readonly http = inject(HttpClient);

  getAuthLogs(query?: AuthAuditLogQuery): Observable<AuthAuditLogListResponse> {
    let params = new HttpParams();

    if (query?.page != null) {
      params = params.set('page', query.page);
    }

    if (query?.size != null) {
      params = params.set('size', query.size);
    }

    if (query?.eventType?.trim()) {
      params = params.set('eventType', query.eventType.trim().toLowerCase());
    }

    if (query?.email?.trim()) {
      params = params.set('email', query.email.trim().toLowerCase());
    }

    if (query?.isSuccess != null) {
      params = params.set('isSuccess', query.isSuccess);
    }

    if (query?.sortBy?.trim()) {
      params = params.set('sortBy', query.sortBy.trim());
    }

    if (query?.sortDir?.trim()) {
      params = params.set('sortDir', query.sortDir.trim().toLowerCase());
    }

    if (query?.fromUtc?.trim()) {
      params = params.set('fromUtc', query.fromUtc.trim());
    }

    if (query?.toUtc?.trim()) {
      params = params.set('toUtc', query.toUtc.trim());
    }

    return this.http
      .get<unknown>(`${API_URL}/audit/auth`, { params })
      .pipe(map((response) => this.normalizeListResponse(response)));
  }

  private normalizeListResponse(raw: unknown): AuthAuditLogListResponse {
    if (!raw || typeof raw !== 'object') {
      return {
        page: 1,
        size: 20,
        totalItems: 0,
        totalPages: 0,
        items: []
      };
    }

    const response = raw as Record<string, unknown>;
    const itemsSource = response['items'] ?? response['Items'];
    const items = Array.isArray(itemsSource)
      ? itemsSource
          .map((item) => this.normalizeLog(item))
          .filter((item): item is AuthAuditLog => item !== null)
      : [];

    return {
      page: this.toNumberValue(response['page'] ?? response['Page']) ?? 1,
      size: this.toNumberValue(response['size'] ?? response['Size']) ?? 20,
      totalItems: this.toNumberValue(response['totalItems'] ?? response['TotalItems']) ?? items.length,
      totalPages: this.toNumberValue(response['totalPages'] ?? response['TotalPages']) ?? 0,
      items
    };
  }

  private normalizeLog(raw: unknown): AuthAuditLog | null {
    if (!raw || typeof raw !== 'object') {
      return null;
    }

    const record = raw as Record<string, unknown>;
    const id = this.toStringValue(record['id'] ?? record['Id']);
    const eventType = this.toStringValue(record['eventType'] ?? record['EventType']);
    const createdAt = this.toStringValue(record['createdAt'] ?? record['CreatedAt']);
    const isSuccessRaw = record['isSuccess'] ?? record['IsSuccess'];

    if (!id || !eventType || !createdAt || typeof isSuccessRaw !== 'boolean') {
      return null;
    }

    return {
      id,
      eventType: eventType.toLowerCase(),
      isSuccess: isSuccessRaw,
      userId: this.toNullableStringValue(record['userId'] ?? record['UserId']),
      email: this.toNullableStringValue(record['email'] ?? record['Email']),
      failureReason: this.toNullableStringValue(record['failureReason'] ?? record['FailureReason']),
      ipAddress: this.toNullableStringValue(record['ipAddress'] ?? record['IpAddress']),
      userAgent: this.toNullableStringValue(record['userAgent'] ?? record['UserAgent']),
      createdAt
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
