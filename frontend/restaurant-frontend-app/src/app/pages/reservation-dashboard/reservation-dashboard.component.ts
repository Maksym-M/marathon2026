import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  finalize,
  forkJoin,
  map,
  mergeMap,
  Observable,
  of,
  Subject,
  switchMap,
} from 'rxjs';

import { AuthenticationService } from '@app/auth/services/authentication.service';
import { CardReservationComponent } from '@app/components/reservations/card-reservation/card-reservation.component';
import { FeedbackWindowComponent } from '@app/components/reservations/feedback-window/feedback-window.component';
import { ReservationConfirmedComponent } from '@app/components/reservations/reservation-confirmed/reservation-confirmed.component';
import { MakeReservationComponent } from '@app/components/tables/make-reservation/make-reservation.component';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import { FeedbackUpdatedEvent } from '@app/models/feedbacks/feedback.model';
import { Order } from '@app/models/orders/order.model';
import {
  Reservation,
  ReservationCustomer,
  ReservationStatus,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { Table } from '@app/models/tables/table.model';
import { TimeRange } from '@app/models/tables/time-range.model';
import { LocationsService } from '@app/services/locations/locations.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { Roles } from '@app/shared/constants/roles';
import { HeaderComponent } from '@app/shared/custom-components/header/header.component';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { DatePipe } from '@angular/common';
import { isUpcomingReservation, splitReservationsByTab } from '@app/shared/helpers/reservation-tabs';
import {
  formatTime,
  revertFormatDate,
} from '@app/shared/helpers/date-helpers';

interface ReservationStatusView {
  class: string;
  label: string;
}

type ReservationTab = 'upcoming' | 'past';

/** Feedback requests in flight at once: the API's Lambda serves only a few requests in parallel. */
const FEEDBACK_CONCURRENCY = 4;

/** Feedback can be left only once the visit has started, the same rule the card uses for its button. */
const canHaveFeedback = (reservation: ReservationCustomer): boolean =>
  reservation.status === ReservationStatusCustomer.InProgress ||
  reservation.status === ReservationStatusCustomer.Finished;

@Component({
  selector: 'app-reservation-dashboard',
  standalone: true,
  imports: [
    HeaderComponent,
    RouterLink,
    CardReservationComponent,
    MakeReservationComponent,
    ReservationConfirmedComponent,
    FeedbackWindowComponent,
    ModalComponent,
    DatePipe,
  ],
  templateUrl: './reservation-dashboard.component.html',
  styleUrl: './reservation-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservationDashboardComponent implements OnInit {
  private readonly authenticationService =
    inject(AuthenticationService);

  private readonly reservationService =
    inject(ReservationService);

  private readonly tableService = inject(TableService);
  private readonly locationsService = inject(LocationsService);
  private readonly ordersService = inject(PreOrdersService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly feedbackQueue = new Subject<string>();
  /** Loaded with Upcoming and reused for Past, which loads later. */
  private preOrders: Order[] = [];

  readonly Roles = Roles;

  readonly firstName = signal('');
  readonly lastName = signal('');
  readonly role = signal<string>(Roles.CUSTOMER);
  readonly customerId = signal('');

  readonly reservations = signal<ReservationCustomer[]>([]);
  readonly reservationsLoaded = signal(false);
  /** Past & Cancelled isn't loaded with the page: `idle` until its tab opens. */
  readonly pastState = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');

  // US20 AC2–AC3, AC8: "Upcoming" (default) and "Past & Cancelled".
  readonly activeTab = signal<ReservationTab>('upcoming');
  readonly tabs = computed(() => splitReservationsByTab(this.reservations()));
  /** The count is known only once Past & Cancelled has loaded. */
  readonly pastTabLabel = computed(() =>
    this.pastState() === 'ready'
      ? `Past & Cancelled (${this.tabs().past.length})`
      : 'Past & Cancelled',
  );
  /** Cancelling can't be undone, so the card only asks; the dialog confirms. */
  readonly reservationToCancel = signal<ReservationCustomer | null>(null);

  readonly visibleReservations = computed(() =>
    this.activeTab() === 'upcoming' ? this.tabs().upcoming : this.tabs().past,
  );

  readonly selectedReservation =
    signal<ReservationCustomer | null>(null);

  readonly selectedTable = signal<Table | null>(null);
  readonly selectedTimeRange = signal<TimeRange | null>(null);

  readonly showEditReservationModal = signal(false);
  readonly showEditReservationConfirmed = signal(false);
  readonly showFeedbackModal = signal(false);

  readonly statusEnum: Record<number, ReservationStatusView> = {
    [ReservationStatusCustomer.Reserved]: {
      class: 'reserved',
      label: 'Reserved',
    },
    [ReservationStatusCustomer.Cancelled]: {
      class: 'canceled',
      label: 'Cancelled',
    },
    [ReservationStatusCustomer.InProgress]: {
      class: 'in-progress',
      label: 'In Progress',
    },
    [ReservationStatusCustomer.Finished]: {
      class: 'finished',
      label: 'Completed',
    },
  };

  readonly selectedReservationAsReservation =
    computed<Reservation | null>(() => {
      const reservation = this.selectedReservation();

      if (!reservation) {
        return null;
      }

      return {
        id: reservation.id,
        tableId: reservation.tableId,
        startTime: reservation.startTime,
        endTime: reservation.endTime,
        guestCount: reservation.guestCount,
        status:
          reservation.status as unknown as ReservationStatus,
        waiterId: reservation.waiterId ?? '',
        customerId:
          reservation.customerId ?? this.customerId(),
        locationAddress: reservation.locationAddress,
        restaurantName: reservation.restaurantName,
        tableCapacity:
          this.selectedTable()?.capacity ??
          reservation.guestCount,
        hasSubmittedPreOrder:
        reservation.hasSubmittedPreOrder,
        preOrder: reservation.preOrder,
        preOrders: reservation.preOrders,
      };
    });

  ngOnInit(): void {
    const token = this.authenticationService.getToken();
    const user = this.authenticationService.getUser();

    if (!token || !user) {
      this.reservationsLoaded.set(true);
      void this.router.navigate(['/login']);
      return;
    }

    if (user.role === Roles.WAITER) {
      void this.router.navigate(['/waiter-reservations']);
      return;
    }

    if (user.role !== Roles.CUSTOMER) {
      void this.router.navigate(['/main']);
      return;
    }

    this.firstName.set(user.firstName ?? '');
    this.lastName.set(user.lastName ?? '');
    this.role.set(user.role);
    this.customerId.set(user.id);

    this.startFeedbackQueue();
    this.loadReservations();
  }

  /** Past & Cancelled loads the first time its tab opens (and again after a failed try). */
  selectTab(tab: ReservationTab): void {
    this.activeTab.set(tab);

    const pastState = this.pastState();
    if (tab === 'past' && (pastState === 'idle' || pastState === 'error')) {
      this.loadPast();
    }
  }

  switchTab(): void {
    this.selectTab(this.activeTab() === 'upcoming' ? 'past' : 'upcoming');
    document.getElementById(`reservations-tab-${this.activeTab()}`)?.focus();
  }

  retryPast(): void {
    this.loadPast();
  }

  askToCancel(id: string): void {
    this.reservationToCancel.set(this.reservations().find((reservation) => reservation.id === id) ?? null);
  }

  confirmCancel(id: string): void {
    this.reservationToCancel.set(null);
    this.cancelReservation(id);
  }

  cancelReservation(id: string): void {
    this.reservationService
      .cancelReservation$(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          if (response.error) {
            console.error(
              'Failed to cancel reservation:',
              response.error,
            );
            return;
          }

          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id === id
                ? {
                  ...reservation,
                  status:
                  ReservationStatusCustomer.Cancelled,
                }
                : reservation,
            ),
          );

          const selected = this.selectedReservation();

          if (selected?.id === id) {
            this.selectedReservation.set({
              ...selected,
              status: ReservationStatusCustomer.Cancelled,
            });
          }
        },
        error: (error) => {
          console.error(
            'Failed to cancel reservation:',
            error,
          );
        },
      });
  }

  openEditModal(reservation: ReservationCustomer): void {
    this.selectedReservation.set(reservation);

    const startTime = new Date(reservation.startTime);
    const date = revertFormatDate(startTime.toISOString());
    const time = formatTime(startTime.toISOString());

    this.locationsService
      .getLocations()
      .pipe(
        map((locations) =>
          locations.find(
            (location) =>
              location.address.trim().toLowerCase() ===
              reservation.locationAddress
                .trim()
                .toLowerCase(),
          ),
        ),
        switchMap((location) => {
          if (!location) {
            console.error(
              'Location not found:',
              reservation.locationAddress,
            );

            return of(null);
          }

          return forkJoin({
            timeRanges: this.tableService.getTableTimeSlots(
              reservation.tableId,
              date,
              time,
            ),
            tables:
              this.tableService.getAvailableTablesWithoutStartTime$(
                {
                  location: location.id,
                  date: null,
                  time: null,
                  guests: reservation.guestCount,
                },
              ),
          }).pipe(
            map(({ timeRanges, tables }) => ({
              locationId: location.id,
              timeRanges,
              tables,
            })),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => {
          if (!result) {
            return;
          }

          const matchingTable = result.tables.find(
            (table) => table.id === reservation.tableId,
          );

          this.selectedTable.set({
            id: reservation.tableId,
            tableNumber:
              matchingTable?.tableNumber ??
              this.extractTableNumber(
                reservation.tableName,
              ),
            capacity:
              matchingTable?.capacity ??
              reservation.guestCount,
            locationId:
              matchingTable?.locationId ??
              result.locationId,
            timeRanges: result.timeRanges ?? [],
          });

          this.selectedTimeRange.set({
            start: new Date(
              reservation.startTime,
            ).toISOString(),
            end: new Date(reservation.endTime).toISOString(),
          });

          this.showEditReservationModal.set(true);
        },
        error: (error) => {
          console.error(
            'Failed to load reservation editing data:',
            error,
          );
        },
      });
  }

  closeEditReservationModal(): void {
    this.showEditReservationModal.set(false);
  }

  onSubmitEditReservation(
    timeRange: TimeRange,
    guests: number,
  ): void {
    const currentReservation = this.selectedReservation();

    if (!currentReservation) {
      return;
    }

    this.reservationService
      .editReservation$(
        currentReservation.id,
        timeRange,
        guests,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updatedReservation) => {
          const updatedCustomerReservation: ReservationCustomer =
            {
              ...currentReservation,
              ...updatedReservation,
              status:
                updatedReservation.status as unknown as ReservationStatusCustomer,
              restaurantName:
                updatedReservation.restaurantName ??
                currentReservation.restaurantName ??
                '',
              feedback: currentReservation.feedback,
              preOrder: currentReservation.preOrder,
              preOrders: currentReservation.preOrders,
              createdByName:
              currentReservation.createdByName,
              createdByRole:
              currentReservation.createdByRole,
            };

          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id ===
              updatedCustomerReservation.id
                ? updatedCustomerReservation
                : reservation,
            ),
          );

          this.selectedReservation.set(
            updatedCustomerReservation,
          );

          this.selectedTimeRange.set({
            start: new Date(
              updatedCustomerReservation.startTime,
            ).toISOString(),
            end: new Date(
              updatedCustomerReservation.endTime,
            ).toISOString(),
          });

          this.showEditReservationModal.set(false);
          this.showEditReservationConfirmed.set(true);
        },
        error: (error) => {
          console.error(
            'Failed to update reservation:',
            error,
          );
        },
      });
  }

  closeReservationConfirmed(): void {
    this.showEditReservationConfirmed.set(false);
  }

  editConfirmedReservation(): void {
    const reservation = this.selectedReservation();

    if (!reservation) {
      return;
    }

    this.showEditReservationConfirmed.set(false);
    this.openEditModal(reservation);
  }

  cancelConfirmedReservation(): void {
    const reservation = this.selectedReservation();

    if (!reservation) {
      return;
    }

    this.cancelReservation(reservation.id);
    this.showEditReservationConfirmed.set(false);
  }

  openFeedback(reservation: ReservationCustomer): void {
    this.selectedReservation.set(reservation);
    this.showFeedbackModal.set(true);
  }

  closeFeedback(): void {
    this.showFeedbackModal.set(false);
  }

  onFeedbackUpdated(event: FeedbackUpdatedEvent): void {
    this.reservationService
      .getReservationFeedback(event.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (feedback) => {
          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id === event.reservationId
                ? {
                  ...reservation,
                  feedback: feedback ?? undefined,
                }
                : reservation,
            ),
          );

          const selected = this.selectedReservation();

          if (selected?.id === event.reservationId) {
            this.selectedReservation.set({
              ...selected,
              feedback: feedback ?? undefined,
            });
          }
        },
        error: (error) => {
          console.error(
            'Failed to refresh reservation feedback:',
            error,
          );
        },
      });
  }

  /**
   * Only the Upcoming tab loads at first (the API builds each reservation with several lookups); Past loads
   * when its tab opens. Feedback is filled in afterwards, a few requests at a time.
   */
  private loadReservations(): void {
    this.reservationsLoaded.set(false);

    forkJoin({
      reservations: this.reservationService.getReservationsForUser$({
        fresh: true,
        scope: 'upcoming',
      }),
      orders: this.ordersService.getPreOrders().pipe(
        catchError((error) => {
          console.error('Failed to load pre-orders:', error);
          return of([]);
        }),
      ),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ reservations, orders }) => {
          this.preOrders = orders;
          const loaded = reservations.map((reservation) =>
            this.toCustomerReservation(reservation),
          );
          this.reservations.set(loaded);
          this.queueFeedback(loaded);

          // A backend without scopes returns every reservation: the Past tab is already here.
          if (loaded.some((reservation) => !isUpcomingReservation(reservation))) {
            this.pastState.set('ready');
          }

          // Nothing upcoming: Past decides between the tabs and "No Reservations".
          if (loaded.length === 0) {
            this.loadPast();
            return;
          }

          this.reservationsLoaded.set(true);
        },
        error: (error) => {
          console.error(
            'Failed to load reservations:',
            error,
          );
          this.reservations.set([]);
          this.reservationsLoaded.set(true);
        },
      });
  }

  private loadPast(): void {
    this.pastState.set('loading');

    this.reservationService
      .getReservationsForUser$({ fresh: true, scope: 'past' })
      .pipe(
        finalize(() => this.reservationsLoaded.set(true)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (reservations) => {
          const past = reservations
            .map((reservation) => this.toCustomerReservation(reservation))
            .filter((reservation) => !isUpcomingReservation(reservation));
          const pastIds = new Set(past.map((reservation) => reservation.id));

          // The server's copy wins, e.g. over a reservation cancelled on this page a moment ago.
          this.reservations.update((current) => [
            ...current.filter((reservation) => !pastIds.has(reservation.id)),
            ...past,
          ]);
          this.pastState.set('ready');
          this.queueFeedback(past);
        },
        error: (error) => {
          console.error('Failed to load past reservations:', error);
          this.pastState.set('error');
        },
      });
  }

  private toCustomerReservation(reservation: Reservation): ReservationCustomer {
    const preOrder = this.preOrders.find(
      (order) => order.reservationId === reservation.id,
    );

    return {
      ...reservation,
      status: reservation.status as unknown as ReservationStatusCustomer,
      restaurantName: reservation.restaurantName ?? '',
      feedback: undefined,
      preOrder,
      hasSubmittedPreOrder:
        preOrder != null && (preOrder.status === 1 || preOrder.status === 3),
    };
  }

  private queueFeedback(reservations: ReservationCustomer[]): void {
    reservations
      .filter(canHaveFeedback)
      .forEach((reservation) => this.feedbackQueue.next(reservation.id));
  }

  /** One queue for both tabs, so at most `FEEDBACK_CONCURRENCY` feedback requests run at any time. */
  private startFeedbackQueue(): void {
    this.feedbackQueue
      .pipe(
        mergeMap(
          (reservationId) =>
            this.loadFeedback$(reservationId).pipe(
              map((feedback) => ({ reservationId, feedback })),
            ),
          FEEDBACK_CONCURRENCY,
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ reservationId, feedback }) => {
        if (!feedback) {
          return;
        }

        this.reservations.update((reservations) =>
          reservations.map((reservation) =>
            reservation.id === reservationId ? { ...reservation, feedback } : reservation,
          ),
        );
      });
  }

  /** null when there is no feedback yet (404) or it couldn't be loaded: the card then offers to leave one. */
  private loadFeedback$(
    reservationId: string,
  ): Observable<FeedbackReservationResponseItem | null> {
    return this.reservationService.getReservationFeedback(reservationId).pipe(
      map((feedback) =>
        feedback?.service != null || feedback?.cuisineExperience != null
          ? feedback
          : null,
      ),
      catchError((error: HttpErrorResponse) => {
        if (error.status !== 404) {
          console.error(
            `Failed to load feedback for reservation ${reservationId}: ${error.status} ${error.message}`,
          );
        }

        return of(null);
      }),
    );
  }

  private extractTableNumber(
    tableName: string | undefined,
  ): number {
    const match = tableName?.match(/\d+/);
    return match ? Number(match[0]) : 0;
  }
}
