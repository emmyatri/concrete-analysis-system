// STAND-IN API TYPES - TODO:(SCRUM-107) - Reconcile these API types with the shared models.ts when the API-client work merges.

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
