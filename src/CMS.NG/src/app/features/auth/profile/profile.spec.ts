import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import { Profile } from './profile';
import { AuthService, AUTH_STORAGE_KEY } from '../auth.service';

/** Seeds a signed-in session so AuthService (read on construction) exposes it to the page. */
function signIn(userName = '編輯者', roles = ['Editor', 'Viewer']): void {
  sessionStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({ userId: 'editor', userName, accessToken: 'header.payload.sig', roles })
  );
}

describe('Profile', () => {
  let httpMock: HttpTestingController;
  const profileUrl = `${environment.apiBaseUrl}/Auth/profile`;

  beforeEach(async () => {
    sessionStorage.clear();
    signIn();
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates', () => {
    const fixture = TestBed.createComponent(Profile);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows UserId and roles read-only, with UserName editable', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    // UserId is displayed but not editable.
    const userId = el.querySelector('#userId') as HTMLInputElement;
    expect(userId.value).toBe('editor');
    expect(userId.disabled || userId.readOnly).toBeTrue();

    // Roles are shown for display only (no input control for them).
    const roles = el.querySelector('[data-testid="roles"]') as HTMLElement;
    expect(roles.textContent).toContain('Editor');
    expect(roles.textContent).toContain('Viewer');
    expect(roles.querySelector('input')).toBeNull();

    // UserName is an editable input seeded with the current name.
    const userName = el.querySelector('#userName') as HTMLInputElement;
    expect(userName.disabled).toBeFalse();
    expect(userName.value).toBe('編輯者');
  });

  it('saves the new UserName and refreshes the shell (service signal + session storage)', () => {
    const fixture = TestBed.createComponent(Profile);
    const auth = TestBed.inject(AuthService);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const userName = el.querySelector('#userName') as HTMLInputElement;
    userName.value = '新名稱';
    userName.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const req = httpMock.expectOne(profileUrl);
    expect(req.request.method).toBe('PUT');
    // Only userName is sent — identity comes from the JWT server-side.
    expect(req.request.body).toEqual({ userName: '新名稱' });
    req.flush({ userId: 'editor', userName: '新名稱', roles: ['Editor', 'Viewer'] });

    // The shell reads auth.userName(); it now reflects the saved value.
    expect(auth.userName()).toBe('新名稱');
    const stored = JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY)!);
    expect(stored.userName).toBe('新名稱');
  });

  it('does not call the API when UserName is blank', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const userName = el.querySelector('#userName') as HTMLInputElement;
    userName.value = '   ';
    userName.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(httpMock.match(profileUrl).length).toBe(0);
  });
});
