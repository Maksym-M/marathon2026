import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DishQuantityChange } from '@app/models/group-orders/dish-quantity-change.model';
import { Participant, ParticipantItem } from '@app/models/group-orders/participant.model';
import { DishThumbComponent } from '../dish-thumb/dish-thumb.component';
import { QuantityStepperComponent } from '../quantity-stepper/quantity-stepper.component';

/**
 * One participant block on the Group Pre-Order page (US21 AC3–AC5, US23 AC2): heading, status,
 * dish lines A–Z, Subtotal. With `editable` the Owner can change quantities and remove dishes.
 */
@Component({
  selector: 'app-participant-panel',
  imports: [CurrencyPipe, DishThumbComponent, QuantityStepperComponent],
  templateUrl: './participant-panel.component.html',
  styleUrl: './participant-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantPanelComponent {
  readonly participant = input.required<Participant>();
  /** "My Pre-Order" for the Owner, "Guest: {name}" for guests. */
  readonly heading = input.required<string>();
  readonly editable = input(false);
  readonly canRemove = input(false);
  readonly busy = input(false);

  readonly quantityChange = output<DishQuantityChange>();
  readonly removeClick = output<Participant>();

  readonly initial = computed(() => this.participant().name.trim().charAt(0).toUpperCase() || '?');
  readonly headingId = computed(() => `participant-${this.participant().id}`);
  readonly removeLabel = computed(() => `Remove guest ${this.participant().name}`);

  /** Only a submitted cart counts (US21 AC5); a draft is never shown. */
  readonly submitted = computed(() => this.participant().isSubmitted && this.participant().items.length > 0);
  readonly statusLabel = computed(() => (this.submitted() ? 'Submitted' : 'Not submitted yet'));

  /** Lines ordered by dish name A–Z, ignoring case (US21 AC5). */
  readonly lines = computed(() =>
    [...this.participant().items].sort((first, second) =>
      first.dish.name.localeCompare(second.dish.name, undefined, { sensitivity: 'base' }),
    ),
  );

  lineTotal(item: ParticipantItem): number {
    return item.dish.price * item.quantity;
  }

  changeQuantity(item: ParticipantItem, quantity: number): void {
    this.quantityChange.emit({ item, quantity });
  }
}
