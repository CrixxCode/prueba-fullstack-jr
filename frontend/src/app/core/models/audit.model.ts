export interface AuthAuditLog {
  id: string;
  eventType: string;
  isSuccess: boolean;
  userId: string | null;
  email: string | null;
  failureReason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export interface AuthAuditLogListResponse {
  page: number;
  size: number;
  totalItems: number;
  totalPages: number;
  items: AuthAuditLog[];
}

export interface AuthAuditLogQuery {
  page?: number;
  size?: number;
  eventType?: string;
  isSuccess?: boolean | null;
  email?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  fromUtc?: string;
  toUtc?: string;
}
