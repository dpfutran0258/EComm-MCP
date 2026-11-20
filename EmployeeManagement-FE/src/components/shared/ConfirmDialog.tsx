import { useEffect, useId, useRef } from 'react'
import './ConfirmDialog.css'
import Button from './Button'

interface ConfirmDialogProps {
    title: string
    message: string
    confirmText?: string
    cancelText?: string
    isDestructive?: boolean
    isOpen: boolean
    isLoading?: boolean
    onConfirm: () => void
    onCancel: () => void
}

/**
 * Modal confirmation dialog built on the native <dialog> element.
 * Escape and clicks on the backdrop cancel; focus moves to the Cancel button
 * on open and returns to the element that opened the dialog on close.
 */
export default function ConfirmDialog({
    title,
    message,
    confirmText = 'Confirm',
    cancelText = 'Cancel',
    isDestructive = false,
    isOpen,
    isLoading = false,
    onConfirm,
    onCancel
}: ConfirmDialogProps) {
    const dialogRef = useRef<HTMLDialogElement>(null)
    const cancelRef = useRef<HTMLButtonElement>(null)
    const titleId = useId()
    const messageId = useId()

    useEffect(() => {
        if (!isOpen) return
        const dialog = dialogRef.current
        const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null

        if (dialog && !dialog.open) {
            if (typeof dialog.showModal === 'function') dialog.showModal()
            else dialog.setAttribute('open', '')
        }
        cancelRef.current?.focus()

        return () => {
            if (dialog?.open) {
                if (typeof dialog.close === 'function') dialog.close()
                else dialog.removeAttribute('open')
            }
            if (trigger?.isConnected) trigger.focus()
        }
    }, [isOpen])

    if (!isOpen) return null

    const requestCancel = () => {
        if (!isLoading) onCancel()
    }

    const handleKeyDown = (e: React.KeyboardEvent<HTMLDialogElement>) => {
        if (e.key === 'Escape') {
            e.preventDefault()
            requestCancel()
        }
    }

    // Native "cancel" fires on Escape (and platform back gestures); keep React in control of closing.
    const handleNativeCancel = (e: React.SyntheticEvent<HTMLDialogElement>) => {
        e.preventDefault()
        requestCancel()
    }

    // A click whose target is the <dialog> itself landed on the ::backdrop.
    const handleClick = (e: React.MouseEvent<HTMLDialogElement>) => {
        if (e.target !== e.currentTarget) return
        const rect = e.currentTarget.getBoundingClientRect()
        const inside = e.clientX >= rect.left && e.clientX <= rect.right
            && e.clientY >= rect.top && e.clientY <= rect.bottom
        if (!inside) requestCancel()
    }

    return (
        <dialog
            ref={dialogRef}
            className="dialog-content"
            aria-labelledby={titleId}
            aria-describedby={messageId}
            onKeyDown={handleKeyDown}
            onCancel={handleNativeCancel}
            onClick={handleClick}
        >
            <div className="dialog-header">
                <h2 id={titleId} className="dialog-title">{title}</h2>
            </div>

            <div className="dialog-body">
                <p id={messageId}>{message}</p>
            </div>

            <div className="dialog-footer">
                <Button
                    ref={cancelRef}
                    variant="ghost"
                    size="md"
                    onClick={requestCancel}
                    disabled={isLoading}
                >
                    {cancelText}
                </Button>
                <Button
                    variant={isDestructive ? 'destructive' : 'primary'}
                    size="md"
                    onClick={onConfirm}
                    isLoading={isLoading}
                    disabled={isLoading}
                >
                    {confirmText}
                </Button>
            </div>
        </dialog>
    )
}
