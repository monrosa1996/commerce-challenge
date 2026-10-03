// Limits must match Upload:MaxFileSizeMb and Upload:MaxRows in the API appsettings.json
export const environment = {
  apiUrl: 'http://localhost:5080',
  maxFileSizeBytes: 5 * 1024 * 1024,
  maxRows: 50_000,
} as const;
