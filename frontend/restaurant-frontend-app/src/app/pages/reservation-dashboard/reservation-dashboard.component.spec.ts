import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NEVER, Observable, of, Subject, throwError } from 'rxjs';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import { ReservationsRequestOptions } from '@app/models/reservations/reservations-request-options.model';
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

  function render(
    reservations: Reservation[],
    feedback: (reservationId: string) => Observable<FeedbackReservationResponseItem | null> = () =>
      throwError(() => ({ status: 404 })),
    list: (options?: ReservationsRequestOptions) => Observable<Reservation[]> = () => of(reservations),
  ) {
    const auth = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['getToken', 'getUser']);
    auth.getToken.and.returnValue('token');
    auth.getUser.and.returnValue({ id: 'user-1', firstName: 'Maksym', lastName: 'Test', email: 'm@test', role: Roles.CUSTOMER });
    reservationService = jasmine.createSpyObj<ReservationService>('ReservationService', [
      'getReservationsForUser$',
      'getReservationFeedback',
      'cancelReservation$',
    ]);
    reservationService.cancelReservation$.and.returnValue(of({ message: 'Cancelled' }));
    reservationService.getReservationsForUser$.and.callFake(list);
    reservationService.getReservationFeedback.and.callFake(feedback);

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
      expect(text(element.querySelector('#reservations-tab-upcoming')!)).toBe('Upcoming (0)');

      (element.querySelector('#reservations-tab-past') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(addresses(element)).toEqual(['soon']);
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
  describe('Feedback loading (fewer, gentler requests)', () => {
    const FEEDBACK = { service: { rating: 5 }, cuisineExperience: { rating: 4 } } as FeedbackReservationResponseItem;

    it('asks for a fresh list, and for feedback only on In Progress and Completed reservations', () => {
      render([
        reservation('reserved', '2030-10-20T12:00:00Z', ReservationStatus.Reserved),
        reservation('cancelled', '2026-10-01T12:00:00Z', ReservationStatus.Cancelled),
        reservation('in-progress', '2026-10-29T12:00:00Z', ReservationStatus.InProggress),
        reservation('finished', '2026-10-02T12:00:00Z', ReservationStatus.Finished),
      ]);
      expect(reservationService.getReservationsForUser$).toHaveBeenCalledWith({ fresh: true, scope: 'upcoming' });
      expect(reservationService.getReservationFeedback.calls.allArgs()).toEqual([['in-progress'], ['finished']]);
    });

    it('shows the list before feedback arrives', () => {
      const { element } = render([reservation('finished', '2026-10-02T12:00:00Z', ReservationStatus.Finished)], () => NEVER);
      expect(element.querySelector('.tabs, [role="tablist"]')).not.toBeNull();
      expect(text(element)).toContain('Past & Cancelled (1)');
    });

    it('keeps at most 4 feedback requests in flight and fills each one in as it arrives', () => {
      const pending = new Map<string, Subject<FeedbackReservationResponseItem | null>>();
      const finished = Array.from({ length: 6 }, (_, index) =>
        reservation(`f${index}`, `2026-10-0${index + 1}T12:00:00Z`, ReservationStatus.Finished),
      );
      const { fixture } = render(finished, (reservationId) => {
        const subject = new Subject<FeedbackReservationResponseItem | null>();
        pending.set(reservationId, subject);
        return subject;
      });
      expect(pending.size).toBe(4);

      pending.get('f0')!.next(FEEDBACK);
      pending.get('f0')!.complete();
      expect(pending.size).toBe(5);

      const component = fixture.componentInstance;
      expect(component.reservations().find((item) => item.id === 'f0')?.feedback).toEqual(FEEDBACK);
      expect(component.reservations().find((item) => item.id === 'f1')?.feedback).toBeUndefined();
    });

    it('a feedback request that fails (status 0) still leaves the reservation on the list', () => {
      const { fixture } = render([reservation('finished', '2026-10-02T12:00:00Z', ReservationStatus.Finished)], () =>
        throwError(() => ({ status: 0, message: 'Unknown Error' })),
      );
      expect(fixture.componentInstance.reservations().map((item) => item.id)).toEqual(['finished']);
      expect(fixture.componentInstance.reservations()[0].feedback).toBeUndefined();
    });
  });
  describe('Upcoming first, Past only when its tab opens (API with scopes)', () => {
    const UPCOMING = [
      reservation('soon', '2030-10-20T12:00:00Z', ReservationStatus.Reserved),
      reservation('now', '2026-10-29T12:00:00Z', ReservationStatus.InProggress),
    ];
    const PAST = [
      reservation('old', '2026-09-01T12:00:00Z', ReservationStatus.Finished),
      reservation('older', '2026-08-01T12:00:00Z', ReservationStatus.Cancelled),
    ];

    /** Answers like a backend that knows `scope`; `past` can be replaced to control that request. */
    function scoped(upcoming: Reservation[], past: () => Observable<Reservation[]> = () => of(PAST)) {
      return (options?: ReservationsRequestOptions) => (options?.scope === 'past' ? past() : of(upcoming));
    }

    function pastTab(element: HTMLElement): HTMLButtonElement {
      return element.querySelector<HTMLButtonElement>('#reservations-tab-past')!;
    }

    it('loads only Upcoming at first: no Past request, and its tab has no count yet', () => {
      const { element } = render([], undefined, scoped(UPCOMING));

      expect(reservationService.getReservationsForUser$.calls.allArgs()).toEqual([[{ fresh: true, scope: 'upcoming' }]]);
      expect(text(element)).toContain('Upcoming (2)');
      expect(text(pastTab(element))).toBe('Past & Cancelled');
      expect(addresses(element)).toEqual(['now', 'soon']);
    });

    it('opening Past loads it once, shows a wait message meanwhile, then the list and its count', () => {
      const past$ = new Subject<Reservation[]>();
      const { fixture, element } = render([], undefined, scoped(UPCOMING, () => past$));

      pastTab(element).click();
      fixture.detectChanges();
      expect(reservationService.getReservationsForUser$).toHaveBeenCalledWith({ fresh: true, scope: 'past' });
      expect(text(element)).toContain('Please wait while we load your past reservations.');

      past$.next(PAST);
      past$.complete();
      fixture.detectChanges();
      expect(addresses(element)).toEqual(['old', 'older']);
      expect(text(pastTab(element))).toBe('Past & Cancelled (2)');

      (element.querySelector('#reservations-tab-upcoming') as HTMLButtonElement).click();
      pastTab(element).click();
      expect(reservationService.getReservationsForUser$.calls.count()).toBe(2);
    });

    it('feedback of completed reservations is asked for only once Past loads', () => {
      const { fixture, element } = render([], undefined, scoped(UPCOMING));
      expect(reservationService.getReservationFeedback.calls.allArgs()).toEqual([['now']]);

      pastTab(element).click();
      fixture.detectChanges();

      expect(reservationService.getReservationFeedback.calls.allArgs()).toEqual([['now'], ['old']]);
    });

    it('Past failed — a message and Try Again, which loads it again', () => {
      let attempts = 0;
      const { fixture, element } = render([], undefined, scoped(UPCOMING, () =>
        ++attempts === 1 ? throwError(() => ({ status: 500 })) : of(PAST),
      ));

      pastTab(element).click();
      fixture.detectChanges();
      expect(text(element)).toContain("We couldn't load your past reservations.");

      [...element.querySelectorAll('button')].find((button) => text(button) === 'Try Again')!.click();
      fixture.detectChanges();
      expect(addresses(element)).toEqual(['old', 'older']);
    });

    it('nothing upcoming — Past loads right away to choose between the tabs and "No Reservations"', () => {
      const { element } = render([], undefined, scoped([]));

      expect(reservationService.getReservationsForUser$).toHaveBeenCalledWith({ fresh: true, scope: 'past' });
      expect(text(element)).toContain('You have no upcoming reservations.');
      expect(text(pastTab(element))).toBe('Past & Cancelled (2)');
    });

    it('nothing at all — "No Reservations"', () => {
      const { element } = render([], undefined, scoped([], () => of([])));

      expect(text(element)).toContain('No Reservations');
      expect(element.querySelector('[role="tablist"]')).toBeNull();
    });
  });
});
