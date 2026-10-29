import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { DishSummaryComponent } from '@app/components/group-orders/dish-summary/dish-summary.component';
import { GroupOrderStore } from '@app/components/group-orders/group-order-cart/group-order.store';
import { GroupOrderDialogsComponent } from '@app/components/group-orders/group-order-dialogs/group-order-dialogs.component';
import { ParticipantPanelComponent } from '@app/components/group-orders/participant-panel/participant-panel.component';
import { DishQuantityChange } from '@app/models/group-orders/dish-quantity-change.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { GroupOrderPageView } from './models';

/**
 * /reservations/:reservationId/group-order — the Owner's "Group Pre-Order" screen (US21–US23):
 * who joined and submitted, everyone's dishes, Dish Summary and Grand Total. While the group is open
 * the Owner edits dishes, removes guests and empty seats, and sends the order to the restaurant.
 */
@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, ParticipantPanelComponent, DishSummaryComponent, GroupOrderDialogsComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [GroupOrderStore],
})
export class GroupOrderPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupOrdersService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly store = inject(GroupOrderStore);

  readonly reservationId = toSignal(this.route.paramMap.pipe(map((params) => params.get('reservationId') ?? '')), {
    initialValue: this.route.snapshot.paramMap.get('reservationId') ?? '',
  });

  private readonly reservation = signal<ReservationCustomer | null>(null);
  private readonly reservationState = signal<'loading' | 'ready' | 'not-found' | 'error'>('loading');

  /** Access and the reservation state are checked first (US21 AC9–AC10), then the group data. */
  readonly view = computed<GroupOrderPageView>(() => {
    const reservationState = this.reservationState();
    if (reservationState !== 'ready') return reservationState;
    const reservation = this.reservation()!;
    if (reservation.status === ReservationStatusCustomer.Cancelled) return 'cancelled';
    if (reservation.status !== ReservationStatusCustomer.Reserved) return 'passed';
    if (reservation.guestCount < 2) return 'single';
    return this.store.loadState();
  });

  readonly statusBadge = computed(() => (this.store.isOpen() ? 'Collecting pre-orders' : 'Sent to Restaurant'));

  /** Progress (US23 AC3): every seat counts, the Owner included; the Owner counts once they have dishes. */
  readonly seats = computed(() => Math.max(this.store.invite()?.MaxGuestCount ?? 1, this.store.participants().length));
  readonly submittedCount = computed(() => (this.store.me()?.items.length ? 1 : 0) + this.store.submittedGuests());
  readonly progressPercent = computed(() => Math.round((this.submittedCount() / this.seats()) * 100));
  /** One "Not joined" placeholder per seat nobody has taken (US23 AC1). */
  readonly openSeatSlots = computed(() => Array.from({ length: this.store.openSeats() }, (_, index) => index));

  /** Back to the menu in pre-order mode for this reservation; the header Cart then opens the group cart. */
  readonly menuQueryParams = computed<Params>(() => {
    const invite = this.store.invite();
    return {
      preOrder: 'true',
      group: 'true',
      reservationId: this.reservationId(),
      reservationLabel: invite ? `${invite.LocationAddress}, Table ${invite.TableNumber}` : null,
    };
  });

  constructor() {
    effect(() => {
      const reservationId = this.reservationId();
      untracked(() => this.loadReservation(reservationId));
    });
  }

  retry(): void {
    if (this.reservationState() !== 'ready') this.loadReservation(this.reservationId());
    else this.store.reload();
  }

  guestHeading(guest: Participant): string {
    return `Guest: ${guest.name}`;
  }

  changeQuantity(participant: Participant, { item, quantity }: DishQuantityChange): void {
    // The Owner can't add a dish back to a guest's cart, so removing one is confirmed first.
    if (quantity === 0 && participant !== this.store.me()) this.store.ask({ kind: 'removeDish', guest: participant, item });
    else this.store.updateQuantity(participant, item.dish.id, quantity);
  }

  private loadReservation(reservationId: string): void {
    this.reservationState.set('loading');
    this.service
      .getReservation$(reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (reservation) => {
          this.reservation.set(reservation);
          this.reservationState.set('ready');
          if (reservation.status === ReservationStatusCustomer.Reserved && reservation.guestCount >= 2) {
            this.store.load(reservationId);
          }
        },
        error: (error: unknown) => {
          const missing = error instanceof HttpErrorResponse && [400, 403, 404].includes(error.status);
          this.reservationState.set(missing ? 'not-found' : 'error');
        },
      });
  }
}
