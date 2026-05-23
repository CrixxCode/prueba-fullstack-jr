export type UserRole = 'admin' | 'user';

export interface User {
  id: string;
  email: string;
  name: string;
  role: UserRole;
  avatarUrl?: string | null;
  isActive: boolean;
}

export interface UserListResponse {
  page: number;
  size: number;
  totalItems: number;
  totalPages: number;
  items: User[];
}

export interface CreateUserRequest {
  email: string;
  password: string;
  name: string;
  role: UserRole;
  isActive: boolean;
}

export interface UpdateUserRequest {
  name: string;
  email?: string | null;
  password?: string | null;
  role?: UserRole | null;
  isActive?: boolean | null;
}
