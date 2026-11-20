import { useId } from 'react'
import './Input.css'

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
    label?: string
    error?: string
    helperText?: string
    required?: boolean
}

export default function Input({
    label,
    error,
    helperText,
    required,
    className,
    id,
    'aria-describedby': ariaDescribedBy,
    'aria-invalid': ariaInvalid,
    ...props
}: InputProps) {
    const generatedId = useId()
    const inputId = id || generatedId
    const errorId = `${inputId}-error`
    const helperId = `${inputId}-helper`
    const showHelper = Boolean(helperText) && !error

    const describedBy = [ariaDescribedBy, error ? errorId : undefined, showHelper ? helperId : undefined]
        .filter(Boolean)
        .join(' ') || undefined

    return (
        <div className="input-wrapper">
            {label && (
                <label htmlFor={inputId} className="input-label">
                    {label}
                    {required && <span className="required-mark" aria-hidden="true">*</span>}
                </label>
            )}
            <input
                id={inputId}
                className={`input-field ${error ? 'error' : ''} ${className || ''}`}
                required={required}
                aria-invalid={error ? true : ariaInvalid}
                aria-describedby={describedBy}
                {...props}
            />
            {error && <span id={errorId} className="input-error">{error}</span>}
            {showHelper && <span id={helperId} className="input-helper">{helperText}</span>}
        </div>
    )
}
