export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

export interface PublishStatusLookup {
  pkid: number;
  description: string;
}
