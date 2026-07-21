/**
 * LLM 어댑터
 *
 * PRD는 스택에 Claude API를 명시하지만 현재 확보된 키는 OpenAI다(D-020).
 * 프롬프트를 다시 짜지 않고 갈아끼울 수 있도록 호출부를 한 겹 감싼다.
 *
 * 규칙 (와이어프레임 A-08)
 *  · API 키는 서버에만 둔다. WebGL 클라에 절대 노출하지 않는다.
 *  · 타임아웃 30초 → 재시도 1회 → 실패해도 설문 응답은 보존한다.
 */

export interface LlmMessage {
  role: "system" | "user";
  content: string;
}

export interface LlmOptions {
  maxTokens?: number;
  temperature?: number;
  timeoutMs?: number;
  retries?: number;
  model?: string;
}

export interface Llm {
  /** JSON 객체 하나를 받아온다. 파싱까지 책임진다. */
  json<T>(messages: LlmMessage[], opts?: LlmOptions): Promise<T>;
}

export class LlmError extends Error {
  readonly detail?: unknown;
  constructor(message: string, detail?: unknown) {
    super(message);
    this.name = "LlmError";
    this.detail = detail;
  }
}

const DEFAULTS = { maxTokens: 1500, temperature: 0.7, timeoutMs: 30_000, retries: 1 };

/** 응답에서 JSON 객체만 뽑는다. 모델이 앞뒤로 말을 붙이는 경우를 대비한다. */
export function extractJson<T>(text: string): T {
  const trimmed = text.trim().replace(/^```(?:json)?\s*/i, "").replace(/```$/, "").trim();
  try {
    return JSON.parse(trimmed) as T;
  } catch {
    const s = trimmed.indexOf("{");
    const e = trimmed.lastIndexOf("}");
    if (s >= 0 && e > s) return JSON.parse(trimmed.slice(s, e + 1)) as T;
    throw new LlmError(`JSON 파싱 실패: ${trimmed.slice(0, 200)}`);
  }
}

async function withTimeout<T>(fn: (signal: AbortSignal) => Promise<T>, ms: number): Promise<T> {
  const ac = new AbortController();
  const timer = setTimeout(() => ac.abort(), ms);
  try {
    return await fn(ac.signal);
  } finally {
    clearTimeout(timer);
  }
}

export function openAI(apiKey: string, defaultModel = "gpt-4.1"): Llm {
  return {
    async json<T>(messages: LlmMessage[], opts: LlmOptions = {}): Promise<T> {
      const o = { ...DEFAULTS, ...opts };
      let lastErr: unknown;

      for (let attempt = 0; attempt <= o.retries; attempt++) {
        try {
          const text = await withTimeout(async (signal) => {
            const res = await fetch("https://api.openai.com/v1/chat/completions", {
              method: "POST",
              signal,
              headers: {
                "Content-Type": "application/json",
                Authorization: `Bearer ${apiKey}`,
              },
              body: JSON.stringify({
                model: o.model ?? defaultModel,
                messages,
                temperature: o.temperature,
                max_tokens: o.maxTokens,
                response_format: { type: "json_object" },
              }),
            });
            if (!res.ok) {
              throw new LlmError(`OpenAI HTTP ${res.status}: ${(await res.text()).slice(0, 200)}`);
            }
            const body = await res.json();
            const content = body?.choices?.[0]?.message?.content;
            if (typeof content !== "string") {
              throw new LlmError(`응답 형식이 예상과 다름: ${JSON.stringify(body).slice(0, 200)}`);
            }
            return content;
          }, o.timeoutMs);

          return extractJson<T>(text);
        } catch (e) {
          lastErr = e;
          // 마지막 시도였으면 그대로 던진다. 재시도는 1회뿐(A-08).
        }
      }
      throw new LlmError("LLM 호출 실패", lastErr);
    },
  };
}

/**
 * Claude로 되돌릴 때 이 함수만 채우면 된다.
 * PRD가 명시한 스택이므로 자리를 비워 둔다 (D-020).
 */
export function anthropic(_apiKey: string, _defaultModel = "claude-sonnet-5"): Llm {
  throw new LlmError("Anthropic 어댑터 미구현 — 키 확보 후 작성 (D-020)");
}

/** 환경변수에서 사용 가능한 프로바이더를 고른다. */
export function fromEnv(env: (k: string) => string | undefined): Llm {
  const openaiKey = env("OPENAI_API_KEY");
  if (openaiKey) return openAI(openaiKey, env("LLM_MODEL") ?? "gpt-4.1");
  const anthropicKey = env("ANTHROPIC_API_KEY");
  if (anthropicKey) return anthropic(anthropicKey, env("LLM_MODEL") ?? "claude-sonnet-5");
  throw new LlmError("LLM 키가 없습니다 (OPENAI_API_KEY 또는 ANTHROPIC_API_KEY)");
}
