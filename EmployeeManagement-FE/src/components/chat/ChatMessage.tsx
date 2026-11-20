import ReactMarkdown from 'react-markdown'
import type { ChatMessage as ChatMessageType, BulkOperationData, SalaryStats, ToolActivity } from '../../types/chat.types'
import './ChatMessage.css'

interface ChatMessageProps {
    message: ChatMessageType
    /** Runs the destructive action held by this message. */
    onConfirm?: (messageId: string) => void
    /** Dismisses the destructive action held by this message. */
    onCancel?: (messageId: string) => void
    /** Disables the Confirm/Cancel buttons while a request is running. */
    busy?: boolean
}

const TOOL_LABELS: Record<string, string> = {
    list_employees: 'Listed employees',
    find_employees: 'Searched employees',
    get_employee: 'Looked up an employee',
    create_employee: 'Created an employee',
    update_employee: 'Updated an employee',
    delete_employee: 'Deleted an employee',
    bulk_create_employees: 'Created employees',
    bulk_update_employees: 'Updated employees',
    bulk_delete_employees: 'Deleted employees',
    list_departments: 'Listed departments',
    get_salary_stats: 'Calculated salary stats',
}

export default function ChatMessage({ message, onConfirm, onCancel, busy = false }: ChatMessageProps) {
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

                    {/* Salary statistics */}
                    {message.data && isStatsData(message.data) && (
                        <StatsCard stats={message.data['stats'] as SalaryStats} />
                    )}

                    {/* Tools the assistant used for this answer */}
                    {message.toolActivity && message.toolActivity.length > 0 && (
                        <ToolChips activity={message.toolActivity} />
                    )}

                    {/* Destructive action waiting for the user */}
                    {message.pendingAction && (
                        <ConfirmActions
                            message={message}
                            busy={busy}
                            onConfirm={onConfirm}
                            onCancel={onCancel}
                        />
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

function isStatsData(data: Record<string, unknown>): boolean {
    return data.stats != null && typeof data.stats === 'object'
}

const money = (n: number) => `$${Math.round(n).toLocaleString()}`

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
                    ✅ {data.successCount} succeeded
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
                            <td>{((r.employee as Record<string, unknown> | undefined)?.fullName as string | undefined) ?? r.name ?? '—'}</td>
                            <td>{r.error ?? 'OK'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}

// ── Salary statistics ─────────────────────────────────────────────────────────

function StatsCard({ stats }: { stats: SalaryStats }) {
    return (
        <div className="stats-card">
            <div className="stats-card__totals">
                <span><strong>{stats.employeeCount}</strong> employees</span>
                <span>Avg <strong>{money(stats.averageSalary)}</strong></span>
                <span>Min <strong>{money(stats.minSalary)}</strong></span>
                <span>Max <strong>{money(stats.maxSalary)}</strong></span>
            </div>
            {stats.byDepartment.length > 1 && (
                <table className="bulk-result__table">
                    <thead>
                        <tr><th>Department</th><th>Count</th><th>Average</th></tr>
                    </thead>
                    <tbody>
                        {stats.byDepartment.map((d) => (
                            <tr key={d.department}>
                                <td>{d.department}</td>
                                <td>{d.count}</td>
                                <td>{money(d.averageSalary)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    )
}

// ── Tool activity ─────────────────────────────────────────────────────────────

function ToolChips({ activity }: { activity: ToolActivity[] }) {
    return (
        <ul className="tool-chips" aria-label="Actions the assistant took">
            {activity.map((a, i) => (
                <li key={`${a.tool}-${i}`} className={`tool-chip${a.isError ? ' tool-chip--error' : ''}`}>
                    {a.isError ? '⚠️' : '🔧'} {TOOL_LABELS[a.tool] ?? a.tool}
                </li>
            ))}
        </ul>
    )
}

// ── Confirmation of destructive actions ───────────────────────────────────────

function ConfirmActions({ message, busy, onConfirm, onCancel }: {
    message: ChatMessageType
    busy: boolean
    onConfirm?: (messageId: string) => void
    onCancel?: (messageId: string) => void
}) {
    const state = message.pendingState ?? 'open'
    const expired = message.pendingAction !== undefined
        && Date.parse(message.pendingAction.expiresAtUtc) < Date.now()

    if (state === 'confirmed') return <p className="confirm-actions__status">Confirmed</p>
    if (state === 'cancelled') return <p className="confirm-actions__status">Cancelled</p>
    if (expired) return <p className="confirm-actions__status">Expired, please ask again.</p>

    return (
        <div className="confirm-actions" role="group" aria-label="Confirm this action">
            <button
                type="button"
                className="confirm-actions__btn confirm-actions__btn--danger"
                disabled={busy}
                onClick={() => onConfirm?.(message.id)}
            >
                Confirm
            </button>
            <button
                type="button"
                className="confirm-actions__btn"
                disabled={busy}
                onClick={() => onCancel?.(message.id)}
            >
                Cancel
            </button>
        </div>
    )
}
