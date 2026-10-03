import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError } from './api-error';
import { RowError } from './models';

interface ProblemBody {
  detail?: unknown;
  errors?: unknown;
}

function fromResponse(response: HttpErrorResponse): ApiError {
  if (response.status === 0) {
    return new ApiError('No se pudo conectar con el servidor. Revise que el API esté en ejecución.', 0);
  }

  const body = (response.error ?? {}) as ProblemBody;
  const detail = typeof body.detail === 'string' ? body.detail : null;
  const errors = Array.isArray(body.errors) ? (body.errors as RowError[]) : [];

  switch (response.status) {
    case 401:
      return new ApiError(detail ?? 'Su sesión ha expirado o no es válida. Inicie sesión de nuevo.', 401);
    case 413:
      return new ApiError('El archivo supera el tamaño máximo permitido.', 413);
    case 409:
      return new ApiError(detail ?? 'Hay registros que ya existen.', 409);
    case 400:
      return new ApiError(detail ?? 'La solicitud no es válida.', 400, errors);
    default:
      return new ApiError('Error interno del servidor. Intente de nuevo más tarde.', response.status);
  }
}

export const errorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      throwError(() =>
        error instanceof HttpErrorResponse ? fromResponse(error) : new ApiError('Ocurrió un error inesperado.', 0),
      ),
    ),
  );
