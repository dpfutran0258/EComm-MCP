export const EMPLOYEES_CHANGED_EVENT = 'employees:changed';
export function notifyEmployeesChanged(): void {
  window.dispatchEvent(new CustomEvent(EMPLOYEES_CHANGED_EVENT));
}
