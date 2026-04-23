---
name: winforms-ui-text-consistency
description: "Unify WinForms UI wording across labels/buttons/status/log hints without changing behavior. Use when: final UI text polish, mixed language cleanup, placeholder wording replacement, and terminology consistency for host/equipment simulator forms."
argument-hint: "Optional scope, e.g. 'l1-only', 'control-only', 'event-access-only', 'mainform-only', or 'status-log-only'"
---

# WinForms UI Text Consistency Skill

## Goal

Apply a final wording pass to WinForms UI files so text is consistent, readable, and behavior-neutral.

Supported scope phrases (canonical):

- `l1-only`
- `control-only`
- `event-access-only`
- `comm-template-only`
- `mainform-only`
- `status-log-only`

Legacy aliases (still understandable, prefer canonical):

- `Control Mode only` -> `control-only`
- `Event & Access only` -> `event-access-only`
- `Comm & Template only` -> `comm-template-only`
- `MainForm only` -> `mainform-only`
- `status/log text only` -> `status-log-only`

Target scope typically includes:

- `HostSimTester.App/MainForm.cs`
- `HostSimTester.App/Pages/*.cs`
- `HostSimTester.App/Dialogs/*.cs`
- `HostSimTester.App/Theme/*.cs` (only user-facing text)

## Safety Rules

1. Do not change event wiring, control names, or logic flow.
2. Prefer text-only edits (`Text`, status strings, log-display strings, tooltips, labels).
3. Keep localization direction consistent within one pass (either Chinese-first or English-first).
4. Preserve test assumptions for user-visible strings when tests assert exact text.

## Standardization Guidelines

- Direction phrase: `equipment-to-host`, `host-to-equipment`
- Reply phrase: `secondary reply to SxFy`
- Button text pairs: `Connect/Disconnect`, `Start/Stop`, `Enable/Disable`
- Status labels: `Connected`, `Disconnected`, `Ready`, `Running`, `Error`
- Confirmation prompts should be explicit and action-oriented

### L1 Initial Test Naming Baseline

When polishing L1 text, keep these labels and patterns stable unless user asks to rename:

- Tabs: `Comm & Template`, `Control Mode`, `Event & Access`
- Equipment mode actions: `Equipment Set Offline`, `Equipment Set Online`, `Equipment Set Local`, `Equipment Set Remote`
- Host mode actions: `Host Set Offline (S1F15)`, `Host Set Online (S1F17)`, `Host Set Local (S2F41)`, `Host Set Remote (S2F41)`
- Access mode actions: `Test AUTO (S3F27)`, `Test MANUAL (S3F27)`
- Progress/result wording should preserve SECS notation format `SxFy`

Preferred tone:

- concise test-instruction style (imperative or neutral)
- avoid marketing-style wording
- avoid mixed casing drift (`Auto` vs `AUTO`) within the same section

## Recommended Workflow

1. Scan for artifacts and inconsistency patterns:

- mojibake/non-ASCII noise where unintended
- placeholders like `normalized`, `TODO text`, `sample`
- inconsistent casing (e.g., `disconnect` vs `Disconnect`)

2. Update text in this order:

- critical status/alert messages
- button and label text
- helper/tooltip/hint text
- comments (if needed for maintainability)

3. Validate behavior remains unchanged:

- build host solution
- run host tests
- manually open main form and spot-check key UI flows if requested

## Do / Don't

Do:

- keep edits minimal and traceable
- use consistent terminology across tabs/forms
- retain placeholders like `{Id}` in formatted messages

Don't:

- refactor UI logic during wording-only pass
- rename controls/fields for this task
- alter command payload generation or dispatcher behavior

## Completion Checklist

- UI text is consistent for key actions and statuses
- no obvious placeholder/mojibake wording remains
- build passes
- tests pass
- only wording/comment diffs are included

## Project Notes

- This repository uses mixed Chinese/English context, but current L1 UI is English-first; keep that direction unless explicitly requested.
- Do not alter operation names used for logs/traceability (example: `L1Initial_Step6_DefineEventReport_S2F33`).
