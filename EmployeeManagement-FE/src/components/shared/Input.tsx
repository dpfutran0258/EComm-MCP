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
    ...props
}: InputProps) {
    const inputId = id || `input-${Math.random()}`

    return (
        <div className="input-wrapper">
            {label && (
                <label htmlFor={inputId} className="input-label">
                    {label}
                    {required && <span className="required-mark">*</span>}
                </label>
            )}
            <input
                id={inputId}
                className={`input-field ${error ? 'error' : ''} ${className || ''}`}
                {...props}
            />
            {error && <span className="input-error">{error}</span>}
            {helperText && !error && <span className="input-helper">{helperText}</span>}
        </div>
    )
}
