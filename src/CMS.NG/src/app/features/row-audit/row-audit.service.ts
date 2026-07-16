import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RowAuditEntry } from './row-audit.model';

/** 讀取任一資料列的異動歷程 (GET /api/rowaudit?tableName=&pkid=)。 */
@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/rowaudit`;

  /** 取得某一資料列的完整異動歷程，最新的在前。 */
  getForRecord(tableName: string, pkid: number | string): Observable<RowAuditEntry[]> {
    const params = new HttpParams().set('tableName', tableName).set('pkid', String(pkid));
    return this.http.get<RowAuditEntry[]>(this.baseUrl, { params });
  }
}
