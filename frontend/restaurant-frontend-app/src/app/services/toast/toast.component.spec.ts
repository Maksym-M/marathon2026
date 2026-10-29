import { TestBed } from '@angular/core/testing';
import { MAT_SNACK_BAR_DATA, MatSnackBarRef } from '@angular/material/snack-bar';
import { ToastComponent, ToastData } from './toast.component';

function render(data: ToastData) {
  const snackBarRef = jasmine.createSpyObj<MatSnackBarRef<ToastComponent>>('MatSnackBarRef', ['dismiss']);
  TestBed.configureTestingModule({
    imports: [ToastComponent],
    providers: [
      { provide: MAT_SNACK_BAR_DATA, useValue: data },
      { provide: MatSnackBarRef, useValue: snackBarRef },
    ],
  });
  const fixture = TestBed.createComponent(ToastComponent);
  // Real layout is measured below, so the toast needs a real width, like inside the snack bar.
  fixture.nativeElement.style.cssText = 'width: 360px; display: flex;';
  document.body.appendChild(fixture.nativeElement);
  fixture.detectChanges();
  return { element: fixture.nativeElement as HTMLElement, snackBarRef };
}

function verticalCenter(element: Element): number {
  const box = element.getBoundingClientRect();
  return box.top + box.height / 2;
}

describe('ToastComponent', () => {
  afterEach(() => document.querySelectorAll('app-toast').forEach((toast) => toast.remove()));

  it('success without a title: the message sits on the same line as the icon', () => {
    const { element } = render({ type: 'success', message: 'Your reservation has been updated successfully.' });
    const icon = element.querySelector('.toast-icon')!;
    const message = element.querySelector('.toast-message')!;

    expect(Math.abs(verticalCenter(icon) - verticalCenter(message))).toBeLessThan(2);
    expect(message.getBoundingClientRect().left).toBeGreaterThan(icon.getBoundingClientRect().right);
  });

  it('success with a title: the title is next to the icon and the message under the title', () => {
    const { element } = render({ type: 'success', title: 'Success', message: 'Dish added to cart successfully!' });
    const icon = element.querySelector('.toast-icon')!.getBoundingClientRect();
    const title = element.querySelector('.toast-title')!.getBoundingClientRect();
    const message = element.querySelector('.toast-message')!.getBoundingClientRect();

    expect(title.left).toBeGreaterThan(icon.right);
    expect(message.left).toBe(title.left);
    expect(message.top).toBeGreaterThanOrEqual(title.bottom);
  });

  it('error toast has no icon and shows the message', () => {
    const { element } = render({ type: 'error', message: 'Bad request.' });
    expect(element.querySelector('.toast-icon')).toBeNull();
    expect(element.querySelector('.toast-message')?.textContent).toContain('Bad request.');
  });

  it('close button has an accessible name and dismisses the toast', () => {
    const { element, snackBarRef } = render({ type: 'info', message: 'Invite link copied' });
    const close = element.querySelector<HTMLButtonElement>('button[aria-label="Close notification"]');

    close?.click();

    expect(snackBarRef.dismiss).toHaveBeenCalled();
  });
});
