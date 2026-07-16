import type { Content, TDocumentDefinitions } from 'pdfmake/interfaces';
import { Course } from './course.model';

/**
 * 課程資料 PDF 的文件定義（pure — no pdfmake import, no DOM）。
 *
 * Content decisions carried over from the reviewed print design: document identity first
 * (課程名稱, then 簡介代碼/科目代碼), a 產生於 provenance line (datetime + generating admin),
 * fields in 檢視-page order, null fields as labeled `—` rows (never dropped — an archive
 * reader must see the field existed and was empty), 允許重聽 as plain 是/否 text, relational
 * counts included, the 引用/無法刪除 usage note excluded (an editing affordance, not course data).
 */

/** The `—` null placeholder, matching the 檢視 page's detail-grid convention. */
const DASH = '—';

/** `Date` → `yyyyMMdd` (local), for the archive filename. */
export function formatFileStamp(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}${month}${day}`;
}

/** `Date` → `yyyy-MM-dd HH:mm` (local), for the 產生於 stamp. */
export function formatGeneratedAt(date: Date): string {
  const hours = `${date.getHours()}`.padStart(2, '0');
  const minutes = `${date.getMinutes()}`.padStart(2, '0');
  return `${formatFileStamp(date).replace(/^(\d{4})(\d{2})(\d{2})$/, '$1-$2-$3')} ${hours}:${minutes}`;
}

/** Suggested download filename: `{courseId} {title} 課程資料 {yyyyMMdd}.pdf`. */
export function coursePdfFilename(course: Course, generatedAt: Date): string {
  return `${course.courseId} ${course.title} 課程資料 ${formatFileStamp(generatedAt)}.pdf`;
}

/** A borderless label/value table for short fields; rows never split across pages. */
function fieldTable(rows: [string, string][]): Content {
  return {
    table: {
      widths: [110, '*'],
      dontBreakRows: true,
      body: rows.map(([label, value]) => [
        { text: label, bold: true, color: '#555555' },
        { text: value }
      ])
    },
    layout: 'noBorders',
    margin: [0, 4, 0, 12]
  };
}

/**
 * A long-text field as ONE text node (label + newline + body): the label can never be
 * separated from the start of its value, while an arbitrarily long body is still allowed
 * to break across pages (expected for nvarchar(max) content).
 */
function longField(label: string, value: string | null): Content {
  return {
    text: [
      { text: `${label}\n`, bold: true, color: '#555555' },
      { text: value || DASH }
    ],
    margin: [0, 4, 0, 8],
    preserveLeadingSpaces: true
  };
}

function sectionHeading(text: string): Content {
  return { text, fontSize: 13, bold: true, margin: [0, 12, 0, 4] };
}

export function buildCourseDocDefinition(course: Course, generatedAt: Date, userName: string): TDocumentDefinitions {
  return {
    pageSize: 'A4',
    pageMargins: [43, 43, 43, 43],
    defaultStyle: { font: 'NotoSansTC', fontSize: 10.5, lineHeight: 1.25 },
    info: { title: `${course.courseId} ${course.title} 課程資料` },
    content: [
      // Document identity first — a filed PDF must say what it is before anything else.
      { text: course.title, fontSize: 16, bold: true },
      { text: `${course.courseId} · ${course.prodCourseId}`, color: '#555555', margin: [0, 2, 0, 0] },
      { text: `產生於 ${formatGeneratedAt(generatedAt)} · ${userName}`, color: '#555555', margin: [0, 2, 0, 8] },

      sectionHeading('基本資料'),
      fieldTable([
        ['主代碼', String(course.pkid)],
        ['顯示順序', String(course.displayOrder)],
        ['簡介代碼', course.courseId],
        ['科目代碼', course.prodCourseId],
        ['課程名稱', course.title],
        ['官方課程名稱', course.officialTitle || DASH],
        ['友善網址', course.friendlyUrl],
        ['原廠', course.partner?.name ?? DASH],
        ['課程群組', course.courseGroup?.description ?? DASH],
        ['上架狀態', course.publishStatus?.description ?? DASH],
        ['上架日期', course.scheduleOn],
        ['下架日期', course.scheduleOff],
        ['時數', String(course.hour)],
        ['定價', String(course.listPrice)],
        ['點數', String(course.learningCredit)],
        ['允許重聽', course.canRepeat ? '是' : '否']
      ]),

      sectionHeading('課程內容'),
      longField('教材', course.material),
      longField('課程目標', course.objective),
      longField('適合對象', course.target),
      longField('先備知識', course.prerequisites),
      longField('課程大綱', course.outline),
      longField('考試／認證說明', course.towardCertOrExam),
      longField('備註', course.note),
      longField('其他資訊', course.otherInfo),

      sectionHeading('關聯資料'),
      fieldTable([
        ['課程問答數', String(course.courseFaqCount)],
        ['認證關聯數', String(course.certificationCount)],
        ['職類關聯數', String(course.jobCategoryCount)],
        ['相關連結數', String(course.relatedLinkCount)],
        ['熱門課程數', String(course.hotCourseCount)],
        ['推薦課程數', String(course.recommCount)]
      ])
    ]
  };
}
