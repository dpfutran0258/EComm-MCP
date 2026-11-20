import { useEffect, useRef, memo } from 'react'
import { useChat } from '../hooks/useChat'
import { CHAT_PANEL_ID } from '../constants/ui'
import ChatMessage from './chat/ChatMessage'
import ChatInput from './chat/ChatInput'
import './ChatBot.css'

interface ChatBotProps {
    isOpen: boolean
    onClose: () => void
}

/**
 * Full chat window with message list, loading indicator, and input.
 * Renders Markdown, bulk operation previews, and confirmation flows.
 */
function ChatBot({ isOpen, onClose }: ChatBotProps) {
    const { messages, isLoading, sendMessage, confirmAction, cancelAction, clearMessages } = useChat()
    const bottomRef = useRef<HTMLDivElement>(null)
    const panelRef = useRef<HTMLElement>(null)

    // Scroll to latest message
    useEffect(() => {
        bottomRef.current?.scrollIntoView?.({ behavior: 'smooth' })
    }, [messages])

    // Move focus into the panel on open and back to the opener on close
    useEffect(() => {
        if (!isOpen) return
        const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
        const field = panelRef.current?.querySelector<HTMLElement>('textarea, input')
        ;(field ?? panelRef.current)?.focus()
        return () => {
            if (opener?.isConnected) opener.focus()
        }
    }, [isOpen])

    // Close on Escape (unless something inside, e.g. a dialog, already handled it)
    useEffect(() => {
        if (!isOpen) return
        const handler = (e: KeyboardEvent) => {
            if (e.key === 'Escape' && !e.defaultPrevented) onClose()
        }
        window.addEventListener('keydown', handler)
        return () => window.removeEventListener('keydown', handler)
    }, [isOpen, onClose])

    if (!isOpen) return null

    return (
        <>
            {/* Backdrop */}
            <div className="chatbot-backdrop" onClick={onClose} aria-hidden="true" />

            {/* Panel */}
            <section
                ref={panelRef}
                id={CHAT_PANEL_ID}
                className="chatbot"
                role="dialog"
                aria-label="Employee AI Assistant"
                tabIndex={-1}
            >
                {/* Header */}
                <header className="chatbot__header">
                    <div className="chatbot__title-group">
                        <h2 className="chatbot__title">Employee Assistant</h2>
                        <span className="chatbot__badge">AI Powered</span>
                    </div>
                    <div className="chatbot__actions">
                        <button
                            className="chatbot__icon-btn"
                            onClick={clearMessages}
                            title="Clear conversation"
                            aria-label="Clear conversation"
                        >
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <polyline points="3 6 5 6 21 6" />
                                <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
                                <path d="M10 11v6M14 11v6" />
                            </svg>
                        </button>
                        <button
                            className="chatbot__icon-btn"
                            onClick={onClose}
                            title="Close"
                            aria-label="Close chat"
                        >
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <line x1="18" y1="6" x2="6" y2="18" strokeLinecap="round" />
                                <line x1="6" y1="6" x2="18" y2="18" strokeLinecap="round" />
                            </svg>
                        </button>
                    </div>
                </header>

                {/* Messages */}
                <div className="chatbot__messages" role="log" aria-live="polite">
                    {messages.length === 0 ? (
                        <EmptyState />
                    ) : (
                        <>
                            {messages.map((m) => (
                                <ChatMessage
                                    key={m.id}
                                    message={m}
                                    busy={isLoading}
                                    onConfirm={confirmAction}
                                    onCancel={cancelAction}
                                />
                            ))}
                            {isLoading && <TypingIndicator />}
                            <div ref={bottomRef} />
                        </>
                    )}
                </div>

                {/* Input */}
                <ChatInput
                    onSend={sendMessage}
                    disabled={isLoading}
                />
            </section>
        </>
    )
}

function EmptyState() {
    return (
        <div className="chatbot__empty">
            <span className="chatbot__empty-icon" aria-hidden="true">🤖</span>
            <h3>Hi! I'm your Employee Assistant</h3>
            <p>Manage employees through natural conversation. Try:</p>
            <ul>
                <li>📝 "Add John Doe, john@co.com, Engineering, $80,000"</li>
                <li>📋 "Show all employees"</li>
                <li>🔍 "Who works in IT?"</li>
                <li>✏️ "Update John's salary to $90,000"</li>
                <li>🗑️ "Delete employee John Doe"</li>
                <li>📊 Paste a table with Name, Email, Dept, Salary</li>
            </ul>
        </div>
    )
}

function TypingIndicator() {
    return (
        <div className="chatbot__typing" aria-label="Assistant is typing">
            <span /><span /><span />
        </div>
    )
}

export default memo(ChatBot)
