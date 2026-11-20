import axios, { AxiosError } from 'axios'

/** Default timeout for API requests. Chat requests override it in chat.api.ts. */
export const DEFAULT_TIMEOUT_MS = 30000

/** How the request failed: an HTTP error response, or no response at all. */
export type ApiErrorKind = 'http' | 'timeout' | 'canceled' | 'network'

/**
 * Error body produced by the backend's ExceptionHandlingMiddleware
 * (ApiErrorResponse), plus the ProblemDetails fields ASP.NET Core emits
 * for model-binding failures and the rate limiter.
 */
interface ServerErrorBody {
    message?: string
    statusCode?: number
    correlationId?: string
    errors?: Record<string, string[] | string> | null
    title?: string
    detail?: string
    error?: string
    traceId?: string
    retryAfter?: number
    retryAfterSeconds?: number
}

/**
 * Typed error thrown by every request made through {@link apiClient}.
 * `status` is 0 when no HTTP response was received (timeout, cancel, network).
 */
export class ApiError extends Error {
    readonly status: number
    readonly kind: ApiErrorKind
    readonly fieldErrors: Record<string, string[]>
    readonly correlationId?: string
    readonly retryAfterSeconds?: number

    constructor(init: {
        message: string
        status: number
        kind: ApiErrorKind
        fieldErrors?: Record<string, string[]>
        correlationId?: string
        retryAfterSeconds?: number
    }) {
        super(init.message)
        this.name = 'ApiError'
        this.status = init.status
        this.kind = init.kind
        this.fieldErrors = init.fieldErrors ?? {}
        this.correlationId = init.correlationId
        this.retryAfterSeconds = init.retryAfterSeconds
    }

    get isTimeout(): boolean { return this.kind === 'timeout' }
    get isCanceled(): boolean { return this.kind === 'canceled' }
    get hasFieldErrors(): boolean { return Object.keys(this.fieldErrors).length > 0 }
}

export function isApiError(value: unknown): value is ApiError {
    return value instanceof ApiError
}

/** True when the error is a cancellation caused by an AbortController (not a failure). */
export function isCanceledError(value: unknown): boolean {
    return (value instanceof ApiError && value.isCanceled) || axios.isCancel(value)
}

/** Returns a user-facing message for any thrown value. */
export function getErrorMessage(value: unknown, fallback = 'An unexpected error occurred.'): string {
    if (value instanceof Error && value.message) return value.message
    return fallback
}

/**
 * Normalizes a server field key to a lower-case property name, so that
 * "Email", "email", "dto.Email" and "$.email" all become "email".
 */
export function normalizeFieldKey(key: string): string {
    const lastSegment = key.split('.').pop() ?? key
    return lastSegment.replace(/^\$/, '').replace(/\[\d+\]$/, '').toLowerCase()
}

/**
 * Maps server field errors onto the given form field names, matching keys
 * case-insensitively. Returns the first message for each matched field.
 */
export function mapFieldErrors<TField extends string>(
    fieldErrors: Record<string, string[]>,
    fields: readonly TField[],
): Partial<Record<TField, string>> {
    const byKey = new Map(fields.map((f) => [f.toLowerCase(), f]))
    const result: Partial<Record<TField, string>> = {}
    for (const [key, messages] of Object.entries(fieldErrors)) {
        const field = byKey.get(normalizeFieldKey(key))
        if (field && messages.length > 0 && result[field] === undefined) {
            result[field] = messages[0]
        }
    }
    return result
}

function normalizeErrors(errors: ServerErrorBody['errors']): Record<string, string[]> {
    if (!errors || typeof errors !== 'object') return {}
    const result: Record<string, string[]> = {}
    for (const [key, value] of Object.entries(errors)) {
        const messages = (Array.isArray(value) ? value : [value]).filter(
            (m): m is string => typeof m === 'string' && m.length > 0,
        )
        if (messages.length > 0) result[key] = messages
    }
    return result
}

function readHeader(headers: unknown, name: string): string | undefined {
    if (!headers || typeof headers !== 'object') return undefined
    const h = headers as { get?: (n: string) => unknown } & Record<string, unknown>
    const value = typeof h.get === 'function' ? h.get(name) : h[name.toLowerCase()]
    return value == null ? undefined : String(value)
}

/** Parses Retry-After (delta-seconds or HTTP date) into whole seconds. */
function parseRetryAfter(header: string | undefined, body: ServerErrorBody | undefined): number | undefined {
    if (header) {
        const seconds = Number(header)
        if (Number.isFinite(seconds) && seconds >= 0) return Math.ceil(seconds)
        const date = Date.parse(header)
        if (!Number.isNaN(date)) return Math.max(0, Math.ceil((date - Date.now()) / 1000))
    }
    const fromBody = body?.retryAfterSeconds ?? body?.retryAfter
    return typeof fromBody === 'number' && fromBody >= 0 ? Math.ceil(fromBody) : undefined
}

function isTimeout(error: AxiosError): boolean {
    // axios reports timeouts as ETIMEDOUT (clarifyTimeoutError) or ECONNABORTED with a "timeout" message.
    if (error.code === AxiosError.ETIMEDOUT) return true
    return error.code === AxiosError.ECONNABORTED && /timeout/i.test(error.message)
}

/** Converts anything thrown by axios into an {@link ApiError}. */
export function toApiError(error: unknown): ApiError {
    if (error instanceof ApiError) return error

    if (!axios.isAxiosError(error)) {
        if (axios.isCancel(error)) {
            return new ApiError({ message: 'The request was canceled.', status: 0, kind: 'canceled' })
        }
        return new ApiError({ message: getErrorMessage(error), status: 0, kind: 'network' })
    }

    // Timeout must be checked before the generic "no response" case: both have no response.
    if (isTimeout(error)) {
        return new ApiError({
            message: 'The request timed out. The server took too long to respond, please try again.',
            status: 0,
            kind: 'timeout',
        })
    }

    if (error.code === AxiosError.ERR_CANCELED || axios.isCancel(error)) {
        return new ApiError({ message: 'The request was canceled.', status: 0, kind: 'canceled' })
    }

    if (!error.response) {
        return new ApiError({
            message: error.code === AxiosError.ECONNABORTED
                ? 'The request was aborted. Please try again.'
                : 'Cannot connect to the server. Please ensure the backend is running.',
            status: 0,
            kind: 'network',
        })
    }

    const { status, headers } = error.response
    const body = (error.response.data && typeof error.response.data === 'object'
        ? error.response.data
        : undefined) as ServerErrorBody | undefined

    const fieldErrors = normalizeErrors(body?.errors)
    const correlationId = body?.correlationId ?? readHeader(headers, 'X-Correlation-ID') ?? body?.traceId
    const serverMessage = body?.message || body?.detail || body?.title || body?.error

    let message: string
    let retryAfterSeconds: number | undefined
    if (status === 429) {
        retryAfterSeconds = parseRetryAfter(readHeader(headers, 'Retry-After'), body)
        message = retryAfterSeconds !== undefined
            ? `Too many requests, try again in ${retryAfterSeconds} s.`
            : 'Too many requests, please try again shortly.'
    } else if (serverMessage) {
        message = serverMessage
    } else if (status === 404) {
        message = 'The requested resource was not found.'
    } else if (status === 409) {
        message = 'The request conflicts with the current state of the data.'
    } else if (status >= 500) {
        message = 'The server encountered an error. Please try again later.'
    } else {
        message = error.message || 'An unexpected error occurred.'
    }

    return new ApiError({ message, status, kind: 'http', fieldErrors, correlationId, retryAfterSeconds })
}

/**
 * Shared Axios instance used by all API service modules.
 * Base URL is configured via VITE_API_URL environment variable.
 * Every rejected request is normalized into an {@link ApiError}.
 */
const apiClient = axios.create({
    baseURL: `${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api`,
    timeout: DEFAULT_TIMEOUT_MS,
    headers: {
        'Content-Type': 'application/json',
    },
})

apiClient.interceptors.response.use(
    (response) => response,
    (error: unknown) => Promise.reject(toApiError(error)),
)

export default apiClient
