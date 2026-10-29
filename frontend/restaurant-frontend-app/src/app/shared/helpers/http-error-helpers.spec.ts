import { HttpErrorResponse } from '@angular/common/http';
import { getServerErrorCode, getServerErrorMessage, isTransientHttpError } from './http-error-helpers';

describe('isTransientHttpError', () => {
  const cases: { status: number; expected: boolean }[] = [
    { status: 0, expected: true },
    { status: 429, expected: true },
    { status: 500, expected: true },
    { status: 503, expected: true },
    { status: 400, expected: false },
    { status: 401, expected: false },
    { status: 404, expected: false },
  ];

  cases.forEach(({ status, expected }) => {
    it(`returns ${expected} for status ${status}`, () => {
      expect(isTransientHttpError(new HttpErrorResponse({ status }))).toBe(expected);
    });
  });

  it('returns false for an error that is not an HTTP response', () => {
    expect(isTransientHttpError(new Error('boom'))).toBeFalse();
  });
});

describe('getServerErrorMessage', () => {
  const cases: { name: string; body: unknown; expected: string }[] = [
    { name: 'reads a plain string body', body: 'Plain error', expected: 'Plain error' },
    { name: 'reads the error field', body: { error: 'Error field' }, expected: 'Error field' },
    { name: 'reads the message field', body: { message: 'Message field' }, expected: 'Message field' },
    {
      name: 'prefers the error field over message',
      body: { error: 'Error field', message: 'Message field' },
      expected: 'Error field',
    },
    { name: 'returns an empty string for an empty body', body: null, expected: '' },
  ];

  cases.forEach(({ name, body, expected }) => {
    it(name, () => {
      const error = new HttpErrorResponse({ error: body, status: 400 });

      expect(getServerErrorMessage(error)).toBe(expected);
    });
  });
});

describe('getServerErrorCode', () => {
  const cases: { name: string; body: unknown; expected: string | null }[] = [
    {
      name: 'reads the errorCode field',
      body: { message: 'Group reservation was submitted.', errorCode: 'GroupReservationSubmitted' },
      expected: 'GroupReservationSubmitted',
    },
    { name: 'returns null without an errorCode', body: { message: 'Message field' }, expected: null },
    { name: 'returns null for a plain string body', body: 'Plain error', expected: null },
    { name: 'returns null for an empty body', body: null, expected: null },
  ];

  cases.forEach(({ name, body, expected }) => {
    it(name, () => {
      const error = new HttpErrorResponse({ error: body, status: 400 });

      expect(getServerErrorCode(error)).toBe(expected);
    });
  });
});
