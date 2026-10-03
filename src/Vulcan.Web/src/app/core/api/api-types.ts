// API types for the Vulcan web application.

// Type representing the severity of a validation result.
export type ValidationSeverity = 'Reject' | 'Warn';

// Interface representing a validation limit for a specific key.
export interface ValidationLimit {
  key: string;
  severity: ValidationSeverity;
  min: number | null;
  max: number | null;
  unit: string;
  message: string;
  source: string;
}

// Interface representing the specific gravity of a material.
export interface SpecificGravity {
  id: number;
  material: 'Cement' | 'Water' | 'FlyAsh' | 'Slag' | 'SilicaFume';
  value: number;
}
