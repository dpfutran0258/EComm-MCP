import { useEffect } from 'react'
import './Toast.css'

interface ToastProps {
    message: string
    type: 'success' | 'error' | 'info' | 'warning'
    duration?: number
    onClose: () => void
}

export default function Toast({ message, type, duration = 4000, onClose }: ToastProps) {
    useEffect(() => {
        const timer = setTimeout(onClose, duration)
        return () => clearTimeout(timer)
    }, [duration, onClose])

    return (
        <div className={`toast toast--${type}`} role="status" aria-live="polite">
            <span className="toast-message">{message}</span>
            <button
                className="toast-close"
                onClick={onClose}
                aria-label="Close notification"
            >
                ×
            </button>
        </div>
    )
}
