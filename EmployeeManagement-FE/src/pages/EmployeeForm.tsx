import { useState, useEffect, useRef } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { employeesApi } from '../api/employees.api'
import { getErrorMessage, isApiError, mapFieldErrors } from '../api/client'
import type { EmployeeVersion } from '../types/employee.types'
import {
    EMPLOYEE_FORM_FIELDS,
    validateEmployeeForm,
    type EmployeeFormErrors,
    type EmployeeFormValues,
} from '../validation/employeeValidation'
import Input from '../components/shared/Input'
import Button from '../components/shared/Button'
import Toast from '../components/shared/Toast'
import { useToast } from '../hooks/useToast'
import './EmployeeForm.css'

const EMPTY_FORM: EmployeeFormValues = { fullName: '', email: '', department: '', salary: '' }

type LoadState = 'idle' | 'loading' | 'ready' | 'notFound' | 'error'

/** Parses a route id; returns undefined for anything that is not a positive integer. */
function parseEmployeeId(id: string | undefined): number | undefined {
    if (id === undefined || !/^\d+$/.test(id)) return undefined
    const value = Number(id)
    return Number.isSafeInteger(value) && value > 0 ? value : undefined
}

/**
 * Add / Edit employee form. App.tsx keys this component by route ("new" or the id),
 * so switching between Edit and Add always mounts a fresh, empty form.
 */
export default function EmployeeForm() {
    const navigate = useNavigate()
    const { id } = useParams<{ id?: string }>()
    const isEdit = id !== undefined
    const employeeId = parseEmployeeId(id)

    const { toast, showToast, hideToast } = useToast()
    const [formData, setFormData] = useState<EmployeeFormValues>(EMPTY_FORM)
    const [version, setVersion] = useState<EmployeeVersion | undefined>(undefined)
    const [errors, setErrors] = useState<EmployeeFormErrors>({})
    const [saving, setSaving] = useState(false)
    const [loadState, setLoadState] = useState<LoadState>(() =>
        !isEdit ? 'idle' : employeeId === undefined ? 'notFound' : 'loading')
    const [loadError, setLoadError] = useState('')
    const [reloadToken, setReloadToken] = useState(0)
    const redirectTimer = useRef<ReturnType<typeof setTimeout>>()

    useEffect(() => () => clearTimeout(redirectTimer.current), [])

    // Reset when the route changes without a remount (defensive; App.tsx keys the routes).
    useEffect(() => {
        setFormData(EMPTY_FORM)
        setVersion(undefined)
        setErrors({})
        setLoadState(!isEdit ? 'idle' : employeeId === undefined ? 'notFound' : 'loading')
    }, [isEdit, employeeId])

    // Load existing employee when editing
    useEffect(() => {
        if (employeeId === undefined) return
        const controller = new AbortController()
        setLoadState('loading')
        employeesApi
            .getById(employeeId, controller.signal)
            .then((emp) => {
                setFormData({
                    fullName: emp.fullName,
                    email: emp.email,
                    department: emp.department,
                    salary: emp.salary.toString(),
                })
                setVersion(emp.version)
                setLoadState('ready')
            })
            .catch((err: unknown) => {
                if (controller.signal.aborted) return
                if (isApiError(err) && err.status === 404) {
                    setLoadState('notFound')
                    return
                }
                setLoadError(getErrorMessage(err, 'Failed to load employee.'))
                setLoadState('error')
            })
        return () => controller.abort()
    }, [employeeId, reloadToken])

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target
        setFormData((prev) => ({ ...prev, [name]: value }))
        if (errors[name as keyof EmployeeFormErrors]) {
            setErrors((prev) => ({ ...prev, [name]: undefined }))
        }
    }

    const applyServerError = (err: unknown) => {
        if (isApiError(err)) {
            const fieldErrors = mapFieldErrors(err.fieldErrors, EMPLOYEE_FORM_FIELDS)
            // A 409 without field details for a duplicate email still belongs on the Email field.
            if (err.status === 409 && !fieldErrors.email && /e-?mail/i.test(err.message)) {
                fieldErrors.email = err.message
            }
            if (Object.keys(fieldErrors).length > 0) {
                setErrors((prev) => ({ ...prev, ...fieldErrors }))
                showToast('Please fix the highlighted fields.', 'error')
                return
            }
            if (err.status === 404) {
                setLoadState('notFound')
                return
            }
        }
        showToast(getErrorMessage(err, 'Operation failed.'), 'error')
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        const validationErrors = validateEmployeeForm(formData)
        setErrors(validationErrors)
        if (Object.keys(validationErrors).length > 0) return

        setSaving(true)
        const payload = {
            fullName: formData.fullName.trim(),
            email: formData.email.trim(),
            department: formData.department.trim(),
            salary: Number(formData.salary.trim()),
        }

        try {
            if (isEdit && employeeId !== undefined) {
                await employeesApi.update(employeeId, { ...payload, version })
                showToast('Employee updated successfully!', 'success')
            } else {
                await employeesApi.create(payload)
                showToast('Employee created successfully!', 'success')
            }
            redirectTimer.current = setTimeout(() => navigate('/'), 1200)
        } catch (err) {
            applyServerError(err)
        } finally {
            setSaving(false)
        }
    }

    if (loadState === 'loading') {
        return (
            <div className="form-container">
                <div className="form-card form-loading" aria-label="Loading employee data">
                    <div className="spinner" aria-hidden="true" />
                    <p>Loading employee…</p>
                </div>
            </div>
        )
    }

    if (loadState === 'notFound' || loadState === 'error') {
        const notFound = loadState === 'notFound'
        return (
            <div className="form-container">
                <div className="form-card form-state" role="alert">
                    <h2 className="form-title">{notFound ? 'Employee not found' : 'Could not load employee'}</h2>
                    <p className="form-state__text">
                        {notFound
                            ? 'The employee you are looking for does not exist or may have been deleted.'
                            : loadError}
                    </p>
                    <div className="form-actions">
                        {!notFound && (
                            <Button type="button" variant="primary" onClick={() => setReloadToken((t) => t + 1)}>
                                Retry
                            </Button>
                        )}
                        <Link className="form-state__link" to="/">← Back to employees</Link>
                    </div>
                </div>
            </div>
        )
    }

    return (
        <div className="form-container">
            <div className="form-card">
                <h2 className="form-title">{isEdit ? 'Edit Employee' : 'Add New Employee'}</h2>

                <form onSubmit={handleSubmit} className="form" noValidate>
                    <Input
                        label="Full Name"
                        name="fullName"
                        type="text"
                        value={formData.fullName}
                        onChange={handleChange}
                        error={errors.fullName}
                        placeholder="e.g. John Doe"
                        required
                        disabled={saving}
                        autoComplete="name"
                    />
                    <Input
                        label="Email"
                        name="email"
                        type="email"
                        value={formData.email}
                        onChange={handleChange}
                        error={errors.email}
                        placeholder="e.g. john@company.com"
                        required
                        disabled={saving}
                        autoComplete="email"
                    />
                    <Input
                        label="Department"
                        name="department"
                        type="text"
                        value={formData.department}
                        onChange={handleChange}
                        error={errors.department}
                        placeholder="e.g. Engineering"
                        required
                        disabled={saving}
                    />
                    <Input
                        label="Salary ($)"
                        name="salary"
                        type="number"
                        value={formData.salary}
                        onChange={handleChange}
                        error={errors.salary}
                        placeholder="0.00"
                        step="0.01"
                        min="0"
                        max="9999999.99"
                        required
                        disabled={saving}
                    />

                    <div className="form-actions">
                        <Button
                            type="button"
                            variant="ghost"
                            size="lg"
                            onClick={() => navigate('/')}
                            disabled={saving}
                        >
                            Cancel
                        </Button>
                        <Button
                            type="submit"
                            variant="primary"
                            size="lg"
                            isLoading={saving}
                            disabled={saving}
                        >
                            {isEdit ? 'Save Changes' : 'Create Employee'}
                        </Button>
                    </div>
                </form>
            </div>

            {toast && <Toast message={toast.message} type={toast.type} onClose={hideToast} />}
        </div>
    )
}
