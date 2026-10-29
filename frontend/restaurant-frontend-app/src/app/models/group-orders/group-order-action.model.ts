import { Participant, ParticipantItem } from './participant.model';

/** An action the Owner started that waits for confirmation in a dialog. */
export type GroupOrderAction =
  | { kind: 'submit' }
  | { kind: 'removeGuest'; guest: Participant }
  | { kind: 'removeDish'; guest: Participant; item: ParticipantItem }
  | { kind: 'removeSeat' };
