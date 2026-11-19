import { useState, useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { employeesApi } from '../api/employees.api'
import Input from '../components/shared/Input'
import Button from '../components/shared/Button'
import Toast from '../components/shared/Toast'
import { useToast } from '../hooks/useToast'
import './EmployeeForm.css'

interface FormData {
    fullName: string
    email: string
    department: string
    salary: string
}

interface FormErrors {
    fullName?: string
    email?: string
    department?: string
    salary?: string
}

export default function EmployeeForm() {
    const navigate = useNavigate()
    const { id } = useParams<{ id?: string }>()
    const isEdit = id !== undefined
    const employeeId = id ? parseInt(id, 10) : undefined

    const { toast, showToast, hideToast } = useToast()
    const [formData, setFormData] = useState<FormData>({ fullName: '', email: '', department: '', salary: '' })
    const [errors, setErrors] = useState<FormErrors>({})
    const [loading, setLoading] = useState(false)
    const [loadingData, setLoadingData] = useState(isEdit)

    // Load existing employee when editing
    useEffect(() => {
        if (!employeeId) return
        setLoadingData(true)
        employeesApi
            .getById(employeeId)
            .then((emp) => {
                setFormData({
                    fullName: emp.fullName,
                    email: emp.email,
                    department: emp.department,
                    salary: emp.salary.toString(),
                })
            })
            .catch((err) => {
                showToast(err instanceof Error ? err.message : 'Failed to load employee.', 'error')
            })
            .finally(() => setLoadingData(false))
    }, [employeeId, showToast])

    const validate = (): boolean => {
        const e: FormErrors = {}
        if (!formData.fullName.trim()) e.fullName = 'Full name is required.'
        else if (formData.fullName.length < 2 || formData.fullName.length > 100)
            e.fullName = 'Must be between 2 and 100 characters.'

        if (!formData.email.trim()) e.email = 'Email is required.'
        else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) e.email = 'Enter a valid email.'

        if (!formData.department.trim()) e.department = 'Department is required.'
        else if (formData.department.length < 2 || formData.department.length > 50)
            e.department = 'Must be between 2 and 50 characters.'

        const salary = parseFloat(formData.salary)
        if (!formData.salary) e.salary = 'Salary is required.'
        else if (isNaN(salary) || salary <= 0) e.salary = 'Enter a valid salary greater than 0.'
        else if (salary > 9999999.99) e.salary = 'Salary cannot exceed 9,999,999.99.'

        setErrors(e)
        return Object.keys(e).length === 0
    }

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target
        setFormData((prev) => ({ ...prev, [name]: value }))
        if (errors[name as keyof FormErrors]) {
            setErrors((prev) => ({ ...prev, [name]: undefined }))
        }
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        if (!validate()) return

        setLoading(true)
        const payload = {
            fullName: formData.fullName.trim(),
            email: formData.email.trim(),
            department: formData.department.trim(),
            salary: parseFloat(formData.salary),
        }

        try {
            if (isEdit && employeeId) {
                await employeesApi.update(employeeId, payload)
                showToast('Employee updated successfully!', 'success')
            } else {
                await employeesApi.create(payload)
                showToast('Employee created successfully!', 'success')
            }
            setTimeout(() => navigate('/'), 1200)
        } catch (err) {
            showToast(err instanceof Error ? err.message : 'Operation failed.', 'error')
        } finally {
            setLoading(false)
        }
    }

    if (loadingData) {
        return (
            <div className="form-container">
                <div className="form-card form-loading" aria-label="Loading employee data">
                    <div className="spinner" aria-hidden="true" />
                    <p>Loading employee…</p>
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
                        disabled={loading}
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
                        disabled={loading}
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
                        disabled={loading}
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
                        disabled={loading}
                    />

                    <div className="form-actions">
                        <Button
                            type="button"
                            variant="ghost"
                            size="lg"
                            onClick={() => navigate('/')}
                            disabled={loading}
                        >
                            Cancel
                        </Button>
                        <Button
                            type="submit"
                            variant="primary"
                            size="lg"
                            isLoading={loading}
                            disabled={loading}
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
