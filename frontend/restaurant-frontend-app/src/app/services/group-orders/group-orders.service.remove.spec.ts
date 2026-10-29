// Бонус · Owner керує кошиками гостей — сервіс. Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('Бонус · removeSession$ / removeSeat$ / updateDishQuantity$', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: BASE_URL }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('removeSession$: DELETE /guest/groups/{id}/sessions/{guestSessionId}', () => {
    service.removeSession$('grp-1', 'gs-2').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions/gs-2`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ participants: [], total: 0 });
  });

  it('removeSeat$: DELETE /guest/groups/{id}/seats, повертає статус групи', () => {
    let maxCapacity = 0;
    service.removeSeat$('grp-1').subscribe((status) => (maxCapacity = status.maxCapacity));
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/seats`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ currentParticipants: 3, maxCapacity: 4, status: 'Open' });
    expect(maxCapacity).toBe(4);
  });

  it('updateDishQuantity$: PUT …/sessions/{guestSessionId}/dishes/{dishId} з тілом { quantity }', () => {
    service.updateDishQuantity$('grp-1', 'gs-2', 'dish-1', 3).subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions/gs-2/dishes/dish-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ quantity: 3 });
    req.flush({ participants: [], total: 0 });
  });

  it('updateDishQuantity$ з 0 прибирає страву (той самий PUT)', () => {
    service.updateDishQuantity$('grp-1', 'gs-2', 'dish-1', 0).subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions/gs-2/dishes/dish-1`);
    expect(req.request.body).toEqual({ quantity: 0 });
    req.flush({ participants: [], total: 0 });
  });
});
