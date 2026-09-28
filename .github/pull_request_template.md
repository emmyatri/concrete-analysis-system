## Jira ticket

SCRUM-

## What this changes

<!-- 1–3 sentences in plain language. What can the app do now that it couldn't before? -->

## Requirements covered

<!-- From the Jira ticket, e.g. FR-2.1, NFR-6. Write N/A for setup or docs-only work. -->

## How to test it

<!-- Steps a reviewer can follow. Example:
1. Run the app
2. Open New Batch
3. Enter cement 9.99% and click Save → error shows "Cement must be 10–15%"
-->

1.

## Screenshots

<!-- Required if you changed a screen. Drag images here. Otherwise write N/A. -->

## Definition of Done

- [ ] Base branch is `dev`
- [ ] Title follows `type(SCRUM-##): description`
- [ ] Tests pass on GitHub (green check)
- [ ] Every validation rule I touched has edge tests (min, max, just below, just above)
- [ ] Requirement numbers are listed above
- [ ] Screenshots are included for screen changes
- [ ] No hard-coded limits, and validation is only in `Vulcan.Domain`
- [ ] No real Vulcan data, `.db` files, `bin/` or `obj/` in this PR
- [ ] I re-read the ticket's "Done when" and it's met
