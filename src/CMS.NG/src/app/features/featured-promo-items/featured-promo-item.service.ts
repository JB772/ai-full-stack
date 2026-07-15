import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
  MoveDirection,
  PromotionLookup,
  TrainingCenterLookup
} from './featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;
  private readonly lookupsUrl = `${environment.apiBaseUrl}/lookups`;

  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, query);
  }

  getByPkid(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${pkid}`);
  }

  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** pkid travels in the body — there is no route param on PUT. */
  update(request: FeaturedPromoItemRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** Moves the row's slot up/down; the API swaps with the adjacent row when the target slot is taken. */
  move(pkid: number, direction: MoveDirection): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${pkid}/move`, { direction });
  }

  /** TrainingCenter tabs. */
  getTrainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.lookupsUrl}/training-centers`);
  }

  /** PromoCode → Promotion_pkid resolution list, also used for the edit form's autocomplete. */
  getPromoCodes(): Observable<PromotionLookup[]> {
    return this.http.get<PromotionLookup[]>(`${this.lookupsUrl}/promo-codes`);
  }
}
