import { HttpClient } from '@angular/common/http';
import { Injectable, InjectionToken, computed, inject, isDevMode, signal } from '@angular/core';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';

/** Remembers the last name on this computer (%LOCALAPPDATA%\VulcanCBAS\settings.json, via the API). */
export const TECHNICIAN_SETTINGS_URL = '/api/settings/technician';

/**
 * True when a session may start without the local API, so `npm start` in a browser still works.
 * Development builds only: the desktop app always needs the API.
 */
export const ALLOW_START_WITHOUT_API = new InjectionToken<boolean>('ALLOW_START_WITHOUT_API', {
  providedIn: 'root',
  factory: () => isDevMode(),
});

/**
 * Who is using the app right now (M1-42). Kept in memory only: closing the app ends the session,
 * and the next launch asks again (with the last name filled in).
 */
@Injectable({ providedIn: 'root' })
export class TechnicianSession {
  private readonly http = inject(HttpClient);
  private readonly allowWithoutApi = inject(ALLOW_START_WITHOUT_API);

  private readonly _name = signal<string | null>(null);
  readonly name = this._name.asReadonly();
  readonly isStarted = computed(() => this._name() !== null);

  /** The last name used on this computer, or null if there isn't one (or the API isn't answering). */
  lastUsed(): Observable<string | null> {
    return this.http.get<{ name: string | null }>(TECHNICIAN_SETTINGS_URL).pipe(
      map((response) => response.name),
      catchError(() => of(null)),
    );
  }

  /** Saves the name (trimmed) as the last one used on this computer, then starts the session. */
  start(name: string): Observable<void> {
    const trimmed = name.trim();
    return this.http.put<void>(TECHNICIAN_SETTINGS_URL, { name: trimmed }).pipe(
      catchError((error: unknown) => (this.allowWithoutApi ? of(undefined) : throwError(() => error))),
      tap(() => this._name.set(trimmed)),
    );
  }
}
