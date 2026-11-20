/**
 * Client-side employee validation. Mirrors the backend FluentValidation rules
 * (CreateEmployeeValidator / UpdateEmployeeValidator) and always checks trimmed values,
 * because the trimmed values are what gets sent to the server.
 */

export interface EmployeeFormValues {
    fullName: string
    email: string
    department: string
    salary: string
}

export type EmployeeFormField = keyof EmployeeFormValues
export type EmployeeFormErrors = Partial<Record<EmployeeFormField, string>>

export const EMPLOYEE_FORM_FIELDS: readonly EmployeeFormField[] = ['fullName', 'email', 'department', 'salary']

export const NAME_MAX_LENGTH = 100
export const DEPARTMENT_MAX_LENGTH = 100
export const EMAIL_MAX_LENGTH = 256
export const SALARY_MAX = 9999999.99

/** Letters in any script (with combining marks), spaces, period, apostrophe and hyphen. */
const NAME_PATTERN = /^[\p{L}\p{M}\s.'-]+$/u
/** Letters, digits, spaces, ampersand and hyphen. */
const DEPARTMENT_PATTERN = /^[\p{L}\p{M}\p{N}\s&-]+$/u
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateEmployeeForm(values: EmployeeFormValues): EmployeeFormErrors {
    const errors: EmployeeFormErrors = {}
    const fullName = values.fullName.trim()
    const email = values.email.trim()
    const department = values.department.trim()
    const salaryText = values.salary.trim()

    if (!fullName) errors.fullName = 'Full name is required.'
    else if (fullName.length < 2) errors.fullName = 'Full name must be at least 2 characters.'
    else if (fullName.length > NAME_MAX_LENGTH) errors.fullName = `Full name cannot exceed ${NAME_MAX_LENGTH} characters.`
    else if (!NAME_PATTERN.test(fullName))
        errors.fullName = "Full name can only contain letters, spaces, periods, apostrophes and hyphens."

    if (!email) errors.email = 'Email is required.'
    else if (email.length > EMAIL_MAX_LENGTH) errors.email = `Email cannot exceed ${EMAIL_MAX_LENGTH} characters.`
    else if (!EMAIL_PATTERN.test(email)) errors.email = 'Enter a valid email.'

    if (!department) errors.department = 'Department is required.'
    else if (department.length < 2) errors.department = 'Department must be at least 2 characters.'
    else if (department.length > DEPARTMENT_MAX_LENGTH)
        errors.department = `Department cannot exceed ${DEPARTMENT_MAX_LENGTH} characters.`
    else if (!DEPARTMENT_PATTERN.test(department))
        errors.department = 'Department can only contain letters, digits, spaces, & and hyphens.'

    const salary = Number(salaryText)
    if (!salaryText) errors.salary = 'Salary is required.'
    else if (!Number.isFinite(salary) || salary <= 0) errors.salary = 'Enter a valid salary greater than 0.'
    else if (salary > SALARY_MAX) errors.salary = 'Salary cannot exceed 9,999,999.99.'

    return errors
}
