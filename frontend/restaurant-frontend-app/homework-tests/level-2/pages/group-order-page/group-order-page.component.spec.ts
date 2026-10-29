// Сторінка «Group Pre-Order» (US21–US23): дані, стани, редагування. Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import {
  GIORGI,
  INVITE,
  LUKA,
  NINO,
  OWNER,
  RESERVATION,
  buttonByLabel,
  buttonByText,
  createServiceSpy,
  dialog,
  sessions,
  text,
} from '@app/components/group-orders/testing/group-order.fixtures';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Сторінка Group Pre-Order', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  function render() {
    const paramMap = convertToParamMap({ reservationId: 'res-1' });
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        // provideRouter first: the ActivatedRoute stub below must win over the router's own.
        provideRouter([]),
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'info']) },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap }, paramMap: of(paramMap) } },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  function panel(element: HTMLElement, heading: string): HTMLElement {
    const panels = [...element.querySelectorAll<HTMLElement>('app-participant-panel')];
    const found = panels.find((item) => item.querySelector('h3')?.textContent?.trim() === heading);
    if (!found) throw new Error(`Немає блоку «${heading}»`);
    return found;
  }

  beforeEach(() => (service = createServiceSpy()));

  describe('дані (US21, US22, US23)', () => {
    it('один <main>, <h1> «Group Pre-Order», «← Back to Reservations» веде на /reservations', () => {
      const { element } = render();
      expect(element.querySelectorAll('main').length).toBe(1);
      expect(element.querySelector('h1')?.textContent).toContain('Group Pre-Order');
      expect(element.querySelector('a.back-link')?.getAttribute('href')).toBe('/reservations');
    });

    it('вантажить бронювання, потім групу', () => {
      render();
      expect(service.getReservation$).toHaveBeenCalledWith('res-1');
      expect(service.getGroupInvite$).toHaveBeenCalledWith('res-1');
    });

    it('деталі бронювання і бейдж «Collecting pre-orders»', () => {
      const content = text(render().element);
      expect(content).toContain('Collecting pre-orders');
      expect(content).toContain('48 Rustaveli Avenue, Table 7');
      expect(content).toContain('Oct 17, 2030');
      expect(content).toContain('12:00 - 13:30');
      expect(content).toContain('4 people');
    });

    it('блоки: «My Pre-Order» першим, гості — «Guest: {ім\'я}»', () => {
      const headings = [...render().element.querySelectorAll('app-participant-panel h3')].map(text);
      expect(headings).toEqual(['My Pre-Order', 'Guest: Nino', 'Guest: Luka', 'Guest: Giorgi']);
    });

    it('чернетка Giorgi не показується і не рахується; статус текстом', () => {
      const giorgi = panel(render().element, 'Guest: Giorgi');
      expect(text(giorgi)).toContain('Not submitted yet');
      expect(text(giorgi)).not.toContain('Berry Cacao Bowl');
    });

    it('рядки страв A–Z і Subtotal', () => {
      const nino = panel(render().element, 'Guest: Nino');
      expect([...nino.querySelectorAll('.line-name')].map(text)).toEqual(['Berry Cacao Bowl', 'Spring Salad']);
      expect(text(nino.querySelector('.subtotal')!)).toBe('Subtotal$33.00');
    });

    it('прогрес: «3 of 4 submitted», «4 of 4 joined», progressbar', () => {
      const { element } = render();
      expect(text(element)).toContain('3 of 4 submitted');
      expect(text(element)).toContain('4 of 4 joined');
      expect(element.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow')).toBe('3');
    });

    it('вільне місце — блок «Guest» зі статусом «Not joined»', () => {
      service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: { ...INVITE, MaxGuestCount: 5 } }));
      const placeholders = render().element.querySelectorAll('article.placeholder');
      expect(placeholders.length).toBe(1);
      expect(text(placeholders[0])).toContain('Not joined');
    });

    it('Grand Total $87.00 і Dish Summary', () => {
      const { element } = render();
      const content = text(element);
      expect(text(element.querySelector('.total-value')!)).toBe('$87.00');
      expect(content).toContain('Dish Summary');
      expect(content).toContain('You × 1 · Luka × 1');
    });
  });

  describe('стани (US21 AC9–AC10, US23 AC10)', () => {
    it('404 — «You don\'t have access to this reservation.», груповий запит не йде', () => {
      service.getReservation$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      expect(text(render().element)).toContain("You don't have access to this reservation.");
      expect(service.getGroupInvite$).not.toHaveBeenCalled();
    });

    it('помилка — «Try Again» вантажить ще раз', () => {
      service.getReservation$.and.returnValues(throwError(() => new HttpErrorResponse({ status: 500 })), of(RESERVATION));
      const { fixture, element } = render();
      expect(text(element)).toContain("We couldn't load group pre-orders.");

      buttonByText(element, 'Try Again')?.click();
      fixture.detectChanges();

      expect(text(element)).toContain('Grand Total');
    });

    it('скасоване бронювання — лише повідомлення', () => {
      service.getReservation$.and.returnValue(of({ ...RESERVATION, status: ReservationStatusCustomer.Cancelled }));
      const content = text(render().element);
      expect(content).toContain('This reservation has been cancelled.');
      expect(content).not.toContain('Grand Total');
    });

    it('візит почався — «passed to the restaurant»', () => {
      service.getReservation$.and.returnValue(of({ ...RESERVATION, status: ReservationStatusCustomer.InProgress }));
      expect(text(render().element)).toContain('Your pre-order has been passed to the restaurant.');
    });

    it('1 гість — групового pre-order немає', () => {
      service.getReservation$.and.returnValue(of({ ...RESERVATION, guestCount: 1 }));
      expect(text(render().element)).toContain('available for reservations with 2 or more guests');
      expect(service.getGroupInvite$).not.toHaveBeenCalled();
    });

    it('менше 30 хв до початку — банер і жодних кнопок редагування', () => {
      const soon = new Date(Date.now() + 10 * 60 * 1000).toISOString();
      service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: { ...INVITE, StartTime: soon } }));
      const { element } = render();
      expect(text(element)).toContain('Less than 30 minutes before the reservation time.');
      expect(element.querySelector('app-quantity-stepper')).toBeNull();
      expect(buttonByText(element, 'Remove Guest')).toBeUndefined();
      expect(buttonByText(element, 'Submit Pre-Order')?.disabled).toBeTrue();
    });

    it('надіслано — «Sent to Restaurant», лише перегляд', () => {
      service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Submitted' as const }));
      const { element } = render();
      expect(text(element)).toContain('Sent to Restaurant');
      expect(element.querySelector('app-quantity-stepper')).toBeNull();
      expect(buttonByText(element, 'Submit Pre-Order')).toBeUndefined();
    });
  });

  describe('редагування', () => {
    it('власна страва: «+» → PUT для сесії Owner', () => {
      service.updateDishQuantity$.and.returnValue(of(sessions([OWNER, NINO, LUKA, GIORGI])));
      const { element } = render();
      buttonByLabel(panel(element, 'My Pre-Order'), 'Increase Fresh Strawberry Mint Salad')?.click();
      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'owner', 'dish-strawberry', 2);
    });

    it('власна страва: кошик прибирає без діалогу', () => {
      service.updateDishQuantity$.and.returnValue(of(sessions([OWNER, NINO, LUKA, GIORGI])));
      const { element } = render();
      buttonByLabel(element, 'Remove Fresh Strawberry Mint Salad from My Pre-Order')?.click();
      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'owner', 'dish-strawberry', 0);
    });

    it('страва гостя: кошик → діалог → «Remove»', () => {
      service.updateDishQuantity$.and.returnValue(of(sessions([OWNER, { ...NINO, items: [NINO.items[0]], total: 19 }, LUKA, GIORGI])));
      const { fixture, element } = render();
      buttonByLabel(element, 'Remove Spring Salad from Guest: Nino')?.click();
      fixture.detectChanges();
      expect(service.updateDishQuantity$).not.toHaveBeenCalled();

      buttonByText(dialog(element), 'Remove')?.click();
      fixture.detectChanges();

      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'nino', 'dish-spring', 0);
      expect(text(element.querySelector('.total-value')!)).toBe('$73.00');
    });

    it('Remove Guest: діалог з ім\'ям → список з відповіді сервера', () => {
      service.removeSession$.and.returnValue(of(sessions([OWNER, NINO, LUKA])));
      const { fixture, element } = render();
      buttonByLabel(element, 'Remove guest Giorgi')?.click();
      fixture.detectChanges();
      expect(text(dialog(element))).toContain('Are you sure you want to remove Giorgi');

      buttonByText(dialog(element), 'Remove Guest')?.click();
      fixture.detectChanges();

      expect(service.removeSession$).toHaveBeenCalledOnceWith('grp-1', 'giorgi');
      expect(element.querySelectorAll('app-participant-panel').length).toBe(3);
    });

    it('Remove seat на вільному місці', () => {
      service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: { ...INVITE, MaxGuestCount: 5 } }));
      service.removeSeat$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Open' as const }));
      const { fixture, element } = render();
      buttonByText(element.querySelector('article.placeholder')!, 'Remove seat')?.click();
      fixture.detectChanges();
      buttonByText(dialog(element), 'Remove seat')?.click();
      fixture.detectChanges();

      expect(service.removeSeat$).toHaveBeenCalledOnceWith('grp-1');
      expect(element.querySelector('article.placeholder')).toBeNull();
    });

    it('Submit Pre-Order → діалог → «Sent to Restaurant»', () => {
      service.submit$.and.returnValue(of([]));
      const { fixture, element } = render();
      buttonByText(element, 'Submit Pre-Order')?.click();
      fixture.detectChanges();
      buttonByText(dialog(element), 'Submit')?.click();
      fixture.detectChanges();

      expect(service.submit$).toHaveBeenCalledOnceWith('grp-1', 'res-1');
      expect(text(element)).toContain('Sent to Restaurant');
    });
  });
});
