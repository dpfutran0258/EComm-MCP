export type ChatResponseType = 'SUCCESS' | 'ERROR' | 'CLARIFICATION' | 'INFO' | 'CONFIRMATION'

// ── Wire format: the transcript the server needs as context ──────────────────

export interface WireToolCall {
    id: string
    name: string
    /** Arguments as a JSON object string. */
    arguments: string
}

/** One message of the conversation sent to the server with every request. */
export interface WireMessage {
    role: 'user' | 'assistant' | 'tool'
    content?: string | null
    toolCalls?: WireToolCall[]
    toolCallId?: string
}

export interface PendingAction {
    token: string
    tool: string
    summary: string
    expiresAtUtc: string
}

export interface ToolActivity {
    tool: string
    isError: boolean
}

export interface ChatApiRequest {
    messages?: WireMessage[]
    /** Token from a previous pendingAction; runs the confirmed action without the model. */
    confirmAction?: string
    preferredModel?: string
}

export interface ChatApiResponse {
    responseText: string
    type: ChatResponseType
    /** Messages to append to the transcript. */
    newMessages: WireMessage[]
    toolActivity?: ToolActivity[]
    data?: Record<string, unknown>
    /** True when a tool changed employee data. */
    dataChanged?: boolean
    pendingAction?: PendingAction
    providerUsed?: string
    modelUsed?: string
}

// ── Display model ────────────────────────────────────────────────────────────

export type PendingState = 'open' | 'confirmed' | 'cancelled'

export interface ChatMessage {
    id: string
    text: string
    sender: 'user' | 'bot'
    timestamp: Date
    type?: ChatResponseType
    data?: Record<string, unknown>
    toolActivity?: ToolActivity[]
    pendingAction?: PendingAction
    pendingState?: PendingState
}

export interface BulkRowResult {
    row: number
    success: boolean
    employee?: Record<string, unknown>
    name?: string
    id?: number
    error?: string
}

export interface BulkOperationData {
    results: BulkRowResult[]
    successCount: number
    failCount: number
    totalRows: number
    message?: string
}

export interface SalaryStats {
    employeeCount: number
    averageSalary: number
    minSalary: number
    maxSalary: number
    totalSalary: number
    byDepartment: Array<{
        department: string
        count: number
        averageSalary: number
        minSalary: number
        maxSalary: number
    }>
}
