import { GuestInvite } from '@app/models/reservations/guest-invite.model';

/** GET /reservations/{reservationId}/invite-token → GET /guest/invites/{token} */
export interface GroupInviteDetails {
  token: string;
  invite: GuestInvite;
}
