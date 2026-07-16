import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { PublishStatus } from '../publish-status.model';
import { PublishStatusService } from '../publish-status.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-publish-status-detail',
  imports: [RouterLink, ButtonModule, RowAuditBadgeComponent],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss'
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: status => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該發布狀態。' });
        this.router.navigate(['/publish-statuses']);
      }
    });
  }

  protected edit(): void {
    const status = this.status();
    if (status) {
      this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
    }
  }
}
