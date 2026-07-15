import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { Login } from './login';
import { AUTH_STORAGE_KEY } from '../auth.service';

describe('Login', () => {
  let httpMock: HttpTestingController;
  const loginUrl = `${environment.apiBaseUrl}/Auth/login`;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates', () => {
    const fixture = TestBed.createComponent(Login);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('posts { userId, password } and stores the session, then navigates home on success', () => {
    const fixture = TestBed.createComponent(Login);
    const router = TestBed.inject(Router);
    const navigateByUrl = spyOn(router, 'navigateByUrl');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const userId = el.querySelector('#userId') as HTMLInputElement;
    const password = el.querySelector('#password') as HTMLInputElement;
    userId.value = 'admin';
    userId.dispatchEvent(new Event('input'));
    password.value = 'P@ssw0rd';
    password.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'admin', password: 'P@ssw0rd' });
    req.flush({ userId: 'admin', userName: '系統管理員', accessToken: 'header.payload.signature' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows an error and does not navigate when login fails', () => {
    const fixture = TestBed.createComponent(Login);
    const router = TestBed.inject(Router);
    const navigateByUrl = spyOn(router, 'navigateByUrl');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    (el.querySelector('#userId') as HTMLInputElement).value = 'admin';
    el.querySelector('#userId')!.dispatchEvent(new Event('input'));
    (el.querySelector('#password') as HTMLInputElement).value = 'wrong';
    el.querySelector('#password')!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    httpMock.expectOne(loginUrl).flush('nope', { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(el.querySelector('.login-error')).toBeTruthy();
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });
});
