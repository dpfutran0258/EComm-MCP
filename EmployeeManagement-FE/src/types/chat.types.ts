export type ChatResponseType = 'SUCCESS' | 'ERROR' | 'CLARIFICATION' | 'INFO' | 'CONFIRMATION'

export interface ChatMessage {
    id: string
    text: string
    sender: 'user' | 'bot'
    timestamp: Date
    type?: ChatResponseType
    data?: Record<string, unknown>
}

export interface ChatApiRequest {
    message: string
    sessionId: string
    context?: Record<string, unknown>
    preferredProvider?: string
    preferredModel?: string
}

export interface ChatApiResponse {
    responseText: string
    type: ChatResponseType
    data?: Record<string, unknown>
    updatedContext?: Record<string, unknown>
    providerUsed?: string
    modelUsed?: string
}

export interface BulkRowResult {
    row: number
    success: boolean
    employee?: Record<string, unknown>
    error?: string
}

export interface BulkOperationData {
    results: BulkRowResult[]
    successCount: number
    failCount: number
    totalRows: number
}
