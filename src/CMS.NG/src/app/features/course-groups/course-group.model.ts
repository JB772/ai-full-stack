/** 課程群組 (CourseGroup) — matches the API response model. */
export interface CourseGroup {
  pkid: number;
  description: string;
  /**
   * 課程數. FK_Course_CourseGroup cascades on delete, so a group with courses must never be deleted —
   * the API returns 409 rather than letting SQL Server destroy them.
   */
  courseCount: number;
  partnerCourseGroupCount: number;
}

/**
 * 課程群組新增／修改 DTO.
 * `pkid` is a smallint IDENTITY: send 0 on create (the DB generates the real key); on update it
 * identifies the row and travels in the PUT body, not the route.
 */
export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

/** 課程群組查詢條件. Description is the table's only non-key column, so keyword is the only filter. */
export interface CourseGroupQuery {
  keyword?: string | null;
}
