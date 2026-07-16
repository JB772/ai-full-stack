import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import QRCode from 'qrcode';
import { Course } from '../course.model';
import { CourseService } from '../course.service';
import { CoursePdfService } from '../course-pdf.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-course-detail',
  imports: [ButtonModule, TagModule, RowAuditBadgeComponent],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss'
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly pdfService = inject(CoursePdfService);
  private readonly auth = inject(AuthService);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(false);

  /** PNG data URL of the rendered QR code, populated once the course loads. */
  protected readonly qrImage = signal<string | null>(null);

  /** True while the PDF engine/font loads and the file generates (first click pays the load). */
  protected readonly savingPdf = signal(false);

  /**
   * The public course page the QR code points at. Built from the record's pkid and CourseId —
   * matches the live site's `/Course/Show/{pkid}/{CourseId}` route shape.
   */
  protected readonly qrUrl = computed(() => {
    const c = this.course();
    return c ? `https://www.uuu.com.tw/Course/Show/${c.pkid}/${c.courseId}` : '';
  });

  /** Any non-zero child count is what makes the API reject a delete with a 409. */
  protected readonly referenceCount = computed(() => {
    const c = this.course();
    if (!c) {
      return 0;
    }

    return c.courseFaqCount + c.certificationCount + c.jobCategoryCount
      + c.relatedLinkCount + c.hotCourseCount + c.recommCount;
  });

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: course => {
        this.course.set(course);
        this.loading.set(false);
        this.generateQr();
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該課程。' });
        this.router.navigate(['/courses']);
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/courses']);
  }

  protected edit(): void {
    const course = this.course();
    if (course) {
      this.router.navigate(['/courses', course.pkid, 'edit']);
    }
  }

  /**
   * 存成 PDF: one click downloads `{courseId} {title} 課程資料 {yyyyMMdd}.pdf` directly —
   * no print dialog. The document carries a 產生於 stamp (generation time + signed-in admin).
   */
  protected async savePdf(): Promise<void> {
    const course = this.course();
    if (!course || this.savingPdf()) {
      return;
    }

    this.savingPdf.set(true);
    try {
      await this.pdfService.download(course, new Date(), this.auth.userName());
    } catch {
      this.messageService.add({ severity: 'error', summary: 'PDF 產生失敗', detail: '無法產生課程資料 PDF。' });
    } finally {
      this.savingPdf.set(false);
    }
  }

  /** Render the QR code for {@link qrUrl} into a PNG data URL held by {@link qrImage}. */
  private generateQr(): void {
    const url = this.qrUrl();
    if (!url) {
      return;
    }

    QRCode.toDataURL(url, { width: 240, margin: 1 })
      .then(dataUrl => this.qrImage.set(dataUrl))
      .catch(() =>
        this.messageService.add({ severity: 'error', summary: 'QR 產生失敗', detail: '無法產生 QR Code。' })
      );
  }

  /** Download the rendered QR code as `{CourseId}.png`. */
  protected downloadQr(): void {
    const image = this.qrImage();
    const course = this.course();
    if (!image || !course) {
      return;
    }

    const link = document.createElement('a');
    link.href = image;
    link.download = `${course.courseId}.png`;
    link.click();
  }
}
