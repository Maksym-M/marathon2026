// Бонус · Owner змінює страви: свої і в підтверджених кошиках гостей. Запуск: npm run test:hw
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import {
  GIORGI,
  LUKA,
  NINO,
  OWNER,
  buttonByLabel,
  buttonByText,
  createServiceSpy,
  dialog,
  renderCart,
  sessions,
  text,
} from '../testing/group-order.fixtures';

describe('Бонус · Owner змінює страви', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(() => (service = createServiceSpy()));

  describe('My Pre-Order', () => {
    it('«+» → PUT з новою кількістю, екран — з відповіді сервера', () => {
      const owner = { ...OWNER, total: 44, items: [{ ...OWNER.items[0], quantity: 2 }, OWNER.items[1]] };
      service.updateDishQuantity$.and.returnValue(of(sessions([owner, NINO, LUKA, GIORGI])));
      const { fixture, element } = renderCart(service);

      buttonByLabel(element.querySelector('.my-pre-order')!, 'Increase Fresh Strawberry Mint Salad')?.click();
      fixture.detectChanges();

      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'owner', 'dish-strawberry', 2);
      expect(text(element.querySelector('.drawer-footer')!)).toContain('$104.00');
    });

    it('кошик прибирає власну страву одразу, без діалогу', () => {
      service.updateDishQuantity$.and.returnValue(of(sessions([OWNER, NINO, LUKA, GIORGI])));
      const { fixture, element } = renderCart(service);

      buttonByLabel(element, 'Remove Roasted Sweet Potato & Lentil Salad')?.click();
      fixture.detectChanges();

      expect(element.querySelector('app-modal')).toBeNull();
      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'owner', 'dish-potato', 0);
    });

    it('поки сервер відповідає, кнопки кошика неактивні — другий клік не шле запит', () => {
      service.updateDishQuantity$.and.returnValue(new Subject<GroupSessionsResponse>());
      const { fixture, element } = renderCart(service);
      const increase = () => buttonByLabel(element.querySelector('.my-pre-order')!, 'Increase Fresh Strawberry Mint Salad')!;

      increase().click();
      fixture.detectChanges();
      increase().click();

      expect(increase().disabled).toBeTrue();
      expect(service.updateDishQuantity$).toHaveBeenCalledTimes(1);
    });

    it('помилка: дані без змін, кнопки знову активні', () => {
      service.updateDishQuantity$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
      const { fixture, element } = renderCart(service);
      const increase = () => buttonByLabel(element.querySelector('.my-pre-order')!, 'Increase Fresh Strawberry Mint Salad')!;

      increase().click();
      fixture.detectChanges();

      expect(increase().disabled).toBeFalse();
      expect(text(element.querySelector('.drawer-footer')!)).toContain('$87.00');
    });
  });

  describe('кошики гостей', () => {
    it('підтверджений гість: «+» змінює його кошик', () => {
      service.updateDishQuantity$.and.returnValue(of(sessions([OWNER, NINO, LUKA, GIORGI])));
      const { element } = renderCart(service);

      buttonByLabel(element, 'Increase Berry Cacao Bowl')?.click();

      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'nino', 'dish-berry', 3);
    });

    it('гість, що ще обирає, — без кнопок редагування', () => {
      const giorgi = [...renderCart(service).element.querySelectorAll('app-guest-pre-order')][2];
      expect(giorgi.querySelector('app-quantity-stepper')).toBeNull();
      expect(giorgi.querySelector('button.trash')).toBeNull();
    });

    it('прибрати страву гостя — лише після підтвердження', () => {
      service.updateDishQuantity$.and.returnValue(
        of(sessions([OWNER, { ...NINO, total: 19, items: [NINO.items[0]] }, LUKA, GIORGI])),
      );
      const { fixture, element } = renderCart(service);

      buttonByLabel(element, "Remove Spring Salad from Nino's pre-order")?.click();
      fixture.detectChanges();
      expect(service.updateDishQuantity$).not.toHaveBeenCalled();
      expect(text(dialog(element))).toContain("Remove Spring Salad from Nino's pre-order? You can't add it back for them.");

      buttonByText(dialog(element), 'Remove')?.click();
      fixture.detectChanges();

      expect(service.updateDishQuantity$).toHaveBeenCalledOnceWith('grp-1', 'nino', 'dish-spring', 0);
      expect(element.querySelector('app-modal')).toBeNull();
      expect(text(element.querySelector('.drawer-footer')!)).toContain('$73.00');
    });

    it('сервер повернув гостя без страв → «Not submitted yet»', () => {
      service.updateDishQuantity$.and.returnValue(
        of(sessions([OWNER, NINO, { ...LUKA, isSubmitted: false, items: [], total: 0 }, GIORGI])),
      );
      const { fixture, element } = renderCart(service);
      buttonByLabel(element, "Remove Fresh Strawberry Mint Salad from Luka's pre-order")?.click();
      fixture.detectChanges();
      buttonByText(dialog(element), 'Remove')?.click();
      fixture.detectChanges();

      expect(text(element)).toContain('1 of 3 submitted');
    });
  });

  it('після Submit — лише перегляд: «Qty: 1», без кнопок', () => {
    service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Submitted' as const }));
    const { element } = renderCart(service);
    expect(element.querySelector('app-quantity-stepper')).toBeNull();
    expect(element.querySelector('button.trash')).toBeNull();
    expect(text(element.querySelector('.my-pre-order')!)).toContain('Qty: 1');
  });
});
