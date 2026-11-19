import { useState, useCallback, useRef } from 'react'
import { chatApi } from '../api/chat.api'
import type { ChatMessage, ChatApiResponse } from '../types/chat.types'

export interface UseChatReturn {
    messages: ChatMessage[]
    isLoading: boolean
    error: Error | null
    sendMessage: (text: string, preferredProvider?: string) => Promise<void>
    clearMessages: () => void
    sessionId: string
}

export function useChat(): UseChatReturn {
    const [messages, setMessages] = useState<ChatMessage[]>([])
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<Error | null>(null)
    const sessionIdRef = useRef<string>(chatApi.generateSessionId())
    const contextRef = useRef<Record<string, unknown>>({})

    const addMessage = useCallback((msg: ChatMessage) => {
        setMessages((prev) => [...prev, msg])
    }, [])

    const sendMessage = useCallback(
        async (text: string, preferredProvider?: string) => {
            if (!text.trim() || isLoading) return

            setError(null)
            setIsLoading(true)

            // Show user message immediately
            addMessage({
                id: `user-${Date.now()}`,
                text,
                sender: 'user',
                timestamp: new Date(),
            })

            try {
                console.log('[useChat] Sending message:', {
                    message: text,
                    sessionId: sessionIdRef.current,
                    contextKeys: Object.keys(contextRef.current),
                    context: contextRef.current
                })

                const response: ChatApiResponse = await chatApi.sendMessage({
                    message: text,
                    sessionId: sessionIdRef.current,
                    context: contextRef.current,
                    preferredProvider,
                })

                console.log('[useChat] Received response:', {
                    type: response.type,
                    hasUpdatedContext: !!response.updatedContext,
                    updatedContextKeys: response.updatedContext ? Object.keys(response.updatedContext) : [],
                    updatedContext: response.updatedContext
                })

                // Persist updated context returned from backend
                if (response.updatedContext) {
                    contextRef.current = {
                        ...contextRef.current,
                        ...response.updatedContext,
                    }
                    
                    console.log('[useChat] Context after merge:', contextRef.current)
                }

                addMessage({
                    id: `bot-${Date.now()}`,
                    text: response.responseText,
                    sender: 'bot',
                    timestamp: new Date(),
                    type: response.type,
                    data: response.data as Record<string, unknown> | undefined,
                })
            } catch (err) {
                const e = err instanceof Error ? err : new Error('Failed to send message.')
                setError(e)
                addMessage({
                    id: `err-${Date.now()}`,
                    text: `❌ ${e.message}`,
                    sender: 'bot',
                    timestamp: new Date(),
                    type: 'ERROR',
                })
            } finally {
                setIsLoading(false)
            }
        },
        [addMessage, isLoading]
    )

    const clearMessages = useCallback(() => {
        setMessages([])
        sessionIdRef.current = chatApi.generateSessionId()
        contextRef.current = {}
        setError(null)
    }, [])

    return {
        messages,
        isLoading,
        error,
        sendMessage,
        clearMessages,
        sessionId: sessionIdRef.current,
    }
}
