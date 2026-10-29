// Бонус · Remove Guest — кошик Owner. Запуск: npm run test:hw
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import {
  INVITE,
  LUKA,
  NINO,
  OWNER,
  buttonByText,
  createServiceSpy,
  dialog,
  renderCart,
  sessions,
  text,
} from '../testing/group-order.fixtures';

function removeButtons(element: HTMLElement): HTMLButtonElement[] {
  return [...element.querySelectorAll<HTMLButtonElement>('app-guest-pre-order button')].filter(
    (button) => button.textContent?.trim() === 'Remove Guest',
  );
}

describe('Бонус · Remove Guest', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(() => (service = createServiceSpy()));

  function confirmRemovalOf(index: number) {
    const rendered = renderCart(service);
    removeButtons(rendered.element)[index].click();
    rendered.fixture.detectChanges();
    return rendered;
  }

  it('«Remove Guest» є в кожного гостя (і в того, хто ще не підтвердив), біля Owner — немає', () => {
    const { element } = renderCart(service);
    expect(removeButtons(element).length).toBe(3);
    expect(element.querySelector('.my-pre-order')?.textContent).not.toContain('Remove Guest');
  });

  it('aria-label з ім\'ям: «Remove guest Nino»', () => {
    expect(removeButtons(renderCart(service).element)[0].getAttribute('aria-label')).toBe('Remove guest Nino');
  });

  it('діалог називає гостя і попереджає, що це не скасувати', () => {
    const content = text(dialog(confirmRemovalOf(2).element));
    expect(content).toContain('Are you sure you want to remove Giorgi and their pre-order from this reservation?');
    expect(content).toContain('This action cannot be undone.');
  });

  it('«Cancel» — без запиту', () => {
    const { fixture, element } = confirmRemovalOf(2);
    buttonByText(dialog(element), 'Cancel')?.click();
    fixture.detectChanges();
    expect(service.removeSession$).not.toHaveBeenCalled();
    expect(element.querySelector('app-modal')).toBeNull();
  });

  it('підтвердження видаляє і оновлює список і суми з відповіді сервера', () => {
    service.removeSession$.and.returnValue(of(sessions([OWNER, LUKA, { ...NINO, isSubmitted: false }])));
    const { fixture, element, toast } = confirmRemovalOf(2);

    buttonByText(dialog(element), 'Remove Guest')?.click();
    fixture.detectChanges();

    expect(service.removeSession$).toHaveBeenCalledOnceWith('grp-1', 'giorgi');
    expect(element.querySelectorAll('app-guest-pre-order').length).toBe(2);
    expect(text(element.querySelector('.drawer-footer')!)).toContain('$54.00');
    expect(toast.success).toHaveBeenCalledWith('Giorgi was removed from the reservation');
    expect(element.querySelector('app-modal')).toBeNull();
  });

  it('подвійний клік — один запит', () => {
    service.removeSession$.and.returnValue(new Subject<GroupSessionsResponse>());
    const { fixture, element } = confirmRemovalOf(0);
    buttonByText(dialog(element), 'Remove Guest')?.click();
    fixture.detectChanges();
    buttonByText(dialog(element), 'Removing…')?.click();
    expect(service.removeSession$).toHaveBeenCalledTimes(1);
  });

  it('помилка DELETE: список без змін', () => {
    service.removeSession$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
    const { fixture, element } = confirmRemovalOf(0);
    buttonByText(dialog(element), 'Remove Guest')?.click();
    fixture.detectChanges();
    expect(element.querySelectorAll('app-guest-pre-order').length).toBe(3);
  });

  it('після Submit кнопок «Remove Guest» немає', () => {
    service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Submitted' as const }));
    expect(removeButtons(renderCart(service).element).length).toBe(0);
  });
});

describe('Бонус · Remove seat (місце, яке ніхто не зайняв)', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  function withGuests(maxGuestCount: number) {
    service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: { ...INVITE, MaxGuestCount: maxGuestCount } }));
  }

  function openSeatDialog(maxGuestCount: number) {
    withGuests(maxGuestCount);
    const rendered = renderCart(service);
    buttonByText(rendered.element, 'Remove seat')?.click();
    rendered.fixture.detectChanges();
    return rendered;
  }

  beforeEach(() => (service = createServiceSpy()));

  it('усі місця зайняті — «Remove seat» немає', () => {
    expect(buttonByText(renderCart(service).element, 'Remove seat')).toBeUndefined();
  });

  it('останнє вільне місце: діалог попереджає, що за посиланням більше ніхто не приєднається', () => {
    expect(text(dialog(openSeatDialog(5).element))).toContain(
      'Your reservation will be for 4 guests, and nobody else will be able to join with the invite link.',
    );
  });

  it('вільних місць кілька — лише нова кількість гостей', () => {
    const content = text(dialog(openSeatDialog(6).element));
    expect(content).toContain('Your reservation will be for 5 guests.');
    expect(content).not.toContain('invite link');
  });

  it('підтвердження: запит, шапка «4 Guests», рядок вільних місць зникає, toast', () => {
    service.removeSeat$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Open' as const }));
    const { fixture, element, toast } = openSeatDialog(5);

    buttonByText(dialog(element), 'Remove seat')?.click();
    fixture.detectChanges();

    expect(service.removeSeat$).toHaveBeenCalledOnceWith('grp-1');
    expect(text(element.querySelector('.reservation')!)).toContain('4 Guests');
    expect(text(element)).not.toContain('not joined yet');
    expect(text(element)).toContain('2 of 3 submitted');
    expect(toast.success).toHaveBeenCalledWith('Your reservation is now for 4 guests');
    expect(element.querySelector('app-modal')).toBeNull();
  });

  it('подвійний клік — один запит', () => {
    service.removeSeat$.and.returnValue(new Subject<GroupStatusResponse>());
    const { fixture, element } = openSeatDialog(5);
    buttonByText(dialog(element), 'Remove seat')?.click();
    fixture.detectChanges();
    buttonByText(dialog(element), 'Removing…')?.click();
    expect(service.removeSeat$).toHaveBeenCalledTimes(1);
  });

  it('помилка: кількість гостей без змін, діалог закрито', () => {
    service.removeSeat$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
    const { fixture, element } = openSeatDialog(5);
    buttonByText(dialog(element), 'Remove seat')?.click();
    fixture.detectChanges();

    expect(text(element.querySelector('.reservation')!)).toContain('5 Guests');
    expect(text(element)).toContain('1 seat not joined yet');
    expect(element.querySelector('app-modal')).toBeNull();
  });

  it('після Submit — немає', () => {
    withGuests(5);
    service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 5, status: 'Submitted' as const }));
    expect(buttonByText(renderCart(service).element, 'Remove seat')).toBeUndefined();
  });
});
