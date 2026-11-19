import ReactMarkdown from 'react-markdown'
import type { ChatMessage as ChatMessageType, BulkOperationData } from '../../types/chat.types'
import './ChatMessage.css'

interface ChatMessageProps {
    message: ChatMessageType
}

export default function ChatMessage({ message }: ChatMessageProps) {
    const isUser = message.sender === 'user'
    const typeClass = message.type ? `message--${message.type.toLowerCase()}` : ''

    return (
        <div className={`chat-message chat-message--${isUser ? 'user' : 'bot'} ${typeClass}`}>
            {/* Avatar */}
            <div className="chat-message__avatar" aria-hidden="true">
                {isUser ? '👤' : '🤖'}
            </div>

            <div className="chat-message__content">
                {/* Bubble */}
                <div className="chat-message__bubble">
                    {isUser ? (
                        <p className="chat-message__text">{message.text}</p>
                    ) : (
                        <div className="chat-message__markdown">
                            <ReactMarkdown>{message.text}</ReactMarkdown>
                        </div>
                    )}

                    {/* Employee data card - for single employee operations */}
                    {message.data && hasEmployeeData(message.data) && (
                        <EmployeeCard employee={message.data['employee'] as Employee} />
                    )}

                    {/* Multiple employees (search results, clarifications) */}
                    {message.data && hasMultipleEmployees(message.data) && (
                        <EmployeeList employees={(message.data['employees'] || message.data['matches']) as Employee[]} />
                    )}

                    {/* Bulk operation result table */}
                    {message.data && isBulkData(message.data) && (
                        <BulkResultTable data={message.data as unknown as BulkOperationData} />
                    )}

                    {/* Confirmation actions — rendered when type is CONFIRMATION */}
                    {message.type === 'CONFIRMATION' && message.data?.['preview'] != null && (
                        <BulkPreviewTable preview={message.data['preview'] as PreviewRow[]} />
                    )}
                </div>

                {/* Timestamp */}
                <span className="chat-message__time" aria-label="Message time">
                    {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    {!isUser && message.type && (
                        <span className={`chat-message__badge chat-message__badge--${message.type.toLowerCase()}`}>
                            {message.type}
                        </span>
                    )}
                </span>
            </div>
        </div>
    )
}

// ── Type definitions ──────────────────────────────────────────────────────────

interface Employee {
    id: number
    fullName: string
    email: string
    department: string
    salary: number
    createdAt?: string
    updatedAt?: string
}

interface PreviewRow {
    row: number
    fullName?: string
    email?: string
    department?: string
    salary?: number
    isValid: boolean
}

// ── Data detection helpers ────────────────────────────────────────────────────

function hasEmployeeData(data: Record<string, unknown>): boolean {
    return data.employee != null && typeof data.employee === 'object'
}

function hasMultipleEmployees(data: Record<string, unknown>): boolean {
    return (Array.isArray(data.employees) && data.employees.length > 0) ||
        (Array.isArray(data.matches) && data.matches.length > 0)
}

function isBulkData(data: Record<string, unknown>): boolean {
    return Array.isArray(data.results) && typeof data.successCount === 'number'
}

// ── Display components ────────────────────────────────────────────────────────

function EmployeeCard({ employee }: { employee: Employee }) {
    return (
        <div className="employee-card">
            <div className="employee-card__header">
                <h3 className="employee-card__name">{employee.fullName}</h3>
                <span className="employee-card__id">ID: {employee.id}</span>
            </div>
            <div className="employee-card__details">
                <div className="employee-card__row">
                    <span className="employee-card__label">📧 Email:</span>
                    <span className="employee-card__value">{employee.email}</span>
                </div>
                <div className="employee-card__row">
                    <span className="employee-card__label">🏢 Department:</span>
                    <span className="employee-card__value">{employee.department}</span>
                </div>
                <div className="employee-card__row">
                    <span className="employee-card__label">💰 Salary:</span>
                    <span className="employee-card__value">${employee.salary.toLocaleString()}</span>
                </div>
                {employee.updatedAt && (
                    <div className="employee-card__row">
                        <span className="employee-card__label">🕒 Updated:</span>
                        <span className="employee-card__value">
                            {new Date(employee.updatedAt).toLocaleString()}
                        </span>
                    </div>
                )}
            </div>
        </div>
    )
}

function EmployeeList({ employees }: { employees: Employee[] }) {
    return (
        <div className="employee-list">
            <div className="employee-list__count">
                {employees.length} employee{employees.length !== 1 ? 's' : ''} found
            </div>
            <div className="employee-list__items">
                {employees.map((emp) => (
                    <div key={emp.id} className="employee-list__item">
                        <div className="employee-list__item-header">
                            <strong>{emp.fullName}</strong>
                            <span className="employee-list__item-id">ID: {emp.id}</span>
                        </div>
                        <div className="employee-list__item-details">
                            {emp.email} • {emp.department} • ${emp.salary.toLocaleString()}
                        </div>
                    </div>
                ))}
            </div>
        </div>
    )
}

// ── Bulk helpers ──────────────────────────────────────────────────────────────

function BulkResultTable({ data }: { data: BulkOperationData }) {
    return (
        <div className="bulk-result">
            <div className="bulk-result__summary">
                <span className="bulk-result__badge bulk-result__badge--success">
                    ✅ {data.successCount} created
                </span>
                {data.failCount > 0 && (
                    <span className="bulk-result__badge bulk-result__badge--error">
                        ❌ {data.failCount} failed
                    </span>
                )}
            </div>
            <table className="bulk-result__table" role="grid">
                <thead>
                    <tr>
                        <th>#</th>
                        <th>Status</th>
                        <th>Name</th>
                        <th>Detail</th>
                    </tr>
                </thead>
                <tbody>
                    {data.results.map((r) => (
                        <tr key={r.row} className={r.success ? 'row--success' : 'row--error'}>
                            <td>{r.row}</td>
                            <td>{r.success ? '✅' : '❌'}</td>
                            <td>{(r.employee as Record<string, unknown>)?.fullName as string ?? '—'}</td>
                            <td>{r.error ?? 'Created successfully'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}

function BulkPreviewTable({ preview }: { preview: PreviewRow[] }) {
    const valid = preview.filter((r) => r.isValid).length
    const invalid = preview.length - valid
    return (
        <div className="bulk-preview">
            <div className="bulk-preview__summary">
                <span className="bulk-result__badge bulk-result__badge--success">{valid} valid</span>
                {invalid > 0 && (
                    <span className="bulk-result__badge bulk-result__badge--error">{invalid} invalid</span>
                )}
            </div>
            <table className="bulk-result__table" role="grid">
                <thead>
                    <tr>
                        <th>#</th>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Dept</th>
                        <th>Salary</th>
                        <th>OK?</th>
                    </tr>
                </thead>
                <tbody>
                    {preview.map((r) => (
                        <tr key={r.row} className={r.isValid ? 'row--success' : 'row--error'}>
                            <td>{r.row}</td>
                            <td>{r.fullName ?? '—'}</td>
                            <td>{r.email ?? '—'}</td>
                            <td>{r.department ?? '—'}</td>
                            <td>{r.salary != null ? `$${r.salary.toLocaleString()}` : '—'}</td>
                            <td>{r.isValid ? '✅' : '❌'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}
