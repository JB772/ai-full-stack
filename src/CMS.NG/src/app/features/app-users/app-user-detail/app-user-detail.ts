import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppUser } from '../app-user.model';
import { AppUserService } from '../app-user.service';

@Component({
  selector: 'app-app-user-detail',
  imports: [RouterLink, DatePipe, ButtonModule, TagModule],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss'
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly loading = signal(false);
  protected readonly resetting = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: user => {
        this.user.set(user);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該使用者。' });
        this.router.navigate(['/app-users']);
      }
    });
  }

  protected edit(): void {
    const user = this.user();
    if (user) {
      this.router.navigate(['/app-users', user.pkid, 'edit']);
    }
  }

  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) {
      return;
    }

    this.confirmationService.confirm({
      header: '重設密碼確認',
      message: `確定要將使用者「${user.userId}」的密碼重設為系統預設值？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () => this.resetPassword(user)
    });
  }

  private resetPassword(user: AppUser): void {
    this.resetting.set(true);
    this.service.resetPassword(user.pkid).subscribe({
      next: () => {
        this.resetting.set(false);
        this.messageService.add({ severity: 'success', summary: '重設成功', detail: `使用者「${user.userId}」的密碼已重設為預設值。` });
        this.reload(user.pkid);
      },
      error: err => {
        this.resetting.set(false);
        const detail = err?.error?.message ?? '重設密碼時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '重設失敗', detail });
      }
    });
  }

  private reload(pkid: number): void {
    this.service.getByPkid(pkid).subscribe({
      next: user => this.user.set(user)
    });
  }
}
