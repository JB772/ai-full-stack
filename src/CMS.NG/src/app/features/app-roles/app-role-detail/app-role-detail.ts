import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { AppRole } from '../app-role.model';
import { AppRoleService } from '../app-role.service';

@Component({
  selector: 'app-app-role-detail',
  imports: [RouterLink, ButtonModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss'
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: role => {
        this.role.set(role);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該角色。' });
        this.router.navigate(['/app-roles']);
      }
    });
  }

  protected edit(): void {
    const role = this.role();
    if (role) {
      this.router.navigate(['/app-roles', role.pkid, 'edit']);
    }
  }
}
