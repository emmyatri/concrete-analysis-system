# Frontend (Vulcan.Web)

The screens are an Angular 21 app using PrimeNG 21, in `src/Vulcan.Web/`. See [ADR-004](adr/004-angular-webview2.md) for why.

## Requirements

- **Node.js 24 LTS** (22.12 or later also works). Check with `node --version`.
- **npm 11** (comes with Node 24). If `npm install` fails with `reading 'edgesOut'`, you're on npm 10: switch to Node 24.

## Install

```powershell
cd src/Vulcan.Web
npm ci
```

## Run

```powershell
npm start    # http://localhost:4200, reloads as you save
```

VS Code users: open `src/Vulcan.Web` as the folder (not the repo root) to get the recommended extension and the "ng serve" debug launch.

## Test and lint

```powershell
npm test -- --watch=false
npm run lint
```

## Build

```powershell
npm run build    # output: src/Vulcan.Api/wwwroot/ (served by the API)
```

## Rules

- **Do not upgrade these packages past these major versions:** `primeng` 21, `@primeuix/themes` 2, `primeicons` 7. Newer versions are commercially licensed and would cost Vulcan money. Versions are pinned exactly in `package.json`. Ask a lead before changing any of them.
- **Do not run `npm audit fix --force`.** It downgrades Angular's build tools and breaks the pinned versions.
- **Nothing loads from the internet (NFR-3).** Fonts and icons are bundled from `node_modules` through `angular.json`. No CDN links, no Google Fonts.
- **The theme lives in one place:** `src/app/theme/vulcan-preset.ts`. Use `primary` colors from the theme, never a hard-coded `#263A78`.
- **Light mode only.** Dark mode is turned off in `app.config.ts`.
