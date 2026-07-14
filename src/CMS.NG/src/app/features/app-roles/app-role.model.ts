/** 角色 (AppRole) — matches the API response model. */
export interface AppRole {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  userCount: number;
}

/** 角色新增／修改 DTO. `roleId` is ignored by the API on update. */
export interface AppRoleRequest {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
}

/** 角色查詢條件. */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevelFrom?: number | null;
  permissionLevelTo?: number | null;
}
