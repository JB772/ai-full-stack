import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { Course } from '../course.model';
import { CourseService } from '../course.service';

@Component({
  selector: 'app-course-detail',
  imports: [ButtonModule, TagModule],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss'
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(false);

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
}
