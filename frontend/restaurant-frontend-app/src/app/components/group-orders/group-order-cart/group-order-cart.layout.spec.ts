// Рівень 1 · верстка кошика Owner за макетом «Cart - Owner & Guests Pre-Orders».
// Сервіс підмінено шпигуном з даними макета — у мережу тест не ходить. Запуск: npm run test:hw
import { createServiceSpy, renderCart, text } from '../testing/group-order.fixtures';

describe('Рівень 1 · верстка кошика', () => {
  let element: HTMLElement;
  let fixture: ReturnType<typeof renderCart>['fixture'];

  beforeEach(() => ({ element, fixture } = renderCart(createServiceSpy())));

  it('бічна панель — діалог із заголовком «Cart»', () => {
    const drawer = element.querySelector('aside[role="dialog"]');
    expect(drawer?.getAttribute('aria-modal')).toBe('true');
    expect(element.querySelector('#' + drawer?.getAttribute('aria-labelledby'))?.textContent).toContain('Cart');
  });

  it('шапка: адреса, дата, час і кількість гостей', () => {
    const header = text(element.querySelector('.reservation')!);
    expect(header).toContain('48 Rustaveli Avenue');
    expect(header).toContain('Oct 17, 2030');
    expect(header).toContain('12:00 - 13:30');
    expect(header).toContain('4 Guests');
  });

  it('вкладки Participants / Dish Summary: tablist, активна — Participants', () => {
    const tabs = [...element.querySelectorAll('[role="tablist"] [role="tab"]')];
    expect(tabs.map((tab) => text(tab))).toEqual(['Participants', 'Dish Summary']);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(element.querySelector<HTMLElement>('#group-cart-panel-summary')?.hidden).toBeTrue();
  });

  it('клік по «Dish Summary» перемикає панель', () => {
    element.querySelector<HTMLButtonElement>('#group-cart-tab-summary')?.click();
    fixture.detectChanges();
    expect(element.querySelector<HTMLElement>('#group-cart-panel-participants')?.hidden).toBeTrue();
    expect(element.querySelector<HTMLElement>('#group-cart-panel-summary')?.hidden).toBeFalse();
  });

  it('«My Pre-Order»: страви Owner — списком карток', () => {
    expect(text(element)).toContain('My Pre-Order');
    expect(element.querySelectorAll('.my-items > li app-my-pre-order-item').length).toBe(2);
  });

  it('«Guests\' pre-orders»: кожен гість — <li> у списку', () => {
    expect(text(element)).toContain("Guests' pre-orders");
    expect(element.querySelectorAll('.guest-list > li app-guest-pre-order').length).toBe(3);
  });

  it('статус кошика гостя видно текстом, не лише кольором', () => {
    expect(text(element)).toContain('Not submitted yet');
  });

  it('футер: Grand Total, підказка і кнопка «Submit Pre-Order»', () => {
    const footer = text(element.querySelector('.drawer-footer')!);
    expect(footer).toContain('Grand Total');
    expect(footer).toContain('Excludes taxes and service charges');
    expect(footer).toContain('Submit Pre-Order');
  });
});
