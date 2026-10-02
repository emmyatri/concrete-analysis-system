export interface ShellMenuItem {
  label: string;
  icon: string;
  route: string;
}

/** Side menu, in mockup order. */
export const SHELL_MENU: readonly ShellMenuItem[] = [
  { label: 'Home', icon: 'pi pi-home', route: '/' },
  { label: 'New batch', icon: 'pi pi-plus-circle', route: '/batches/new' },
  { label: 'Search batches', icon: 'pi pi-search', route: '/batches' },
  { label: 'Break results', icon: 'pi pi-chart-bar', route: '/breaks' },
  { label: 'Curing readings', icon: 'pi pi-clock', route: '/curing' },
];
