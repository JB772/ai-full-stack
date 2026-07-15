import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AutoCompleteModule, AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { ConfirmationService, MessageService } from 'primeng/api';
import {
  FeaturedPromoItem,
  MoveDirection,
  PromotionLookup,
  TrainingCenterLookup
} from '../featured-promo-item.model';
import { FeaturedPromoItemService } from '../featured-promo-item.service';
import { addDays, dayLabel, fromIsoDate, mondayOf, toIsoDate, weekDays, weekRangeLabel } from '../week.util';

const CENTER_KEY = 'featured-promo-item-center';
const WEEK_KEY = 'featured-promo-item-week';

/** The three slots every day exposes. */
const SLOTS = [1, 2, 3] as const;

/** Which cell (day + slot) is currently open in the inline editor. */
interface EditingCell {
  scheduleOn: string;
  slot: number;
  pkid: number; // 0 when adding into an empty slot
}

/** The inline edit form's fields. promotionPkid is resolved from promoCode on save. */
interface EditModel {
  promoCode: string;
  topic: string;
  description: string;
}

/** Row values captured by Copy, replayed by Paste into an empty slot. */
interface Clipboard {
  promoCode: string;
  topic: string;
  description: string;
}

@Component({
  selector: 'app-featured-promo-item-list',
  imports: [FormsModule, ButtonModule, InputTextModule, AutoCompleteModule],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss'
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly slots = SLOTS;

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly activeCenterPkid = signal<number | null>(null);
  protected readonly promoCodes = signal<PromotionLookup[]>([]);

  protected readonly weekMonday = signal<Date>(mondayOf(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);

  protected readonly editing = signal<EditingCell | null>(null);
  protected editModel: EditModel = { promoCode: '', topic: '', description: '' };
  protected readonly clipboard = signal<Clipboard | null>(null);

  /** Autocomplete suggestions for the PromoCode field. */
  protected readonly promoSuggestions = signal<PromotionLookup[]>([]);

  /** The seven Monday→Sunday dates of the current week. */
  protected readonly days = computed(() => weekDays(this.weekMonday()));
  protected readonly weekLabel = computed(() => weekRangeLabel(this.weekMonday()));

  protected readonly dayLabel = dayLabel;
  protected readonly toIsoDate = toIsoDate;

  ngOnInit(): void {
    this.restoreState();
    this.loading.set(true);

    forkJoin({
      centers: this.service.getTrainingCenters(),
      promoCodes: this.service.getPromoCodes()
    }).subscribe({
      next: ({ centers, promoCodes }) => {
        this.trainingCenters.set(centers);
        this.promoCodes.set(promoCodes);

        // Default to the first tab when nothing was restored (or the restored one no longer exists).
        const restored = this.activeCenterPkid();
        const valid = restored != null && centers.some(c => c.pkid === restored);
        this.activeCenterPkid.set(valid ? restored : (centers[0]?.pkid ?? null));

        this.load();
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得訓練中心／促銷資料。' });
      }
    });
  }

  protected load(): void {
    const centerPkid = this.activeCenterPkid();
    if (centerPkid == null) {
      this.items.set([]);
      return;
    }

    const monday = this.weekMonday();
    this.loading.set(true);
    this.service.query({
      trainingCenterPkid: centerPkid,
      scheduleOnFrom: toIsoDate(monday),
      scheduleOnTo: toIsoDate(addDays(monday, 6))
    }).subscribe({
      next: items => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得上稿資料。' });
      }
    });
  }

  // ---------- TrainingCenter tabs ----------

  protected selectCenter(pkid: number): void {
    if (pkid === this.activeCenterPkid()) {
      return;
    }
    this.cancelEdit();
    this.activeCenterPkid.set(pkid);
    sessionStorage.setItem(CENTER_KEY, String(pkid));
    this.load();
  }

  // ---------- Week navigation ----------

  protected prevWeek(): void {
    this.shiftWeek(-7);
  }

  protected nextWeek(): void {
    this.shiftWeek(7);
  }

  private shiftWeek(days: number): void {
    this.cancelEdit();
    const monday = addDays(this.weekMonday(), days);
    this.weekMonday.set(monday);
    sessionStorage.setItem(WEEK_KEY, toIsoDate(monday));
    this.load();
  }

  // ---------- Grid lookup ----------

  /** The item scheduled at the given day + slot, or undefined when the slot is empty. */
  protected itemAt(day: Date, slot: number): FeaturedPromoItem | undefined {
    const iso = toIsoDate(day);
    return this.items().find(i => i.scheduleOn === iso && i.slot === slot);
  }

  protected isEditing(day: Date, slot: number): boolean {
    const cell = this.editing();
    return cell != null && cell.scheduleOn === toIsoDate(day) && cell.slot === slot;
  }

  // ---------- Inline edit / new ----------

  /** Opens the inline editor for a slot. `item` is undefined for an empty slot (New). */
  protected startEdit(day: Date, slot: number, item?: FeaturedPromoItem): void {
    this.editing.set({ scheduleOn: toIsoDate(day), slot, pkid: item?.pkid ?? 0 });
    this.editModel = item
      ? { promoCode: item.promotion?.promoCode ?? '', topic: item.topic, description: item.description }
      : { promoCode: '', topic: '', description: '' };
  }

  protected cancelEdit(): void {
    this.editing.set(null);
    this.editModel = { promoCode: '', topic: '', description: '' };
  }

  protected completePromo(event: AutoCompleteCompleteEvent): void {
    const q = event.query.toLowerCase();
    this.promoSuggestions.set(this.promoCodes().filter(p => p.promoCode.toLowerCase().includes(q)));
  }

  /** When a suggestion is picked, fill any blank Topic/Description from the promotion for convenience. */
  protected onPromoSelect(promo: PromotionLookup): void {
    this.editModel.promoCode = promo.promoCode;
    if (!this.editModel.topic.trim()) {
      this.editModel.topic = promo.topic;
    }
    if (!this.editModel.description.trim()) {
      this.editModel.description = promo.description;
    }
  }

  /** Resolves the typed PromoCode to its Promotion_pkid (exact, case-insensitive). */
  protected resolvePromotionPkid(code: string): number | null {
    const match = this.promoCodes().find(p => p.promoCode.toLowerCase() === code.trim().toLowerCase());
    return match?.pkid ?? null;
  }

  protected save(): void {
    const cell = this.editing();
    if (cell == null) {
      return;
    }

    const promoCode = typeof this.editModel.promoCode === 'string'
      ? this.editModel.promoCode
      : (this.editModel.promoCode as PromotionLookup).promoCode; // autocomplete may hand back the object
    const promotionPkid = this.resolvePromotionPkid(promoCode);

    if (promotionPkid == null) {
      this.messageService.add({ severity: 'warn', summary: '找不到 PromoCode', detail: `無此促銷代碼「${promoCode}」。` });
      return;
    }
    if (!this.editModel.topic.trim() || !this.editModel.description.trim()) {
      this.messageService.add({ severity: 'warn', summary: '欄位不完整', detail: '請填寫標題與說明。' });
      return;
    }

    const centerPkid = this.activeCenterPkid();
    if (centerPkid == null) {
      return;
    }

    const request = {
      pkid: cell.pkid,
      scheduleOn: cell.scheduleOn,
      trainingCenterPkid: centerPkid,
      slot: cell.slot,
      promotionPkid,
      topic: this.editModel.topic.trim(),
      description: this.editModel.description.trim()
    };

    const isEdit = cell.pkid > 0;
    const op: Observable<unknown> = isEdit ? this.service.update(request) : this.service.create(request);

    op.subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: isEdit ? '儲存成功' : '新增成功',
          detail: `${cell.scheduleOn} 第 ${cell.slot} 欄位已${isEdit ? '更新' : '建立'}。`
        });
        this.cancelEdit();
        this.load();
      },
      error: err => this.messageService.add({
        severity: 'error',
        summary: '儲存失敗',
        detail: err?.error?.message ?? '儲存上稿項目時發生錯誤。'
      })
    });
  }

  // ---------- Copy / Paste ----------

  protected copy(item: FeaturedPromoItem): void {
    this.clipboard.set({
      promoCode: item.promotion?.promoCode ?? '',
      topic: item.topic,
      description: item.description
    });
    this.messageService.add({ severity: 'info', summary: '已複製', detail: '已複製此列，可貼上到空欄位。' });
  }

  protected paste(day: Date, slot: number): void {
    const clip = this.clipboard();
    if (clip == null) {
      return;
    }

    const centerPkid = this.activeCenterPkid();
    const promotionPkid = this.resolvePromotionPkid(clip.promoCode);
    if (centerPkid == null || promotionPkid == null) {
      this.messageService.add({ severity: 'warn', summary: '無法貼上', detail: '複製來源的 PromoCode 已失效。' });
      return;
    }

    this.service.create({
      pkid: 0,
      scheduleOn: toIsoDate(day),
      trainingCenterPkid: centerPkid,
      slot,
      promotionPkid,
      topic: clip.topic,
      description: clip.description
    }).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '貼上成功', detail: `已貼上到第 ${slot} 欄位。` });
        this.load();
      },
      error: err => this.messageService.add({
        severity: 'error',
        summary: '貼上失敗',
        detail: err?.error?.message ?? '貼上時發生錯誤。'
      })
    });
  }

  // ---------- Slot move (＋ / －) ----------

  protected move(item: FeaturedPromoItem, direction: MoveDirection): void {
    this.cancelEdit();
    this.service.move(item.pkid, direction).subscribe({
      next: () => this.load(),
      error: err => this.messageService.add({
        severity: 'warn',
        summary: '無法移動',
        detail: err?.error?.message ?? '移動欄位時發生錯誤。'
      })
    });
  }

  // ---------- Delete ----------

  protected confirmDelete(item: FeaturedPromoItem): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除 ${item.scheduleOn} 第 ${item.slot} 欄位「${item.topic}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(item)
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '刪除成功', detail: '上稿項目已刪除。' });
        this.load();
      },
      error: err => this.messageService.add({
        severity: 'error',
        summary: '刪除失敗',
        detail: err?.error?.message ?? '刪除上稿項目時發生錯誤。'
      })
    });
  }

  // ---------- State persistence ----------

  private restoreState(): void {
    const center = sessionStorage.getItem(CENTER_KEY);
    if (center) {
      const parsed = Number(center);
      if (!Number.isNaN(parsed)) {
        this.activeCenterPkid.set(parsed);
      }
    }

    const week = fromIsoDate(sessionStorage.getItem(WEEK_KEY));
    if (week) {
      this.weekMonday.set(mondayOf(week));
    }
  }
}
