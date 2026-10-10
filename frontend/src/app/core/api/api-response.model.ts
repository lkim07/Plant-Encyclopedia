/** Successful single-resource response envelope (API_SPEC §9). */
export interface ApiResponse<T> {
  readonly data: T;
}

/** Error response body (API_SPEC §12). */
export interface ApiErrorResponse {
  readonly error: {
    readonly code: string;
    readonly message: string;
    readonly details?: Readonly<Record<string, string>>;
  };
}
