/** Successful single-resource response envelope (API_SPEC §9). */
export interface ApiResponse<T> {
  readonly data: T;
}

/** Successful paginated response envelope (API_SPEC §9–10). */
export interface PagedResponse<T> {
  readonly data: T;
  readonly pagination: Pagination;
}

export interface Pagination {
  readonly page: number;
  readonly pageSize: number;
  readonly hasNext: boolean;
}

/** Error response body (API_SPEC §12). */
export interface ApiErrorResponse {
  readonly error: {
    readonly code: string;
    readonly message: string;
    readonly details?: Readonly<Record<string, string>>;
  };
}
