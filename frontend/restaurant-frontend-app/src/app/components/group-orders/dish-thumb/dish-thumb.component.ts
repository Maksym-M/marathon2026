import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DishShort } from '@app/models/group-orders/participant.model';

/** Round dish photo; a dish without a photo shows the first letter of its name. */
@Component({
  selector: 'app-dish-thumb',
  templateUrl: './dish-thumb.component.html',
  styleUrl: './dish-thumb.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DishThumbComponent {
  readonly dish = input.required<DishShort>();
  readonly size = input(40);

  readonly initial = computed(() => this.dish().name.trim().charAt(0).toUpperCase());
}
