import { ParticipantItem } from './participant.model';

/** The Owner sets a new quantity for one dish of a cart; 0 removes the dish. */
export interface DishQuantityChange {
  item: ParticipantItem;
  quantity: number;
}
