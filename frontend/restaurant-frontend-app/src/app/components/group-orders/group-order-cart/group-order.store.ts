import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, exhaustMap, filter, forkJoin, map, Observable, Subscription, switchMap, tap, timer } from 'rxjs';
import { GroupInviteDetails } from '@app/models/group-orders/group-invite-details.model';
import { GroupOrderAction } from '@app/models/group-orders/group-order-action.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { buildDishSummary } from '@app/shared/helpers/dish-summary';

export const GROUP_ORDER_SYNC_MS = 15_000;
/** Pre-order cut-off (US23 AC7): no changes from 30 minutes before the reservation starts. */
export const PRE_ORDER_CUTOFF_MS = 30 * 60 * 1000;
/** Busy key for seat changes — never a session id, those are GUIDs or `owner-…`. */
const SEATS_BUSY_ID = 'seats';

export type GroupOrderLoadState = 'loading' | 'ready' | 'not-found' | 'error';
export type GroupOrderRefreshState = 'idle' | 'refreshing' | 'failed';

/**
 * State of one group pre-order for its Owner. Provided by the screen that shows it (the cart drawer or the
 * Group Pre-Order page), so it lives as long as that screen.
 */
@Injectable()
export class GroupOrderStore {
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly reservationId = signal('');
  private readonly details = signal<GroupInviteDetails | null>(null);
  private readonly ownerSessionId = signal<string | null>(null);
  private readonly busyIds = signal<ReadonlySet<string>>(new Set());

  readonly participants = signal<Participant[]>([]);
  readonly status = signal<GroupReservationStatus>('Open');
  readonly loadState = signal<GroupOrderLoadState>('loading');
  readonly refreshState = signal<GroupOrderRefreshState>('idle');
  readonly updatedAt = signal<Date | null>(null);
  readonly submitting = signal(false);
  /** The action waiting for confirmation in a dialog, and whether its request is running. */
  readonly pendingAction = signal<GroupOrderAction | null>(null);
  readonly actionRunning = signal(false);

  readonly invite = computed(() => this.details()?.invite ?? null);
  readonly inviteLink = computed(() => {
    const token = this.details()?.token;
    return token ? `${window.location.origin}/join/${token}` : null;
  });

  readonly me = computed(() => {
    const ownerId = this.ownerSessionId();
    const participants = this.participants();
    return participants.find((person) => person.id === ownerId) ?? participants.find((person) => person.type === 'Owner') ?? null;
  });
  readonly guests = computed(() => this.participants().filter((person) => person !== this.me()));

  /** Only submitted carts go to the restaurant, so the Grand Total is NOT the `total` from the API. */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly grandTotal = computed(() => this.consolidated().total);
  readonly dishCount = computed(() => this.consolidated().items.reduce((sum, item) => sum + item.quantity, 0));
  readonly dishSummary = computed(() => buildDishSummary(this.participants()));

  readonly isOpen = computed(() => this.status() === 'Open');
  /** Re-checked on every load, refresh and edit (they all set `updatedAt`). */
  readonly isLocked = computed(() => {
    this.updatedAt();
    const start = this.invite()?.StartTime;
    return !!start && Date.now() > new Date(start).getTime() - PRE_ORDER_CUTOFF_MS;
  });
  /** Changes are possible while the group collects pre-orders and the cut-off hasn't passed. */
  readonly canEdit = computed(() => this.isOpen() && !this.isLocked());
  /** Seats for guests: the guests count includes the Owner. */
  readonly guestSeats = computed(() => Math.max((this.invite()?.MaxGuestCount ?? 1) - 1, this.guests().length));
  readonly openSeats = computed(() => this.guestSeats() - this.guests().length);
  readonly submittedGuests = computed(() => this.guests().filter((guest) => guest.isSubmitted).length);
  readonly pendingGuests = computed(() => this.guests().filter((guest) => !guest.isSubmitted).length);
  readonly includesGuestDishes = computed(() => this.guests().some((guest) => guest.isSubmitted && guest.items.length > 0));
  readonly canSubmit = computed(() => this.canEdit() && this.consolidated().items.length > 0 && !this.submitting());

  private readonly groupId = computed(() => this.invite()?.GroupReservationId ?? '');
  private loadSubscription?: Subscription;
  /** Bumped by every edit, so a background refresh that started before it can't bring the old cart back. */
  private mutationVersion = 0;

  constructor() {
    timer(GROUP_ORDER_SYNC_MS, GROUP_ORDER_SYNC_MS)
      .pipe(
        filter(() => this.loadState() === 'ready' && this.isOpen() && this.busyIds().size === 0),
        exhaustMap(() => this.refresh$()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  load(reservationId: string): void {
    this.reservationId.set(reservationId);
    this.loadState.set('loading');
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = this.service
      .getGroupInvite$(reservationId)
      .pipe(
        switchMap((details) =>
          forkJoin({
            sessions: this.service.getSessions$(details.invite.GroupReservationId),
            ownerSessionId: this.service.getOwnerSessionId$(reservationId),
            status: this.service.getStatus$(details.invite.GroupReservationId),
          }).pipe(map((data) => ({ details, ...data }))),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ details, sessions, ownerSessionId, status }) => {
          this.details.set(details);
          this.ownerSessionId.set(ownerSessionId);
          this.applySnapshot(sessions);
          this.status.set(status.status);
          this.refreshState.set('idle');
          this.loadState.set('ready');
        },
        error: (error: unknown) =>
          this.loadState.set(error instanceof HttpErrorResponse && error.status === 404 ? 'not-found' : 'error'),
      });
  }

  reload(): void {
    this.load(this.reservationId());
  }

  refresh(): void {
    if (this.loadState() !== 'ready' || this.refreshState() === 'refreshing') return;
    this.refresh$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  isBusy(sessionId: string): boolean {
    return this.busyIds().has(sessionId);
  }

  /** The Owner edits their own dishes and submitted guest carts; a guest who is still choosing owns their cart. */
  canEditParticipant(participant: Participant): boolean {
    return this.canEdit() && (participant === this.me() || participant.isSubmitted);
  }

  async copyInviteLink(): Promise<void> {
    const link = this.inviteLink();
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link);
      this.toast.success('Invite link copied');
    } catch {
      this.toast.error(`Couldn't copy the link. Share it manually: ${link}`);
    }
  }

  ask(action: GroupOrderAction): void {
    if (!this.actionRunning()) this.pendingAction.set(action);
  }

  cancelAction(): void {
    if (!this.actionRunning()) this.pendingAction.set(null);
  }

  confirmAction(): void {
    const action = this.pendingAction();
    if (!action || this.actionRunning()) return;
    this.actionRunning.set(true);
    const settled = () => {
      this.actionRunning.set(false);
      this.pendingAction.set(null);
    };
    switch (action.kind) {
      case 'submit':
        return this.submit(settled);
      case 'removeGuest':
        return this.removeGuest(action.guest, settled);
      case 'removeDish':
        return this.updateQuantity(action.guest, action.item.dish.id, 0, settled);
      case 'removeSeat':
        return this.removeSeat(settled);
    }
  }

  submit(onSettled: () => void): void {
    if (!this.canSubmit()) return onSettled();
    this.submitting.set(true);
    this.service
      .submit$(this.groupId(), this.reservationId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.status.set('Submitted');
          this.toast.success('Pre-order sent to the restaurant');
          onSettled();
        },
        // The error toast comes from tokenInterceptor; the button stays available for another try.
        error: () => {
          this.submitting.set(false);
          onSettled();
        },
      });
  }

  removeGuest(guest: Participant, onSettled: () => void): void {
    this.mutate(guest.id, this.service.removeSession$(this.groupId(), guest.id), onSettled, () =>
      this.toast.success(`${guest.name} was removed from the reservation`),
    );
  }

  removeSeat(onSettled: () => void): void {
    if (!this.canEdit() || this.openSeats() < 1 || this.isBusy(SEATS_BUSY_ID)) return onSettled();
    this.setBusy(SEATS_BUSY_ID, true);
    this.service
      .removeSeat$(this.groupId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ maxCapacity, status }) => {
          this.setBusy(SEATS_BUSY_ID, false);
          this.details.update((details) => details && { ...details, invite: { ...details.invite, MaxGuestCount: maxCapacity } });
          this.status.set(status);
          this.toast.success(`Your reservation is now for ${maxCapacity} guests`);
          onSettled();
        },
        error: () => {
          this.setBusy(SEATS_BUSY_ID, false);
          onSettled();
        },
      });
  }

  updateQuantity(participant: Participant, dishId: string, quantity: number, onSettled: () => void = () => undefined): void {
    this.mutate(
      participant.id,
      this.service.updateDishQuantity$(this.groupId(), participant.id, dishId, quantity),
      onSettled,
    );
  }

  private mutate(
    sessionId: string,
    request$: Observable<GroupSessionsResponse>,
    onSettled: () => void,
    onSuccess: () => void = () => undefined,
  ): void {
    if (!this.canEdit() || this.isBusy(sessionId)) return onSettled();
    this.mutationVersion++;
    this.setBusy(sessionId, true);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (snapshot) => {
        this.mutationVersion++;
        this.setBusy(sessionId, false);
        this.applySnapshot(snapshot);
        onSuccess();
        onSettled();
      },
      error: () => {
        this.mutationVersion++;
        this.setBusy(sessionId, false);
        onSettled();
      },
    });
  }

  private refresh$(): Observable<unknown> {
    const version = this.mutationVersion;
    this.refreshState.set('refreshing');
    return forkJoin({
      sessions: this.service.getSessions$(this.groupId(), { silent: true }),
      status: this.service.getStatus$(this.groupId(), { silent: true }),
    }).pipe(
      tap(({ sessions, status }) => {
        if (version === this.mutationVersion) this.applySnapshot(sessions);
        this.status.set(status.status);
        this.refreshState.set('idle');
      }),
      catchError(() => {
        this.refreshState.set('failed');
        return EMPTY;
      }),
    );
  }

  private applySnapshot(snapshot: GroupSessionsResponse): void {
    this.participants.set(snapshot.participants);
    this.updatedAt.set(new Date());
  }

  private setBusy(sessionId: string, busy: boolean): void {
    this.busyIds.update((ids) => {
      const next = new Set(ids);
      if (busy) next.add(sessionId);
      else next.delete(sessionId);
      return next;
    });
  }
}
