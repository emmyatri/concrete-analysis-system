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
      {
        path: '',
        pathMatch: 'full',
        // Loaded on demand, like the other pages, to keep the app's start under its size budget.
        loadComponent: () => import('./pages/home/home').then((m) => m.HomePage),
      },
      { path: 'batches/new', component: BatchEntry },
      {
        path: 'batches',
        loadComponent: () =>
          import('./pages/batch-search/batch-search').then((m) => m.BatchSearchPage),
      },
      // After batches/new, so "new" isn't read as a batch number.
      {
        path: 'batches/:batchNumber',
        // Loaded when first opened, so its tables and tabs don't slow down the app's start.
        loadComponent: () =>
          import('./pages/batch-detail/batch-detail').then((m) => m.BatchDetailPage),
      },
      {
        path: 'batches/:batchNumber/edit',
        component: PlaceholderPage,
        data: { heading: 'Edit batch' },
      },
      // Add cylinder is now a popup on Batch Detail.
      {
        path: 'batches/:batchNumber/cylinders/new',
        redirectTo: ({ params }) => `/batches/${params['batchNumber']}?addCylinder=1`,
      },
      {
        path: 'breaks',
        loadComponent: () =>
          import('./pages/break-results/break-results').then((m) => m.BreakResultsPage),
      },
      {
        path: 'curing',
        loadComponent: () =>
          import('./pages/curing-readings/curing-readings').then((m) => m.CuringReadingsPage),
      },
    ],
  },

  {
    // Standalone printable report (M3-31 / FR-19).
    // Outside Shell so the application menu and header
    // do not appear in the printed report.
    path: 'batches/:batchNumber/report',
    canActivate: [technicianGuard],
    loadComponent: () => import('./pages/batch-report/batch-report').then((m) => m.BatchReport),
  },
  { path: '**', redirectTo: '' },
];
