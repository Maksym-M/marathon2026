import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DishQuantityChange } from '@app/models/group-orders/dish-quantity-change.model';
import { Participant, ParticipantItem } from '@app/models/group-orders/participant.model';
import { DishSummaryComponent } from '../dish-summary/dish-summary.component';
import { GroupOrderDialogsComponent } from '../group-order-dialogs/group-order-dialogs.component';
import { GuestPreOrderComponent } from '../guest-pre-order/guest-pre-order.component';
import { MyPreOrderItemComponent } from '../my-pre-order-item/my-pre-order-item.component';
import { GroupOrderStore } from './group-order.store';
import { CartTab } from './models';

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** Owner's cart for a group reservation: own dishes, guests' pre-orders, Dish Summary and Submit. */
@Component({
  selector: 'app-group-order-cart',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    MyPreOrderItemComponent,
    GuestPreOrderComponent,
    DishSummaryComponent,
    GroupOrderDialogsComponent,
  ],
  templateUrl: './group-order-cart.component.html',
  styleUrl: './group-order-cart.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [GroupOrderStore],
  host: { '(document:keydown.escape)': 'onEscape()' },
})
export class GroupOrderCartComponent {
  protected readonly store = inject(GroupOrderStore);

  readonly reservationId = input.required<string>();
  readonly closed = output<void>();
  /** "Browse Menu": the host opens the menu in pre-order mode; emits the reservation label for its banner. */
  readonly browseMenu = output<string>();

  readonly activeTab = signal<CartTab>('participants');

  /** Once the group is sent to the restaurant nothing is left in the cart, so it shows as empty. */
  readonly view = computed(() =>
    this.store.loadState() === 'ready' && this.store.status() === 'Submitted' ? 'submitted' : this.store.loadState(),
  );

  /** For the host page: the menu behind the cart shows which reservation is being pre-ordered. */
  readonly reservationLabel = computed(() => this.store.invite()?.LocationAddress ?? '');

  readonly seatsLabel = computed(() => `${plural(this.store.openSeats(), 'seat', 'seats')} not joined yet`);
  readonly totalHint = computed(() =>
    this.store.includesGuestDishes()
      ? "Includes guests' pre-orders · Excludes taxes and service charges."
      : "Excludes taxes and service charges. Guests' pre-orders are added here when they submit.",
  );

  constructor() {
    effect(() => {
      const reservationId = this.reservationId();
      untracked(() => this.store.load(reservationId));
    });
  }

  selectTab(tab: CartTab): void {
    this.activeTab.set(tab);
  }

  switchTab(): void {
    this.activeTab.update((tab) => (tab === 'participants' ? 'summary' : 'participants'));
    document.getElementById(`group-cart-tab-${this.activeTab()}`)?.focus();
  }

  close(): void {
    this.closed.emit();
  }

  openMenu(): void {
    this.browseMenu.emit(this.reservationLabel());
  }

  onEscape(): void {
    // The open dialog closes itself on Escape; the cart closes only when no dialog is open.
    if (!this.store.pendingAction()) this.close();
  }

  changeMyQuantity(me: Participant, item: ParticipantItem, quantity: number): void {
    this.store.updateQuantity(me, item.dish.id, quantity);
  }

  changeGuestQuantity(guest: Participant, { item, quantity }: DishQuantityChange): void {
    // The Owner can't add a dish back to a guest's cart, so removing one is confirmed first.
    if (quantity === 0) this.store.ask({ kind: 'removeDish', guest, item });
    else this.store.updateQuantity(guest, item.dish.id, quantity);
  }
}
