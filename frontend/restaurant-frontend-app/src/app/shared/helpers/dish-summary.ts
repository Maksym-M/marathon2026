import { DishSummary, DishSummaryLine } from '@app/models/group-orders/dish-summary.model';
import { Participant } from '@app/models/group-orders/participant.model';

/**
 * Every dish of the submitted pre-orders, once, with who ordered how many (US22).
 * Sorted by quantity, highest first, then by name A–Z ignoring case.
 */
export function buildDishSummary(participants: Participant[]): DishSummary {
  const byDish = new Map<string, DishSummaryLine>();

  for (const participant of participants.filter((person) => person.isSubmitted)) {
    for (const item of participant.items) {
      const line = byDish.get(item.dish.id) ?? { dish: item.dish, quantity: 0, total: 0, shares: [] };
      byDish.set(item.dish.id, {
        ...line,
        quantity: line.quantity + item.quantity,
        total: line.total + item.dish.price * item.quantity,
        shares: [...line.shares, { participantId: participant.id, name: participant.name, quantity: item.quantity }],
      });
    }
  }

  const lines = [...byDish.values()].sort(
    (first, second) =>
      second.quantity - first.quantity ||
      first.dish.name.localeCompare(second.dish.name, undefined, { sensitivity: 'base' }),
  );
  return {
    lines,
    quantity: lines.reduce((sum, line) => sum + line.quantity, 0),
    total: lines.reduce((sum, line) => sum + line.total, 0),
  };
}
