import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AuthAuditLog } from '../../core/models/audit.model';
import { AuditService } from '../../core/services/audit.service';

@Component({
  selector: 'app-auth-logs',
  imports: [CommonModule, FormsModule],
  templateUrl: './auth-logs.html',
  styleUrl: './auth-logs.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuthLogs implements OnInit {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly auditService = inject(AuditService);

  logs: AuthAuditLog[] = [];
  loading = false;
  errorMessage = '';

  page = 1;
  size = 20;
  totalItems = 0;
  totalPages = 0;

  eventType = '';
  email = '';
  outcome: 'all' | 'success' | 'failed' = 'all';

  ngOnInit(): void {
    this.loadLogs();
  }

  applyFilters(): void {
    this.page = 1;
    this.loadLogs();
  }

  clearFilters(): void {
    this.page = 1;
    this.eventType = '';
    this.email = '';
    this.outcome = 'all';
    this.loadLogs();
  }

  previousPage(): void {
    if (this.page <= 1 || this.loading) {
      return;
    }

    this.page--;
    this.loadLogs();
  }

  nextPage(): void {
    if (this.page >= this.totalPages || this.loading) {
      return;
    }

    this.page++;
    this.loadLogs();
  }

  trackByLogId(_: number, log: AuthAuditLog): string {
    return log.id;
  }

  formatEventType(value: string): string {
    const normalized = value.replace(/_/g, ' ').trim();
    if (!normalized) {
      return '-';
    }

    return normalized.charAt(0).toUpperCase() + normalized.slice(1);
  }

  formatDate(value: string): string {
    const normalizedValue = /(?:[zZ]|[+\-]\d{2}:\d{2})$/.test(value)
      ? value
      : `${value}Z`;
    const parsedDate = new Date(normalizedValue);

    if (Number.isNaN(parsedDate.getTime())) {
      return value;
    }

    return parsedDate.toLocaleString('es-CO', {
      timeZone: 'America/Bogota',
      dateStyle: 'short',
      timeStyle: 'medium'
    });
  }

  private loadLogs(): void {
    this.loading = true;
    this.errorMessage = '';

    this.auditService.getAuthLogs({
      page: this.page,
      size: this.size,
      eventType: this.eventType,
      email: this.email,
      sortBy: 'createdAt',
      sortDir: 'desc',
      isSuccess:
        this.outcome === 'all'
          ? null
          : this.outcome === 'success'
            ? true
            : false
    }).subscribe({
      next: (response) => {
        this.logs = response.items;
        this.page = response.page;
        this.size = response.size;
        this.totalItems = response.totalItems;
        this.totalPages = response.totalPages;
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudieron cargar los logs.';
        this.loading = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
