import { rename, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

export type SessionStartReason = "startup" | "reload" | "new" | "resume" | "fork";

export function shouldInjectHandoff(options: {
	reason: SessionStartReason;
	hasUserMessages: boolean;
	alreadyInjected: boolean;
	hasHandoffContent: boolean;
}): boolean {
	if (options.alreadyInjected || !options.hasHandoffContent) return false;
	if (options.reason === "new") return true;
	return options.reason === "startup" && !options.hasUserMessages;
}

export function hasMeaningfulConversation(
	entries: Array<{ type: string; message?: { role?: string } }>,
): boolean {
	return entries.some(
		(entry) => entry.type === "message" && (entry.message?.role === "user" || entry.message?.role === "assistant"),
	);
}

export function shouldSaveOnShutdown(reason: string, saveAttempted: boolean): boolean {
	return reason === "quit" && !saveAttempted;
}

export async function writeHandoffAtomically(cwd: string, summary: string): Promise<string> {
	if (!summary.trim()) throw new Error("Cannot write a blank handoff summary");

	const handoffPath = resolve(cwd, "HANDOFF.md");
	const temporaryPath = `${handoffPath}.${process.pid}.${Date.now()}.tmp`;
	try {
		await writeFile(temporaryPath, summary, "utf8");
		await rename(temporaryPath, handoffPath);
		return handoffPath;
	} finally {
		await rm(temporaryPath, { force: true });
	}
}
