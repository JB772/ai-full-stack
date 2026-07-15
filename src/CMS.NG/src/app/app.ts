import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

/** One expandable group in the sidebar. */
export interface NavGroup {
  label: string;
  icon: string;
  expanded: boolean;
  items: NavItem[];
}

export interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('UWA');
  protected readonly collapsed = signal(false);

  protected readonly navGroups = signal<NavGroup[]>([
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      items: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }
      ]
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-book',
      expanded: true,
      items: [
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
        { label: '合作夥伴 Partner', icon: 'pi pi-building', route: '/partners' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' }
      ]
    }
  ]);

  protected toggleSidebar(): void {
    this.collapsed.update(v => !v);
  }

  protected toggleGroup(group: NavGroup): void {
    this.navGroups.update(groups =>
      groups.map(g => (g.label === group.label ? { ...g, expanded: !g.expanded } : g))
    );
  }
}
