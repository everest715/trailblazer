# Pi Auto Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install a project-local pi extension that atomically saves `HANDOFF.md` on session switches or normal exit and injects it once into a subsequent new session.

**Architecture:** A small `core.ts` contains dependency-free decisions and filesystem operations tested with Node's built-in test runner. `index.ts` adapts those utilities to pi lifecycle events, generates summaries with the current model, and injects the saved handoff through `before_agent_start` without changing the system prompt.

**Tech Stack:** TypeScript, pi 1.5.2 extension API, Node 24 built-ins (`node:test`, `node:fs`, `node:path`).

**Spec:** `docs/superpowers/specs/2026-11-16-pi-auto-handoff-design.md`

## Global Constraints

- Create files only under `.pi/extensions/auto-handoff/` plus the design and plan documents; do not modify existing Unity assets.
- Save only on `session_before_switch` for `new`/`resume` and `session_shutdown` for `quit`; never save after every turn.
- Empty sessions, blank summaries, and model failures must preserve an existing `HANDOFF.md`.
- Inject once for `new`, or for `startup` with no existing user messages; never inject for `resume`, `reload`, or `fork`.
- Do not modify the system prompt or add npm dependencies.

## Review Focus

- Missing or blank `HANDOFF.md`: continue normally without injection; pinned by Task 1 injection-decision tests.
- Startup into an existing session: do not duplicate old context; pinned by Task 1 startup/reason tests.
- Repeated `before_agent_start` events: inject at most once; pinned by Task 1 `alreadyInjected` test.
- Switch followed by shutdown: make only one save attempt; pinned by Task 1 shutdown-decision tests and Task 2 event wiring.
- Blank summary or failed generation: preserve the previous file; pinned by Task 1 atomic-write tests and Task 2 validation before writing.

---

### Task 1: Safe Handoff Core

**Files:**
- Create: `.pi/extensions/auto-handoff/core.ts`
- Test: `.pi/extensions/auto-handoff/core.test.ts`

**Interfaces:**
- Produces: `shouldInjectHandoff(options: { reason: "startup" | "reload" | "new" | "resume" | "fork"; hasUserMessages: boolean; alreadyInjected: boolean; hasHandoffContent: boolean }): boolean`.
- Produces: `hasMeaningfulConversation(entries: Array<{ type: string; message?: { role?: string } }>): boolean`.
- Produces: `shouldSaveOnShutdown(reason: string, saveAttempted: boolean): boolean`.
- Produces: `writeHandoffAtomically(cwd: string, summary: string): Promise<string>`, resolving to the absolute handoff path and rejecting blank summaries.

- [ ] **Step 1: Write the failing tests**

Use `node:test` and `node:assert/strict`. Cover allowed/disallowed injection reasons, existing user messages, repeated injection, missing content, user/assistant conversation detection, quit-only shutdown saves, duplicate-save suppression, atomic create/replace, and rejection of blank summaries without changing an existing file. Use a temporary directory for filesystem tests.

- [ ] **Step 2: Run tests to verify they fail**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`

Expected: FAIL because `core.ts` does not exist.

- [ ] **Step 3: Implement the core utilities**

Implement the exact interfaces above. Write to a temporary file in the same directory, then rename it over `HANDOFF.md`; clean up the temporary file on failure. Return the absolute path. Keep all pi imports out of this file.

- [ ] **Step 4: Run tests to verify they pass**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`

Expected: PASS for all tests.

- [ ] **Step 5: Commit the tested core and planning documents**

```bash
git add .pi/extensions/auto-handoff/core.ts .pi/extensions/auto-handoff/core.test.ts docs/superpowers/specs/2026-11-16-pi-auto-handoff-design.md docs/superpowers/plans/2026-11-16-pi-auto-handoff.md
git commit -m "feat(pi): add safe automatic handoff core"
```

### Task 2: Pi Lifecycle Integration

**Files:**
- Create: `.pi/extensions/auto-handoff/index.ts`
- Consumes: `.pi/extensions/auto-handoff/core.ts`

**Interfaces:**
- Consumes: all Task 1 exports.
- Produces: default pi extension factory registering `session_start`, `session_before_switch`, `session_shutdown`, and `before_agent_start` handlers.

- [ ] **Step 1: Run the extension smoke test to verify it fails**

Run: `pi -e ./.pi/extensions/auto-handoff/index.ts --print "Reply only OK"`

Expected: FAIL because the extension entry point does not exist.

- [ ] **Step 2: Implement the pi adapter**

Follow the installed `examples/extensions/handoff.ts` pattern: serialize the current branch with `convertToLlm` and `serializeConversation`, including the latest compaction boundary. Generate a concise Markdown handoff with `ctx.modelRegistry.complete(ctx.model, ...)`, `cacheRetention: "none"`, and a fresh `uuidv7()` session ID. Validate the response before calling `writeHandoffAtomically`.

On `session_start`, retain the reason and whether the branch already contains user messages. On the first eligible user-driven `before_agent_start`, read `HANDOFF.md`, return a visible `auto-handoff` custom message instructing the model to treat it as historical context and verify current files, then mark injection complete. Guard switch/shutdown saves with one `saveAttempted` flag; notify on successful saves and failures when UI is available.

- [ ] **Step 3: Run static and unit verification**

Run:

```bash
node --check .pi/extensions/auto-handoff/index.ts
node --test .pi/extensions/auto-handoff/core.test.ts
```

Expected: syntax check exits 0 and all unit tests PASS.

- [ ] **Step 4: Verify real save and automatic injection**

In a temporary directory, load the extension by absolute path and run a one-prompt pi session; confirm it creates a non-empty `HANDOFF.md`. Start a second independent session in that directory with a unique marker in the handoff and a prompt asking for that marker; confirm the response contains it. Confirm model failure and empty-session paths leave a pre-existing handoff unchanged through the Task 1 tests and code path review.

- [ ] **Step 5: Commit the installed extension**

```bash
git add .pi/extensions/auto-handoff/index.ts
git commit -m "feat(pi): automate handoff save and restore"
```
