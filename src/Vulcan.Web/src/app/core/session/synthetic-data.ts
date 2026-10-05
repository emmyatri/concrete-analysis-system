import { Injectable, signal } from '@angular/core';

/**
 * The "Synthetic data" switch in the top bar (FR-20). Off hides the clearly marked test batches
 * from Home and Search; on shows them with a SYNTHETIC badge. Shared so every screen follows it.
 */
@Injectable({ providedIn: 'root' })
export class SyntheticDataSetting {
  readonly show = signal(true);
}
