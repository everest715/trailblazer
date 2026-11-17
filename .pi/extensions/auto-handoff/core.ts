import { rename, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

export type SessionStartReason = "startup" | "reload" | "new" | "resume" | "fork";

export const TIMED_HANDOFF_INTERVAL_MS = 300000;
export const TIMED_HANDOFF_TOKEN_THRESHOLD = 100000;

export function shouldAttemptIdleCompaction(options: {
	tokens: number | null | undefined;
	hasNewContent: boolean;
	isIdle: boolean;
	hasPendingMessages: boolean;
}): boolean {
	return (
		options.hasNewContent &&
		options.isIdle &&
		!options.hasPendingMessages &&
		typeof options.tokens === "number" &&
		Number.isFinite(options.tokens) &&
		options.tokens > TIMED_HANDOFF_TOKEN_THRESHOLD
	);
}

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

export function compactAndWriteHandoff(options: {
	cwd: string;
	compact: (callbacks: {
		onComplete: (result: { summary: string }) => void;
		onError: (error: Error) => void;
	}) => void;
}): Promise<string> {
	return new Promise<string>((resolve, reject) => {
		try {
			options.compact({
				onComplete: (result) => {
					void writeHandoffAtomically(options.cwd, result.summary).then(resolve, reject);
				},
				onError: reject,
			});
		} catch (error) {
			reject(error);
		}
	});
}

export class HandoffScheduler {
	private timer: ReturnType<typeof setTimeout> | undefined;
	private pending: Promise<void> | undefined;
	private readonly options: {
		idleMs: number;
		shouldSave: () => boolean;
		save: () => Promise<void>;
	};

	constructor(options: {
		idleMs: number;
		shouldSave: () => boolean;
		save: () => Promise<void>;
	}) {
		this.options = options;
	}

	enterIdle(): void {
		this.enterBusy();
		this.timer = setTimeout(() => {
			this.timer = undefined;
			if (this.options.shouldSave()) void this.save();
		}, this.options.idleMs);
		this.timer.unref();
	}

	enterBusy(): void {
		if (this.timer) {
			clearTimeout(this.timer);
			this.timer = undefined;
		}
	}

	private async save(): Promise<void> {
		if (this.pending) return this.pending;
		const pending = this.options.save();
		this.pending = pending;
		try {
			await pending;
		} finally {
			if (this.pending === pending) this.pending = undefined;
		}
	}

	async stop(): Promise<void> {
		this.enterBusy();
		await this.pending;
	}
}
