import { Injectable } from '@angular/core';
import { Course } from './course.model';
import { buildCourseDocDefinition, coursePdfFilename } from './course-pdf.def';

type PdfMake = typeof import('pdfmake/build/pdfmake');

/**
 * 存成 PDF: builds the course archive document and downloads it as a real `.pdf` file —
 * no print dialog. pdfmake (~2 MB) and the Noto Sans TC font (~11 MB, needed because PDF
 * generators cannot render 繁體中文 without an embedded font) are lazy-loaded on the first
 * download and cached for the session, so the main bundle and every non-printing page load
 * pay nothing for this feature.
 */
@Injectable({ providedIn: 'root' })
export class CoursePdfService {
  private engine: Promise<PdfMake> | null = null;

  /** Generates and downloads `{courseId} {title} 課程資料 {yyyyMMdd}.pdf`. */
  async download(course: Course, generatedAt: Date, userName: string): Promise<void> {
    const pdfMake = await this.loadEngine();
    const definition = buildCourseDocDefinition(course, generatedAt, userName);
    pdfMake.createPdf(definition).download(coursePdfFilename(course, generatedAt));
  }

  private loadEngine(): Promise<PdfMake> {
    // Cache the promise, not the result, so concurrent first clicks share one load —
    // but drop it on failure so a transient font-fetch error doesn't poison the session.
    this.engine ??= this.createEngine().catch(error => {
      this.engine = null;
      throw error;
    });
    return this.engine;
  }

  private async createEngine(): Promise<PdfMake> {
    const [pdfMakeModule, regular, bold] = await Promise.all([
      import('pdfmake/build/pdfmake'),
      this.fetchFontBase64('fonts/NotoSansTC-Regular.otf'),
      this.fetchFontBase64('fonts/NotoSansTC-Bold.otf')
    ]);

    // esbuild's CommonJS interop exposes the module both as `default` and as named exports.
    const pdfMake = (pdfMakeModule as { default?: PdfMake }).default ?? pdfMakeModule;

    pdfMake.addVirtualFileSystem({
      'NotoSansTC-Regular.otf': regular,
      'NotoSansTC-Bold.otf': bold
    });
    pdfMake.setFonts({
      NotoSansTC: {
        normal: 'NotoSansTC-Regular.otf',
        bold: 'NotoSansTC-Bold.otf',
        italics: 'NotoSansTC-Regular.otf',
        bolditalics: 'NotoSansTC-Bold.otf'
      }
    });

    return pdfMake;
  }

  /** Fetches a static font asset and returns its bytes as base64 (pdfmake's vfs format). */
  private async fetchFontBase64(url: string): Promise<string> {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`字型載入失敗: ${url} (${response.status})`);
    }
    const blob = await response.blob();
    return await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve((reader.result as string).split(',')[1]);
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(blob);
    });
  }
}
