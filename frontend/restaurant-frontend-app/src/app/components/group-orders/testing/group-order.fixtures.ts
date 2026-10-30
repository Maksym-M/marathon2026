// Спільні дані для тестів домашки — ті самі, що на макеті «Cart - Owner & Guests Pre-Orders».
import { Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { Order } from '@app/models/orders/order.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupOrderCartComponent } from '../group-order-cart/group-order-cart.component';

export const BERRY = { id: 'dish-berry', name: 'Berry Cacao Bowl', price: 9.5, imageUrl: 'assets/img/dishes/spring-salad.jpg' };
export const SPRING = { id: 'dish-spring', name: 'Spring Salad', price: 14, imageUrl: 'assets/img/dishes/spring-salad.jpg' };
export const STRAWBERRY = { id: 'dish-strawberry', name: 'Fresh Strawberry Mint Salad', price: 17 };
export const POTATO = { id: 'dish-potato', name: 'Roasted Sweet Potato & Lentil Salad', price: 10 };

export const OWNER: Participant = {
  id: 'owner', type: 'Owner', name: 'Dmytro', isSubmitted: true, total: 27,
  items: [{ dish: STRAWBERRY, quantity: 1 }, { dish: POTATO, quantity: 1 }],
};
export const NINO: Participant = {
  id: 'nino', type: 'Guest', name: 'Nino', isSubmitted: true, total: 33,
  items: [{ dish: BERRY, quantity: 2 }, { dish: SPRING, quantity: 1 }],
};
export const LUKA: Participant = {
  id: 'luka', type: 'Guest', name: 'Luka', isSubmitted: true, total: 27,
  items: [{ dish: STRAWBERRY, quantity: 1 }, { dish: POTATO, quantity: 1 }],
};
/** Ще обирає: його чернетка не показується і не рахується. */
export const GIORGI: Participant = {
  id: 'giorgi', type: 'Guest', name: 'Giorgi', isSubmitted: false, total: 9.5,
  items: [{ dish: BERRY, quantity: 1 }],
};

/** `total` з API включає чернетку Giorgi ($96.50); у ресторан іде лише $87.00. */
export const SESSIONS: GroupSessionsResponse = { participants: [OWNER, NINO, LUKA, GIORGI], total: 96.5 };

export const INVITE = {
  GroupReservationId: 'grp-1',
  RestaurantName: 'Green & Tasty',
  LocationAddress: '48 Rustaveli Avenue',
  TableNumber: 7,
  MaxGuestCount: 4,
  // Far in the future: the 30-minute pre-order cut-off never locks the tests.
  StartTime: '2030-10-17T12:00:00',
  EndTime: '2030-10-17T13:30:00',
} as GuestInvite;

/** The reservation behind the group (GET /reservations/{id}). */
export const RESERVATION = {
  id: 'res-1',
  tableId: 'table-7',
  startTime: new Date('2030-10-17T12:00:00'),
  endTime: new Date('2030-10-17T13:30:00'),
  guestCount: 4,
  status: ReservationStatusCustomer.Reserved,
  restaurantName: 'Green & Tasty',
  locationAddress: '48 Rustaveli Avenue',
  waiterId: 'waiter-1',
} satisfies ReservationCustomer;

/** The merged Order after Submit (GET /orders/{reservationId}): every submitted dish, no owners. */
export const SUBMITTED_ORDER = {
  orderId: 'order-1',
  reservationId: 'res-1',
  reservationAddress: '48 Rustaveli Avenue',
  reservationTime: '12:00',
  status: 1,
  orderItems: [
    { dish: BERRY, quantity: 2, state: 0 },
    { dish: STRAWBERRY, quantity: 2, state: 0 },
    { dish: POTATO, quantity: 2, state: 0 },
    { dish: SPRING, quantity: 1, state: 0 },
  ],
} as unknown as Order;

export const OPEN: GroupStatusResponse = { currentParticipants: 4, maxCapacity: 4, status: 'Open' };

export function sessions(participants: Participant[]): GroupSessionsResponse {
  return structuredClone({ participants, total: participants.reduce((sum, person) => sum + person.total, 0) });
}

export function createServiceSpy(): jasmine.SpyObj<GroupOrdersService> {
  const service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
    'getGroupInvite$', 'getReservation$', 'getOrder$', 'getSessions$', 'getOwnerSessionId$', 'getStatus$', 'submit$', 'removeSession$', 'removeSeat$', 'updateDishQuantity$',
  ]);
  service.getGroupInvite$.and.returnValue(of({ token: 'tok', invite: INVITE }));
  service.getOrder$.and.returnValue(of(structuredClone(SUBMITTED_ORDER)));
  service.getReservation$.and.returnValue(of(RESERVATION));
  service.getSessions$.and.callFake(() => of(structuredClone(SESSIONS)));
  service.getOwnerSessionId$.and.returnValue(of('owner'));
  service.getStatus$.and.returnValue(of(OPEN));
  return service;
}

export function renderCart(service: jasmine.SpyObj<GroupOrdersService>, providers: Provider[] = []) {
  const toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info']);
  TestBed.configureTestingModule({
    imports: [GroupOrderCartComponent],
    providers: [
      { provide: GroupOrdersService, useValue: service },
      { provide: ToastService, useValue: toast },
      provideRouter([]),
      ...providers,
    ],
  });
  const fixture = TestBed.createComponent(GroupOrderCartComponent);
  fixture.componentRef.setInput('reservationId', 'res-1');
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, toast };
}

export function buttonByText(root: ParentNode, text: string): HTMLButtonElement | undefined {
  return [...root.querySelectorAll('button')].find((button) => button.textContent?.trim() === text);
}

export function buttonByLabel(root: ParentNode, label: string): HTMLButtonElement | null {
  return root.querySelector(`button[aria-label="${label}"]`);
}

export function dialog(element: HTMLElement): HTMLElement {
  const modal = element.querySelector<HTMLElement>('app-modal [role="dialog"]');
  if (!modal) throw new Error('Очікував відкритий <app-modal>');
  return modal;
}

/** Текст без зайвих пробілів — шаблон переносить рядки. */
export function text(element: Element): string {
  return (element.textContent ?? '').replace(/\s+/g, ' ').trim();
}
