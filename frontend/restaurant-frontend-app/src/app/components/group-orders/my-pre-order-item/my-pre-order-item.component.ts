import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ParticipantItem } from '@app/models/group-orders/participant.model';
import { DishThumbComponent } from '../dish-thumb/dish-thumb.component';
import { QuantityStepperComponent } from '../quantity-stepper/quantity-stepper.component';

/** One dish of the Owner's own cart ("My Pre-Order"). */
@Component({
  selector: 'app-my-pre-order-item',
  imports: [CurrencyPipe, DishThumbComponent, QuantityStepperComponent],
  templateUrl: './my-pre-order-item.component.html',
  styleUrl: './my-pre-order-item.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class MyPreOrderItemComponent {
  readonly item = input.required<ParticipantItem>();
  readonly editable = input(false);
  readonly busy = input(false);

  readonly quantityChange = output<number>();
  readonly removeClick = output<void>();

  readonly lineTotal = computed(() => this.item().dish.price * this.item().quantity);
}
