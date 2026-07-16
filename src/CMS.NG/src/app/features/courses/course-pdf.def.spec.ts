import { Course } from './course.model';
import { buildCourseDocDefinition, coursePdfFilename, formatFileStamp, formatGeneratedAt } from './course-pdf.def';

function makeCourse(overrides: Partial<Course> = {}): Course {
  return {
    pkid: 1,
    title: 'Azure 基礎',
    officialTitle: 'Microsoft Azure Fundamentals',
    courseId: 'AZ-900',
    prodCourseId: 'PROD-AZ900',
    friendlyUrl: 'azure-fundamentals',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: 1,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 12,
    listPrice: 8000,
    learningCredit: 3.5,
    material: '教材X',
    objective: '目標X',
    target: '對象X',
    prerequisites: '先備X',
    outline: '大綱X',
    towardCertOrExam: '認證X',
    note: '備註X',
    otherInfo: '其他X',
    canRepeat: true,
    partner: { pkid: 1, name: 'Microsoft' },
    courseGroup: { pkid: 1, description: '雲端服務' },
    publishStatus: { pkid: 2, description: '已上架' },
    courseFaqCount: 1,
    certificationCount: 2,
    jobCategoryCount: 3,
    relatedLinkCount: 4,
    hotCourseCount: 5,
    recommCount: 6,
    ...overrides
  };
}

const GENERATED_AT = new Date(2026, 6, 16, 14, 30); // 2026-07-16 14:30 local

describe('course-pdf.def', () => {
  describe('filename', () => {
    it('formats {courseId} {title} 課程資料 {yyyyMMdd}.pdf', () => {
      expect(coursePdfFilename(makeCourse(), GENERATED_AT)).toBe('AZ-900 Azure 基礎 課程資料 20260716.pdf');
    });

    it('zero-pads single-digit months and days', () => {
      expect(formatFileStamp(new Date(2026, 0, 5))).toBe('20260105');
    });
  });

  describe('產生於 stamp', () => {
    it('formats yyyy-MM-dd HH:mm', () => {
      expect(formatGeneratedAt(GENERATED_AT)).toBe('2026-07-16 14:30');
    });
  });

  describe('document definition', () => {
    /** Flattens the docDefinition into one searchable string. */
    function rendered(course: Course): string {
      return JSON.stringify(buildCourseDocDefinition(course, GENERATED_AT, 'admin01'));
    }

    it('completeness (F3 guard): every substantive Course field appears in the document', () => {
      const course = makeCourse();
      const text = rendered(course);

      // These keys are represented indirectly (nav-object labels, or 是/否 text), not by their
      // own raw value — checked separately below instead of via the generic loop.
      const representedIndirectly = new Set<keyof Course>([
        'partnerPkid', 'courseGroupPkid', 'publishStatusPkid', 'canRepeat', 'partner', 'courseGroup', 'publishStatus'
      ]);

      for (const key of Object.keys(course) as (keyof Course)[]) {
        if (representedIndirectly.has(key)) {
          continue;
        }
        expect(text).withContext(`Course.${key} should appear in the PDF document`).toContain(String(course[key]));
      }

      expect(text).toContain(course.partner!.name);
      expect(text).toContain(course.courseGroup!.description);
      expect(text).toContain(course.publishStatus!.description);
      expect(text).toContain('是');
    });

    it('opens with the document identity: title, codes, then the provenance line', () => {
      const definition = buildCourseDocDefinition(makeCourse(), GENERATED_AT, 'admin01');
      const content = definition.content as { text: unknown }[];

      expect(content[0].text).toBe('Azure 基礎');
      expect(content[1].text).toBe('AZ-900 · PROD-AZ900');
      expect(content[2].text).toBe('產生於 2026-07-16 14:30 · admin01');
    });

    it('renders null fields as labeled — rows, never dropping them', () => {
      const text = rendered(makeCourse({
        officialTitle: null,
        courseGroup: null,
        material: null,
        objective: null,
        target: null,
        prerequisites: null,
        outline: null,
        towardCertOrExam: null,
        note: null,
        otherInfo: null
      }));

      for (const label of ['官方課程名稱', '課程群組', '教材', '課程目標', '適合對象', '先備知識', '課程大綱', '考試／認證說明', '備註', '其他資訊']) {
        expect(text).withContext(`${label} should stay as a labeled row`).toContain(label);
      }
      expect(text).toContain('—');
    });

    it('renders 允許重聽 as plain 否 text for a non-repeatable course', () => {
      expect(rendered(makeCourse({ canRepeat: false }))).toContain('否');
    });

    it('includes the relational counts but not the 引用/無法刪除 usage note', () => {
      const text = rendered(makeCourse());

      for (const label of ['課程問答數', '認證關聯數', '職類關聯數', '相關連結數', '熱門課程數', '推薦課程數']) {
        expect(text).toContain(label);
      }
      expect(text).not.toContain('無法刪除');
    });

    it('uses the embedded CJK font as the default style', () => {
      const definition = buildCourseDocDefinition(makeCourse(), GENERATED_AT, 'admin01');
      expect(definition.defaultStyle?.font).toBe('NotoSansTC');
    });
  });
});
