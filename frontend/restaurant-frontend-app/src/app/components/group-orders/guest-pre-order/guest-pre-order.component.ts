import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DishQuantityChange } from '@app/models/group-orders/dish-quantity-change.model';
import { Participant, ParticipantItem } from '@app/models/group-orders/participant.model';
import { DishThumbComponent } from '../dish-thumb/dish-thumb.component';
import { QuantityStepperComponent } from '../quantity-stepper/quantity-stepper.component';

/** One guest in "Guests' pre-orders": a collapsible block with the dishes of a submitted cart. */
@Component({
  selector: 'app-guest-pre-order',
  imports: [CurrencyPipe, DishThumbComponent, QuantityStepperComponent],
  templateUrl: './guest-pre-order.component.html',
  styleUrl: './guest-pre-order.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GuestPreOrderComponent {
  readonly participant = input.required<Participant>();
  /** The Owner may change quantities — only in a submitted cart. */
  readonly editable = input(false);
  readonly canRemove = input(false);
  readonly busy = input(false);

  readonly quantityChange = output<DishQuantityChange>();
  readonly removeClick = output<Participant>();

  readonly expanded = signal(true);

  readonly dishCount = computed(() => this.participant().items.reduce((sum, item) => sum + item.quantity, 0));
  readonly bodyId = computed(() => `guest-${this.participant().id}-dishes`);
  readonly toggleLabel = computed(() => `${this.participant().name}'s dishes`);
  readonly removeLabel = computed(() => `Remove guest ${this.participant().name}`);

  toggle(): void {
    this.expanded.update((expanded) => !expanded);
  }

  lineTotal(item: ParticipantItem): number {
    return item.dish.price * item.quantity;
  }

  changeQuantity(item: ParticipantItem, quantity: number): void {
    this.quantityChange.emit({ item, quantity });
  }
}
