import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Roles } from '@app/shared/constants/roles';
import { HeaderComponent } from '@app/shared/custom-components/header/header.component';
import { ReservationDashboardComponent } from './reservation-dashboard.component';

const reservation = (id: string, start: string, status: ReservationStatus): Reservation =>
  ({
    id,
    tableId: 'table-1',
    customerId: 'user-1',
    startTime: new Date(start),
    endTime: new Date(new Date(start).getTime() + 90 * 60 * 1000),
    guestCount: 2,
    status,
    locationAddress: `Address ${id}`,
    tableCapacity: 4,
    waiterId: 'waiter-1',
  }) as Reservation;

describe('ReservationDashboardComponent (US20)', () => {
  let reservationService: jasmine.SpyObj<ReservationService>;

  function render(reservations: Reservation[]) {
    const auth = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['getToken', 'getUser']);
    auth.getToken.and.returnValue('token');
    auth.getUser.and.returnValue({ id: 'user-1', firstName: 'Maksym', lastName: 'Test', email: 'm@test', role: Roles.CUSTOMER });
    reservationService = jasmine.createSpyObj<ReservationService>('ReservationService', [
      'getReservationsForUser$',
      'getReservationFeedback',
      'cancelReservation$',
    ]);
    reservationService.cancelReservation$.and.returnValue(of({ message: 'Cancelled' }));
    reservationService.getReservationsForUser$.and.returnValue(of(reservations));
    reservationService.getReservationFeedback.and.returnValue(throwError(() => ({ status: 404 })));

    TestBed.configureTestingModule({
      imports: [ReservationDashboardComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: auth },
        { provide: ReservationService, useValue: reservationService },
        { provide: PreOrdersService, useValue: { getPreOrders: () => of([]) } },
      ],
    });
    // The header has its own services and HTTP — not part of this page's behaviour.
    TestBed.overrideComponent(ReservationDashboardComponent, {
      remove: { imports: [HeaderComponent] },
      add: { schemas: [CUSTOM_ELEMENTS_SCHEMA] },
    });
    const fixture = TestBed.createComponent(ReservationDashboardComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const text = (element: Element) => (element.textContent ?? '').replace(/\s+/g, ' ').trim();
  const addresses = (element: HTMLElement) =>
    [...element.querySelectorAll('app-card-reservation .row-address')].map((heading) =>
      text(heading).replace('Address ', ''),
    );

  it('AC1: greeting is the page <h1>, name only; the assistance note has a tappable phone number', () => {
    const { element } = render([]);
    expect(text(element.querySelector('h1')!)).toBe('Hello, Maksym Test');
    expect(text(element)).not.toContain('(Customer)');
    expect(text(element)).toContain('please call us at this number: +123 445 678 8990');
    expect(element.querySelector('a[href="tel:+1234456788990"]')).not.toBeNull();
  });

  it('tabs show how many reservations each one has', () => {
    const { element } = render([
      reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved),
      reservation('old', '2030-09-01T12:00:00Z', ReservationStatus.Finished),
      reservation('older', '2030-08-01T12:00:00Z', ReservationStatus.Cancelled),
    ]);
    expect(text(element.querySelector('#reservations-tab-upcoming')!)).toBe('Upcoming (1)');
    expect(text(element.querySelector('#reservations-tab-past')!)).toBe('Past & Cancelled (2)');
  });

  it('reservations are list items of one list (screen readers count them)', () => {
    const { element } = render([reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved)]);
    expect(element.querySelector('ul.reservation-list > app-card-reservation')?.getAttribute('role')).toBe('listitem');
  });

  describe('Cancel reservation asks first', () => {
    const cancelLink = (element: HTMLElement) =>
      [...element.querySelectorAll('button')].find((button) => text(button) === 'Cancel reservation');
    const dialogButton = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('app-modal button')].find((button) => text(button) === label);

    it('the link opens a dialog naming the reservation; "Keep Reservation" cancels nothing', () => {
      const { fixture, element } = render([reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved)]);
      cancelLink(element)?.click();
      fixture.detectChanges();

      expect(text(element.querySelector('app-modal')!)).toContain('Your reservation at Address soon');
      dialogButton(element, 'Keep Reservation')?.click();
      fixture.detectChanges();

      expect(reservationService.cancelReservation$).not.toHaveBeenCalled();
      expect(element.querySelector('app-modal')).toBeNull();
    });

    it('"Cancel Reservation" in the dialog cancels it and moves it to Past & Cancelled', () => {
      const { fixture, element } = render([reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved)]);
      cancelLink(element)?.click();
      fixture.detectChanges();
      dialogButton(element, 'Cancel Reservation')?.click();
      fixture.detectChanges();

      expect(reservationService.cancelReservation$).toHaveBeenCalledOnceWith('soon');
      expect(text(element.querySelector('#reservations-tab-past')!)).toBe('Past & Cancelled (1)');
    });
  });

  it('AC8: no reservations at all — "No Reservations" and Book a Table, no tabs', () => {
    const { element } = render([]);
    expect(text(element.querySelector('.empty-title')!)).toBe('No Reservations');
    expect(text(element)).toContain("Looks like you haven't made any reservations yet.");
    expect(element.querySelector('a[href="/tables"]')).not.toBeNull();
    expect(element.querySelector('[role="tablist"]')).toBeNull();
  });

  it('AC2–AC3: Upcoming is the default tab, Reserved and In Progress, nearest first', () => {
    const { element } = render([
      reservation('later', '2030-10-20T12:00:00Z', ReservationStatus.Reserved),
      reservation('past', '2030-10-01T12:00:00Z', ReservationStatus.Finished),
      reservation('sooner', '2030-10-18T12:00:00Z', ReservationStatus.Reserved),
      reservation('now', '2030-10-17T12:00:00Z', ReservationStatus.InProggress),
    ]);
    expect(element.querySelector('#reservations-tab-upcoming')?.getAttribute('aria-selected')).toBe('true');
    expect(addresses(element)).toEqual(['now', 'sooner', 'later']);
  });

  it('AC2–AC3: Past & Cancelled — Completed and Cancelled, most recent first, label "Completed"', () => {
    const { fixture, element } = render([
      reservation('upcoming', '2030-10-20T12:00:00Z', ReservationStatus.Reserved),
      reservation('old', '2030-09-01T12:00:00Z', ReservationStatus.Finished),
      reservation('cancelled', '2030-10-05T12:00:00Z', ReservationStatus.Cancelled),
    ]);
    element.querySelector<HTMLButtonElement>('#reservations-tab-past')?.click();
    fixture.detectChanges();

    expect(addresses(element)).toEqual(['cancelled', 'old']);
    expect(text(element)).toContain('Completed');
  });

  it('AC8: empty Upcoming tab — message and Book a Table', () => {
    const content = text(render([reservation('old', '2030-09-01T12:00:00Z', ReservationStatus.Finished)]).element);
    expect(content).toContain('You have no upcoming reservations.');
  });

  it('AC8: empty Past & Cancelled tab — message', () => {
    const { fixture, element } = render([reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved)]);
    element.querySelector<HTMLButtonElement>('#reservations-tab-past')?.click();
    fixture.detectChanges();
    expect(text(element)).toContain('No past or cancelled reservations.');
  });
});
