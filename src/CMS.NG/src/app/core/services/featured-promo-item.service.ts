import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
  PromoClipboard,
} from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/featured-promo-items`;

  // Copy/Paste clipboard for the weekly grid; lives here so it survives reloads of the list.
  private readonly clipboardSig = signal<PromoClipboard | null>(null);
  readonly clipboard = this.clipboardSig.asReadonly();

  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, query);
  }

  getById(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${pkid}`);
  }

  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  update(request: FeaturedPromoItemRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** '+' moves the slot down (1→2), '-' moves it up (2→1); occupants are swapped. */
  move(pkid: number, direction: 'up' | 'down'): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${pkid}/move`, { direction });
  }

  copy(item: FeaturedPromoItem): void {
    this.clipboardSig.set({
      promotionPkid: item.promotionPkid,
      promoCode: item.promoCode,
      topic: item.topic,
      description: item.description,
    });
  }

  clearClipboard(): void {
    this.clipboardSig.set(null);
  }
}
