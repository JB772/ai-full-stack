import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideNoopAnimations(), MessageService, ConfirmationService]
    }).compileComponents();
  });

  it('creates the app shell', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin nav group with the 角色 AppRole entry', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.textContent).toContain('系統管理 Admin');
    expect(sidebar.textContent).toContain('角色 AppRole');

    const links = [...sidebar.querySelectorAll('.nav-items a')] as HTMLAnchorElement[];
    const appRoleLink = links.find(a => a.getAttribute('href') === '/app-roles');
    expect(appRoleLink).toBeTruthy();
  });

  it('renders the 首頁 Home nav group with the 上稿作業 FeaturedPromoItem entry', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.textContent).toContain('首頁 Home');
    expect(sidebar.textContent).toContain('上稿作業 FeaturedPromoItem');

    const links = [...sidebar.querySelectorAll('.nav-items a')] as HTMLAnchorElement[];
    const link = links.find(a => a.getAttribute('href') === '/featured-promo-items');
    expect(link).toBeTruthy();
  });
});
