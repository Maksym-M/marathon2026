import { ComponentFixture, DeferBlockBehavior, DeferBlockState, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Roles } from '@app/shared/constants/roles';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { createServiceSpy } from '@app/components/group-orders/testing/group-order.fixtures';

import { HeaderComponent } from './header.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { NotificationsService } from '@app/services/notifications/notifications.service';

describe('HeaderComponent', () => {
  let component: HeaderComponent;
  let fixture: ComponentFixture<HeaderComponent>;

  let authMock: jasmine.SpyObj<AuthenticationService>;
  let notificationsMock: jasmine.SpyObj<NotificationsService>;

  beforeEach(async () => {
    authMock = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['signOut'],
    {
      user$: of(null),
      isAuthorised$: of(false),
    },
  );
  
  notificationsMock = jasmine.createSpyObj<NotificationsService>(
    'NotificationsService',
    ['loadNotifications', 'markAsRead'],
  );

  notificationsMock.loadNotifications.and.returnValue(of([]));
  notificationsMock.markAsRead.and.returnValue(of(void 0));

  await TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: 'https://test.api' },
      { provide: AuthenticationService, useValue: authMock },
      { provide: NotificationsService, useValue: notificationsMock },
      provideRouter([]),
    ],
    imports: [HeaderComponent],
  }).compileComponents();

  fixture = TestBed.createComponent(HeaderComponent);
  component = fixture.componentInstance;
  fixture.detectChanges();
});

  it('should create', () => {
    expect(component).toBeTruthy();
  });

});

describe('HeaderComponent · Cart in group pre-order mode', () => {
  let reservations: Reservation[] = [];

  const upcoming = (id: string, guestCount: number) =>
    ({ id, guestCount, status: ReservationStatus.Reserved, startTime: new Date('2030-10-17T12:00:00Z'), endTime: new Date('2030-10-17T13:30:00Z') }) as Reservation;

  async function openCartAt(url: string) {
    const auth = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['signOut'], {
      user$: of({ id: 'u1', firstName: 'Dmytro', lastName: 'Owner', email: 'owner@test', role: Roles.CUSTOMER }),
      isAuthorised$: of(true),
    });
    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      deferBlockBehavior: DeferBlockBehavior.Manual,
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: auth },
        { provide: GroupOrdersService, useValue: createServiceSpy() },
        { provide: ReservationService, useValue: { getReservationsForUser$: () => of(reservations) } },
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    });
    await TestBed.inject(Router).navigateByUrl(url);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    fixture.componentInstance.toggleCart();
    fixture.detectChanges();
    for (const block of await fixture.getDeferBlocks()) await block.render(DeferBlockState.Complete);
    return fixture.nativeElement as HTMLElement;
  }

  it('group reservation: Cart opens the group cart for that reservation', async () => {
    const element = await openCartAt('/menu?preOrder=true&group=true&reservationId=res-1');
    expect(element.querySelectorAll('app-group-order-cart').length).withContext('rendered once, not per layout').toBe(1);
    expect(element.querySelector('app-cart')).toBeNull();
  });

  it('Group Pre-Order page: Cart opens that reservation\'s group cart', async () => {
    const element = await openCartAt('/reservations/res-9/group-order');
    expect(element.querySelector('app-group-order-cart')).not.toBeNull();
  });

  it('other pages: Cart opens the nearest upcoming group reservation', async () => {
    reservations = [upcoming('single', 1), upcoming('group', 3)];
    const element = await openCartAt('/reservations');
    expect(element.querySelector('app-group-order-cart')).not.toBeNull();
    expect(element.querySelector('app-cart')).toBeNull();
  });

  it('other pages, only 1-guest reservations: the regular cart', async () => {
    reservations = [upcoming('single', 1)];
    const element = await openCartAt('/reservations');
    expect(element.querySelector('app-cart')).not.toBeNull();
    expect(element.querySelector('app-group-order-cart')).toBeNull();
  });

  it('pre-order without group mode: the regular cart', async () => {
    reservations = [];
    const element = await openCartAt('/menu?preOrder=true&reservationId=res-1');
    expect(element.querySelector('app-cart')).not.toBeNull();
    expect(element.querySelector('app-group-order-cart')).toBeNull();
  });
});
