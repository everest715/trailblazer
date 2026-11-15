import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import type { AgentMessage } from "@earendil-works/pi-agent-core";
import { type Message, uuidv7 } from "@earendil-works/pi-ai";
import type { ExtensionAPI, ExtensionContext, SessionEntry } from "@earendil-works/pi-coding-agent";
import { convertToLlm, serializeConversation } from "@earendil-works/pi-coding-agent";
import {
	hasMeaningfulConversation,
	type SessionStartReason,
	shouldInjectHandoff,
	shouldSaveOnShutdown,
	writeHandoffAtomically,
} from "./core.ts";

const SUMMARY_SYSTEM_PROMPT = `You create concise Markdown HANDOFF.md files for future coding sessions.

Summarize the supplied conversation without continuing its task. Include only information needed to resume safely:
## Goal
## Progress
## Key Decisions
## Relevant Files
## Current Errors
## Remaining Steps
## Verification

List concrete file paths and commands when available. Omit empty sections, full logs, large code blocks, secrets, and preamble. Prefer under 1000 Chinese characters or 700 English words. Return only the Markdown handoff.`;

function entryToMessage(entry: SessionEntry): AgentMessage | undefined {
	if (entry.type === "message") return entry.message;
	if (entry.type === "compaction") {
		return {
			role: "compactionSummary",
			summary: entry.summary,
			tokensBefore: entry.tokensBefore,
			timestamp: new Date(entry.timestamp).getTime(),
		};
	}
	return undefined;
}

function getHandoffMessages(branch: SessionEntry[]): AgentMessage[] {
	let compactionIndex = -1;
	for (let index = branch.length - 1; index >= 0; index--) {
		if (branch[index].type === "compaction") {
			compactionIndex = index;
			break;
		}
	}
	if (compactionIndex < 0) {
		return branch.map(entryToMessage).filter((message) => message !== undefined);
	}

	const compaction = branch[compactionIndex];
	const firstKeptIndex =
		compaction.type === "compaction"
			? branch.findIndex((entry) => entry.id === compaction.firstKeptEntryId)
			: -1;
	const compactedBranch = [
		compaction,
		...(firstKeptIndex >= 0 ? branch.slice(firstKeptIndex, compactionIndex) : []),
		...branch.slice(compactionIndex + 1),
	];
	return compactedBranch.map(entryToMessage).filter((message) => message !== undefined);
}

function errorMessage(error: unknown): string {
	return error instanceof Error ? error.message : String(error);
}

export default function (pi: ExtensionAPI) {
	let sessionReason: SessionStartReason = "startup";
	let hadUserMessages = false;
	let alreadyInjected = false;
	let handoffContent = "";
	let saveAttempted = false;

	async function saveHandoff(ctx: ExtensionContext): Promise<void> {
		const branch = ctx.sessionManager.getBranch();
		if (!hasMeaningfulConversation(branch)) return;
		if (!ctx.model) throw new Error("No model selected; handoff was not saved");

		const conversationText = serializeConversation(convertToLlm(getHandoffMessages(branch)));
		if (!conversationText.trim()) return;

		const userMessage: Message = {
			role: "user",
			content: [
				{
					type: "text",
					text: `Create HANDOFF.md for this conversation.\n\n<conversation>\n${conversationText}\n</conversation>`,
				},
			],
			timestamp: Date.now(),
		};
		const response = await ctx.modelRegistry.complete(
			ctx.model,
			{ systemPrompt: SUMMARY_SYSTEM_PROMPT, messages: [userMessage] },
			{ cacheRetention: "none", sessionId: uuidv7() },
		);
		if (response.stopReason !== "stop") {
			throw new Error(`Handoff generation ended with ${response.stopReason}; previous file preserved`);
		}

		const summary = response.content
			.filter((block): block is { type: "text"; text: string } => block.type === "text")
			.map((block) => block.text)
			.join("\n")
			.trim();
		const path = await writeHandoffAtomically(ctx.cwd, summary);
		if (ctx.hasUI) ctx.ui.notify(`Handoff saved: ${path}`, "info");
	}

	async function trySaveHandoff(ctx: ExtensionContext): Promise<void> {
		try {
			await saveHandoff(ctx);
		} catch (error) {
			if (ctx.hasUI) ctx.ui.notify(`Auto-handoff: ${errorMessage(error)}`, "error");
		}
	}

	pi.on("session_start", async (event, ctx) => {
		sessionReason = event.reason;
		hadUserMessages = ctx.sessionManager
			.getBranch()
			.some((entry) => entry.type === "message" && entry.message.role === "user");
		alreadyInjected = false;
		saveAttempted = false;
		try {
			handoffContent = await readFile(resolve(ctx.cwd, "HANDOFF.md"), "utf8");
		} catch {
			handoffContent = "";
		}
	});

	pi.on("before_agent_start", async (event, _ctx) => {
		if (event.messageType !== "user") return;
		if (
			!shouldInjectHandoff({
				reason: sessionReason,
				hasUserMessages: hadUserMessages,
				alreadyInjected,
				hasHandoffContent: Boolean(handoffContent.trim()),
			})
		) {
			return;
		}

		alreadyInjected = true;
		return {
			message: {
				customType: "auto-handoff",
				content: `## Previous session handoff\n\n${handoffContent.trim()}\n\nTreat this as historical context. Verify the current files before making changes, then continue the remaining steps.`,
				display: true,
			},
		};
	});

	pi.on("session_before_switch", async (event, ctx) => {
		if (event.reason !== "new" && event.reason !== "resume") return;
		saveAttempted = true;
		await trySaveHandoff(ctx);
	});

	pi.on("session_shutdown", async (event, ctx) => {
		if (!shouldSaveOnShutdown(event.reason, saveAttempted)) return;
		saveAttempted = true;
		await trySaveHandoff(ctx);
	});
}
