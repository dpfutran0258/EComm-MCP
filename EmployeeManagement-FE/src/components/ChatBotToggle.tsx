import { CHAT_PANEL_ID } from '../constants/ui'
import './ChatBotToggle.css'

interface ChatBotToggleProps {
    isOpen: boolean
    onToggle: (open: boolean) => void
    hasUnread?: number
}

export default function ChatBotToggle({ isOpen, onToggle, hasUnread = 0 }: ChatBotToggleProps) {
    return (
        <button
            type="button"
            className={`chatbot-toggle ${isOpen ? 'active' : ''}`}
            onClick={() => onToggle(!isOpen)}
            title="Chat assistant"
            aria-label="Chat assistant"
            aria-expanded={isOpen}
            aria-controls={isOpen ? CHAT_PANEL_ID : undefined}
        >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
            </svg>
            {hasUnread > 0 && <span className="chatbot-toggle-badge">{hasUnread > 9 ? '9+' : hasUnread}</span>}
        </button>
    )
}
