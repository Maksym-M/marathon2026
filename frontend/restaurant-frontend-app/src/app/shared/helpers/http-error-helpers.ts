import { HttpErrorResponse } from '@angular/common/http';

export function getServerErrorMessage(error: HttpErrorResponse): string {
  if (typeof error.error === 'string') {
    return error.error;
  }

  return error.error?.error ?? error.error?.message ?? '';
}

/** Worth retrying: no response at all (network, CORS on a throttled Lambda), throttling, or a server error. */
export function isTransientHttpError(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse)) {
    return false;
  }

  return error.status === 0 || error.status === 429 || error.status >= 500;
}

/** The `errorCode` the API adds to some 400s, e.g. `GroupReservationSubmitted`; null when there is none. */
export function getServerErrorCode(error: HttpErrorResponse): string | null {
  return typeof error.error === 'object' ? (error.error?.errorCode ?? null) : null;
}
