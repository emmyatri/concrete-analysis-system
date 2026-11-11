# ADR-007: Report Generation, Export, and Cloud Storage

- **Status:** Proposed — leads approved November 11, 2026; cloud folder pending Don's confirmation (M3-80)
- **Decision:** WebView2 PDF generation, CSV export, and a configurable cloud sync folder
- **Requirements:** FR-19, NFR-3, SC-6, SLA 1.2, SLA Section 4

---

## 1. Context

CBAS must allow operators to generate PDF reports, export analysis data, and upload reports to a cloud storage platform.

The application must remain functional without an internet connection (NFR-3). Additionally, all third-party dependencies must comply with the licensing requirements established in SLA Section 4, including commercial use by an organization of Vulcan Materials' size.

ADR-004 established Angular with PrimeNG hosted inside Microsoft Edge WebView2 as the application's user interface, with WPF responsible for hosting the desktop application. This architecture provides an existing browser rendering engine capable of producing PDF documents without introducing another dependency.

The team considered two approaches for PDF generation:

- **WebView2 PrintToPdfAsync.** Uses the existing WebView2 runtime to render an Angular report page into a PDF. Supports offline generation and avoids additional PDF library licensing.
- **QuestPDF.** Provides a native .NET PDF generation API with precise document layout controls. However, its Community License is restricted by organization revenue and other eligibility requirements. Vulcan would require an appropriate commercial license.

Cloud storage introduces a separate architectural consideration. Direct integration with Microsoft OneDrive or Google Drive would require provider-specific authentication, API integration, token management, and additional testing.

An alternative is to use the cloud synchronization clients already available on the operator's workstation.

## 2. Decision

The team will use **WebView2 for PDF generation, CSV for structured data export, and a configurable local cloud synchronization folder for cloud storage**.

CSV was chosen over native Excel (`.xlsx`) so the export opens in Excel without adding any dependency. The cloud synchronization approach remains subject to confirmation from Don.

### 2.1 PDF Generation

PDF reports will be generated using Microsoft Edge WebView2's built-in `PrintToPdfAsync` functionality, with `CoreWebView2PrintSettings` set to US Letter (8.5 × 11 in), 0.5 in margins, and `ShouldPrintBackgrounds = true` so warning colors and card backgrounds print as they appear on screen.

The Angular application will provide a dedicated report page containing everything FR-19 requires: the batch record, the strength chart, the 3-, 7- and 28-day predictions with their basis, and the warnings raised for the batch. Synthetic batches are labeled as synthetic on every page (A-2).

The WPF host will invoke WebView2's PDF functionality to save the rendered report to the operator's selected destination. Printing uses WebView2's print dialog (`ShowPrintUI`) on the same report page.

- Reports will reuse existing Angular components and chart rendering.
- Print-specific CSS will control page dimensions, margins, page breaks, and content visibility.
- PDF generation will occur locally without requiring internet connectivity.
- The application will wait for report content and charts to finish rendering before initiating PDF generation.
- Failed PDF generation or file writes will produce an appropriate error message and log entry.
- No additional PDF generation library will be introduced.

The generated PDF will represent the report as rendered in WebView2 rather than requiring a separate document layout implementation.

### 2.2 Data Export

The data export format is UTF-8 CSV.

CSV files will contain structured analysis data that can be opened using Microsoft Excel or other spreadsheet applications.

- Exported values will use consistent formatting and the canonical measurement units established in ADR-003.
- Column headers will identify the exported measurements and units.
- CSV exports will contain data rather than formatted report layouts.
- Exports will be generated locally and remain available offline.
- Exported text fields will be escaped appropriately, including protection against spreadsheet formula injection.

Native Excel workbooks were considered. They would need a library (Microsoft's Open XML SDK is free under the MIT license; EPPlus requires a paid license), so CSV was chosen to keep the export simple and dependency-free.

### 2.3 Cloud Storage

The proposed cloud storage implementation uses a configurable local synchronization folder.

Operators will configure a directory associated with an existing cloud synchronization client, such as Microsoft OneDrive or Google Drive for desktop.

When an operator selects the cloud save action, CBAS will write the generated report to the configured directory.

The external synchronization client will be responsible for uploading the file to its associated cloud storage platform.

- CBAS will not implement Microsoft or Google authentication.
- CBAS will not communicate directly with cloud storage APIs.
- No cloud credentials or access tokens will be stored by CBAS.
- Operators must install, configure, and authenticate their cloud synchronization client independently.
- CBAS will validate that the configured destination exists and is writable before saving.
- The application will report successful local saving but will not claim that cloud synchronization has completed.

The synchronization client is responsible for transferring locally saved files when connectivity becomes available.

**Pending confirmation from Don:** Whether saving to a locally synchronized folder satisfies the requirement to upload reports to a cloud storage platform.

### 2.4 Offline Operation (NFR-3)

Report generation and export must remain functional without internet connectivity.

- PDF reports will be generated entirely through the locally installed WebView2 runtime.
- CSV exports will be generated locally.
- Reports can be saved to ordinary local directories without a synchronization client.
- Files can be written to a configured synchronization directory while offline, provided the directory remains locally available and writable.
- Cloud synchronization is handled independently and may occur after connectivity returns.

A synchronization failure must not prevent operators from generating reports or exporting data locally.

### 2.5 Configuration and Error Handling

The application will provide a configurable destination for cloud-synchronized reports.

The configured path will be stored locally as an application setting.

If the destination is missing, inaccessible, or not writable, CBAS will display an error and allow the operator to select another destination.

The application will distinguish between:

- A report successfully generated and saved locally.
- A report that could not be generated or saved.
- Cloud synchronization status, which CBAS cannot independently verify.

The application will not infer successful cloud upload from a successful local file write.

### 2.6 Versions and Licensing

WebView2 is already included in the application architecture under ADR-004.

Its built-in PDF generation functionality does not require introducing a separate third-party PDF library.

QuestPDF was considered as an alternative. Its Community License has eligibility restrictions that exclude organizations of Vulcan Materials' size. Using QuestPDF would therefore require a suitable commercial license.

The team will not introduce QuestPDF or another commercial PDF dependency without additional review and approval.

CSV export will use existing .NET functionality without requiring a separate spreadsheet generation library.

### 2.7 Security

Report generation and file export will operate locally.

- CBAS will not collect or store cloud authentication credentials.
- Files will be written only to operator-selected or configured local destinations.
- Local file system permissions will determine whether CBAS can access the selected directory.
- Report content will not be transmitted directly to external services by CBAS. Saving to a cloud folder is the operator's choice on Vulcan's own storage, which SLA 1.2 allows; CBAS never sends data anywhere itself, consistent with SC-6.
- Cloud synchronization clients remain responsible for their own authentication, encryption, and transfer behavior.

Operators and administrators remain responsible for ensuring that configured cloud storage destinations are authorized for company data.

## 3. Consequences

| Positive Consequences | Trade-offs |
| --- | --- |
| Reuses the existing WebView2 runtime for PDF generation | PDF layout requires print-specific CSS and testing |
| Avoids additional PDF generation dependencies and commercial licensing costs | Reports depend on WebView2 rendering behavior |
| Reuses Angular report components and existing charts | Charts must finish rendering before PDF generation |
| PDF and CSV exports work without internet connectivity | CSV does not provide native Excel formatting or embedded charts |
| Avoids implementing cloud authentication and provider-specific APIs | Cloud synchronization requires an independently configured client |
| Supports both OneDrive and Google Drive synchronization folders | CBAS cannot independently confirm successful cloud upload |
| Keeps report generation and export within the existing desktop architecture | Missing or inaccessible synchronization folders require error handling |

### Effect on Other Documents

- ADR-004 remains the authority for Angular and WebView2 hosting.
- ADR-003 remains the authority for canonical measurement units used in reports and exports.
- Report implementation tickets will reference ADR-007 for PDF generation and export behavior.
- Cloud storage implementation tickets will reference ADR-007 for synchronization folder configuration and local save behavior.

### Future Changes

If Vulcan later requires direct cloud integration, the application can introduce provider-specific APIs and authentication without replacing the local PDF generation mechanism.

If native Excel workbooks become necessary, Microsoft's Open XML SDK (MIT license) can produce them without licensing cost.

If report formatting requirements exceed WebView2's printing capabilities, alternative PDF generation libraries may be reconsidered subject to licensing approval.

## 4. Status

**Proposed** — leads approved November 11, 2026. Implementation (M3-31 to M3-34) proceeds on this decision. The cloud sync folder is pending Don's confirmation (M3-80); a different answer means revising this ADR.

## References

- [ADR-002: In-Process ASP.NET Core API](002-in-process-api.md)
- [ADR-003: Canonical Measurement Units](003-canonical-units.md)
- [ADR-004: Angular UI Hosted in WebView2](004-angular-webview2.md)
- [Microsoft WebView2 PrintToPdfAsync documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.printtopdfasync)
- [QuestPDF Licensing](https://www.questpdf.com/license/)
- SLA: Section 1.2 (cloud upload option), Section 4 (third-party licensing), NFR-3 (offline operation), SC-6 (data stays with Vulcan)
- M3-30: Decide how reports become PDFs and reach the cloud
- M3-80: Client clarification and approval questions
