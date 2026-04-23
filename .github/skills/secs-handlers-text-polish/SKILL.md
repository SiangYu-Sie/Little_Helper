---
name: secs-handlers-text-polish
description: "Standardize and clean text/comments/log wording in SecsHostSimulator.Core/Handlers without changing message logic. Use when: mojibake cleanup, placeholder text cleanup, comment style unification, final readability pass on handlers. Includes safe workflow and validation checklist."
argument-hint: "Optional scope, e.g. 'l1-only', 'control-only', 'event-access-only', 'mainform-only', 'status-log-only', or handler scope like 's12-only' / 'all-handlers'"
---

# SECS Handlers Text Polish Skill

## Supported Scope Phrases

Canonical shared scopes:

- `l1-only`
- `control-only`
- `event-access-only`
- `comm-template-only`
- `mainform-only`
- `status-log-only`

Handler-specific scopes:

- `s12-only`
- `all-handlers`
- `placeholder-logs-only`

Legacy aliases (still understandable, prefer canonical):

- `S12 only` -> `s12-only`
- `all handlers` -> `all-handlers`
- `replace placeholder logs only` -> `placeholder-logs-only`

## Goal

Apply a final readability pass to handler files under `SecsHostSimulator.Core/Handlers` while preserving behavior.

This skill is **text-only**:

- fix garbled comments / mojibake text
- replace placeholder wording in log messages
- unify comment style (e.g., `secondary reply to SxFy`, `equipment-to-host`)
- keep method signatures, switch flow, and message payload logic unchanged

## Safety Rules

1. Never change SECs payload structure or message semantics.
2. Prefer comment/log wording updates only.
3. Avoid changing constants, enums, stream/function numbers, and reply conditions.
4. If accidental syntax break happens, immediately repair and re-run build.

## Recommended Workflow

1. Scan target files for text artifacts.

Example patterns:

- `[^\\x00-\\x7F]`
- `\\?\\?HOST`
- `Requestquipment`
- `normalized comment`
- `normalized message`

2. Fix in this order:

- broken XML summary markers first
- placeholder comments/log text
- remaining wording consistency

3. Keep style consistent:

- direction phrase: `equipment-to-host`
- secondary reply phrase: `secondary reply to SxFy`
- concise case comment format: `SxFy - Description`

4. Validate:

- `dotnet build SecsHostSimulator.slnx -v minimal`
- `dotnet test SecsHostSimulator.Tests/SecsHostSimulator.Tests.csproj --no-build -v minimal`

## Do / Don't

Do:

- update only comments and log literals
- preserve parameter placeholders in logs (e.g., `{Id}`, `{Ack}`)
- keep existing naming and public API unchanged

Don't:

- rewrite parser logic or payload item access paths
- change replyExpected behavior
- remove events or event invocation logic

## Minimal Completion Checklist

- No mojibake patterns remain in targeted files
- No placeholder tokens like `normalized ...` remain
- Build passes
- Tests pass
- Diff is text-focused and behavior-neutral
