import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AppUser, AppUserQuery, AppUserRequest, UserRole } from './app-user.model';

@Injectable({ providedIn: 'root' })
export class AppUserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-users`;

  getAll(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl);
  }

  query(query: AppUserQuery): Observable<AppUser[]> {
    return this.http.post<AppUser[]>(`${this.baseUrl}/query`, query);
  }

  getByPkid(pkid: number): Observable<AppUser> {
    return this.http.get<AppUser>(`${this.baseUrl}/${pkid}`);
  }

  create(request: AppUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.baseUrl, request);
  }

  update(request: AppUserRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** 取得使用者目前的角色清單 (含角色名稱). */
  getRoles(pkid: number): Observable<UserRole[]> {
    return this.http.get<UserRole[]>(`${this.baseUrl}/${pkid}/roles`);
  }

  /** 指派角色給使用者 (僅限 Admin). 回傳更新後的角色清單. */
  assignRole(pkid: number, roleId: string): Observable<UserRole[]> {
    return this.http.post<UserRole[]>(`${this.baseUrl}/${pkid}/roles`, { roleId });
  }

  /** 移除使用者的角色 (僅限 Admin). */
  removeRole(pkid: number, roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}/roles/${encodeURIComponent(roleId)}`);
  }
}
