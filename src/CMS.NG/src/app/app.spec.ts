import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';
import { AUTH_STORAGE_KEY } from './features/auth/auth.service';

/** Seeds a signed-in session with the given roles so the authenticated shell renders. */
function signIn(roles: string[], userName = '系統管理員'): void {
  sessionStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({ userId: 'admin', userName, accessToken: 'token', roles })
  );
}

describe('App', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();
  });

  afterEach(() => sessionStorage.clear());

  it('creates the app shell', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin nav group with the 角色 AppRole entry for an Admin user', () => {
    signIn(['Admin']);
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
    signIn(['Admin']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.textContent).toContain('首頁 Home');
    expect(sidebar.textContent).toContain('上稿作業 FeaturedPromoItem');

    const links = [...sidebar.querySelectorAll('.nav-items a')] as HTMLAnchorElement[];
    const link = links.find(a => a.getAttribute('href') === '/featured-promo-items');
    expect(link).toBeTruthy();
  });

  it('hides the 系統管理 Admin nav group when the user is not an Admin', () => {
    signIn(['Editor']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.textContent).not.toContain('系統管理 Admin');
    // Non-admin groups are still shown.
    expect(sidebar.textContent).toContain('首頁 Home');
    expect(sidebar.textContent).toContain('課程管理 Course');
  });

  it('shows the signed-in user name and a logout control in the shell', () => {
    signIn(['Admin'], '王小明');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.textContent).toContain('王小明');
    expect(sidebar.querySelector('.logout-button')).toBeTruthy();
  });

  it('renders only the login outlet (no sidebar) when signed out', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sidebar')).toBeNull();
  });
});
