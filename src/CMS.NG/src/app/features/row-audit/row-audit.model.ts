/**
 * 一筆資料異動稽核記錄 (RowAudit) — matches the /api/rowaudit response projection.
 * `dateTime` arrives as an ISO string over JSON.
 */
export interface RowAuditEntry {
  dateTime: string;
  userName: string;
  actionType: string;
  actionDesc: string | null;
}
