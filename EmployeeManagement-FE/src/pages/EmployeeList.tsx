import { useState, useEffect, useId } from 'react'
import { useNavigate } from 'react-router-dom'
import { employeesApi } from '../api/employees.api'
import { getErrorMessage, isCanceledError } from '../api/client'
import { EMPLOYEES_CHANGED_EVENT } from '../constants/events'
import type { Employee } from '../types/employee.types'
import Input from '../components/shared/Input'
import Button from '../components/shared/Button'
import Toast from '../components/shared/Toast'
import ConfirmDialog from '../components/shared/ConfirmDialog'
import { useToast } from '../hooks/useToast'
import './EmployeeList.css'

const PAGE_SIZE = 10
const SEARCH_DEBOUNCE_MS = 300

interface ListQuery {
    page: number
    search: string
    department: string
}

export default function EmployeeList() {
    const navigate = useNavigate()
    const { toast, showToast, hideToast } = useToast()
    const departmentListId = useId()

    // Raw input values update on every keystroke; `query` is the debounced request state.
    const [searchInput, setSearchInput] = useState('')
    const [departmentInput, setDepartmentInput] = useState('')
    const [query, setQuery] = useState<ListQuery>({ page: 1, search: '', department: '' })
    const [reloadToken, setReloadToken] = useState(0)

    const [employees, setEmployees] = useState<Employee[]>([])
    const [totalPages, setTotalPages] = useState(0)
    const [loading, setLoading] = useState(true)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [departments, setDepartments] = useState<string[]>([])
    const [deleteId, setDeleteId] = useState<number | null>(null)
    const [deleting, setDeleting] = useState(false)

    // Debounce the filters; changing a filter always returns to page 1.
    useEffect(() => {
        const search = searchInput.trim()
        const department = departmentInput.trim()
        const timer = setTimeout(() => {
            setQuery((q) => (q.search === search && q.department === department
                ? q
                : { page: 1, search, department }))
        }, SEARCH_DEBOUNCE_MS)
        return () => clearTimeout(timer)
    }, [searchInput, departmentInput])

    // Fetch the current page; a newer request aborts the previous one.
    useEffect(() => {
        const controller = new AbortController()
        let pageClamped = false
        setLoading(true)
        setLoadError(null)

        employeesApi
            .getAll({
                pageNumber: query.page,
                pageSize: PAGE_SIZE,
                search: query.search || undefined,
                department: query.department || undefined,
            }, controller.signal)
            .then((result) => {
                // The page no longer exists (e.g. its last row was deleted elsewhere): go to the last page.
                if (result.data.length === 0 && query.page > 1) {
                    pageClamped = true
                    setQuery((q) => ({ ...q, page: Math.max(1, Math.min(q.page - 1, result.totalPages)) }))
                    return
                }
                setEmployees(result.data)
                setTotalPages(result.totalPages)
            })
            .catch((err: unknown) => {
                if (controller.signal.aborted || isCanceledError(err)) return
                setLoadError(getErrorMessage(err, 'Failed to load employees.'))
            })
            .finally(() => {
                if (!controller.signal.aborted && !pageClamped) setLoading(false)
            })

        return () => controller.abort()
    }, [query, reloadToken])

    // Refetch when the chat assistant (or anything else) changes employee data.
    useEffect(() => {
        const handler = () => setReloadToken((t) => t + 1)
        window.addEventListener(EMPLOYEES_CHANGED_EVENT, handler)
        return () => window.removeEventListener(EMPLOYEES_CHANGED_EVENT, handler)
    }, [])

    // Department suggestions; an unavailable endpoint just means no suggestions.
    useEffect(() => {
        const controller = new AbortController()
        employeesApi
            .getDepartments(controller.signal)
            .then(setDepartments)
            .catch(() => undefined)
        return () => controller.abort()
    }, [reloadToken])

    const setPage = (page: number) => setQuery((q) => ({ ...q, page }))

    const handleDelete = async (id: number) => {
        setDeleting(true)
        try {
            await employeesApi.remove(id)
            showToast('Employee deleted successfully.', 'success')
            setDeleteId(null)
            if (employees.length === 1 && query.page > 1) {
                setPage(query.page - 1)
            } else {
                setReloadToken((t) => t + 1)
            }
        } catch (err) {
            showToast(getErrorMessage(err, 'Failed to delete employee.'), 'error')
        } finally {
            setDeleting(false)
        }
    }

    const deletingEmployee = employees.find((e) => e.id === deleteId)
    const hasFilters = Boolean(searchInput.trim() || departmentInput.trim())

    let content: React.ReactNode
    if (loadError) {
        content = (
            <div className="list-error" role="alert">
                <p>{loadError}</p>
                <Button variant="primary" size="sm" onClick={() => setReloadToken((t) => t + 1)}>
                    Retry
                </Button>
            </div>
        )
    } else if (loading && employees.length === 0) {
        content = (
            <div className="list-loading" aria-label="Loading employees">
                <div className="spinner" aria-hidden="true" />
                <p>Loading employees…</p>
            </div>
        )
    } else if (employees.length === 0) {
        content = (
            <div className="list-empty">
                <p>No employees found</p>
                <span className="empty-text">
                    {hasFilters
                        ? 'Try adjusting your search or filter.'
                        : 'Get started by adding an employee.'}
                </span>
            </div>
        )
    } else {
        content = (
            <div className="list-table-wrapper" aria-busy={loading}>
                <table className="list-table">
                    <thead>
                        <tr>
                            <th scope="col">Name</th>
                            <th scope="col">Email</th>
                            <th scope="col">Department</th>
                            <th scope="col">Salary</th>
                            <th scope="col">Created</th>
                            <th scope="col">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {employees.map((emp) => (
                            <tr key={emp.id} className="table-row">
                                <td data-label="Name">
                                    <span className="cell-name">{emp.fullName}</span>
                                </td>
                                <td data-label="Email">
                                    <span className="cell-email">{emp.email}</span>
                                </td>
                                <td data-label="Department">
                                    <span className="cell-tag">{emp.department}</span>
                                </td>
                                <td data-label="Salary">
                                    <span className="cell-salary">
                                        ${emp.salary.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                    </span>
                                </td>
                                <td data-label="Created">
                                    <span className="cell-date">
                                        {new Date(emp.createdAt).toLocaleDateString('en-US', {
                                            year: 'numeric', month: 'short', day: 'numeric',
                                        })}
                                    </span>
                                </td>
                                <td data-label="Actions">
                                    <div className="action-buttons">
                                        <Button
                                            variant="primary"
                                            size="sm"
                                            onClick={() => navigate(`/employees/${emp.id}/edit`)}
                                            aria-label={`Edit ${emp.fullName}`}
                                        >
                                            Edit
                                        </Button>
                                        <Button
                                            variant="destructive"
                                            size="sm"
                                            onClick={() => setDeleteId(emp.id)}
                                            disabled={deleting}
                                            aria-label={`Delete ${emp.fullName}`}
                                        >
                                            Delete
                                        </Button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        )
    }

    return (
        <div className="list-container">
            <div className="list-card">
                {/* Search + filter: never disabled, so typing keeps focus while results load */}
                <div className="list-controls">
                    <Input
                        name="search"
                        type="search"
                        value={searchInput}
                        onChange={(e) => setSearchInput(e.target.value)}
                        placeholder="Search by name or email…"
                        aria-label="Search employees"
                    />
                    <Input
                        name="department"
                        type="text"
                        value={departmentInput}
                        onChange={(e) => setDepartmentInput(e.target.value)}
                        placeholder="Filter by department…"
                        aria-label="Filter by department"
                        list={departments.length > 0 ? departmentListId : undefined}
                        autoComplete="off"
                    />
                    {departments.length > 0 && (
                        <datalist id={departmentListId}>
                            {departments.map((d) => <option key={d} value={d} />)}
                        </datalist>
                    )}
                </div>

                {content}

                {/* Pagination: shown whenever there is more than one page */}
                {totalPages > 1 && !loadError && (
                    <div className="list-pagination">
                        <Button
                            variant="ghost"
                            size="sm"
                            disabled={query.page <= 1 || loading}
                            onClick={() => setPage(query.page - 1)}
                            aria-label="Previous page"
                        >
                            Previous
                        </Button>
                        <span className="pagination-info" aria-live="polite">
                            Page {query.page} of {totalPages}
                        </span>
                        <Button
                            variant="ghost"
                            size="sm"
                            disabled={query.page >= totalPages || loading}
                            onClick={() => setPage(query.page + 1)}
                            aria-label="Next page"
                        >
                            Next
                        </Button>
                    </div>
                )}
            </div>

            {/* Delete confirmation */}
            <ConfirmDialog
                title="Delete Employee?"
                message={`Are you sure you want to delete ${deletingEmployee?.fullName ?? 'this employee'}? This action cannot be undone.`}
                confirmText="Delete"
                cancelText="Cancel"
                isDestructive
                isOpen={deleteId !== null}
                isLoading={deleting}
                onConfirm={() => deleteId !== null && handleDelete(deleteId)}
                onCancel={() => setDeleteId(null)}
            />

            {toast && (
                <Toast message={toast.message} type={toast.type} onClose={hideToast} />
            )}
        </div>
    )
}
