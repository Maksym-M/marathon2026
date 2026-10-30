import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DishCardComponent } from './dish-card.component';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishState } from '@app/models/enums/dish-state.model';

describe('DishCardComponent', () => {
  let component: DishCardComponent;
  let fixture: ComponentFixture<DishCardComponent>;

  const dish: DishListResponse = {
    id: 'dish-1',
    name: 'Khachapuri',
    imageUrl: 'assets/img/dish.png',
    price: 15,
    state: DishState.Available,
    weight: '350',
    dishType: 'MainCourse',
    description: 'Georgian cheese bread',
    calories: '500',
    proteins: '20',
    carbohydrates: '45',
    fats: '25',
    vitamins: 'A, B',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishCardComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('dish', dish);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark a dish as out of stock when GET /dishes sends the label', () => {
    fixture.componentRef.setInput('dish', { ...dish, state: 'Out of stock' });
    fixture.componentRef.setInput('preOrderEnabled', true);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(component.isUnavailable()).toBe(true);
    expect(element.querySelector('.status-badge')?.textContent?.trim()).toBe('Out Of Stock');
    expect(element.querySelector('.dish-card')?.classList).toContain('unavailable');
  });

  it('should mark a dish as unavailable when the guest menu sends the enum key', () => {
    fixture.componentRef.setInput('dish', { ...dish, state: DishState.OutOfStock });
    fixture.detectChanges();

    expect(component.isUnavailable()).toBe(true);
  });

  it('should mark an on-stop dish as unavailable', () => {
    fixture.componentRef.setInput('dish', { ...dish, state: 'On stop' });
    fixture.detectChanges();

    expect(component.isUnavailable()).toBe(true);
    expect(component.dishStateLabel()).toBe('On Stop');
  });

  it('should keep an available dish selectable', () => {
    expect(component.isUnavailable()).toBe(false);
    expect(fixture.nativeElement.querySelector('.status-badge')).toBeNull();
  });
});
