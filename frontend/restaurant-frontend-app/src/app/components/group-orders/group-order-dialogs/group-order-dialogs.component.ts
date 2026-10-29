import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { GroupOrderStore } from '../group-order-cart/group-order.store';

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/**
 * Confirmation dialogs for the Owner's group actions (submit, remove guest, remove a guest's dish, remove a seat).
 * Shared by the cart drawer and the Group Pre-Order page; reads the pending action from their GroupOrderStore.
 */
@Component({
  selector: 'app-group-order-dialogs',
  imports: [CurrencyPipe, ModalComponent],
  templateUrl: './group-order-dialogs.component.html',
  styleUrl: './group-order-dialogs.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderDialogsComponent {
  protected readonly store = inject(GroupOrderStore);

  readonly dishCountLabel = computed(() => plural(this.store.dishCount(), 'dish', 'dishes'));
  readonly pendingWarning = computed(() => {
    const pending = this.store.pendingGuests();
    return `${plural(pending, 'guest', 'guests')} ${pending === 1 ? "hasn't" : "haven't"} submitted yet — their dishes won't be included.`;
  });
  readonly seatRemovalText = computed(() => {
    const guests = (this.store.invite()?.MaxGuestCount ?? 1) - 1;
    const lastSeat = this.store.openSeats() === 1;
    return `Your reservation will be for ${plural(guests, 'guest', 'guests')}${
      lastSeat ? ', and nobody else will be able to join with the invite link.' : '.'
    }`;
  });
}
