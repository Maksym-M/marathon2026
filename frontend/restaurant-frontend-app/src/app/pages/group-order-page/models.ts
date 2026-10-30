/** What the Group Pre-Order page shows (US21 AC8–AC10, US23 AC10). */
/** The "Reservation" card: from the group invite while it loads, from the reservation once the group is submitted. */
export interface GroupOrderDetails {
  restaurantName: string;
  location: string;
  startTime: Date | string;
  endTime: Date | string;
  guestCount: number;
}

export type GroupOrderPageView =
  | 'loading'
  | 'error'
  | 'not-found'
  | 'cancelled'
  | 'passed'
  | 'single'
  | 'submitted'
  | 'ready';
