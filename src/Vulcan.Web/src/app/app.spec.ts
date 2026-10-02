import { TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';

import { App } from './app';
import { VulcanPreset } from './theme/vulcan-preset';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [providePrimeNG({ theme: { preset: VulcanPreset } })],
    }).compileComponents();
  });

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the Vulcan logo', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const logo = (fixture.nativeElement as HTMLElement).querySelector('img.app-logo');
    expect(logo?.getAttribute('src')).toBe('assets/vulcan-logo.svg');
  });

  it('shows a PrimeNG button', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const button = (fixture.nativeElement as HTMLElement).querySelector('p-button button');
    expect(button?.textContent).toContain('Save batch');
  });
});
