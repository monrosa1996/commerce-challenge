export interface RowError {
  row: number | null;
  column: string | null;
  message: string;
}

export interface UploadResult {
  sourceFile: string;
  insertedCount: number;
  warnings: string[];
}

export interface ProcessResult {
  processDate: string;
  quarantinedCount: number;
}

export interface CommerceRecord {
  id: number;
  sourceFile: string;
  processDate: string;
  codComRed: string;
  nomComRed: string | null;
  tipDoc: string | null;
  numDoc: string | null;
  createdAt: string;
}

export interface QuarantineRecord {
  id: number;
  sourceFile: string;
  processDate: string;
  codComRed: string;
  nomComRed: string | null;
  tipDoc: string | null;
  numDoc: string | null;
  motivo: string;
  quarantinedAt: string;
}

export interface LoginResult {
  accessToken: string;
  expiresAt: string;
  username: string;
  displayName: string;
}
