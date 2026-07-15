export interface FeaturedPromoItem {
  pkid: number;
  scheduleOn: string; // ISO date from the API
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
  promoCode: string;
}

export interface FeaturedPromoItemRequest {
  pkid: number;
  scheduleOn: string; // 'yyyy-MM-dd'
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
}

export interface FeaturedPromoItemQuery {
  trainingCenterPkid?: number | null;
  scheduleFrom?: string | null;
  scheduleTo?: string | null;
}

/** Values carried by the Copy/Paste workflow on the weekly grid. */
export interface PromoClipboard {
  promotionPkid: number;
  promoCode: string;
  topic: string;
  description: string;
}

export interface TrainingCenterLookup {
  pkid: number;
  name: string;
}

export interface PromotionLookup {
  pkid: number;
  promoCode: string;
  topic: string;
  description: string;
}
