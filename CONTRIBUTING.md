# Contributing to Vulcan CBAS

Read this before your first pull request. It's short on purpose.

---

## The workflow (every ticket, every time)

1. **Pick up your ticket.** Make sure it's assigned to you in Jira.
2. **Start from the latest `dev`.**
   ```powershell
   git checkout dev
   git pull
   ```
3. **Create your branch** with the ticket number in it (see naming below), then push it right away.
   ```powershell
   git checkout -b feat/SCRUM-42-batch-form
   git push -u origin feat/SCRUM-42-batch-form
   ```
   Jira moves your ticket to **In Progress** automatically.
4. **Do the work.** Commit small and often. Push at least once a day so the team can see progress.
5. **Open a pull request into `dev`** (not `main`). Fill in the template that appears.
   Jira moves your ticket to **In Review** automatically.
6. **Fix review comments** by pushing more commits to the same branch. Don't open a new PR.
7. **A lead merges it.** Jira moves your ticket to **Done** automatically.

You can't push directly to `main` or `dev`. GitHub will reject it. Everything goes through a pull request.

---

## Branch names

```
<type>/SCRUM-<number>-<short-name>
```

| Type    | Use it for                  | Example                      |
|---------|-----------------------------|------------------------------|
| `feat`  | New features or screens     | `feat/SCRUM-42-batch-form`   |
| `fix`   | Bug fixes                   | `fix/SCRUM-57-ph-message`    |
| `docs`  | Documentation only          | `docs/SCRUM-86-schema`       |
| `test`  | Adding or fixing tests only | `test/SCRUM-61-oxide-edges`  |
| `chore` | Setup, config, CI, packages | `chore/SCRUM-88-ci-workflow` |

Rules:

- **`SCRUM-##` must be in capitals.** Lowercase `scrum-42` won't link to Jira.
- Short name: lowercase, words joined with dashes, 2–4 words.
- One branch per ticket. If you find a second problem, it gets its own ticket.

---

## Commit messages

We use [Conventional Commits](https://www.conventionalcommits.org) with the ticket number in parentheses:

```
<type>(SCRUM-<number>): <what you did, lowercase, present tense>
```

Examples:

```
feat(SCRUM-42): add batch entry form
fix(SCRUM-57): show allowed range in pH error message
test(SCRUM-61): add boundary tests for MgO limits
docs(SCRUM-86): add approved data schema
```

- The type matches your branch type.
- Write what the commit **does**, not what you did today. "add batch form," not "worked on stuff."
- The ticket number is what shows your commit on the Jira ticket. Don't leave it out.
- ALWAYS Run `dotnet format` before you push. **CI fails on formatting.**

---

## Pull requests

- **Title:** same format as a commit: `feat(SCRUM-42): add batch entry form`
- **Base branch: `dev`.** GitHub sometimes defaults to `main`, so check it before you click Create.
- **Fill in every section of the template.** Write "N/A" if something doesn't apply. Don't delete the section.
- **Keep it small.** One ticket per PR. If your PR changes more than ~400 lines, talk to a lead about splitting it.
- **Screen changes need a screenshot.** Reviewers can't run every branch.

### Reviews

- Every PR needs **one approval from a lead** (Amelia or Tyler). GitHub adds them automatically.
- You can't approve your own PR. Leads review each other's.
- Leads aim to review within **one business day**. If yours has waited longer, ping in the team channel.
- Reply to every comment, either by fixing it or explaining why not. The PR can't merge until all conversations are
  resolved.

### Merging

- Leads merge with **Squash and merge**, so each ticket becomes one clean commit on `dev`.
- The branch is deleted after merge. Start your next ticket from a fresh `dev`.

---

## Definition of Done

A ticket is **Done** only when all of these are true:

1. **Tests pass on GitHub.** The green check on the PR, not just on your machine.
2. **Every validation rule you touched has edge tests.** Test the minimum, the maximum, just below, and just above.
   Example for cement 10–15%: test 9.99 (rejected), 10 (saved), 15 (saved), 15.01 (rejected).
3. **A lead approved the PR.**
4. **Requirement numbers are listed** in the PR (e.g. FR-2.1, NFR-6). They're on the Jira ticket.
5. **Screen changes have a screenshot** in the PR.
6. **The ticket's "Done when" is met.** Read it again before you open the PR.

---

## Project rules

These are the ones reviewers will send PRs back for.

- **Validation rules live in `Vulcan.Domain` and the `ValidationLimit` data.** Screens may read `ValidationLimit` for instant feedback, but never hard-code a range. The server always checks again.
- **Never hard-code a limit.** Ranges come from the `ValidationLimit` table. If you type `15` or `3000` in a rule, it's
  wrong.
- **The schema is locked.** Need a new or renamed field? Open a PR that changes `docs/schema.md` first, and write the
  code after it's approved.
- **No real Vulcan data in the repo, ever.** Only synthetic data, in `data/synthetic/`. This is in our contract.
- **Ask a lead before adding a NuGet package.**
- **Don't commit `bin/`, `obj/`, `.db` files, or anything in `.gitignore`.** If `git status` shows them, stop and ask.
- **Line endings are LF.** `.gitattributes` and `.editorconfig` handle it, so don't change them. If `git status` shows files you didn't touch, check with `git diff --ignore-cr-at-eol`. If that's empty, it's line-ending noise: `git restore <file>` clears it.

---

## Keeping your branch up to date

If `dev` moved on while you were working (GitHub shows "This branch is out-of-date"):

```powershell
git checkout dev
git pull
git checkout <your-branch>
git merge dev
git push
```

If you get a merge conflict and don't know which side to keep, **stop and ask a lead**. Don't guess.

---

## Things you should never do

- `git push --force` on a branch someone else is using
- Commit directly to `main` or `dev` (you can't, but don't try to get around it)
- Merge your own PR
- Delete a teammate's branch

---

## Stuck?

Ask in the team channel. Include the ticket number, what you tried, and the exact error message or a screenshot. If
you've been stuck for more than an hour, you've waited too long to ask.
