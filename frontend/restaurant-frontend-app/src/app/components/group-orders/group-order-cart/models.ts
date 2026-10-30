import { GroupInviteDetails } from '@app/models/group-orders/group-invite-details.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';

export type CartTab = 'participants' | 'summary';

/** One load of the group; `details` (the invite) is null when the group was reached through the Owner's session. */
export interface GroupOrderSnapshot {
  details: GroupInviteDetails | null;
  groupReservationId: string;
  sessions: GroupSessionsResponse;
  status: GroupStatusResponse;
}
