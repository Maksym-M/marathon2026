import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    const authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });
});

describe('tokenInterceptor · /guest/ endpoints', () => {
  const API = 'https://test.api';
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['getTokens']);
    authService.getTokens.and.returnValue({ idToken: 'owner-id-token', accessToken: 'owner-access-token' });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['error']) },
        { provide: AuthenticationService, useValue: authService },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const ownerEndpoints: [string, string][] = [
    ['GET', '/guest/groups/grp-1/sessions'],
    ['DELETE', '/guest/groups/grp-1/sessions/gs-2'],
    ['PUT', '/guest/groups/grp-1/sessions/gs-2/dishes/dish-1'],
    ['DELETE', '/guest/groups/grp-1/seats'],
  ];
  for (const [method, path] of ownerEndpoints) {
    it(`Owner endpoint ${method} ${path} carries the Owner's token`, () => {
      http.request(method, API + path, { body: method === 'PUT' ? { quantity: 1 } : null }).subscribe();
      const req = httpMock.expectOne(API + path);
      expect(req.request.headers.get('Authorization')).toBe('Bearer owner-id-token');
      req.flush({});
    });
  }

  it('guest-link endpoints keep the guest session token and are not touched', () => {
    const headers = { Authorization: 'Bearer guest-session-token' };
    http.post(`${API}/guest/sessions/gs-2/dishes`, { dishId: 'dish-1', quantity: 1 }, { headers }).subscribe();
    const req = httpMock.expectOne(`${API}/guest/sessions/gs-2/dishes`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer guest-session-token');
    req.flush({});
  });

  it('public invite lookup gets no Owner token', () => {
    http.get(`${API}/guest/invites/tok`).subscribe();
    const req = httpMock.expectOne(`${API}/guest/invites/tok`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
