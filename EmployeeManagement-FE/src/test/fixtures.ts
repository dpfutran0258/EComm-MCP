import type { Employee, EmployeeListResponse } from '../types/employee.types'

export function makeEmployee(id: number, overrides: Partial<Employee> = {}): Employee {
    return {
        id,
        fullName: `Employee ${id}`,
        email: `employee${id}@example.com`,
        department: 'Engineering',
        salary: 50000 + id,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides,
    }
}

export function makePage(data: Employee[], pageNumber: number, totalCount: number, pageSize = 10): EmployeeListResponse {
    const totalPages = Math.ceil(totalCount / pageSize)
    return {
        data,
        pageNumber,
        pageSize,
        totalCount,
        totalPages,
        hasNextPage: pageNumber < totalPages,
        hasPreviousPage: pageNumber > 1,
    }
}
