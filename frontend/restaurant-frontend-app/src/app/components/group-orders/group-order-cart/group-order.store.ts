import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  catchError,
  EMPTY,
  exhaustMap,
  filter,
  finalize,
  forkJoin,
  map,
  Observable,
  Subscription,
  switchMap,
  tap,
  timer,
} from 'rxjs';
import { GroupInviteDetails } from '@app/models/group-orders/group-invite-details.model';
import { GroupOrderAction } from '@app/models/group-orders/group-order-action.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { buildDishSummary } from '@app/shared/helpers/dish-summary';
import { getServerErrorCode } from '@app/shared/helpers/http-error-helpers';
import { GroupOrderSnapshot } from './models';

export const GROUP_ORDER_SYNC_MS = 15_000;
/** invite-token answers 400 with this code once the group pre-order is sent to the restaurant. */
const GROUP_SUBMITTED_ERROR_CODE = 'GroupReservationSubmitted';
/** The API names the Owner's session `owner-{groupReservationId}`; after Submit it's the only way to the group. */
const OWNER_SESSION_PREFIX = 'owner-';

/** null when the id has another format: then the group is reached through the invite. */
function groupIdFromOwnerSession(ownerSessionId: string): string | null {
  return ownerSessionId.startsWith(OWNER_SESSION_PREFIX) ? ownerSessionId.slice(OWNER_SESSION_PREFIX.length) || null : null;
}
/** Pre-order cut-off (US23 AC7): no changes from 30 minutes before the reservation starts. */
export const PRE_ORDER_CUTOFF_MS = 30 * 60 * 1000;
/** Busy key for seat changes — never a session id, those are GUIDs or `owner-…`. */
const SEATS_BUSY_ID = 'seats';

/** `submitted`: the group was sent to the restaurant before this screen opened and its participants couldn't be loaded. */
export type GroupOrderLoadState = 'loading' | 'ready' | 'submitted' | 'not-found' | 'error';
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
  /** Known before the invite arrives (from the Owner's session), so the cart works while it loads. */
  private readonly groupReservationId = signal('');
  /** The reservation's guests count from the group status; the invite has the same number. */
  private readonly capacity = signal(1);
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
  /** The invite loads after the cart shows; until then its time is unknown, so nothing is editable yet. */
  readonly detailsLoading = signal(false);

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
  readonly canEdit = computed(() => this.isOpen() && !this.isLocked() && !this.detailsLoading());
  /** Seats for guests: the guests count includes the Owner. */
  readonly guestSeats = computed(() =>
    Math.max((this.invite()?.MaxGuestCount ?? this.capacity()) - 1, this.guests().length),
  );
  readonly openSeats = computed(() => this.guestSeats() - this.guests().length);
  readonly submittedGuests = computed(() => this.guests().filter((guest) => guest.isSubmitted).length);
  readonly pendingGuests = computed(() => this.guests().filter((guest) => !guest.isSubmitted).length);
  readonly includesGuestDishes = computed(() => this.guests().some((guest) => guest.isSubmitted && guest.items.length > 0));
  readonly canSubmit = computed(() => this.canEdit() && this.consolidated().items.length > 0 && !this.submitting());

  private readonly groupId = computed(() => this.invite()?.GroupReservationId ?? this.groupReservationId());
  private loadSubscription?: Subscription;
  private detailsSubscription?: Subscription;
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

  /**
   * The Owner's session leads to the group first, so a group already sent to the restaurant never asks for
   * the invite: invite-token answers 400 then. Participants show as soon as they arrive; an open group's
   * invite (details and invite link) follows in the background.
   * Silent: the cart and the page show their own error states.
   */
  load(reservationId: string): void {
    this.reservationId.set(reservationId);
    this.loadState.set('loading');
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = this.service
      .getOwnerSessionId$(reservationId, { silent: true })
      .pipe(
        switchMap((ownerSessionId) => {
          const groupReservationId = groupIdFromOwnerSession(ownerSessionId);
          const group$ = groupReservationId
            ? this.loadByGroup$(groupReservationId)
            : this.loadByInvite$(reservationId);
          return group$.pipe(map((group) => ({ ownerSessionId, ...group })));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ details, groupReservationId, sessions, ownerSessionId, status }) => {
          this.details.set(details);
          this.groupReservationId.set(groupReservationId);
          this.capacity.set(status.maxCapacity);
          this.ownerSessionId.set(ownerSessionId);
          this.applySnapshot(sessions);
          this.status.set(status.status);
          this.refreshState.set('idle');
          this.loadState.set('ready');
          if (!details && status.status === 'Open') this.loadDetails(reservationId);
        },
        error: (error: unknown) => this.onLoadError(error),
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
          this.capacity.set(maxCapacity);
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
        this.capacity.set(status.maxCapacity);
        this.refreshState.set('idle');
      }),
      catchError(() => {
        this.refreshState.set('failed');
        return EMPTY;
      }),
    );
  }

  private onLoadError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.loadState.set('error');
    } else if (getServerErrorCode(error) === GROUP_SUBMITTED_ERROR_CODE) {
      // Only when the group couldn't be reached without the invite, or it was sent while this screen loaded.
      this.status.set('Submitted');
      this.loadState.set('submitted');
    } else {
      this.loadState.set(error.status === 404 ? 'not-found' : 'error');
    }
  }

  private loadByGroup$(groupReservationId: string): Observable<GroupOrderSnapshot> {
    return forkJoin({
      sessions: this.service.getSessions$(groupReservationId, { silent: true }),
      status: this.service.getStatus$(groupReservationId, { silent: true }),
    }).pipe(map((data) => ({ details: null, groupReservationId, ...data })));
  }

  private loadByInvite$(reservationId: string): Observable<GroupOrderSnapshot> {
    return this.service.getGroupInvite$(reservationId).pipe(
      switchMap((details) => {
        const groupReservationId = details.invite.GroupReservationId;
        return forkJoin({
          sessions: this.service.getSessions$(groupReservationId, { silent: true }),
          status: this.service.getStatus$(groupReservationId, { silent: true }),
        }).pipe(map((data) => ({ details, groupReservationId, ...data })));
      }),
    );
  }

  /**
   * It can fail while the group is still open: the API refuses the invite once every seat is taken, or
   * the group was just sent from another tab. The cart then works without the invite link and its details.
   */
  private loadDetails(reservationId: string): void {
    this.detailsSubscription?.unsubscribe();
    this.detailsLoading.set(true);
    this.detailsSubscription = this.service
      .getGroupInvite$(reservationId)
      .pipe(
        finalize(() => this.detailsLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (details) => this.details.set(details),
        error: () => undefined,
      });
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
