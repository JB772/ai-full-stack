import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { FeaturedPromoItemList } from './featured-promo-item-list';
import { FeaturedPromoItemService } from '../featured-promo-item.service';
import { FeaturedPromoItem, PromotionLookup, TrainingCenterLookup } from '../featured-promo-item.model';

const CENTERS: TrainingCenterLookup[] = [
  { pkid: 1, name: '台北', displayOrder: 1 },
  { pkid: 2, name: '新竹', displayOrder: 2 }
];

const PROMOS: PromotionLookup[] = [
  { pkid: 10, promoCode: '20251204_SkillTrainAI', topic: '成為能AI協作的程式設計師', description: '轉職就業養成班' },
  { pkid: 20, promoCode: '251211_GoogleAI', topic: 'Google AI工具一次掌握', description: '不需技術基礎' }
];

function makeItem(overrides: Partial<FeaturedPromoItem>): FeaturedPromoItem {
  return {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    trainingCenter: { pkid: 1, name: '台北' },
    promotion: { pkid: 10, promoCode: '20251204_SkillTrainAI' },
    ...overrides
  };
}

// The seeded week is 2026-03-16 (Mon) .. 2026-03-22 (Sun): Monday slots 1 and 2 are filled.
const MON_SLOT_1 = makeItem({ pkid: 1, slot: 1 });
const MON_SLOT_2 = makeItem({
  pkid: 2, slot: 2, promotionPkid: 20, topic: 'Google AI工具一次掌握', description: '不需技術基礎',
  promotion: { pkid: 20, promoCode: '251211_GoogleAI' }
});

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: FeaturedPromoItemList;
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let confirmationService: ConfirmationService;

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService', [
      'getTrainingCenters', 'getPromoCodes', 'query', 'create', 'update', 'delete', 'move'
    ]);
    service.getTrainingCenters.and.returnValue(of(CENTERS));
    service.getPromoCodes.and.returnValue(of(PROMOS));
    service.query.and.callFake(() => of([makeItem({ ...MON_SLOT_1 }), makeItem({ ...MON_SLOT_2 })]));
    service.create.and.returnValue(of(makeItem({ pkid: 7 })));
    service.update.and.returnValue(of(void 0));
    service.delete.and.returnValue(of(void 0));
    service.move.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: FeaturedPromoItemService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    // Pin the week to the seeded one before init runs, so itemAt lines up with the mock data.
    api().weekMonday.set(new Date(2026, 2, 16));
  });

  afterEach(() => sessionStorage.clear());

  /** The component's members are `protected`; they exist at runtime. */
  function api(): any {
    return component as any;
  }

  it('loads centres, promo codes and the first tab’s week on init', () => {
    fixture.detectChanges();

    expect(service.getTrainingCenters).toHaveBeenCalledTimes(1);
    expect(service.getPromoCodes).toHaveBeenCalledTimes(1);
    expect(api().activeCenterPkid()).toBe(1); // defaults to the first tab
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({
      trainingCenterPkid: 1,
      scheduleOnFrom: '2026-03-16',
      scheduleOnTo: '2026-03-22'
    }));
    expect(api().items().length).toBe(2);
  });

  it('renders a tab per training centre', () => {
    fixture.detectChanges();

    const tabs = fixture.nativeElement.querySelectorAll('.center-tab');
    expect(tabs.length).toBe(2);
    expect(tabs[0].textContent).toContain('台北');
    expect(tabs[1].textContent).toContain('新竹');
  });

  it('renders seven day blocks and resolves the PromoCode for a filled slot', () => {
    fixture.detectChanges();

    const days = fixture.nativeElement.querySelectorAll('.day-block');
    expect(days.length).toBe(7);

    // Monday (first block) slot 1 shows the resolved PromoCode + topic.
    const monday = days[0];
    expect(monday.textContent).toContain('3/16 (一)');
    expect(monday.textContent).toContain('20251204_SkillTrainAI');
    expect(monday.textContent).toContain('成為能AI協作的程式設計師');
  });

  it('switches the active tab, cancels editing and reloads for that centre', () => {
    fixture.detectChanges();
    service.query.calls.reset();

    api().selectCenter(2);

    expect(api().activeCenterPkid()).toBe(2);
    expect(sessionStorage.getItem('featured-promo-item-center')).toBe('2');
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ trainingCenterPkid: 2 }));
  });

  it('navigates to the next and previous week, persisting the Monday', () => {
    fixture.detectChanges();

    api().nextWeek();
    expect(sessionStorage.getItem('featured-promo-item-week')).toBe('2026-03-23');
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({
      scheduleOnFrom: '2026-03-23', scheduleOnTo: '2026-03-29'
    }));

    api().prevWeek();
    expect(sessionStorage.getItem('featured-promo-item-week')).toBe('2026-03-16');
  });

  it('locates the item scheduled at a given day + slot', () => {
    fixture.detectChanges();

    const monday = new Date(2026, 2, 16);
    expect(api().itemAt(monday, 1)?.pkid).toBe(1);
    expect(api().itemAt(monday, 2)?.pkid).toBe(2);
    expect(api().itemAt(monday, 3)).toBeUndefined(); // slot 3 is empty
  });

  it('resolves a typed PromoCode to its Promotion_pkid (case-insensitive)', () => {
    fixture.detectChanges();

    expect(api().resolvePromotionPkid('251211_googleai')).toBe(20);
    expect(api().resolvePromotionPkid('does-not-exist')).toBeNull();
  });

  it('opens the editor for an existing slot pre-filled, and saves via update', () => {
    fixture.detectChanges();

    const monday = new Date(2026, 2, 16);
    api().startEdit(monday, 1, MON_SLOT_1);

    expect(api().editModel.promoCode).toBe('20251204_SkillTrainAI');
    expect(api().editModel.topic).toBe('成為能AI協作的程式設計師');

    api().editModel.topic = '改過的標題';
    api().save();

    expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({
      pkid: 1, slot: 1, promotionPkid: 10, topic: '改過的標題'
    }));
    expect(api().editing()).toBeNull();
  });

  it('opens the editor for an empty slot and saves via create (pkid 0)', () => {
    fixture.detectChanges();

    const monday = new Date(2026, 2, 16);
    api().startEdit(monday, 3); // empty slot
    api().editModel = { promoCode: '251211_GoogleAI', topic: 'T', description: 'D' };
    api().save();

    expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
      pkid: 0, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 3, promotionPkid: 20
    }));
  });

  it('warns and does not save when the PromoCode cannot be resolved', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().startEdit(new Date(2026, 2, 16), 3);
    api().editModel = { promoCode: 'unknown', topic: 'T', description: 'D' };
    api().save();

    expect(service.create).not.toHaveBeenCalled();
    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn' }));
  });

  it('copies a row then pastes it into an empty slot via create', () => {
    fixture.detectChanges();

    api().copy(MON_SLOT_1);
    expect(api().clipboard()?.promoCode).toBe('20251204_SkillTrainAI');

    api().paste(new Date(2026, 2, 18), 2); // Wednesday slot 2
    expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
      pkid: 0, scheduleOn: '2026-03-18', slot: 2, promotionPkid: 10
    }));
  });

  it('moves a slot down and reloads', () => {
    fixture.detectChanges();
    service.query.calls.reset();

    api().move(MON_SLOT_1, 'down');

    expect(service.move).toHaveBeenCalledWith(1, 'down');
    expect(service.query).toHaveBeenCalledTimes(1); // reloaded
  });

  it('surfaces the API message when a move hits the boundary', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    service.move.and.returnValue(throwError(() => ({ error: { message: '已達邊界，無法再移動。' } })));
    fixture.detectChanges();

    api().move(MON_SLOT_1, 'up');

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'warn', detail: '已達邊界，無法再移動。' })
    );
  });

  it('deletes a slot after confirmation and reloads', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();
    service.query.calls.reset();

    api().confirmDelete(MON_SLOT_1);

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('surfaces a slot-conflict (409) message when a save is rejected', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    service.create.and.returnValue(
      throwError(() => ({ error: { message: '2026-03-16 訓練中心第 3 欄位已有排程，請改用其他欄位。' } }))
    );
    fixture.detectChanges();

    api().startEdit(new Date(2026, 2, 16), 3);
    api().editModel = { promoCode: '251211_GoogleAI', topic: 'T', description: 'D' };
    api().save();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: jasmine.stringContaining('已有排程') })
    );
  });

  it('restores the active tab and week from session storage on init', () => {
    sessionStorage.setItem('featured-promo-item-center', '2');
    sessionStorage.setItem('featured-promo-item-week', '2026-03-23');

    const fresh = TestBed.createComponent(FeaturedPromoItemList);
    fresh.detectChanges();

    const restored = fresh.componentInstance as any;
    expect(restored.activeCenterPkid()).toBe(2);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({
      trainingCenterPkid: 2, scheduleOnFrom: '2026-03-23'
    }));
  });
});
