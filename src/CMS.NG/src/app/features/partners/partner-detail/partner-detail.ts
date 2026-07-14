import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { Partner } from '../partner.model';
import { PartnerService } from '../partner.service';

@Component({
  selector: 'app-partner-detail',
  imports: [RouterLink, ButtonModule],
  templateUrl: './partner-detail.html',
  styleUrl: './partner-detail.scss'
})
export class PartnerDetail implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(false);

  /** Any non-zero child count is what makes the API reject a delete with a 409. */
  protected readonly referenceCount = computed(() => {
    const p = this.partner();
    if (!p) {
      return 0;
    }

    return p.courseCount + p.certificationCount + p.courseGroupCount + p.promotionCount + p.seminarCount;
  });

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: partner => {
        this.partner.set(partner);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該合作夥伴。' });
        this.router.navigate(['/partners']);
      }
    });
  }

  protected edit(): void {
    const partner = this.partner();
    if (partner) {
      this.router.navigate(['/partners', partner.pkid, 'edit']);
    }
  }
}
