import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { employeesApi } from '../api/employees.api'
import type { Employee } from '../types/employee.types'
import Input from '../components/shared/Input'
import Button from '../components/shared/Button'
import Toast from '../components/shared/Toast'
import ConfirmDialog from '../components/shared/ConfirmDialog'
import { useToast } from '../hooks/useToast'
import './EmployeeList.css'

const PAGE_SIZE = 10

export default function EmployeeList() {
    const navigate = useNavigate()
    const { toast, showToast, hideToast } = useToast()

    const [employees, setEmployees] = useState<Employee[]>([])
    const [loading, setLoading] = useState(true)
    const [page, setPage] = useState(1)
    const [totalPages, setTotalPages] = useState(1)
    const [search, setSearch] = useState('')
    const [department, setDepartment] = useState('')
    const [deleteId, setDeleteId] = useState<number | null>(null)
    const [deleting, setDeleting] = useState(false)

    const fetchEmployees = useCallback(async () => {
        setLoading(true)
        try {
            const result = await employeesApi.getAll({
                pageNumber: page,
                pageSize: PAGE_SIZE,
                search: search || undefined,
                department: department || undefined,
            })
            setEmployees(result.data)
            setTotalPages(result.totalPages)
        } catch (err) {
            showToast(err instanceof Error ? err.message : 'Failed to load employees.', 'error')
        } finally {
            setLoading(false)
        }
    }, [page, search, department, showToast])

    useEffect(() => { fetchEmployees() }, [fetchEmployees])

    const handleDelete = async (id: number) => {
        setDeleting(true)
        try {
            await employeesApi.remove(id)
            showToast('Employee deleted successfully.', 'success')
            setDeleteId(null)
            fetchEmployees()
        } catch (err) {
            showToast(err instanceof Error ? err.message : 'Failed to delete employee.', 'error')
        } finally {
            setDeleting(false)
        }
    }

    const deletingEmployee = employees.find((e) => e.id === deleteId)

    return (
        <div className="list-container">
            <div className="list-card">
                {/* Search + filter */}
                <div className="list-controls">
                    <Input
                        name="search"
                        type="search"
                        value={search}
                        onChange={(e) => { setSearch(e.target.value); setPage(1) }}
                        placeholder="Search by name or email…"
                        disabled={loading}
                        aria-label="Search employees"
                    />
                    <Input
                        name="department"
                        type="text"
                        value={department}
                        onChange={(e) => { setDepartment(e.target.value); setPage(1) }}
                        placeholder="Filter by department…"
                        disabled={loading}
                        aria-label="Filter by department"
                    />
                </div>

                {/* Table */}
                {loading && employees.length === 0 ? (
                    <div className="list-loading" aria-label="Loading employees">
                        <div className="spinner" aria-hidden="true" />
                        <p>Loading employees…</p>
                    </div>
                ) : employees.length === 0 ? (
                    <div className="list-empty">
                        <p>No employees found</p>
                        <span className="empty-text">
                            {search || department
                                ? 'Try adjusting your search or filter.'
                                : 'Get started by adding an employee.'}
                        </span>
                    </div>
                ) : (
                    <>
                        <div className="list-table-wrapper">
                            <table className="list-table" role="grid">
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

                        {/* Pagination */}
                        <div className="list-pagination">
                            <Button
                                variant="ghost"
                                size="sm"
                                disabled={page === 1 || loading}
                                onClick={() => setPage((p) => p - 1)}
                                aria-label="Previous page"
                            >
                                Previous
                            </Button>
                            <span className="pagination-info" aria-live="polite">
                                Page {page} of {totalPages}
                            </span>
                            <Button
                                variant="ghost"
                                size="sm"
                                disabled={page === totalPages || loading}
                                onClick={() => setPage((p) => p + 1)}
                                aria-label="Next page"
                            >
                                Next
                            </Button>
                        </div>
                    </>
                )}
            </div>

            {/* Delete confirmation */}
            <ConfirmDialog
                title="Delete Employee?"
                message={`Are you sure you want to delete ${deletingEmployee?.fullName}? This action cannot be undone.`}
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
