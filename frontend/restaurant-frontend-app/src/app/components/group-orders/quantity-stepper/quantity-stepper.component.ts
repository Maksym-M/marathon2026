import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** − / quantity / +. Stateless: the quantity comes from the server, the stepper only asks for a new one. */
@Component({
  selector: 'app-quantity-stepper',
  templateUrl: './quantity-stepper.component.html',
  styleUrl: './quantity-stepper.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  host: { '[class.compact]': 'compact()' },
})
export class QuantityStepperComponent {
  readonly quantity = input.required<number>();
  /** Dish name, so screen readers hear "Decrease Spring Salad", not just "Decrease". */
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly compact = input(false);

  readonly quantityChange = output<number>();

  decrease(): void {
    this.quantityChange.emit(this.quantity() - 1);
  }

  increase(): void {
    this.quantityChange.emit(this.quantity() + 1);
  }
}
