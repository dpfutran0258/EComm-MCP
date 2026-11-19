import { useRef, useEffect } from 'react'
import './ChatInput.css'

interface ChatInputProps {
    onSend: (message: string) => void
    disabled?: boolean
    placeholder?: string
}

/**
 * Chat input area.
 * Auto-grows to accommodate pasted tables (multiline textarea).
 * Sends on Enter; Shift+Enter inserts a newline.
 */
export default function ChatInput({ onSend, disabled, placeholder }: ChatInputProps) {
    const ref = useRef<HTMLTextAreaElement>(null)

    // Auto-resize textarea on content change
    useEffect(() => {
        const el = ref.current
        if (!el) return
        el.style.height = 'auto'
        el.style.height = `${Math.min(el.scrollHeight, 180)}px`
    })

    const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault()
            submit()
        }
    }

    const submit = () => {
        const text = ref.current?.value.trim()
        if (!text || disabled) return
        onSend(text)
        if (ref.current) {
            ref.current.value = ''
            ref.current.style.height = 'auto'
        }
    }

    return (
        <div className="chat-input">
            <textarea
                ref={ref}
                className="chat-input__field"
                placeholder={placeholder ?? 'Ask me anything about employees... (Shift+Enter for new line)'}
                disabled={disabled}
                rows={1}
                onKeyDown={handleKeyDown}
                aria-label="Chat message input"
            />
            <button
                className="chat-input__send"
                onClick={submit}
                disabled={disabled}
                aria-label="Send message"
                title="Send message (Enter)"
            >
                <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                    <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
                </svg>
            </button>
        </div>
    )
}
