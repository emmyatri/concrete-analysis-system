# ADR-004: Angular UI Hosted in WebView2

## 1. Context

ADR-002 established that the application runs as a single WPF desktop program that hosts the ASP.NET Core API in-process, and that every UI data operation goes through that API over HTTP. The original plan was to build the screens themselves in WPF using XAML and MVVM.

Because the UI already talks to the API only over HTTP, the screens do not need to be written in WPF. Any front end that can call a local HTTP API can be used, as long as it runs inside the same desktop window and meets the SLA's offline and single-installer requirements.

The team considered two options:

- **Blazor Hybrid (BlazorWebView).** Keeps the UI in C#, but the team has little Blazor experience, and its component libraries do not match the approved designs.
- **Angular + PrimeNG in WebView2.** Uses the team's existing Angular skills. PrimeNG provides the data table, dialog, dropdown, date picker and tag components the screens need, and includes a chart component for FR-11 in Milestone 2. The approved hi-fi mockups were built on the PrimeNG design kit, so each mockup element maps directly to a component.


## 2. Decision

The team has selected **Angular with PrimeNG, displayed inside the WPF window using Microsoft Edge WebView2**.

WPF remains the host: it owns the window, starts and stops the API (ADR-002), and shows the UI. Angular draws every screen.

### 2.1 Hosting

The WPF main window contains a single WebView2 control and no other screens.

On launch, the application starts the API on `127.0.0.1` with a port chosen by the operating system. Once the API reports that it is ready, WebView2 navigates to `http://127.0.0.1:{port}/`.

If the API fails to start, the application shows an error dialog, logs the error and exits. The operator never sees a blank window.

### 2.2 Serving the UI

The Angular source lives in `src/Vulcan.Web/`. Its production build is copied into the API's `wwwroot/` folder and served by the API as static files.

All API routes live under `/api`. Any other path returns `index.html` so that Angular can handle its own routing.

Because the UI and the API share one address, no CORS configuration is needed.

### 2.3 Data Access and Validation

The rules from ADR-002 apply to the Angular UI exactly as they did to the planned WPF screens:

- The UI reads and writes data only through `/api`. It never accesses application services, Entity Framework Core or the SQLite database directly.
- The UI performs preliminary validation for immediate feedback. It gets the allowed ranges from the API (the `ValidationLimit` table) and never hard-codes a limit in TypeScript.
- The API remains the authority. Every rule is enforced on the server before data is saved (NFR-6).

### 2.4 Offline Operation (NFR-3)

The application must work with no internet connection. Therefore:

- All fonts (Inter), icons (PrimeIcons), styles and scripts are bundled into the Angular build.
- The UI makes no requests to CDNs, Google Fonts or any other external address.
- The API sends a Content Security Policy that only allows scripts, fonts, images and network requests from the application's own address, so an accidental external reference fails during testing instead of at Vulcan. Styles are limited to the application's own address but may be inline (`style-src 'self' 'unsafe-inline'`), because PrimeNG adds its theme styles at runtime. Scripts are never allowed inline. Angular's critical CSS inlining (`optimization.styles.inlineCritical`) is turned off, because it relies on an inline script handler that this policy blocks, and it gives no benefit when the UI loads from the same computer.

### 2.5 Installation (NFR-1)

The installer remains a single file:

- The installer bundles the WebView2 Evergreen Standalone Installer and runs it silently only if the WebView2 runtime is missing. It does not download anything.
- WebView2 stores its browser data in `%LOCALAPPDATA%\VulcanCBAS\WebView2\`. The default location is next to the program in `C:\Program Files\`, which ordinary users cannot write to.
- In Release builds, browser developer tools, the default right-click menu and browser keyboard shortcuts (such as reload) are turned off.

### 2.6 Build and Continuous Integration

- Node.js (LTS) is required to build the application, both on developer machines and in CI.
- Building `Vulcan.Desktop` (Debug or Release) also builds the Angular app and copies it into `wwwroot/`. The generated `wwwroot/` content is not committed.
- CI runs the Angular unit tests and production build in addition to the .NET build and tests. Either failing fails the pipeline.
- For screen development, the Angular app can run on its own in a browser (`npm start`) against mock API responses.

## 3. Consequences

| Positive Consequences                                        | Trade-offs                                                   |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| Uses the team's existing Angular and TypeScript skills       | Two toolchains to maintain: .NET and Node.js                 |
| Ready-made PrimeNG components for tables, dialogs, validation display and Milestone 2 charts | The WebView2 runtime must be present on the workstation and bundled in the installer |
| Hi-fi mockups map directly to PrimeNG components             | Larger installer because of the bundled Angular build and WebView2 installer |
| Screens can be developed and tested in a browser without the desktop app | UI bugs that only appear inside WebView2 must still be checked in the desktop app |
| The UI to API boundary from ADR-002 is enforced by design, since the UI can only reach the API over HTTP | Every screen depends on API endpoints, so endpoint work can block UI work (mitigated by mock mode) |
| One local address for the UI and the API, so no CORS setup   | Validation messages must be returned by the API in a shape the UI can map to fields |

### Effect on Other Documents

- ADR-002 and ADR-003 now refer to "the UI" instead of "the WPF interface".
- CommunityToolkit.Mvvm will not be added. WPF keeps only the window and the WebView2 host.
- The README tech stack lists Angular + PrimeNG in WebView2, hosted by WPF.

### Security

The UI is only reachable at `127.0.0.1`. It loads no external content and, in Release builds, does not expose developer tools. As noted in ADR-002, other processes on the same workstation can still reach the local address.

### Future Changes

If the system later moves to multiple workstations or central hosting (see ADR-001 and ADR-002), the Angular UI can be served from that central API with little change, because it already communicates with the backend only through HTTP.

## 4. Status

**Proposed** — Pending Co-Lead approval.

## References

- [ADR-002: In-Process ASP.NET Core API](002-in-process-api.md)
- [ADR-003: Canonical Measurement Units](003-canonical-units.md)
- [Approved Data Schema](../schema.md)
- SLA: NFR-1 (single installer), NFR-3 (offline operation), NFR-6 (server-side validation)
- Vulcan CBAS Hi-Fi mockups (Figma, link pinned in the team channel)
