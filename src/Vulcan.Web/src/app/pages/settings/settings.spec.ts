import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';

import { CLOUD_FOLDER_URL } from '../../core/api/vulcan-api.service';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { SettingsPage } from './settings';

const FOLDER = 'C:\\Users\\jrivera\\OneDrive\\Lab';

describe('SettingsPage (SCRUM-214)', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let http: HttpTestingController;
  let postMessage: ReturnType<typeof vi.fn>;
  let listeners: ((event: MessageEvent) => void)[];

  /** Pretends to be the desktop app's WebView2 bridge. */
  const stubDesktop = () => {
    postMessage = vi.fn();
    listeners = [];
    vi.stubGlobal('chrome', {
      webview: {
        postMessage,
        addEventListener: (_: string, listener: (event: MessageEvent) => void) =>
          listeners.push(listener),
        removeEventListener: (_: string, listener: (event: MessageEvent) => void) =>
          (listeners = listeners.filter((l) => l !== listener)),
      },
    });
  };

  const render = (setting: { path: string | null; exists: boolean }): HTMLElement => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
    http.expectOne({ method: 'GET', url: CLOUD_FOLDER_URL }).flush(setting);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const testId = (root: HTMLElement, id: string) =>
    root.querySelector<HTMLElement>(`[data-testid="${id}"]`);

  afterEach(() => {
    http.verify();
    vi.unstubAllGlobals();
  });

  it('shows "Not set" when no cloud folder is chosen', () => {
    stubDesktop();
    const root = render({ path: null, exists: false });

    expect(testId(root, 'cloud-folder-not-set')?.textContent).toContain('Not set');
    expect(testId(root, 'choose-cloud-folder')?.textContent).toContain('Choose folder');
    expect(testId(root, 'clear-cloud-folder')).toBeNull();
  });

  it('shows the saved folder', () => {
    stubDesktop();
    const root = render({ path: FOLDER, exists: true });

    expect(testId(root, 'cloud-folder-path')?.textContent).toContain(FOLDER);
    expect(testId(root, 'cloud-folder-missing')).toBeNull();
    expect(testId(root, 'choose-cloud-folder')?.textContent).toContain('Change folder');
  });

  it('says so when the saved folder is missing', () => {
    stubDesktop();
    const root = render({ path: FOLDER, exists: false });

    expect(testId(root, 'cloud-folder-missing')?.textContent).toContain("can't be found");
  });

  it('asks the desktop app for the folder picker', () => {
    stubDesktop();
    const root = render({ path: null, exists: false });

    root.querySelector<HTMLButtonElement>('[data-testid="choose-cloud-folder"] button')!.click();

    expect(postMessage).toHaveBeenCalledExactlyOnceWith({ type: 'choose-cloud-folder' });
  });

  it('saves the folder the desktop app sends back, then shows it', () => {
    stubDesktop();
    render({ path: null, exists: false });

    listeners.forEach((listener) =>
      listener(
        new MessageEvent('message', { data: { type: 'cloud-folder-chosen', path: FOLDER } }),
      ),
    );

    const put = http.expectOne({ method: 'PUT', url: CLOUD_FOLDER_URL });
    expect(put.request.body).toEqual({ path: FOLDER });
    put.flush(null, { status: 204, statusText: 'No Content' });

    http.expectOne({ method: 'GET', url: CLOUD_FOLDER_URL }).flush({ path: FOLDER, exists: true });
    fixture.detectChanges();

    expect(testId(fixture.nativeElement, 'cloud-folder-path')?.textContent).toContain(FOLDER);
  });

  it('shows the server message when a folder is rejected', () => {
    stubDesktop();
    render({ path: null, exists: false });

    listeners.forEach((listener) =>
      listener(
        new MessageEvent('message', { data: { type: 'cloud-folder-chosen', path: FOLDER } }),
      ),
    );
    http
      .expectOne({ method: 'PUT', url: CLOUD_FOLDER_URL })
      .flush(
        { errors: { 'CloudFolder.Path': ["That folder doesn't exist."] } },
        { status: 400, statusText: 'Bad Request' },
      );
    fixture.detectChanges();

    expect(testId(fixture.nativeElement, 'cloud-folder-save-error')?.textContent).toContain(
      "That folder doesn't exist.",
    );
  });

  it('ignores other desktop messages', () => {
    stubDesktop();
    render({ path: null, exists: false });

    listeners.forEach((listener) =>
      listener(new MessageEvent('message', { data: { type: 'something-else', path: FOLDER } })),
    );

    http.expectNone({ method: 'PUT', url: CLOUD_FOLDER_URL });
  });

  it('clears the folder', () => {
    stubDesktop();
    const root = render({ path: FOLDER, exists: true });

    root.querySelector<HTMLButtonElement>('[data-testid="clear-cloud-folder"] button')!.click();

    http
      .expectOne({ method: 'DELETE', url: CLOUD_FOLDER_URL })
      .flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne({ method: 'GET', url: CLOUD_FOLDER_URL }).flush({ path: null, exists: false });
    fixture.detectChanges();

    expect(testId(fixture.nativeElement, 'cloud-folder-not-set')).not.toBeNull();
  });

  it('stops listening to the desktop app when the page closes', () => {
    stubDesktop();
    render({ path: null, exists: false });

    fixture.destroy();

    expect(listeners).toEqual([]);
  });

  it('explains that choosing a folder needs the desktop app', () => {
    vi.stubGlobal('chrome', undefined);
    const root = render({ path: null, exists: false });

    expect(testId(root, 'choose-cloud-folder')).toBeNull();
    expect(testId(root, 'cloud-folder-desktop-only')?.textContent).toContain('desktop app');
  });

  it('says so when the setting cannot be loaded', () => {
    vi.stubGlobal('chrome', undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
    http.expectOne(CLOUD_FOLDER_URL).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(testId(fixture.nativeElement, 'cloud-folder-load-error')).not.toBeNull();
  });
});
