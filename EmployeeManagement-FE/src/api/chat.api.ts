import apiClient from './client'
import type { ChatApiRequest, ChatApiResponse } from '../types/chat.types'

export const chatApi = {
    sendMessage(request: ChatApiRequest): Promise<ChatApiResponse> {
        return apiClient
            .post<ChatApiResponse>('/chat/message', request, { timeout: 120000 })
            .then((r) => r.data)
    },

    /** Generates a new stable session ID for a conversation. */
    generateSessionId(): string {
        return `session-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
    },
}
