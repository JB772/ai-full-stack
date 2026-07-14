/** 發布狀態 (PublishStatus) — matches the API response model. */
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  courseCount: number;
  promotionCount: number;
}

/**
 * 發布狀態新增／修改 DTO.
 * `pkid` is a caller-supplied tinyint (0–255), not a generated identity: it must be sent on
 * create. On update it identifies the row and the API will not re-write it.
 */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** 發布狀態查詢條件. Bool filters are tri-state: null = 不篩選. */
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
