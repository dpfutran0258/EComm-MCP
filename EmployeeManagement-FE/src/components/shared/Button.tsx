import './Button.css'

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
    variant?: 'primary' | 'secondary' | 'destructive' | 'ghost'
    size?: 'sm' | 'md' | 'lg'
    isLoading?: boolean
    children: React.ReactNode
}

export default function Button({
    variant = 'primary',
    size = 'md',
    isLoading = false,
    disabled,
    children,
    className,
    ...props
}: ButtonProps) {
    return (
        <button
            className={`btn btn--${variant} btn--${size} ${className || ''}`}
            disabled={disabled || isLoading}
            {...props}
        >
            {isLoading ? (
                <>
                    <span className="btn-loader"></span>
                    <span>{children}</span>
                </>
            ) : (
                children
            )}
        </button>
    )
}
