import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import test from "node:test";
import {
	hasMeaningfulConversation,
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
