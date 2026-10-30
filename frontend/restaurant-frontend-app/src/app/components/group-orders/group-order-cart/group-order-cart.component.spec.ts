// Рівень 2 · кошик Owner на даних із сервісу: суми, Dish Summary, стани, Submit.
// Сервіс підмінено шпигуном — у мережу тест не ходить. Запуск: npm run test:hw
import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, Subject, of, throwError } from 'rxjs';
import { SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import {
  GIORGI,
  INVITE,
  NINO,
  OWNER,
  buttonByText,
  createServiceSpy,
  dialog,
  renderCart,
  sessions,
  text,
} from '../testing/group-order.fixtures';

describe('Рівень 2 · кошик Owner', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(() => (service = createServiceSpy()));

  function openSubmitDialog() {
    const rendered = renderCart(service);
    buttonByText(rendered.element, 'Submit Pre-Order')?.click();
    rendered.fixture.detectChanges();
    return rendered;
  }

  describe('дані', () => {
    it('бере reservationId і вантажить групу за GroupReservationId', () => {
      renderCart(service);
      expect(service.getGroupInvite$).toHaveBeenCalledWith('res-1');
      expect(service.getOwnerSessionId$).toHaveBeenCalledWith('res-1', { silent: true });
      expect(service.getSessions$.calls.mostRecent().args[0]).toBe('grp-1');
      expect(service.getStatus$.calls.mostRecent().args[0]).toBe('grp-1');
    });

    it('«My Pre-Order» — учасник з id власної сесії, навіть якщо він не перший', () => {
      service.getSessions$.and.returnValue(of(sessions([NINO, OWNER])));
      const { element } = renderCart(service);
      expect(text(element.querySelector('.my-pre-order')!)).toContain('Fresh Strawberry Mint Salad');
      expect(element.querySelectorAll('app-guest-pre-order').length).toBe(1);
    });

    it('«2 of 3 submitted»: гостьових місць = гостей мінус Owner', () => {
      expect(text(renderCart(service).element)).toContain('2 of 3 submitted');
    });
  });

  describe('Grand Total — головна пастка', () => {
    it('рахує лише підтверджені кошики: $87.00, а не $96.50 з API', () => {
      const footer = text(renderCart(service).element.querySelector('.drawer-footer')!);
      expect(footer).toContain('$87.00');
      expect(footer).not.toContain('$96.50');
    });

    it('є підтверджені страви гостей → «Includes guests\' pre-orders»', () => {
      expect(text(renderCart(service).element)).toContain("Includes guests' pre-orders");
    });

    it('гості ще нічого не підтвердили → лише страви Owner і інша підказка', () => {
      service.getSessions$.and.returnValue(of(sessions([OWNER, { ...NINO, isSubmitted: false }])));
      const footer = text(renderCart(service).element.querySelector('.drawer-footer')!);
      expect(footer).toContain('$27.00');
      expect(footer).toContain("Guests' pre-orders are added here when they submit.");
    });
  });

  describe('Dish Summary', () => {
    function summaryText() {
      return text(renderCart(service).element.querySelector('#group-cart-panel-summary')!);
    }

    it('кожна страва один раз, найпопулярніші першими, при рівності — за назвою', () => {
      const names = [...renderCart(service).element.querySelectorAll('#group-cart-panel-summary .line-name')].map(text);
      expect(names).toEqual([
        'Berry Cacao Bowl',
        'Fresh Strawberry Mint Salad',
        'Roasted Sweet Potato & Lentil Salad',
        'Spring Salad',
      ]);
    });

    it('хто скільки замовив: «You × 1 · Luka × 1»', () => {
      expect(summaryText()).toContain('You × 1 · Luka × 1');
    });

    it('«7 dishes», чернетка Giorgi не рахується', () => {
      const summary = summaryText();
      expect(summary).toContain('7 dishes');
      expect(summary).not.toContain('Giorgi');
    });
  });

  describe('вільні місця', () => {
    it('«1 seat not joined yet» і «Invite Guests» копіює посилання', async () => {
      service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: { ...INVITE, MaxGuestCount: 5 } }));
      const writeText = spyOn(navigator.clipboard, 'writeText').and.resolveTo();
      const { element, toast } = renderCart(service);

      expect(text(element)).toContain('1 seat not joined yet');
      buttonByText(element, 'Invite Guests')?.click();
      await writeText.calls.mostRecent().returnValue;

      expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/join/tok`);
      expect(toast.success).toHaveBeenCalledWith('Invite link copied');
    });

    it('усі місця зайняті — рядка немає', () => {
      expect(text(renderCart(service).element)).not.toContain('not joined yet');
    });
  });

  describe('стани', () => {
    it('loading: «Loading pre-orders…», даних ще немає', () => {
      service.getGroupInvite$.and.returnValue(NEVER);
      const { element } = renderCart(service);
      expect(text(element)).toContain('Loading pre-orders…');
      expect(text(element)).not.toContain('Grand Total');
    });

    it('404: «You don\'t have access to this reservation.»', () => {
      service.getGroupInvite$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      expect(text(renderCart(service).element)).toContain("You don't have access to this reservation.");
    });

    it('група вже надіслана (400 GroupReservationSubmitted): порожній кошик, а не помилка', () => {
      service.getGroupInvite$.and.returnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { message: 'Group reservation was submitted.', errorCode: 'GroupReservationSubmitted' },
            }),
        ),
      );
      const content = text(renderCart(service).element);
      expect(content).toContain('Your cart is empty');
      expect(content).toContain('Go to my Reservations');
      expect(content).not.toContain("We couldn't load group pre-orders.");
    });

    it('група вже надіслана, знайдена через сесію Owner — порожній кошик без запиту invite-token', () => {
      service.getOwnerSessionId$.and.returnValue(of('owner-grp-1'));
      service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Submitted' as const }));
      const content = text(renderCart(service).element);
      expect(service.getGroupInvite$).not.toHaveBeenCalled();
      expect(content).toContain('Your cart is empty');
      expect(content).not.toContain('Grand Total');
    });

    it('інший 400 без errorCode — як і раніше, «Try Again»', () => {
      service.getGroupInvite$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { message: 'Bad' } })));
      expect(text(renderCart(service).element)).toContain("We couldn't load group pre-orders.");
    });

    it('інша помилка: «Try Again» вантажить ще раз і показує дані', () => {
      service.getGroupInvite$.and.returnValues(
        throwError(() => new HttpErrorResponse({ status: 500 })),
        of({ token: 'tok', invite: INVITE }),
      );
      const { fixture, element } = renderCart(service);
      expect(text(element)).toContain("We couldn't load group pre-orders.");

      buttonByText(element, 'Try Again')?.click();
      fixture.detectChanges();

      expect(service.getGroupInvite$).toHaveBeenCalledTimes(2);
      expect(text(element)).toContain('$87.00');
    });

    it('порожній кошик Owner: «Your cart is empty», «Browse Menu» просить відкрити меню цього бронювання', () => {
      service.getSessions$.and.returnValue(of(sessions([{ ...OWNER, items: [], total: 0 }, NINO])));
      const { fixture, element } = renderCart(service);
      const labels: string[] = [];
      fixture.componentInstance.browseMenu.subscribe((label) => labels.push(label));

      expect(text(element)).toContain('Your cart is empty');
      buttonByText(element, 'Browse Menu')?.click();

      expect(labels).toEqual(['48 Rustaveli Avenue']);
    });

    it('Escape закриває кошик', () => {
      const { fixture } = renderCart(service);
      let closed = 0;
      fixture.componentInstance.closed.subscribe(() => closed++);
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      expect(closed).toBe(1);
    });
  });

  describe('Submit Pre-Order', () => {
    it('неактивна, якщо підтверджених страв немає', () => {
      service.getSessions$.and.returnValue(of(sessions([{ ...OWNER, items: [], total: 0 }, GIORGI])));
      expect(buttonByText(renderCart(service).element, 'Submit Pre-Order')?.disabled).toBeTrue();
    });

    it('активна, коли страви є лише в Owner', () => {
      service.getSessions$.and.returnValue(of(sessions([OWNER, GIORGI])));
      expect(buttonByText(renderCart(service).element, 'Submit Pre-Order')?.disabled).toBeFalse();
    });

    it('діалог: що піде в ресторан і попередження про непідтверджених гостей', () => {
      const content = text(dialog(openSubmitDialog().element));
      expect(content).toContain('The restaurant will receive 7 dishes for $87.00.');
      expect(content).toContain("1 guest hasn't submitted yet — their dishes won't be included.");
    });

    it('«Cancel» закриває діалог без запиту', () => {
      const { fixture, element } = openSubmitDialog();
      buttonByText(dialog(element), 'Cancel')?.click();
      fixture.detectChanges();
      expect(element.querySelector('app-modal')).toBeNull();
      expect(service.submit$).not.toHaveBeenCalled();
    });

    it('подвійний клік «Submit» відправляє один запит; кнопка — «Submitting…»', () => {
      const response$ = new Subject<SubmittedDish[]>();
      service.submit$.and.returnValue(response$);
      const { fixture, element } = openSubmitDialog();

      buttonByText(dialog(element), 'Submit')?.click();
      fixture.detectChanges();
      buttonByText(dialog(element), 'Submit')?.click();
      buttonByText(dialog(element), 'Submitting…')?.click();
      fixture.detectChanges();

      expect(buttonByText(dialog(element), 'Submitting…')?.disabled).toBeTrue();
      expect(service.submit$).toHaveBeenCalledOnceWith('grp-1', 'res-1');
    });

    it('успіх: toast і порожній кошик — «Your cart is empty»', () => {
      service.submit$.and.returnValue(of([{ dishId: 'dish-berry', quantity: 2 }]));
      const { fixture, element, toast } = openSubmitDialog();
      buttonByText(dialog(element), 'Submit')?.click();
      fixture.detectChanges();

      expect(toast.success).toHaveBeenCalledWith('Pre-order sent to the restaurant');
      expect(text(element)).toContain('Your cart is empty');
      expect(text(element)).toContain('Your group pre-order was sent to the restaurant.');
      expect(text(element)).not.toContain('Fresh Strawberry Mint Salad');
      expect(text(element)).not.toContain('Grand Total');
      expect(buttonByText(element, 'Submit Pre-Order')).toBeUndefined();
      expect(element.querySelector('app-quantity-stepper')).toBeNull();
      expect(buttonByText(element, 'Remove Guest')).toBeUndefined();
    });

    it('помилка: діалог закрито, кнопку можна натиснути знову, toast не дублюємо', () => {
      service.submit$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
      const { fixture, element, toast } = openSubmitDialog();
      buttonByText(dialog(element), 'Submit')?.click();
      fixture.detectChanges();

      expect(element.querySelector('app-modal')).toBeNull();
      expect(buttonByText(element, 'Submit Pre-Order')?.disabled).toBeFalse();
      expect(toast.error).not.toHaveBeenCalled();
    });
  });
  describe('швидке завантаження: група через сесію Owner, invite — у фоні', () => {
    beforeEach(() => {
      service.getOwnerSessionId$.and.returnValue(of('owner-grp-1'));
      service.getSessions$.and.returnValue(of(sessions([{ ...OWNER, id: 'owner-grp-1' }, NINO, GIORGI])));
    });

    it('учасники видно одразу, ще до invite; редагування — лише коли відомий час (30-хв cut-off)', () => {
      const invite$ = new Subject<{ token: string; invite: typeof INVITE }>();
      service.getGroupInvite$.and.returnValue(invite$);
      const { fixture, element } = renderCart(service);

      expect(service.getSessions$).toHaveBeenCalledWith('grp-1', { silent: true });
      expect(text(element)).toContain('Fresh Strawberry Mint Salad');
      expect(element.querySelector('app-quantity-stepper')).toBeNull();

      invite$.next({ token: 'tok', invite: INVITE });
      invite$.complete();
      fixture.detectChanges();

      expect(text(element)).toContain('48 Rustaveli Avenue');
      expect(element.querySelector('app-quantity-stepper')).not.toBeNull();
    });

    it('invite не віддали (усі місця зайняті — GuestLimitExceeded): кошик працює, без посилання', () => {
      service.getGroupInvite$.and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 400, error: { errorCode: 'GuestLimitExceeded' } })),
      );
      const { element } = renderCart(service);

      expect(text(element)).not.toContain("We couldn't load group pre-orders.");
      expect(element.querySelector('app-quantity-stepper')).not.toBeNull();
      expect(buttonByText(element, 'Invite Guests')).toBeUndefined();
    });

    it('зміни й Submit ідуть у групу з сесії Owner, навіть без invite', () => {
      service.getGroupInvite$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
      service.submit$.and.returnValue(of([] as SubmittedDish[]));
      const { fixture, element } = renderCart(service);

      buttonByText(element, 'Submit Pre-Order')?.click();
      fixture.detectChanges();
      buttonByText(dialog(element), 'Submit')?.click();

      expect(service.submit$).toHaveBeenCalledOnceWith('grp-1', 'res-1');
    });
  });
});
