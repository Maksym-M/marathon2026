import { Participant } from '@app/models/group-orders/participant.model';
import { buildDishSummary } from './dish-summary';

const dish = (id: string, name: string, price: number) => ({ id, name, price });
const APPLE = dish('apple', 'apple tart', 5);
const BOWL = dish('bowl', 'Bowl', 10);
const CAKE = dish('cake', 'Cake', 7.5);

const person = (id: string, isSubmitted: boolean, items: [ReturnType<typeof dish>, number][]): Participant => ({
  id,
  name: id.toUpperCase(),
  type: 'Guest',
  isSubmitted,
  total: 0,
  items: items.map(([item, quantity]) => ({ dish: item, quantity })),
});

describe('buildDishSummary', () => {
  it('returns an empty summary for no participants', () => {
    expect(buildDishSummary([])).toEqual({ lines: [], quantity: 0, total: 0 });
  });

  it('ignores carts that are not submitted', () => {
    expect(buildDishSummary([person('draft', false, [[BOWL, 3]])]).lines).toEqual([]);
  });

  it('merges a dish across participants and keeps who ordered how many', () => {
    const [line] = buildDishSummary([person('ann', true, [[BOWL, 1]]), person('bob', true, [[BOWL, 2]])]).lines;
    expect(line.quantity).toBe(3);
    expect(line.total).toBe(30);
    expect(line.shares).toEqual([
      { participantId: 'ann', name: 'ANN', quantity: 1 },
      { participantId: 'bob', name: 'BOB', quantity: 2 },
    ]);
  });

  const sortCases: { name: string; items: [ReturnType<typeof dish>, number][]; expected: string[] }[] = [
    { name: 'most ordered first', items: [[BOWL, 1], [CAKE, 3]], expected: ['cake', 'bowl'] },
    { name: 'equal quantity → name A–Z', items: [[CAKE, 2], [BOWL, 2]], expected: ['bowl', 'cake'] },
    { name: 'name order ignores case', items: [[BOWL, 1], [APPLE, 1]], expected: ['apple', 'bowl'] },
  ];
  for (const { name, items, expected } of sortCases) {
    it(`sorts: ${name}`, () => {
      expect(buildDishSummary([person('ann', true, items)]).lines.map((line) => line.dish.id)).toEqual(expected);
    });
  }

  it('totals: quantity of all lines and the sum of line totals', () => {
    const summary = buildDishSummary([person('ann', true, [[BOWL, 1], [CAKE, 2]]), person('bob', true, [[CAKE, 1]])]);
    expect(summary.quantity).toBe(4);
    expect(summary.total).toBe(32.5);
  });

  it('does not change the input', () => {
    const participants = [person('ann', true, [[BOWL, 1]]), person('bob', true, [[BOWL, 1]])];
    const before = structuredClone(participants);
    buildDishSummary(participants);
    expect(participants).toEqual(before);
  });
});
