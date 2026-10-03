export interface ValidationSeverity {
  severity: 'Reject' | 'Warn';
}

export interface ValidationLimit {
  key: string;
  severity: ValidationSeverity;
  min: number | null;
  max: number | null;
  unit: string;
  message: string;
  source: string;
}

export interface SpecificGravity {
  id: number;
  material: 'Cement' | 'Water' | 'FlyAsh' | 'Slag' | 'SilicaFume';
  value: number;
}
