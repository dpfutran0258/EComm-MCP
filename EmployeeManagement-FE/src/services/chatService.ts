/// <reference types="vite/client" />
import axios from 'axios'

export interface ChatMessage {
    id: string
    text: string
    sender: 'user' | 'bot'
    timestamp: Date
    type?: 'SUCCESS' | 'ERROR' | 'CLARIFICATION' | 'INFO' | 'CONFIRMATION'
    data?: Record<string, any>
}

export interface ChatRequest {
    message: string
    sessionId: string
    context?: Record<string, any>
}

export interface ChatResponse {
    responseText: string
    type: 'SUCCESS' | 'ERROR' | 'CLARIFICATION' | 'INFO' | 'CONFIRMATION'
    data?: Record<string, any>
    updatedContext?: Record<string, any>
}

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

const chatApiClient = axios.create({
    baseURL: `${API_BASE_URL}/api`,
    headers: {
        'Content-Type': 'application/json',
    },
})

export const chatService = {
    /**
     * Send a chat message and get a response from the AI chatbot
     */
    async sendMessage(
        message: string,
        sessionId: string,
        context: Record<string, any> = {}
    ): Promise<ChatResponse> {
        try {
            const request: ChatRequest = {
                message,
                sessionId,
                context,
            }

            console.log('📤 Sending to API:', `${API_BASE_URL}/api/chat/message`, request)

            const response = await chatApiClient.post<ChatResponse>('/chat/message', request, {
                timeout: 100000, // 10 second timeout
            })
            console.log('✅ Response received:', response.data)
            return response.data
        } catch (error) {
            console.error('❌ API Error:', error)

            if (axios.isAxiosError(error)) {
                // Network errors (no connection)
                if (error.code === 'ERR_NETWORK' || error.message === 'Network Error') {
                    throw new Error('🔴 Cannot connect to backend. Is it running on http://localhost:5000?')
                }

                // Timeout errors
                if (error.code === 'ECONNABORTED') {
                    throw new Error('⏱️ Backend request timed out. Is the server responding? Check if backend is running.')
                }

                // Request failed (connection refused)
                if (error.code === 'ERR_BAD_REQUEST' || !error.response) {
                    throw new Error('🔴 Cannot connect to backend. Is it running on http://localhost:5000?')
                }

                const errorMessage = error.response?.data?.error ||
                    error.response?.data?.message ||
                    error.message ||
                    'Failed to send message'
                throw new Error(errorMessage)
            }
            throw error
        }
    },

    /**
     * Generate a unique session ID for a chat conversation
     */
    generateSessionId(): string {
        return `session-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
    },
}
