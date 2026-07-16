import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { CourseGroup } from '../course-group.model';
import { CourseGroupService } from '../course-group.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-group-detail',
  imports: [RouterLink, ButtonModule, RowAuditBadgeComponent],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss'
})
export class CourseGroupDetail implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly group = signal<CourseGroup | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: group => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該課程群組。' });
        this.router.navigate(['/course-groups']);
      }
    });
  }

  protected edit(): void {
    const group = this.group();
    if (group) {
      this.router.navigate(['/course-groups', group.pkid, 'edit']);
    }
  }
}
