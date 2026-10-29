import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import {
  ReservationCustomer,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { PlaceOrderWaiterComponent } from '@app/components/place-order-waiter/place-order-waiter.component';
import { GuestInviteModalComponent } from '@app/components/reservations/guest-invite-modal/guest-invite-modal.component';
import { Roles } from '@app/shared/constants/roles';
import { CustomSelectComponent } from '@app/shared/custom-components/custom-select/custom-select.component';
import { buildPreOrderMenuParams } from '@app/shared/helpers/pre-order-menu-params';

interface ReservationStatusView {
  class: string;
  label: string;
}

interface WaiterPreOrderNavigationData {
  reservationId: string;
  createdByName: string;
  createdByRole: string;
  tableName: string;
}

@Component({
  selector: 'app-card-reservation',
  standalone: true,
  imports: [
    DatePipe,
    CustomSelectComponent,
    PlaceOrderWaiterComponent,
    GuestInviteModalComponent,
  ],
  templateUrl: './card-reservation.component.html',
  styleUrl: './card-reservation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The host is the list item: both dashboards render the cards inside a <ul>.
  host: { role: 'listitem', '[class.row-layout]': "layout() === 'row'" },
})
export class CardReservationComponent {
  private readonly router = inject(Router);
  readonly Roles = Roles;
  readonly ReservationStatusCustomer = ReservationStatusCustomer;

  readonly r = input.required<ReservationCustomer>();
  readonly role = input<string>(Roles.CUSTOMER);
  /** "row" — one reservation per row with a date block (Customer's Reservations page); "grid" — the waiter's cards. */
  readonly layout = input<'grid' | 'row'>('grid');
  readonly statusEnum =
    input<Record<number, ReservationStatusView>>({});

  readonly availableTableOptions = input<string[]>([]);

  readonly availableTableOptionsMap =
    input<Map<string, string>>(new Map<string, string>());

  readonly selectedTableLabel = input('');

  readonly createdByName = input<string | undefined>();

  readonly createdByRole =
    input<ReservationCustomer['createdByRole']>();
  readonly tableChanged = output<{
    reservationId: string;
    newTableId: string;
  }>();

  readonly cancelReservation = output<string>();

  readonly editReservation =
    output<ReservationCustomer>();

  readonly postponeReservation =
    output<ReservationCustomer>();

  readonly leaveFeedbackReservation =
    output<ReservationCustomer>();

  readonly updateFeedbackReservation =
    output<ReservationCustomer>();

  readonly sendAnonymousFeedback =
    output<ReservationCustomer>();

  readonly statusChanged = output<{
    id: string;
    newStatus: ReservationStatusCustomer;
  }>();

  readonly preOrder = output<CreateOrderRequest>();
  readonly showWaiterOrderModal = signal(false);
  readonly showInviteModal = signal(false);

  // US20 AC5–AC7: pre-order actions and the group pre-order line of the Owner's card.
  readonly isGroup = computed(() => this.r().guestCount >= 2);
  readonly groupPreOrder = computed(() => (this.isGroup() ? (this.r().groupPreOrder ?? null) : null));
  /** A cancelled visit has nothing left to pre-order, so it shows no counter; a completed one keeps it (AC7). */
  readonly preOrdersCounter = computed(() => {
    const group = this.groupPreOrder();
    if (!group || this.r().status === ReservationStatusCustomer.Cancelled) return null;
    return `Pre-orders: ${group.submitted}/${group.seats}`;
  });
  /** From 30 minutes before the start no pre-order changes are possible (US23 AC7). */
  readonly cutOffPassed = computed(() => Date.now() > new Date(this.r().startTime).getTime() - 30 * 60 * 1000);
  readonly preOrderSent = computed(() => this.groupPreOrder()?.status === 'Submitted');
  readonly canAddPreOrder = computed(() => !this.cutOffPassed() && !this.preOrderSent());
  readonly progressPercent = computed(() => {
    const group = this.groupPreOrder();
    return group ? Math.round((group.submitted / Math.max(group.seats, 1)) * 100) : 0;
  });
  readonly guestsLabel = computed(() => `${this.r().guestCount} ${this.r().guestCount === 1 ? 'guest' : 'guests'}`);
  /** Edit and cancel only while the reservation is upcoming and before the 30-minute cut-off (US20). */
  readonly canManageReservation = computed(
    () => this.r().status === ReservationStatusCustomer.Reserved && !this.cutOffPassed(),
  );
  /** Row layout: the one main next step — add dishes, or look at the group pre-order once it's sent. */
  readonly primaryAction = computed<'add' | 'details' | null>(() => {
    if (this.r().status !== ReservationStatusCustomer.Reserved) return null;
    if (this.canAddPreOrder()) return 'add';
    return this.isGroup() ? 'details' : null;
  });
  readonly needsFeedback = computed(
    () =>
      this.r().status === ReservationStatusCustomer.InProgress || this.r().status === ReservationStatusCustomer.Finished,
  );
  /** Row layout: the right-hand action column is left out when there is nothing to do (e.g. a cancelled visit). */
  readonly hasRowActions = computed(() => this.primaryAction() !== null || this.needsFeedback());
  /** The invite hint and button disappear once every seat is taken or after the cut-off. */
  readonly canInviteGuests = computed(() => {
    const group = this.groupPreOrder();
    return (
      this.r().status === ReservationStatusCustomer.Reserved &&
      !this.cutOffPassed() &&
      group?.status === 'Open' &&
      group.joined < this.r().guestCount
    );
  });

  readonly statusView = computed<ReservationStatusView>(() => {
    const reservation = this.r();

    return (
      this.statusEnum()[reservation.status] ?? {
        class: '',
        label: 'Unknown',
      }
    );
  });

  readonly displayedTableLabel = computed(() => {
    return this.selectedTableLabel() || 'Table (Unknown)';
  });

  readonly creatorName = computed(() => {
    return (
      this.createdByName() ||
      this.r().createdByName ||
      'Unknown'
    );
  });

  readonly creatorRole = computed(() => {
    return (
      this.createdByRole() ||
      this.r().createdByRole ||
      Roles.CUSTOMER
    );
  });

  readonly preOrderItemCount = computed(() => {
    return this.r().preOrder?.orderItems?.length ?? 0;
  });

  readonly canOpenWaiterPreOrder = computed(() => {
    return (
      this.preOrderItemCount() > 0 ||
      this.creatorRole() === Roles.WAITER
    );
  });

  readonly isAnonymous = computed(() => {
    return !this.r().customerId;
  });

  readonly hasFullFeedback = computed(() => {
    const feedback = this.r().feedback;

    return (
      feedback?.cuisineExperience != null &&
      feedback?.service != null
    );
  });

  onPreOrder(): void {
    void this.router.navigate(['/menu'], {
      queryParams: buildPreOrderMenuParams(this.r()),
    });
  }
  /** US21: the Owner's Group Pre-Order page for this reservation. */
  openGroupPreOrder(): void {
    void this.router.navigate(['/reservations', this.r().id, 'group-order']);
  }

  onPreOrderWaiter(): void {
    if (this.preOrderItemCount() > 0) {
      this.navigateToWaiterPreOrder();
      return;
    }

    if (this.creatorRole() === Roles.WAITER) {
      this.showWaiterOrderModal.set(true);
    }
  }

  closeWaiterOrderModal(): void {
    this.showWaiterOrderModal.set(false);
  }

  onWaiterOrderCreated(order: CreateOrderRequest): void {
    this.showWaiterOrderModal.set(false);
    this.preOrder.emit(order);
  }

  onTableChange(label: string): void {
    const reservation = this.r();
    const newTableId =
      this.availableTableOptionsMap().get(label);

    if (!newTableId || newTableId === reservation.tableId) {
      return;
    }

    this.tableChanged.emit({
      reservationId: reservation.id,
      newTableId,
    });
  }

  onStatusChange(
    newStatus: ReservationStatusCustomer,
  ): void {
    this.statusChanged.emit({
      id: this.r().id,
      newStatus,
    });
  }

  private navigateToWaiterPreOrder(): void {
    const reservation = this.r();

    const navigationData: WaiterPreOrderNavigationData = {
      reservationId: reservation.id,
      createdByName: this.creatorName(),
      createdByRole: this.creatorRole(),
      tableName: reservation.tableName || 'Unknown table',
    };

    sessionStorage.setItem(
      'waiterPreOrderData',
      JSON.stringify(navigationData),
    );

    void this.router.navigate(['/waiter-pre-order'], {
      state: navigationData,
    });
  }
}
