/** 使用者 (AppUser) — matches the API response model. PasswordHash is backend-only and never present. */
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  passwordUpdatedTime: string | null;
  roleCount: number;
}

/** 使用者新增／修改 DTO. `userId` is ignored by the API on update; the password is never sent. */
export interface AppUserRequest {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
}

/** 使用者查詢條件. */
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
}

/** 使用者的單一角色 (含角色名稱) — matches the API `/app-users/{id}/roles` response item. */
export interface UserRole {
  roleId: string;
  roleName: string;
}
