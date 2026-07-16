import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { RowAuditEntry } from '../row-audit.model';
import { RowAuditService } from '../row-audit.service';

/**
 * 可重複使用的「異動紀錄 History」徽章。放在任一 detail / form 頁的工具列，傳入該頁的
 * `tableName` 與目前資料列的 `pkid`。載入時即抓取該資料列的異動歷程，並在徽章上就地顯示
 * 「最新一筆」(類型 + 異動者 + 時間)；點擊後開啟對話框列出完整異動歷程 (最新在前)。
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [DatePipe, DialogModule],
  templateUrl: './row-audit-badge.html',
  styleUrl: './row-audit-badge.scss'
})
export class RowAuditBadgeComponent {
  private readonly service = inject(RowAuditService);

  /** 異動所屬的資料表名稱 (例如 "Course")，對應 dbo.RowAudit.TableName。 */
  readonly tableName = input.required<string>();

  /** 目前資料列的主鍵值。0 或空值時不抓取 (例如新增表單尚無資料列)。 */
  readonly pkid = input.required<number>();

  protected readonly entries = signal<RowAuditEntry[]>([]);
  protected readonly loading = signal(false);
  protected readonly dialogVisible = signal(false);

  /** 服務回傳「最新在前」，因此第一筆即為最近一次異動。 */
  protected readonly latest = computed<RowAuditEntry | null>(() => this.entries()[0] ?? null);

  constructor() {
    // Re-fetch whenever the record identity changes (handles pages that set pkid asynchronously).
    effect(() => {
      const table = this.tableName();
      const id = this.pkid();
      if (table && id > 0) {
        this.load(table, id);
      }
    });
  }

  protected openDialog(): void {
    this.dialogVisible.set(true);
  }

  private load(tableName: string, pkid: number): void {
    this.loading.set(true);
    this.service.getForRecord(tableName, pkid).subscribe({
      next: entries => {
        this.entries.set(entries);
        this.loading.set(false);
      },
      error: () => {
        this.entries.set([]);
        this.loading.set(false);
      }
    });
  }
}
