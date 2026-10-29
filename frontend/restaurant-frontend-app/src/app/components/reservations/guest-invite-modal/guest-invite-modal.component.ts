import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';

/** "Guest Invite link" modal from a reservation card (US20 AC6): the link to share and a copy button. */
@Component({
  selector: 'app-guest-invite-modal',
  imports: [ModalComponent],
  templateUrl: './guest-invite-modal.component.html',
  styleUrl: './guest-invite-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GuestInviteModalComponent implements OnInit {
  private readonly guestInviteService = inject(GuestInviteService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly reservationId = input.required<string>();
  readonly closed = output<void>();

  private readonly token = signal<string | null>(null);
  readonly loadFailed = signal(false);
  /** The browser blocked the clipboard: the link is shown as selectable text instead. */
  readonly copyFailed = signal(false);

  readonly link = computed(() => {
    const token = this.token();
    return token ? `${window.location.origin}/join/${token}` : null;
  });

  ngOnInit(): void {
    this.guestInviteService
      .getInviteToken$(this.reservationId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ token }) => this.token.set(token),
        error: () => this.loadFailed.set(true),
      });
  }

  async copy(): Promise<void> {
    const link = this.link();
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link);
      this.copyFailed.set(false);
      this.toast.success('Share it with your friends.', 'Link copied!');
    } catch {
      this.copyFailed.set(true);
    }
  }
}
