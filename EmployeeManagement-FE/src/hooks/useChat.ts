import { useState, useCallback, useRef } from 'react'
import { chatApi } from '../api/chat.api'
import { getErrorMessage } from '../api/client'
import { notifyEmployeesChanged } from '../constants/events'
import type { ChatMessage, ChatApiResponse, WireMessage } from '../types/chat.types'

/** The server trims too; this only keeps request bodies small. */
const MAX_WIRE_MESSAGES = 60

export interface UseChatReturn {
    messages: ChatMessage[]
    isLoading: boolean
    sendMessage: (text: string) => Promise<void>
    /** Runs the destructive action held by the bot message with this id. */
    confirmAction: (messageId: string) => Promise<void>
    /** Dismisses the pending action of the bot message with this id. */
    cancelAction: (messageId: string) => void
    clearMessages: () => void
}

/** Drops the oldest wire messages but always starts at a user message, so no tool result is orphaned. */
function trimWire(wire: WireMessage[]): WireMessage[] {
    if (wire.length <= MAX_WIRE_MESSAGES) return wire
    const tail = wire.slice(wire.length - MAX_WIRE_MESSAGES)
    const firstUser = tail.findIndex((m) => m.role === 'user')
    return firstUser >= 0 ? tail.slice(firstUser) : tail
}

let idCounter = 0
const nextId = (prefix: string) => `${prefix}-${Date.now()}-${idCounter++}`

/**
 * Chat state. The conversation context lives here: `wireRef` is the transcript (user messages,
 * assistant tool calls, tool results, assistant answers) that is sent with every request so the
 * AI knows what "him", "the second one" or "yes" refers to. `messages` is only what is displayed.
 */
export function useChat(): UseChatReturn {
    const [messages, setMessages] = useState<ChatMessage[]>([])
    const [isLoading, setIsLoading] = useState(false)
    const wireRef = useRef<WireMessage[]>([])
    const busyRef = useRef(false)

    const addMessage = useCallback((msg: ChatMessage) => {
        setMessages((prev) => [...prev, msg])
    }, [])

    const applyResponse = useCallback((response: ChatApiResponse) => {
        wireRef.current = trimWire([...wireRef.current, ...(response.newMessages ?? [])])
        if (response.dataChanged) notifyEmployeesChanged()

        addMessage({
            id: nextId('bot'),
            text: response.responseText,
            sender: 'bot',
            timestamp: new Date(),
            type: response.type,
            data: response.data,
            toolActivity: response.toolActivity,
            pendingAction: response.pendingAction,
            pendingState: response.pendingAction ? 'open' : undefined,
        })
    }, [addMessage])

    const showError = useCallback((err: unknown) => {
        addMessage({
            id: nextId('err'),
            text: `❌ ${getErrorMessage(err, 'Failed to reach the assistant.')}`,
            sender: 'bot',
            timestamp: new Date(),
            type: 'ERROR',
        })
    }, [addMessage])

    const sendMessage = useCallback(async (text: string) => {
        const trimmed = text.trim()
        if (!trimmed || busyRef.current) return

        busyRef.current = true
        setIsLoading(true)
        addMessage({ id: nextId('user'), text: trimmed, sender: 'user', timestamp: new Date() })

        const previous = wireRef.current
        const outgoing = trimWire([...previous, { role: 'user', content: trimmed }])

        try {
            const response = await chatApi.send({ messages: outgoing })
            // The user message joins the context only once the server accepted the request.
            wireRef.current = outgoing
            applyResponse(response)
        } catch (err) {
            wireRef.current = previous
            showError(err)
        } finally {
            busyRef.current = false
            setIsLoading(false)
        }
    }, [addMessage, applyResponse, showError])

    const setPendingState = useCallback((messageId: string, state: 'confirmed' | 'cancelled') => {
        setMessages((prev) => prev.map((m) => (m.id === messageId ? { ...m, pendingState: state } : m)))
    }, [])

    const confirmAction = useCallback(async (messageId: string) => {
        if (busyRef.current) return
        const message = messages.find((m) => m.id === messageId)
        if (!message?.pendingAction || message.pendingState !== 'open') return

        busyRef.current = true
        setIsLoading(true)
        setPendingState(messageId, 'confirmed')
        try {
            applyResponse(await chatApi.send({ confirmAction: message.pendingAction.token }))
        } catch (err) {
            showError(err)
        } finally {
            busyRef.current = false
            setIsLoading(false)
        }
    }, [messages, applyResponse, setPendingState, showError])

    const cancelAction = useCallback((messageId: string) => {
        const message = messages.find((m) => m.id === messageId)
        if (!message?.pendingAction || message.pendingState !== 'open') return

        setPendingState(messageId, 'cancelled')
        // Tell the model, so the next message is understood in context.
        wireRef.current = trimWire([
            ...wireRef.current,
            { role: 'user', content: 'No, cancel that. Do not do it.' },
            { role: 'assistant', content: 'Okay, cancelled. Nothing was changed.' },
        ])
        addMessage({
            id: nextId('bot'),
            text: 'Okay, cancelled. Nothing was changed.',
            sender: 'bot',
            timestamp: new Date(),
            type: 'INFO',
        })
    }, [messages, addMessage, setPendingState])

    const clearMessages = useCallback(() => {
        setMessages([])
        wireRef.current = []
    }, [])

    return { messages, isLoading, sendMessage, confirmAction, cancelAction, clearMessages }
}
