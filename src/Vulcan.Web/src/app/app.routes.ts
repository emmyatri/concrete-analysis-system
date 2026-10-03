import { Routes } from '@angular/router';

import { Shell } from './layout/shell/shell';
import { PlaceholderPage } from './pages/placeholder-page';
import { BatchEntry } from './pages/batch-entry/batch-entry';

/** Every screen is a child of the shell, so it shows inside the header and side menu. */
export const routes: Routes = [
  {
    path: '',
    component: Shell,
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
