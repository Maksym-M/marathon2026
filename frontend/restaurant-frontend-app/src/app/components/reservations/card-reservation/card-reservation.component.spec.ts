import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Roles } from '@app/shared/constants/roles';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

const RESERVATION: ReservationCustomer = {
  id: 'res-1',
  tableId: 'table-1',
  startTime: new Date('2026-10-21T16:00:00Z'),
  endTime: new Date('2026-10-21T17:30:00Z'),
  guestCount: 2,
  status: ReservationStatusCustomer.Reserved,
  restaurantName: 'Green & Tasty',
  locationAddress: 'Rustaveli Avenue 48',
  waiterId: 'waiter-1',
};

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: API_BASE_URL, useValue: 'https://test.api' }, provideRouter([])],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    // r — обов'язковий input: без нього компонент падає з NG0950
    fixture.componentRef.setInput('r', RESERVATION);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Pre-order', () => {
    function preOrderQueryParams(guestCount: number) {
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
      fixture.componentRef.setInput('r', { ...RESERVATION, guestCount });
      component.onPreOrder();
      return navigate.calls.mostRecent().args[1]?.queryParams;
    }

    it('2+ guests: menu opens in group mode, so the Cart shows the group cart', () => {
      expect(preOrderQueryParams(2)).toEqual(jasmine.objectContaining({ preOrder: 'true', group: 'true', reservationId: 'res-1' }));
    });

    it('1 guest: no group mode — the regular cart', () => {
      expect(preOrderQueryParams(1)).toEqual(jasmine.objectContaining({ preOrder: 'true', group: null }));
    });
  });

  describe('Group Pre-Order (US20 → US21)', () => {
    function groupButton(guestCount: number): HTMLButtonElement | undefined {
      fixture.componentRef.setInput('role', Roles.CUSTOMER);
      fixture.componentRef.setInput('r', { ...RESERVATION, guestCount });
      fixture.detectChanges();
      return [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
        (button) => button.textContent?.trim() === 'View Details',
      );
    }

    it('2+ guests: the button opens the Group Pre-Order page of this reservation', () => {
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
      groupButton(3)?.click();
      expect(navigate).toHaveBeenCalledWith(['/reservations', 'res-1', 'group-order']);
    });

    it('1 guest: no View Details button', () => {
      expect(groupButton(1)).toBeUndefined();
    });
  });

  describe('US20 · group pre-order line and actions', () => {
    const IN_A_WEEK = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);

    // The Customer's Reservations page shows the cards as rows.
    function render(overrides: Partial<ReservationCustomer>) {
      fixture.componentRef.setInput('layout', 'row');
      fixture.componentRef.setInput('role', Roles.CUSTOMER);
      fixture.componentRef.setInput('r', { ...RESERVATION, startTime: IN_A_WEEK, endTime: IN_A_WEEK, ...overrides });
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    const text = (element: HTMLElement) => (element.textContent ?? '').replace(/\s+/g, ' ').trim();
    const button = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll('button')].find((item) => item.textContent?.trim() === label);
    const OPEN = { status: 'Open' as const, submitted: 2, joined: 3, seats: 4 };

    it('AC4: shows "Table N"', () => {
      expect(text(render({ tableNumber: 7 }))).toContain('Table 7');
    });

    it('AC6: "Pre-orders: X/Y", the hint and Invite Guests while a seat is free', () => {
      const element = render({ guestCount: 4, groupPreOrder: OPEN });
      expect(text(element)).toContain('Pre-orders: 2/4');
      expect(text(element)).toContain('Share link with your guests so they can place their pre-orders');
      expect(button(element, 'Invite Guests')).toBeDefined();
    });

    it('AC6: every seat taken — no hint and no Invite Guests, the counter stays', () => {
      const element = render({ guestCount: 3, groupPreOrder: { ...OPEN, joined: 3, seats: 3 } });
      expect(text(element)).toContain('Pre-orders: 2/3');
      expect(button(element, 'Invite Guests')).toBeUndefined();
    });

    it('AC6: Invite Guests opens the "Guest Invite link" modal', () => {
      const element = render({ guestCount: 4, groupPreOrder: OPEN });
      button(element, 'Invite Guests')?.click();
      fixture.detectChanges();
      expect(text(element)).toContain('Guest Invite link');
    });

    it('AC5: group not sent yet — Add Pre-Order and View Details', () => {
      const element = render({ guestCount: 4, groupPreOrder: OPEN });
      expect(button(element, 'Add Pre-Order')).toBeDefined();
      expect(button(element, 'View Details')).toBeDefined();
    });

    it('AC5: sent to the restaurant — only View Details', () => {
      const element = render({ guestCount: 4, groupPreOrder: { ...OPEN, status: 'Submitted' } });
      expect(button(element, 'Add Pre-Order')).toBeUndefined();
      expect(button(element, 'View Details')).toBeDefined();
    });

    it('AC5: after the 30-minute cut-off — no Add Pre-Order and no Invite Guests', () => {
      const soon = new Date(Date.now() + 10 * 60 * 1000);
      const element = render({ guestCount: 4, groupPreOrder: OPEN, startTime: soon, endTime: soon });
      expect(button(element, 'Add Pre-Order')).toBeUndefined();
      expect(button(element, 'Invite Guests')).toBeUndefined();
      expect(button(element, 'View Details')).toBeDefined();
    });

    it('AC6: 1 guest — no counter, no hint, no View Details', () => {
      const element = render({ guestCount: 1, groupPreOrder: null });
      expect(text(element)).not.toContain('Pre-orders:');
      expect(button(element, 'View Details')).toBeUndefined();
      expect(button(element, 'Add Pre-Order')).toBeDefined();
    });

    it('AC7: Completed — no pre-order actions, the counter stays', () => {
      const element = render({ guestCount: 4, groupPreOrder: OPEN, status: ReservationStatusCustomer.Finished });
      expect(text(element)).toContain('Pre-orders: 2/4');
      expect(button(element, 'Add Pre-Order')).toBeUndefined();
      expect(button(element, 'View Details')).toBeUndefined();
      expect(button(element, 'Invite Guests')).toBeUndefined();
    });

    it('Cancelled: no counter and no progress bar', () => {
      const element = render({
        guestCount: 4,
        groupPreOrder: { status: 'Closed', submitted: 0, joined: 1, seats: 4 },
        status: ReservationStatusCustomer.Cancelled,
      });
      expect(text(element)).not.toContain('Pre-orders:');
      expect(element.querySelector('[role="progressbar"]')).toBeNull();
    });
  });

  describe('layout', () => {
    it('row (Customer\'s Reservations page): one row with a month/day date block', () => {
      fixture.componentRef.setInput('layout', 'row');
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;
      expect(element.classList).toContain('row-layout');
      expect(element.querySelector('.date-month')?.textContent?.trim()).toBe('Oct');
      expect(element.querySelector('.date-day')?.textContent?.trim()).toBe('21');
    });

    it('grid by default (waiter dashboard): no date block', () => {
      const element = fixture.nativeElement as HTMLElement;
      expect(element.classList).not.toContain('row-layout');
      expect(element.querySelector('.date-block')).toBeNull();
    });
  });

  describe('row layout (Customer\'s Reservations page)', () => {
    const IN_A_WEEK = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
    const OPEN = { status: 'Open' as const, submitted: 2, joined: 3, seats: 4 };

    function renderRow(overrides: Partial<ReservationCustomer>) {
      fixture.componentRef.setInput('layout', 'row');
      fixture.componentRef.setInput('role', Roles.CUSTOMER);
      fixture.componentRef.setInput('r', { ...RESERVATION, startTime: IN_A_WEEK, endTime: IN_A_WEEK, ...overrides });
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    const button = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll('button')].find((item) => item.textContent?.trim() === label);

    it('the host is the list item', () => {
      expect((fixture.nativeElement as HTMLElement).getAttribute('role')).toBe('listitem');
    });

    it('one primary step: Add Pre-Order while it\'s possible, View Details as secondary', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN });
      expect(button(element, 'Add Pre-Order')?.classList).toContain('button--primary');
      expect(button(element, 'View Details')?.classList).toContain('button--secondary');
    });

    it('after the group pre-order is sent: View Details becomes the primary step', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: { ...OPEN, status: 'Submitted' } });
      expect(button(element, 'Add Pre-Order')).toBeUndefined();
      expect(button(element, 'View Details')?.classList).toContain('button--primary');
    });

    it('Edit and Cancel are quiet text links, not buttons next to the primary one', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN });
      expect(button(element, 'Edit reservation')?.classList).toContain('link-button');
      expect(button(element, 'Cancel reservation')?.classList).toContain('link-button--danger');
    });

    it('after the 30-minute cut-off: no Edit, no Cancel', () => {
      const soon = new Date(Date.now() + 10 * 60 * 1000);
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN, startTime: soon, endTime: soon });
      expect(button(element, 'Edit reservation')).toBeUndefined();
      expect(button(element, 'Cancel reservation')).toBeUndefined();
    });

    it('In Progress: no Edit, no Cancel, no pre-order steps', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN, status: ReservationStatusCustomer.InProgress });
      expect(button(element, 'Cancel reservation')).toBeUndefined();
      expect(button(element, 'Add Pre-Order')).toBeUndefined();
      expect(button(element, 'View Details')).toBeUndefined();
    });

    it('meta line: 24-hour time, guests and table — the date isn\'t repeated next to the date block', () => {
      const start = new Date(IN_A_WEEK);
      start.setHours(19, 0, 0, 0);
      const end = new Date(start.getTime() + 90 * 60 * 1000);
      const element = renderRow({ startTime: start, endTime: end, guestCount: 5, tableNumber: 7 });
      const visibleMeta = [...element.querySelectorAll('.row-meta > span:not(.visually-hidden)')]
        .map((span) => span.textContent?.trim())
        .join(' | ');
      expect(visibleMeta).toBe('19:00 – 20:30 | 5 guests | Table 7');
      expect(element.querySelectorAll('.row-meta .meta-icon').length).toBe(3);
      expect(element.querySelector('.date-weekday')?.textContent?.trim()).toBeTruthy();
    });

    it('Cancelled: no action column at all', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN, status: ReservationStatusCustomer.Cancelled });
      expect(element.querySelector('.row-actions')).toBeNull();
    });

    it('pre-order progress: "Pre-orders: 2/4" with a progress bar', () => {
      const element = renderRow({ guestCount: 4, groupPreOrder: OPEN });
      expect(element.querySelector('.pre-orders-counter')?.textContent?.trim()).toBe('Pre-orders: 2/4');
      expect(element.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow')).toBe('2');
    });
  });
});
