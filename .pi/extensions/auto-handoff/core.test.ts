import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import test, { mock } from "node:test";
import {
	compactAndWriteHandoff,
	HandoffScheduler,
	hasMeaningfulConversation,
	shouldAttemptIdleCompaction,
	shouldInjectHandoff,
	shouldSaveOnShutdown,
	writeHandoffAtomically,
} from "./core.ts";

const injectionDefaults = {
	reason: "new" as const,
	hasUserMessages: false,
	alreadyInjected: false,
	hasHandoffContent: true,
};

test("new sessions inject an existing handoff", () => {
	assert.equal(shouldInjectHandoff(injectionDefaults), true);
});

test("startup injects only when no user messages exist", () => {
	assert.equal(shouldInjectHandoff({ ...injectionDefaults, reason: "startup" }), true);
	assert.equal(
		shouldInjectHandoff({ ...injectionDefaults, reason: "startup", hasUserMessages: true }),
		false,
	);
});

test("resume, reload, and fork never inject a handoff", () => {
	for (const reason of ["resume", "reload", "fork"] as const) {
		assert.equal(shouldInjectHandoff({ ...injectionDefaults, reason }), false);
	}
});

test("repeated runs do not inject twice", () => {
	assert.equal(shouldInjectHandoff({ ...injectionDefaults, alreadyInjected: true }), false);
});

test("missing or blank handoff content is not injected", () => {
	assert.equal(shouldInjectHandoff({ ...injectionDefaults, hasHandoffContent: false }), false);
});

test("only user or assistant messages make a conversation meaningful", () => {
	assert.equal(hasMeaningfulConversation([{ type: "message", message: { role: "user" } }]), true);
	assert.equal(hasMeaningfulConversation([{ type: "message", message: { role: "assistant" } }]), true);
	assert.equal(
		hasMeaningfulConversation([
			{ type: "message", message: { role: "toolResult" } },
			{ type: "custom_message" },
		]),
		false,
	);
});

test("only quit triggers a shutdown save", () => {
	assert.equal(shouldSaveOnShutdown("quit", false), true);
	for (const reason of ["reload", "new", "resume", "fork"]) {
		assert.equal(shouldSaveOnShutdown(reason, false), false);
	}
});

test("a prior switch save suppresses a duplicate shutdown save", () => {
	assert.equal(shouldSaveOnShutdown("quit", true), false);
});

test("atomic write creates and replaces HANDOFF.md without temporary files", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));

	const handoffPath = resolve(directory, "HANDOFF.md");
	assert.equal(await writeHandoffAtomically(directory, "# First"), handoffPath);
	assert.equal(await readFile(handoffPath, "utf8"), "# First");

	assert.equal(await writeHandoffAtomically(directory, "# Replacement"), handoffPath);
	assert.equal(await readFile(handoffPath, "utf8"), "# Replacement");
	assert.deepEqual(await readdir(directory), ["HANDOFF.md"]);
});

test("blank summaries preserve an existing handoff", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));
	const handoffPath = resolve(directory, "HANDOFF.md");
	await writeFile(handoffPath, "# Keep me", "utf8");

	await assert.rejects(writeHandoffAtomically(directory, "  \n"), /blank/i);
	assert.equal(await readFile(handoffPath, "utf8"), "# Keep me");
});

test("compaction summary is written to HANDOFF.md", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));

	const handoffPath = resolve(directory, "HANDOFF.md");
	const result = await compactAndWriteHandoff({
		cwd: directory,
		compact: ({ onComplete }) => {
			onComplete({ summary: "# Compacted" });
		},
	});

	assert.equal(result, handoffPath);
	assert.equal(await readFile(handoffPath, "utf8"), "# Compacted");
});

test("compaction callback failure preserves the existing handoff", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));
	const handoffPath = resolve(directory, "HANDOFF.md");
	await writeFile(handoffPath, "# Keep me", "utf8");

	await assert.rejects(
		compactAndWriteHandoff({
			cwd: directory,
			compact: ({ onError }) => {
				onError(new Error("failed"));
			},
		}),
		/failed/,
	);
	assert.equal(await readFile(handoffPath, "utf8"), "# Keep me");
});

test("synchronous compaction failure preserves the existing handoff", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));
	const handoffPath = resolve(directory, "HANDOFF.md");
	await writeFile(handoffPath, "# Keep me", "utf8");

	await assert.rejects(
		compactAndWriteHandoff({
			cwd: directory,
			compact: () => {
				throw new Error("sync failed");
			},
		}),
		/sync failed/,
	);
	assert.equal(await readFile(handoffPath, "utf8"), "# Keep me");
});

test("blank compaction summary preserves the existing handoff", async (t) => {
	const directory = await mkdtemp(resolve(tmpdir(), "auto-handoff-"));
	t.after(() => rm(directory, { recursive: true, force: true }));
	const handoffPath = resolve(directory, "HANDOFF.md");
	await writeFile(handoffPath, "# Keep me", "utf8");

	await assert.rejects(
		compactAndWriteHandoff({
			cwd: directory,
			compact: ({ onComplete }) => {
				onComplete({ summary: "  " });
			},
		}),
		/blank/i,
	);
	assert.equal(await readFile(handoffPath, "utf8"), "# Keep me");
});

test("idle compaction requires new content, idle state, no pending messages, and more than 100k tokens", () => {
	const defaults = {
		tokens: 100001,
		hasNewContent: true,
		isIdle: true,
		hasPendingMessages: false,
	};
	for (const tokens of [null, undefined, 99999, 100000]) {
		assert.equal(shouldAttemptIdleCompaction({ ...defaults, tokens }), false);
	}
	assert.equal(shouldAttemptIdleCompaction(defaults), true);
	assert.equal(shouldAttemptIdleCompaction({ ...defaults, hasNewContent: false }), false);
	assert.equal(shouldAttemptIdleCompaction({ ...defaults, isIdle: false }), false);
	assert.equal(shouldAttemptIdleCompaction({ ...defaults, hasPendingMessages: true }), false);
});

test("scheduler runs once after the idle delay", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let calls = 0;
	const scheduler = new HandoffScheduler({
		idleMs: 300000,
		shouldSave: () => true,
		save: async () => {
			calls++;
		},
	});

	scheduler.enterIdle();
	await timers.tick(299999);
	assert.equal(calls, 0);
	await timers.tick(1);
	assert.equal(calls, 1);
	await timers.tick(600000);
	assert.equal(calls, 1);
	await scheduler.stop();
});

test("entering busy cancels a pending idle run", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let calls = 0;
	const scheduler = new HandoffScheduler({
		idleMs: 300000,
		shouldSave: () => true,
		save: async () => {
			calls++;
		},
	});

	scheduler.enterIdle();
	await timers.tick(1000);
	scheduler.enterBusy();
	await timers.tick(600000);
	assert.equal(calls, 0);
	await scheduler.stop();
});

test("repeated idle entries restart the delay", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let calls = 0;
	const scheduler = new HandoffScheduler({
		idleMs: 300000,
		shouldSave: () => true,
		save: async () => {
			calls++;
		},
	});

	scheduler.enterIdle();
	await timers.tick(200000);
	scheduler.enterIdle();
	await timers.tick(299999);
	assert.equal(calls, 0);
	await timers.tick(1);
	assert.equal(calls, 1);
	await scheduler.stop();
});

test("a rejected idle check does not create a periodic retry", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let allowed = false;
	let calls = 0;
	const scheduler = new HandoffScheduler({
		idleMs: 300000,
		shouldSave: () => allowed,
		save: async () => {
			calls++;
		},
	});

	scheduler.enterIdle();
	await timers.tick(300000);
	assert.equal(calls, 0);
	allowed = true;
	await timers.tick(600000);
	assert.equal(calls, 0);
	scheduler.enterIdle();
	await timers.tick(300000);
	assert.equal(calls, 1);
	await scheduler.stop();
});

test("stop cancels a pending timer", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let calls = 0;
	const scheduler = new HandoffScheduler({
		idleMs: 1000,
		shouldSave: () => true,
		save: async () => {
			calls++;
		},
	});

	scheduler.enterIdle();
	await scheduler.stop();
	await timers.tick(1000);
	assert.equal(calls, 0);
});

test("stop awaits an in-flight save", async (t) => {
	const timers = mock.timers;
	timers.enable({ apis: ["setTimeout"] });
	t.after(() => timers.reset());
	let resolveSave!: () => void;
	const scheduler = new HandoffScheduler({
		idleMs: 1000,
		shouldSave: () => true,
		save: () => {
			return new Promise<void>((resolve) => {
				resolveSave = resolve;
			});
		},
	});

	scheduler.enterIdle();
	await timers.tick(1000);
	let stopped = false;
	const stopping = scheduler.stop().then(() => {
		stopped = true;
	});
	await Promise.resolve();
	assert.equal(stopped, false);

	resolveSave();
	await stopping;
	assert.equal(stopped, true);
});
