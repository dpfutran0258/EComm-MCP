export interface Employee {
    id: number
    fullName: string
    email: string
    department: string
    salary: number
    createdAt: string
    updatedAt: string
}

export interface CreateEmployeePayload {
    fullName: string
    email: string
    department: string
    salary: number
}

export interface UpdateEmployeePayload {
    fullName: string
    email: string
    department: string
    salary: number
}

export interface EmployeeListResponse {
    data: Employee[]
    pageNumber: number
    pageSize: number
    totalCount: number
    totalPages: number
    hasNextPage: boolean
    hasPreviousPage: boolean
}

export interface EmployeeListParams {
    pageNumber?: number
    pageSize?: number
    search?: string
    department?: string
}
