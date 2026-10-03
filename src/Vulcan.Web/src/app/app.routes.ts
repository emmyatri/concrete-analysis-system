import { Routes } from '@angular/router';

import { technicianGuard } from './core/session/technician.guard';
import { Shell } from './layout/shell/shell';
import { BatchEntry } from './pages/batch-entry/batch-entry';
import { PlaceholderPage } from './pages/placeholder-page';
import { StartSession } from './pages/start-session/start-session';

export const routes: Routes = [
  // Asks who is using the app. Outside the shell: no menu until there's a technician (M1-42).
  { path: 'start', component: StartSession },
  {
    // Every screen is a child of the shell, so it shows inside the header and side menu.
    path: '',
    component: Shell,
    canActivate: [technicianGuard],
    children: [
      { path: '', pathMatch: 'full', component: PlaceholderPage, data: { heading: 'Home' } },
      { path: 'batches/new', component: BatchEntry },
      { path: 'batches/:id', component: PlaceholderPage, data: { heading: 'Batch detail' } },
      { path: 'batches', component: PlaceholderPage, data: { heading: 'Search batches' } },
      { path: 'breaks', component: PlaceholderPage, data: { heading: 'Break results' } },
      { path: 'curing', component: PlaceholderPage, data: { heading: 'Curing readings' } },
    ],
  },
  { path: '**', redirectTo: '' },
];
