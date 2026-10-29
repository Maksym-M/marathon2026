// Практичне · блок гостя в «Guests' pre-orders». Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { DishQuantityChange } from '@app/models/group-orders/dish-quantity-change.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GIORGI, LUKA, NINO, buttonByLabel, buttonByText, text } from '../testing/group-order.fixtures';
import { GuestPreOrderComponent } from './guest-pre-order.component';

function render(participant: Participant, inputs: Partial<Record<'editable' | 'canRemove' | 'busy', boolean>> = {}) {
  const fixture = TestBed.createComponent(GuestPreOrderComponent);
  fixture.componentRef.setInput('participant', participant);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, component: fixture.componentInstance };
}

describe('Практичне · блок гостя', () => {
  describe('підтверджений кошик', () => {
    it('показує ім\'я заголовком і суму гостя', () => {
      const { element } = render(NINO);
      expect(element.querySelector('h4')?.textContent).toContain('Nino');
      expect(text(element)).toContain('$33.00');
    });

    it('страви — рядком «назва × кількість» і ціною рядка', () => {
      const content = text(render(NINO).element);
      expect(content).toContain('Berry Cacao Bowl × 2');
      expect(content).toContain('$19.00');
      expect(content).toContain('Spring Salad × 1');
    });

    it('фото страви 40×40, без фото — перша літера назви', () => {
      const { element } = render(LUKA);
      expect(element.querySelector('img.thumb')).toBeNull();
      expect(text(element.querySelector('.thumb--letter')!)).toBe('F');

      const nino = render(NINO).element.querySelector('img.thumb');
      expect(nino?.getAttribute('width')).toBe('40');
      expect(nino?.getAttribute('height')).toBe('40');
    });

    it('згортається: aria-expanded=false, замість страв — «3 dishes · $33.00»', () => {
      const { fixture, element } = render(NINO);
      const toggle = buttonByLabel(element, "Nino's dishes")!;
      expect(toggle.getAttribute('aria-expanded')).toBe('true');

      toggle.click();
      fixture.detectChanges();

      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      expect(text(element)).toContain('3 dishes · $33.00');
      expect(text(element)).not.toContain('Berry Cacao Bowl');
    });
  });

  describe('кошик не підтверджений', () => {
    it('статус текстом «Not submitted yet», чернетка не показується', () => {
      const { element } = render(GIORGI);
      expect(text(element)).toContain('Not submitted yet');
      expect(text(element)).not.toContain('Berry Cacao Bowl');
      expect(buttonByLabel(element, "Giorgi's dishes")).toBeNull();
    });
  });

  describe('Remove Guest', () => {
    it('без canRemove посилання немає', () => {
      expect(buttonByText(render(NINO).element, 'Remove Guest')).toBeUndefined();
    });

    it('aria-label містить видимий текст і ім\'я: «Remove guest Nino»', () => {
      const button = buttonByText(render(NINO, { canRemove: true }).element, 'Remove Guest');
      expect(button?.getAttribute('aria-label')).toBe('Remove guest Nino');
    });

    it('стоїть біля імені — видно і в згорнутому блоці', () => {
      const { fixture, element } = render(NINO, { canRemove: true });
      buttonByLabel(element, "Nino's dishes")?.click();
      fixture.detectChanges();

      expect(element.querySelector('.guest-header')?.contains(buttonByText(element, 'Remove Guest')!)).toBeTrue();
    });

    it('є і в гостя, що ще не підтвердив; клік віддає учасника в removeClick', () => {
      const { element, component } = render(GIORGI, { canRemove: true });
      let removed: Participant | undefined;
      component.removeClick.subscribe((participant) => (removed = participant));

      buttonByText(element, 'Remove Guest')?.click();

      expect(removed?.id).toBe('giorgi');
    });
  });

  describe('редагування (editable)', () => {
    it('без editable — лише «× 2», без кнопок', () => {
      const { element } = render(NINO);
      expect(buttonByLabel(element, 'Increase Berry Cacao Bowl')).toBeNull();
      expect(element.querySelector('button.trash')).toBeNull();
    });

    it('«+» просить кількість на 1 більшу', () => {
      const { element, component } = render(NINO, { editable: true });
      const changes: DishQuantityChange[] = [];
      component.quantityChange.subscribe((change) => changes.push(change));

      buttonByLabel(element, 'Increase Berry Cacao Bowl')?.click();

      expect(changes.map(({ item, quantity }) => [item.dish.id, quantity])).toEqual([['dish-berry', 3]]);
    });

    it('«−» неактивний на 1 — прибрати страву можна лише кошиком', () => {
      const { element, component } = render(NINO, { editable: true });
      const changes: DishQuantityChange[] = [];
      component.quantityChange.subscribe((change) => changes.push(change));

      expect(buttonByLabel(element, 'Decrease Spring Salad')?.disabled).toBeTrue();
      buttonByLabel(element, "Remove Spring Salad from Nino's pre-order")?.click();

      expect(changes.map(({ item, quantity }) => [item.dish.id, quantity])).toEqual([['dish-spring', 0]]);
    });

    it('busy блокує кнопки, поки сервер не відповів', () => {
      const { element } = render(NINO, { editable: true, canRemove: true, busy: true });
      expect(buttonByLabel(element, 'Increase Berry Cacao Bowl')?.disabled).toBeTrue();
      expect(buttonByText(element, 'Remove Guest')?.disabled).toBeTrue();
    });
  });
});
