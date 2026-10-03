import { RowError } from './models';

/** Error with a message ready to show to the user. Built from ProblemDetails by the error interceptor. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly errors: RowError[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError('Ocurrió un error inesperado.', 0);
}
