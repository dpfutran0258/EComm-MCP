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
    if (!isOpen) return null

    return (
        <>
            <div className="dialog-overlay" onClick={onCancel} />
            <dialog className="dialog-content" open>
                <div className="dialog-header">
                    <h2 className="dialog-title">{title}</h2>
                </div>

                <div className="dialog-body">
                    <p>{message}</p>
                </div>

                <div className="dialog-footer">
                    <Button
                        variant="ghost"
                        size="md"
                        onClick={onCancel}
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
        </>
    )
}
