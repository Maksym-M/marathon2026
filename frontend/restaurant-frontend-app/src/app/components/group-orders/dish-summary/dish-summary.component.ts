import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DishSummary } from '@app/models/group-orders/dish-summary.model';
import { DishThumbComponent } from '../dish-thumb/dish-thumb.component';

/** "Dish Summary" tab: each dish once, most ordered first, with who ordered it. */
@Component({
  selector: 'app-dish-summary',
  imports: [CurrencyPipe, DishThumbComponent],
  templateUrl: './dish-summary.component.html',
  styleUrl: './dish-summary.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DishSummaryComponent {
  readonly summary = input.required<DishSummary>();
  /** The Owner's session id: their share reads "You". */
  readonly meId = input<string | null>(null);
  readonly heading = input('All dishes');

  readonly rows = computed(() =>
    this.summary().lines.map((line) => ({
      ...line,
      sharesLabel: line.shares
        .map((share) => `${share.participantId === this.meId() ? 'You' : share.name} × ${share.quantity}`)
        .join(' · '),
    })),
  );
}
