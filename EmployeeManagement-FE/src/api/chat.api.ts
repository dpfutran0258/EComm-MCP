import apiClient from './client'
import type { ChatApiRequest, ChatApiResponse } from '../types/chat.types'

export const chatApi = {
    /**
     * Sends the transcript (or a confirmation token) to the AI assistant. The server is stateless:
     * everything the model needs as context travels in `messages`.
     */
    send(request: ChatApiRequest): Promise<ChatApiResponse> {
        return apiClient
            .post<ChatApiResponse>('/chat', request, { timeout: 120000 })
            .then((r) => r.data)
    },
}
