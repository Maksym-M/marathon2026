// Рівень 3 · синхронізація з бекендом: кожні 15 с, поки група Open.
// fakeAsync + tick(15_000) «прокручує» час без очікування. Запуск: npm run test:hw
import { discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, Subject, of, throwError } from 'rxjs';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import {
  GIORGI,
  INVITE,
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
import { GROUP_ORDER_SYNC_MS } from './group-order.store';

describe('Рівень 3 · синхронізація', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(() => (service = createServiceSpy()));

  it('кожні 15 с підтягує свіжі кошики — без перезавантаження сторінки', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    expect(text(element)).toContain('Not submitted yet');

    // Giorgi підтвердив кошик, поки Owner дивиться
    service.getSessions$.and.returnValue(of(sessions([OWNER, NINO, LUKA, { ...GIORGI, isSubmitted: true }])));
    tick(GROUP_ORDER_SYNC_MS);
    fixture.detectChanges();

    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    expect(text(element)).not.toContain('Not submitted yet');
    expect(text(element)).toContain('3 of 3 submitted');
    discardPeriodicTasks();
  }));

  it('фонові запити тихі: { silent: true }', fakeAsync(() => {
    renderCart(service);
    tick(GROUP_ORDER_SYNC_MS);
    expect(service.getSessions$.calls.mostRecent().args).toEqual(['grp-1', jasmine.objectContaining({ silent: true })]);
    expect(service.getStatus$.calls.mostRecent().args).toEqual(['grp-1', jasmine.objectContaining({ silent: true })]);
    discardPeriodicTasks();
  }));

  it('показує «Updated …» і кнопку Refresh, яка оновлює одразу', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    expect(text(element)).toMatch(/Updated \d{2}:\d{2}/);

    buttonByLabel(element, 'Refresh')?.click();
    fixture.detectChanges();

    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    discardPeriodicTasks();
  }));

  it('поки оновлення триває, Refresh неактивна', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    service.getSessions$.and.returnValue(NEVER);
    buttonByLabel(element, 'Refresh')?.click();
    fixture.detectChanges();
    expect(buttonByLabel(element, 'Refresh')?.disabled).toBeTrue();
    discardPeriodicTasks();
  }));

  it('фонова помилка: дані лишаються, «Couldn\'t refresh. Showing data from …»', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    service.getSessions$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    tick(GROUP_ORDER_SYNC_MS);
    fixture.detectChanges();

    expect(text(element)).toMatch(/Couldn't refresh\. Showing data from \d{2}:\d{2}/);
    expect(element.querySelectorAll('app-guest-pre-order').length).toBe(3);
    discardPeriodicTasks();
  }));

  it('після наступного вдалого оновлення повідомлення зникає', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    service.getSessions$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    tick(GROUP_ORDER_SYNC_MS);
    service.getSessions$.and.callFake(() => of(sessions([OWNER, NINO])));
    tick(GROUP_ORDER_SYNC_MS);
    fixture.detectChanges();
    expect(text(element)).not.toContain("Couldn't refresh");
    discardPeriodicTasks();
  }));

  it('якщо попередній запит ще не відповів, новий не стартує', fakeAsync(() => {
    renderCart(service);
    service.getSessions$.and.returnValue(NEVER);
    tick(GROUP_ORDER_SYNC_MS * 3);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    discardPeriodicTasks();
  }));

  it('«Try Again» не запускає другий таймер', fakeAsync(() => {
    service.getGroupInvite$.and.returnValues(
      throwError(() => new HttpErrorResponse({ status: 500 })),
      of({ token: 'tok', invite: INVITE }),
    );
    const { fixture, element } = renderCart(service);
    buttonByText(element, 'Try Again')?.click();
    fixture.detectChanges();

    tick(GROUP_ORDER_SYNC_MS);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    discardPeriodicTasks();
  }));

  it('після Submit синхронізація зупиняється', fakeAsync(() => {
    service.submit$.and.returnValue(of([{ dishId: 'dish-berry', quantity: 2 }]));
    const { fixture, element } = renderCart(service);
    buttonByText(element, 'Submit Pre-Order')?.click();
    fixture.detectChanges();
    buttonByText(dialog(element), 'Submit')?.click();
    fixture.detectChanges();

    tick(GROUP_ORDER_SYNC_MS * 3);
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    discardPeriodicTasks();
  }));

  it('статус Submitted з фонового оновлення (інша вкладка) — порожній кошик і стоп', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    service.getStatus$.and.returnValue(of({ currentParticipants: 4, maxCapacity: 4, status: 'Submitted' as const }));
    tick(GROUP_ORDER_SYNC_MS);
    fixture.detectChanges();
    tick(GROUP_ORDER_SYNC_MS * 2);

    expect(text(element)).toContain('Your cart is empty');
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    discardPeriodicTasks();
  }));

  it('при закритті кошика таймер зупиняється', fakeAsync(() => {
    const { fixture } = renderCart(service);
    fixture.destroy();
    tick(GROUP_ORDER_SYNC_MS * 3);
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    discardPeriodicTasks();
  }));

  it('оновлення, що стартувало до правки, не повертає старий кошик', fakeAsync(() => {
    const { fixture, element } = renderCart(service);
    const staleRefresh$ = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(staleRefresh$);
    tick(GROUP_ORDER_SYNC_MS); // фонове оновлення пішло і чекає

    service.updateDishQuantity$.and.returnValue(
      of(sessions([OWNER, { ...NINO, total: 42.5, items: [{ ...NINO.items[0], quantity: 3 }, NINO.items[1]] }, LUKA, GIORGI])),
    );
    buttonByLabel(element, 'Increase Berry Cacao Bowl')?.click();
    fixture.detectChanges();

    staleRefresh$.next(sessions([OWNER, NINO, LUKA, GIORGI])); // відповідь зі старими даними
    staleRefresh$.complete();
    fixture.detectChanges();

    expect(text(element)).toContain('$42.50');
    discardPeriodicTasks();
  }));
});
