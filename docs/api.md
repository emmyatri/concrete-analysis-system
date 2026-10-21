# API Documentation

## Common Results

### `400` Response

A `400` status code represents a malformed request or one or more fields that failed validation. Validation errors from every endpoint returns the following:

```json
{
    "title": "ValidationProblemDetails",
    "type": "object",
    "properties": {
        "title": {
            "type": "string",
        },
        "status": {
            "type": "number"
        },
        "errors": {
            "type": "object",
            "additionalProperties": {
                "type": "array",
                "items": {
                    "type": "string"
                }
            }
        }
    },
    "required": ["title", "status", "errors"]
}
```

## Batch

`cementSurfacePh` records cement surface pH separately from `mixWaterPh` (SCRUM-149,
FR-7 extended, NFR-6). Send it inside `batch` for POST and at the top level for PUT.
It is required on create/update, accepts at most two decimal places, rejects values
outside 0-14, and saves with a warning outside 7.1-8.0. Numeric ranges and warning
messages come from the configured `Batch.CementSurfacePh` validation limits.

Omitted or null values return HTTP 400 with an error keyed `Batch.CementSurfacePh`.
Detail responses include `cementSurfacePh`; historical batches can return null.
New and edited batches must supply a measurement; historical values are not backfilled.

### Create Batch

Use to create a batch.

**Endpoint:** `POST /api/batches`

**Request Schema:**

```json
{
    "title": "CreateBatchRequest",
    "type": "object",
    "properties": {
        "batch": {
            "type": "object",
            "properties": {
                "batchNumber": {
                    "type": "string"
                },
                "castAt": {
                    "type": "string",
                    "format": "date-time"
                },
                "finalWeightLb": {
                    "type": "number"
                },
                "cementType": {
                    "enum": ["TypeI", "TypeII", "TypeIII"]
                },
                "mixWaterPh": {
                    "type": "number"
                },
                "cementSurfacePh": {
                    "type": "number",
                    "multipleOf": 0.01
                },
                "isSynthetic": {
                    "type": "boolean",
                    "default": false
                },
                "expectedFinalBreakPsi": {
                    "type": "integer",
                }
            },
            "required": [
                "batchNumber",
                "castAt",
                "finalWeightLb",
                "cementType",
                "mixWaterPh",
                "cementSurfacePh",
                "expectedFinalBreakResultPsi"
            ]
        },
        "composition": {
            "type": "object",
            "properties": {
                "cementPct": {
                    "type": "number"
                },
                "waterPct": {
                    "type": "number"
                },
                "coarseAggregatePct": {
                    "type": " number"
                },
                "fineAggregatePct": {
                    "type": "number"
                },
                "scmType": {
                    "enum": ["None", "FlyAsh", "Slag", "SilicaFume"]
                },
                "scmPct": {
                    "type": "number"
                },
            },
            "required": [
                "cementPct",
                "waterPct",
                "coarseAggregatePct",
                "fineAggregatePct",
                "scmType",
                "scmPct"
            ]
        },
        "oxides": {
            "type": "object",
            "properties": {
                "aluminaPct": {
                    "type": "number"
                },
                "ironOxidePct": {
                    "type": "number"
                },
                "magnesiumOxidePct": {
                    "type": "number"
                },
                "sulfurTrioxidePct": {
                    "type": "number"
                },
            },
            "required": [
                "aluminaPct",
                "ironOxidePct",
                "magnesiumOxidePct",
                "sulfurTrioxidePct"
            ]
        },
    },
    "required": ["batch"]
}
```

**Response Status Codes:**

* `201`: if the batch was successfully created
* `400`: if any part of the request is malformed
* `409`: if the batch number already exists

**Response Schema:**

Sent with the `201` response.

```json
{
    "title": "CreateBatchResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "number"
        },
        "batchNumber": {
            "type": "number"
        },
        "warnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "message": {
                        "type": "string"
                    }
                },
                "required": ["key", "message"]
            }
        }
    },
    "required": ["id", "batchNumber", "warnings"]
}
```

### Update Batch

Updates the batch by batch number.

**Endpoint:** `PUT /api/batches/{number}`

**Request Parameters:**

* `number`: from route. Represents the batch number to update.

**Request Schema:**

```json
{
    "title": "UpdateBatchRequest",
    "type": "object",
    "properties": {
        "castAt": {
            "type": "string",
            "format": "date-time"
        },
        "finalWeightLb": {
            "type": "number"
        },
        "cementType": {
            "enum": ["TypeI", "TypeII", "TypeIII"]
        },
        "mixWaterPh": {
            "type": "number"
        },
        "cementSurfacePh": {
            "type": "number",
            "multipleOf": 0.01
        },
        "isSynthetic": {
            "type": "boolean",
            "default": false
        },
        "expectedFinalBreakPsi": {
            "type": "integer"
        },
        "composition": {
            "type": "object",
            "properties": {
                "cementPct": {
                    "type": "number"
                },
                "waterPct": {
                    "type": "number"
                },
                "coarseAggregatePct": {
                    "type": " number"
                },
                "fineAggregatePct": {
                    "type": "number"
                },
                "scmType": {
                    "enum": ["None", "FlyAsh", "Slag", "SilicaFume"]
                },
                "scmPct": {
                    "type": "number"
                },
            },
            "required": [
                "cementPct",
                "waterPct",
                "coarseAggregatePct",
                "fineAggregatePct",
                "scmType",
                "scmPct"
            ]
        },
        "oxides": {
            "type": "object",
            "properties": {
                "aluminaPct": {
                    "type": "number"
                },
                "ironOxidePct": {
                    "type": "number"
                },
                "magnesiumOxidePct": {
                    "type": "number"
                },
                "sulfurTrioxidePct": {
                    "type": "number"
                },
            },
            "required": [
                "aluminaPct",
                "ironOxidePct",
                "magnesiumOxidePct",
                "sulfurTrioxidePct"
            ]
        },
    },
    "required": ["castAt", "finalWeightLb", "cementType", "mixWaterPh", "cementSurfacePh", "expectedFinalBreakPsi"]
}
```

**Response Status Codes:**

* `200`: if the batch was successfully updated or there were no changes to apply
* `400`: if the request was malformed
* `404`: if the batch number provided in the route does not exist

**Response Schema:**

Sent with the `200` response.

```json
{
    "title": "UpdateBatchResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "number"
        },
        "batchNumber": {
            "type": "number"
        },
        "warnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "message": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["id", "batchNumber", "warnings"]
}
```

### Get Batch

Returns the batch with the provided batch number.

**Endpoint:** `GET /api/batches/{number}`

**Request Parameters:**

* `number`: from route. Must be the batch number of an existing batch.

**Response Status Codes:**

* `200`: if the batch was found
* `404`: if the batch was not found

**Response Schema:**

Sent with the `200` response.

```json
{
    "title": "GetBatchResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "integer"
        },
        "batchNumber": {
            "type": "string"
        },
        "castAt": {
            "type": "string",
            "format": "date-time"
        },
        "finalWeightLb": {
            "type": "number"
        },
        "cementType": {
            "enum": ["TypeI", "TypeII", "TypeIII"]
        },
        "mixWaterPh": {
            "type": "number"
        },
        "cementSurfacePh": {
            "type": ["number", "null"],
            "multipleOf": 0.01
        },
        "isSynthetic": {
            "type": "boolean",
        },
        "createdAtUtc": {
            "type": "string",
            "format": "date-time"
        },
        "createdBy": {
            "type": "string"
        },
        "modifiedAtUtc": {
            "type": "string",
            "format": "date-time"
        },
        "modifiedBy": {
            "type": "string",
        },
        "warnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "id": {
                        "type": "integer"
                    },
                    "entityType": {
                        "enum": [
                            "Batch",
                            "Composition",
                            "CementOxideAnalysis",
                            "Specimen",
                            "BreakResult",
                            "CuringReading"
                        ]
                    },
                    "entityId": {
                        "type": "integer"
                    },
                    "fieldName": {
                        "type": "string"
                    },
                    "limitKey": {
                        "type": "string"
                    },
                    "value": {
                        "type": "number"
                    },
                    "message": {
                        "type": "string"
                    },
                    "createdAtUtc": {
                        "type": "string",
                        "format": "date-time"
                    }
                },
                "required": [
                    "id",
                    "entityType",
                    "entityId",
                    "fieldName",
                    "limitKey",
                    "value"
                    "message",
                    "createdAtUtc"
                ]
            }
        },
        "composition": {
            "type": "object",
            "properties": {
                "cementPct": {
                    "type": "number"
                },
                "waterPct": {
                    "type": "number"
                },
                "coarseAggregatePct": {
                    "type": " number"
                },
                "fineAggregatePct": {
                    "type": "number"
                },
                "scmType": {
                    "enum": ["None", "FlyAsh", "Slag", "SilicaFume"]
                },
                "scmPct": {
                    "type": "number"
                },
                "totalPct": {
                    "type": "number"
                },
                "airOtherPct": {
                    "type": "number"
                },
                "waterCementRatio": {
                    "type": "number"
                }
            },
            "required": [
                "cementPct",
                "waterPct",
                "coarseAggregatePct",
                "fineAggregatePct",
                "scmType",
                "scmPct",
                "totalPct",
                "airOtherPct"
            ]
        },
        "oxides": {
            "type": "object",
            "properties": {
                "aluminaPct": {
                    "type": "number"
                },
                "ironOxidePct": {
                    "type": "number"
                },
                "magnesiumOxidePct": {
                    "type": "number"
                },
                "sulfurTrioxidePct": {
                    "type": "number"
                },
            },
            "required": [
                "aluminaPct",
                "ironOxidePct",
                "magnesiumOxidePct",
                "sulfurTrioxidePct"
            ]
        },
        "specimens": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "id": {
                        "type": "string"
                    },
                    "label": {
                        "type": "string"
                    },
                    "lengthIn": {
                        "type": "number"
                    },
                    "widthIn": {
                        "type": "number"
                    },
                    "testAgeDays": {
                        "enum": ["Day3", "Day7", "Day28"]
                    },
                    "isSynthetic": {
                        "type": "boolean"
                    },
                    "breakResult": {
                        "type": "object",
                        "properties": {
                            "id": {
                                "type": "integer"
                            },
                            "brokenAt": {
                                "type": "string",
                                "format": "date-time"
                            },
                            "strengthPsi": {
                                "type": "integer"
                            }
                        },
                        "required": ["id", "brokenAt", "strengthPsi"]
                    },
                    "curingReadings": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "id": {
                                    "type": "integer",
                                },
                                "takenAt": {
                                    "type": "string",
                                    "format": "date-time",
                                },
                                "internalTempF": {
                                    "type": "number"
                                },
                                "ambientTempF": {
                                    "type": "number"
                                },
                                "moisturePct": {
                                    "type": "number"
                                },
                                "relativeHumidityPct": {
                                    "type": "number"
                                },
                                "resistivityOhmPerFt": {
                                    "type": "number"
                                },
                                "co2Ppm": {
                                    "type": "number"
                                }
                            },
                            "required": [
                                "id",
                                "takenAt",
                                "internalTempF",
                                "ambientTempF",
                                "moisturePct",
                                "relativeHumidityPct",
                                "resistivityOhmPerFt",
                                "co2Ppm"
                            ]
                        }
                    }
                },
                "required": [
                    "id",
                    "label",
                    "lengthIn",
                    "widthIn",
                    "testAgeDays",
                    "isSynthetic"
                ]
            }
        }
    },
    "required": [
        "id",
        "batchNumber",
        "castAt",
        "finalWeightLb",
        "cementType",
        "mixWaterPh",
        "isSynthetic",
        "createdAtUtc",
        "createdBy",
        "warnings"
    ]
}
```


### Get Batch List

Returns a paged list of batches.

**Endpoint:** `GET /api/batches`

**Request Parameters:**

* `search`: (optional) from query. The batch search query.
* `castFrom`: (optional) from query. Beginning of range to filter by each batch's castAt field.
* `castTo`: (optional) from query. End of range to filter by each batch's castAt field.
* `cementType`: (optional) from query. Filters by cement type.
* `hasWarnings`: (optional) from query. Filters batches by whether they have warnings.
* `includeSynthetic`: (optional) from query. Whether to include synthetic batches. Defaults to true.
* `page`: (optional) from query. Page number, starting at 1. Defaults to 1. Each page contains up to 50 batches.

**Response Status Codes:**

* `200`: if the server was able to respond with the list of batches

**Response Schema:**

Send with the `200` response.

```json
{
    "title": "GetBatchListResponse",
    "type": "array",
    "items": {
        "type": "object",
        "properties": {
            "id": {
                "type": "integer"
            },
            "batchNumber": {
                "type": "string"
            },
            "castAt": {
                "type": "string",
                "format": "date-time"
            },
            "finalWeightLb": {
                "type": "number"
            },
            "cementType": {
                "enum": ["TypeI", "TypeII", "TypeIII"]
            },
            "mixWaterPh": {
                "type": "number"
            },
            "isSynthetic": {
                "type": "boolean"
            },
            "expectedFinalBreakPsi": {
                "type": "integer"
            },
            "cylinderCount": {
                "type": "integer"
            },
            "breaksRecorded": {
                "type": "integer"
            },
            "average28DayPsi": {
                "type": "number"
            },
            "warningCount": {
                "type": "integer"
            }
        },
        "required": [
            "batchNumber",
            "castAt",
            "finalWeightLb",
            "cementType",
            "mixWaterPh",
            "isSynthetic",
            "expectedFinalBreakPsi",
            "cylinderCount",
            "breaksRecorded",
            "warningCount"
        ]
    }
}
```

## Specimen

### Create Specimen

Use to create a new specimen under a batch.

**Endpoint:** `POST /api/batches/{number}/specimens`

**Request Parameters:**

* `number`: from route. Must be the number of an existing batch.

**Request Schema:**

```json
{
    "title": "CreateSpecimenRequest",
    "type": "object",
    "properties": {
        "label": {
            "type": "string",
        },
        "lengthIn": {
            "type": "number",
        },
        "widthIn": {
            "type": "number",
        },
        "testAgeDays": {
            "enum": ["Day3", "Day7", "Day28"]
        },
        "isSynthetic": {
            "type": "boolean",
            "default": false
        }
    },
    "required": [
        "label",
        "lengthIn",
        "widthIn",
        "testAgeDays"
    ]
}
```

**Response Status Codes:**

* `201`: if the specimen was successfully created
* `400`: if the request was malformed
* `404`: if the batch number was not found

**Response Schema:**

Sent with the `201` response.

```json
{
    "title": "CreateSpecimenResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "number",
            "description": "Specimen id"
        },
        "label": {
            "type": "string"
        },
        "batchNumber": {
            "type": "string"
        },
        "warnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "message": {
                        "type": "string"
                    }
                },
                "required": ["key", "message"]
            }
        }
    },
    "required": ["id", "label", "batchNumber", "warnings"]
}
```

### Create Break Result

Use to create a new break result for a specimen.

**Endpoint:** `POST /api/specimens/{id}/break`

**Request Parameters:**

* `id`: from route. Must be the id of an existing specimen.

**Request Schema:**

```json
{
    "title": "CreateBreakResultRequest",
    "type": "object",
    "properties": {
        "brokenAt": {
            "type": "string",
            "format": "date-time"
        },
        "strengthPsi": {
            "type": "integer"
        }
    },
    "required": ["brokenAt", "strengthPsi"]
}
```

**Response Status Codes:**

* `201`: if the break result was successfully created
* `400`: if the request was malformed
* `404`: if the specimen `id` does not exist
* `409`: if the specimen already has a break result

**Response Schema:**

Sent with the `201` response.

```json
{
    "title": "CreateBreakResultResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "integer",
            "description": "Break result id"
        },
        "specimenId": {
            "type": "integer",
        },
        "warnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "message": {
                        "type": "string"
                    }
                },
                "required": ["key", "message"]
            }
        }
    },
    "required": ["id", "specimenId", "warnings"]
}
```

### Create Curing Reading

Use to create a curing reading for a specimen.

**Endpoint:** `POST /api/specimens/{id}/readings`

**Request Parameters:**

* `id`: from route. Must be the id of an existing specimen.

**Request Schema:**

```json
{
    "title": "CreateCuringReadingRequest",
    "type": "object",
    "properties": {
        "takenAt": {
            "type": "string",
            "format": "date-time",
        },
        "internalTempF": {
            "type": "number"
        },
        "ambientTempF": {
            "type": "number"
        },
        "moisturePct": {
            "type": "number"
        },
        "relativeHumidityPct": {
            "type": "number"
        },
        "resistivityOhmPerFt": {
            "type": "number"
        },
        "co2Ppm": {
            "type": "number"
        }
    },
    "required": [
        "takenAt",
        "internalTempF",
        "ambientTempF",
        "moisturePct",
        "relativeHumidityPct",
        "resistivityOhmPerFt",
        "co2Ppm"
    ]
}
```

**Response Status Codes:**

* `201`: if the curing reading was successfully created
* `400`: if the request was malformed
* `404`: if the specimen `id` does not exist

**Response Schema:**

Sent with the `201` response.

```json
{
    "title": "CreateCuringReadingResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "integer",
            "description": "Curing reading id"
        },
        "specimenId": {
            "type": "integer"
        },
        "warnings": {
            "type": "array",
            "items": {
                "type": "object",
                "default": [],
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "message": {
                        "type": "string"
                    }
                },
                "required": ["key", "message"]
            }
        }
    },
    "required": ["id", "specimenId", "warnings"]
}
```

## Validation Limit

### Get Validation Limit List

Use to get all validation limits.

**Endpoint:** `GET /api/validation-limits`

**Response Status Codes:**

* `200`: if the server was able to respond with a list of all validation limits

**Response Schema:**

Sent with the `200` response.

```json
{
    "title": "GetValidationLimitListResponse",
    "type": "array",
    "default": [],
    "items": {
        "type": "object",
        "properties": {
            "key": {
                "type": "string"
            },
            "severity": {
                "enum": ["Reject", "Warn"]
            },
            "min": {
                "type": "number"
            },
            "max": {
                "type": "number"
            },
            "unit": {
                "type": "string"
            },
            "message": {
                "type": "string"
            },
            "source": {
                "type": "string"
            }
        },
        "required": [
            "key",
            "severity",
            "unit",
            "message",
            "source"
        ]
    }
}
```

## Specific Gravity

### Get Specific Gravity List

Use to get all specific gravities.

**Endpoint:** `GET /api/specific-gravities`

**Response Status Codes:**

* `200`: if the server was able to return a list of all specific gravities.

**Response Schema:**

Sent with the `200` response.

```json
{
    "title": "GetSpecificGravityListResponse",
    "type": "array",
    "default": [],
    "items": {
        "type": "object",
        "properties": {
            "material": {
                "enum": [
                    "Cement",
                    "Water",
                    "FlyAsh",
                    "Slag",
                    "SilicaFume"
                ]
            },
            "value": {
                "type": "number"
            }
        },
        "required": ["material", "value"]
    }
}
```

## Prediction

### Get Batch Strength Prediction

Use to get the strength predictions for a given batch.

**Endpoint:** `GET /api/batches/{batchNumber}/strength`

**Request Parameters:**

* `batchNumber`: from route. Must be the batch number of an existing batch.

**Response Status Codes:**

- `404`: if the batch is not found
- `200`: if the batch was found

**Response Schema:**

```json
{
    "title": "GetBatchStrengthPredictionResponse",
    "type": "object",
    "properties": {
        "id": {
            "type": "integer"
        },
        "batchNumber": {
            "type": "string"
        },
        "isSynthetic": {
            "type": "boolean"
        },
        "expectedFinalBreakPsi": {
            "type": "integer"
        },
        "cementType": {
            "enum": ["TypeI", "TypeII", "TypeIII"]
        },
        "status": {
            "enum": ["Estimated", "InsufficientCuringData"]
        },
        "atRisk": {
            "type": "boolean"
        },
        "projected28DayPsi": {
            "type": ["integer", "null"]
        },
        "shortfallPsi": {
            "type": ["integer", "null"]
        },
        "shortfallPct": {
            "type": ["number", "null"]
        },
        "curvePoints": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "day": {
                        "type": "integer"
                    },
                    "psi": {
                        "type": "integer"
                    }
                },
                "required": ["day", "psi"]
            }
        },
        "unadjustedCurvePoints": {
            "type": "array",
            "default": [],
            "description": "Present when adjustmentStage is not None",
            "items": {
                "type": "object",
                "properties": {
                    "day": {
                        "type": "integer"
                    },
                    "psi": {
                        "type": "integer"
                    }
                },
                "required": ["day", "psi"]
            }
        },
        "estimates": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "horizonDays": {
                        "type": "integer",
                        "enum": [3, 7, 28]
                    },
                    "estimatedPsi": {
                        "type": ["number", "null"],
                        "minimum": 0
                    },
                    "equivalentAgeDays": {
                        "type": "number",
                        "minimum": 0
                    },
                    "adjustmentStage": {
                        "enum": [
                            "None",
                            "ThreeDay",
                            "ThreeAndSevenDay"
                        ]
                    },
                    "unadjustedPsi": {
                        "type": "integer"
                    },
                    "basis": {
                        "type": "string"
                    },
                    "calibratedVersion": {
                        "type": "string"
                    },
                    "isSynthetic": {
                        "type": "boolean"
                    },
                    "createdAtUtc": {
                        "type": "string",
                        "format": "date-time"
                    },
                    "createdBy": {
                        "type": "string"
                    },
                    "modifiedAtUtc": {
                        "type": "string",
                        "format": "date-time"
                    },
                    "modifiedBy": {
                        "type": "string"
                    },
                },
                "required": [
                    "horizonDays",
                    "estimatedPsi",
                    "equivalentAgeDays",
                    "adjustmentStage",
                    "unadjustedPsi",
                    "basis",
                    "calibratedVersion",
                    "isSynthetic",
                    "createdAtUtc",
                    "createdBy",
                ]
            },
        },
        "measuredBreaks": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "specimenId": {
                        "type": "integer"
                    },
                    "specimenLabel": {
                        "type": "string"
                    },
                    "ageDays": {
                        "type": "number"
                    },
                    "psi": {
                        "type": "number"
                    },
                    "isSynthetic": {
                        "type": "boolean"
                    }
                },
                "required": [
                    "specimenId",
                    "specimenLabel",
                    "ageDays",
                    "psi",
                    "isSynthetic"
                ]
            }
        },
        "earlyWarnings": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "warning": {
                        "type": "string",
                        "description": "Warning message"
                    },
                    "field": {
                        "type": "string",
                        "description": "What field the warning refers to"
                    },
                    "value": {
                        "type": "number",
                        "description": "The value that triggered the warning"
                    },
                    "limitKey": {
                        "type": "string",
                    },
                    "limit": {
                        "type": "number"
                    }
                },
                "required": [
                    "warning",
                    "field",
                    "value",
                    "limitKey",
                    "limit"
                ]
            }
        },
        "curingPoints": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "specimenId": {
                        "type": "integer"
                    },
                    "specimenLabel": {
                        "type": "string"
                    },
                    "time": {
                        "type": "string",
                        "format": "date-time"
                    },
                    "internalTempF": {
                        "type": "number"
                    },
                    "ambientTempF": {
                        "type": "number"
                    },
                    "maturityFHours": {
                        "type": "number"
                    },
                    "moisturePct": {
                        "type": "number"
                    },
                    "relativeHumidityPct": {
                        "type": "number"
                    },
                    "resistivityOhmPerFt": {
                        "type": "number"
                    },
                    "co2Ppm": {
                        "type": "number"
                    },
                },
                "required": [
                    "specimenId",
                    "specimenLabel",
                    "time",
                    "internalTempF",
                    "ambientTempF",
                    "maturityFHours",
                    "moisturePct",
                    "relativeHumidityPct",
                    "resistivityOhmPerFt",
                    "co2Ppm"
                ]
            }
        }
    },
    "required": [
        "id",
        "batchNumber",
        "isSynthetic",
        "cementType",
        "status",
        "atRisk",
        "expectedFinalBreakPsi",
        "curvePoints",
        "measuredBreaks",
        "estimates",
        "earlyWarnings",
        "curingPoints"
    ]
}
```

**Example Response:***

Status code: `200`

```json
{
    "id": 1,
    "batchNumber": "VM-SYN-001",
    "isSynthetic": true,
    "expectedFinalBreakPsi": 6200,
    "atRisk": false,
    "projected28DayPsi": 6390,
    "cementType": "TypeI",
    "status": "Estimated",
    "curvePoints":
    [
        {
            "day": 1,
            "psi": 1800
        },
        {
            "day": 2,
            "psi": 2600
        },
        {
            "day": 3,
            "psi": 3360
        },
        {
            "day": 4,
            "psi": 3900
        },
        {
            "day": 5,
            "psi": 4300
        },
        {
            "day": 6,
            "psi": 4600
        },
        {
            "day": 7,
            "psi": 4827
        },
        {
            "day": 8,
            "psi": 5010
        },
        {
            "day": 9,
            "psi": 5170
        },
        {
            "day": 10,
            "psi": 5310
        },
        {
            "day": 11,
            "psi": 5430
        },
        {
            "day": 12,
            "psi": 5540
        },
        {
            "day": 13,
            "psi": 5640
        },
        {
            "day": 14,
            "psi": 5730
        },
        {
            "day": 15,
            "psi": 5810
        },
        {
            "day": 16,
            "psi": 5880
        },
        {
            "day": 17,
            "psi": 5950
        },
        {
            "day": 18,
            "psi": 6010
        },
        {
            "day": 19,
            "psi": 6070
        },
        {
            "day": 20,
            "psi": 6120
        },
        {
            "day": 21,
            "psi": 6170
        },
        {
            "day": 22,
            "psi": 6220
        },
        {
            "day": 23,
            "psi": 6260
        },
        {
            "day": 24,
            "psi": 6300
        },
        {
            "day": 25,
            "psi": 6340
        },
        {
            "day": 26,
            "psi": 6360
        },
        {
            "day": 27,
            "psi": 6375
        },
        {
            "day": 28,
            "psi": 6390
        }
    ],
    "estimates":
    [
        {
            "horizonDays": 3,
            "estimatedPsi": 3350.0,
            "equivalentAgeDays": 3.0,
            "adjustmentStage": "None",
            "unadjustedPsi": 3350,
            "basis": "Standard maturity model",
            "calibratedVersion": "v1",
            "isSynthetic": true,
            "createdAtUtc": "2026-06-01T12:00:00Z",
            "createdBy": "system"
        },
        {
            "horizonDays": 7,
            "estimatedPsi": 4794.0,
            "equivalentAgeDays": 7.0,
            "adjustmentStage": "ThreeDay",
            "unadjustedPsi": 4794,
            "basis": "Standard maturity model",
            "calibratedVersion": "v1",
            "isSynthetic": true,
            "createdAtUtc": "2026-06-01T12:00:00Z",
            "createdBy": "system"
        },
        {
            "horizonDays": 28,
            "estimatedPsi": 6690.0,
            "equivalentAgeDays": 28.0,
            "adjustmentStage": "ThreeAndSevenDay",
            "unadjustedPsi": 6690,
            "basis": "Standard maturity model",
            "calibratedVersion": "v1",
            "isSynthetic": true,
            "createdAtUtc": "2026-06-01T12:00:00Z",
            "createdBy": "system"
        }
    ],
    "measuredBreaks":
    [
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "ageDays": 3.0,
            "psi": 3350,
            "isSynthetic": true
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "ageDays": 3.0,
            "psi": 3359,
            "isSynthetic": true
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "ageDays": 3.0,
            "psi": 3371,
            "isSynthetic": true
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "ageDays": 7.0,
            "psi": 4794,
            "isSynthetic": true
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "ageDays": 7.0,
            "psi": 4825,
            "isSynthetic": true
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "ageDays": 7.0,
            "psi": 4861,
            "isSynthetic": true
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "ageDays": 28.0,
            "psi": 6690,
            "isSynthetic": true
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "ageDays": 28.0,
            "psi": 6614,
            "isSynthetic": true
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "ageDays": 28.0,
            "psi": 6740,
            "isSynthetic": true
        }
    ],
    "earlyWarnings": [],
    "curingPoints":
    [
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 79.8,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 91.8,
            "relativeHumidityPct": 96.1,
            "resistivityOhmPerFt": 14.88,
            "co2Ppm": 418.5
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 80.5,
            "ambientTempF": 69.9,
            "maturityFHours": 961.8,
            "moisturePct": 93.4,
            "relativeHumidityPct": 96.2,
            "resistivityOhmPerFt": 15.88,
            "co2Ppm": 430.5
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 82.7,
            "ambientTempF": 71.3,
            "maturityFHours": 1941.0,
            "moisturePct": 93.3,
            "relativeHumidityPct": 96.2,
            "resistivityOhmPerFt": 18.25,
            "co2Ppm": 427.1
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 81.6,
            "ambientTempF": 70.3,
            "maturityFHours": 2920.8,
            "moisturePct": 93.1,
            "relativeHumidityPct": 96.3,
            "resistivityOhmPerFt": 19.07,
            "co2Ppm": 437.2
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 79.8,
            "ambientTempF": 69.0,
            "maturityFHours": 3879.6,
            "moisturePct": 92.2,
            "relativeHumidityPct": 93.7,
            "resistivityOhmPerFt": 20.12,
            "co2Ppm": 426.1
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 78.2,
            "ambientTempF": 67.7,
            "maturityFHours": 4818.0,
            "moisturePct": 93.3,
            "relativeHumidityPct": 96.6,
            "resistivityOhmPerFt": 22.76,
            "co2Ppm": 420.8
        },
        {
            "specimenId": 1,
            "specimenLabel": "D3-1",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 76.8,
            "ambientTempF": 66.5,
            "maturityFHours": 5737.2,
            "moisturePct": 91.7,
            "relativeHumidityPct": 95.1,
            "resistivityOhmPerFt": 22.78,
            "co2Ppm": 421.5
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 79.4,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 93.1,
            "relativeHumidityPct": 95.4,
            "resistivityOhmPerFt": 15.13,
            "co2Ppm": 433.9
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.7,
            "ambientTempF": 69.9,
            "maturityFHours": 966.6,
            "moisturePct": 93.6,
            "relativeHumidityPct": 96.3,
            "resistivityOhmPerFt": 17.34,
            "co2Ppm": 426.0
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 82.8,
            "ambientTempF": 71.3,
            "maturityFHours": 1952.2,
            "moisturePct": 92.5,
            "relativeHumidityPct": 93.7,
            "resistivityOhmPerFt": 18.34,
            "co2Ppm": 427.5
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 82.2,
            "ambientTempF": 70.3,
            "maturityFHours": 2938.2,
            "moisturePct": 92.3,
            "relativeHumidityPct": 95.2,
            "resistivityOhmPerFt": 19.52,
            "co2Ppm": 416.7
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 78.0,
            "ambientTempF": 69.0,
            "maturityFHours": 3898.4,
            "moisturePct": 93.6,
            "relativeHumidityPct": 93.6,
            "resistivityOhmPerFt": 21.59,
            "co2Ppm": 423.5
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 76.1,
            "ambientTempF": 67.7,
            "maturityFHours": 4822.8,
            "moisturePct": 91.2,
            "relativeHumidityPct": 94.5,
            "resistivityOhmPerFt": 21.41,
            "co2Ppm": 432.0
        },
        {
            "specimenId": 2,
            "specimenLabel": "D3-2",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 76.1,
            "ambientTempF": 66.5,
            "maturityFHours": 5736.0,
            "moisturePct": 92.3,
            "relativeHumidityPct": 94.7,
            "resistivityOhmPerFt": 24.61,
            "co2Ppm": 409.5
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 78.8,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 93.9,
            "relativeHumidityPct": 93.2,
            "resistivityOhmPerFt": 14.24,
            "co2Ppm": 423.6
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.5,
            "ambientTempF": 69.9,
            "maturityFHours": 961.2,
            "moisturePct": 93.2,
            "relativeHumidityPct": 94.8,
            "resistivityOhmPerFt": 16.07,
            "co2Ppm": 418.6
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 82.5,
            "ambientTempF": 71.3,
            "maturityFHours": 1945.2,
            "moisturePct": 91.7,
            "relativeHumidityPct": 96.8,
            "resistivityOhmPerFt": 18.14,
            "co2Ppm": 423.6
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 80.3,
            "ambientTempF": 70.3,
            "maturityFHours": 2928.6,
            "moisturePct": 92.8,
            "relativeHumidityPct": 95.6,
            "resistivityOhmPerFt": 19.11,
            "co2Ppm": 426.9
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 80.4,
            "ambientTempF": 69.0,
            "maturityFHours": 3893.0,
            "moisturePct": 91.7,
            "relativeHumidityPct": 94.4,
            "resistivityOhmPerFt": 20.41,
            "co2Ppm": 422.2
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 76.4,
            "ambientTempF": 67.7,
            "maturityFHours": 4814.2,
            "moisturePct": 90.3,
            "relativeHumidityPct": 96.5,
            "resistivityOhmPerFt": 21.45,
            "co2Ppm": 422.2
        },
        {
            "specimenId": 3,
            "specimenLabel": "D3-3",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 74.9,
            "ambientTempF": 66.5,
            "maturityFHours": 5721.7,
            "moisturePct": 93.5,
            "relativeHumidityPct": 93.1,
            "resistivityOhmPerFt": 24.21,
            "co2Ppm": 420.2
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 79.4,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 90.5,
            "relativeHumidityPct": 94.3,
            "resistivityOhmPerFt": 14.63,
            "co2Ppm": 425.4
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.9,
            "ambientTempF": 69.9,
            "maturityFHours": 967.8,
            "moisturePct": 91.4,
            "relativeHumidityPct": 94.3,
            "resistivityOhmPerFt": 16.82,
            "co2Ppm": 403.3
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 84.0,
            "ambientTempF": 71.3,
            "maturityFHours": 1957.2,
            "moisturePct": 90.8,
            "relativeHumidityPct": 96.7,
            "resistivityOhmPerFt": 17.07,
            "co2Ppm": 407.8
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 80.9,
            "ambientTempF": 70.3,
            "maturityFHours": 2932.6,
            "moisturePct": 90.1,
            "relativeHumidityPct": 96.7,
            "resistivityOhmPerFt": 18.75,
            "co2Ppm": 415.3
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 79.4,
            "ambientTempF": 69.0,
            "maturityFHours": 3891.4,
            "moisturePct": 91.5,
            "relativeHumidityPct": 96.9,
            "resistivityOhmPerFt": 20.78,
            "co2Ppm": 417.2
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 76.4,
            "ambientTempF": 67.7,
            "maturityFHours": 4813.2,
            "moisturePct": 92.1,
            "relativeHumidityPct": 95.9,
            "resistivityOhmPerFt": 22.34,
            "co2Ppm": 411.9
        },
        {
            "specimenId": 4,
            "specimenLabel": "D7-1",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 74.7,
            "ambientTempF": 66.5,
            "maturityFHours": 5719.7,
            "moisturePct": 90.9,
            "relativeHumidityPct": 96.9,
            "resistivityOhmPerFt": 24.22,
            "co2Ppm": 421.0
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 80.0,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 91.8,
            "relativeHumidityPct": 96.6,
            "resistivityOhmPerFt": 14.64,
            "co2Ppm": 419.5
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 82.1,
            "ambientTempF": 69.9,
            "maturityFHours": 972.6,
            "moisturePct": 90.2,
            "relativeHumidityPct": 95.4,
            "resistivityOhmPerFt": 17.23,
            "co2Ppm": 413.6
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 82.0,
            "ambientTempF": 71.3,
            "maturityFHours": 1946.0,
            "moisturePct": 93.3,
            "relativeHumidityPct": 93.9,
            "resistivityOhmPerFt": 18.43,
            "co2Ppm": 407.6
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 82.5,
            "ambientTempF": 70.3,
            "maturityFHours": 2938.6,
            "moisturePct": 90.5,
            "relativeHumidityPct": 96.3,
            "resistivityOhmPerFt": 19.11,
            "co2Ppm": 404.5
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 78.7,
            "ambientTempF": 69.0,
            "maturityFHours": 3892.4,
            "moisturePct": 93.4,
            "relativeHumidityPct": 94.3,
            "resistivityOhmPerFt": 19.83,
            "co2Ppm": 426.9
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 76.4,
            "ambientTempF": 67.7,
            "maturityFHours": 4815.1,
            "moisturePct": 92.1,
            "relativeHumidityPct": 96.6,
            "resistivityOhmPerFt": 21.35,
            "co2Ppm": 434.2
        },
        {
            "specimenId": 5,
            "specimenLabel": "D7-2",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 74.5,
            "ambientTempF": 66.5,
            "maturityFHours": 5718.3,
            "moisturePct": 91.4,
            "relativeHumidityPct": 95.2,
            "resistivityOhmPerFt": 23.12,
            "co2Ppm": 436.1
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 78.2,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 92.9,
            "relativeHumidityPct": 94.5,
            "resistivityOhmPerFt": 15.36,
            "co2Ppm": 409.3
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.2,
            "ambientTempF": 69.9,
            "maturityFHours": 956.4,
            "moisturePct": 92.3,
            "relativeHumidityPct": 94.1,
            "resistivityOhmPerFt": 17.16,
            "co2Ppm": 404.9
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 81.9,
            "ambientTempF": 71.3,
            "maturityFHours": 1939.8,
            "moisturePct": 91.0,
            "relativeHumidityPct": 96.7,
            "resistivityOhmPerFt": 17.24,
            "co2Ppm": 427.8
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 82.2,
            "ambientTempF": 70.3,
            "maturityFHours": 2933.4,
            "moisturePct": 93.5,
            "relativeHumidityPct": 94.8,
            "resistivityOhmPerFt": 20.21,
            "co2Ppm": 418.6
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 79.4,
            "ambientTempF": 69.0,
            "maturityFHours": 3892.4,
            "moisturePct": 92.7,
            "relativeHumidityPct": 95.2,
            "resistivityOhmPerFt": 19.98,
            "co2Ppm": 411.0
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 77.7,
            "ambientTempF": 67.7,
            "maturityFHours": 4823.0,
            "moisturePct": 92.9,
            "relativeHumidityPct": 93.2,
            "resistivityOhmPerFt": 21.29,
            "co2Ppm": 416.6
        },
        {
            "specimenId": 6,
            "specimenLabel": "D7-3",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 75.7,
            "ambientTempF": 66.5,
            "maturityFHours": 5733.4,
            "moisturePct": 93.6,
            "relativeHumidityPct": 94.2,
            "resistivityOhmPerFt": 23.66,
            "co2Ppm": 410.8
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 78.9,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 91.1,
            "relativeHumidityPct": 96.0,
            "resistivityOhmPerFt": 14.54,
            "co2Ppm": 412.7
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 80.3,
            "ambientTempF": 69.9,
            "maturityFHours": 959.4,
            "moisturePct": 91.5,
            "relativeHumidityPct": 96.5,
            "resistivityOhmPerFt": 16.49,
            "co2Ppm": 408.6
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 83.6,
            "ambientTempF": 71.3,
            "maturityFHours": 1953.0,
            "moisturePct": 91.8,
            "relativeHumidityPct": 96.8,
            "resistivityOhmPerFt": 18.22,
            "co2Ppm": 419.5
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 82.1,
            "ambientTempF": 70.3,
            "maturityFHours": 2938.2,
            "moisturePct": 93.7,
            "relativeHumidityPct": 93.8,
            "resistivityOhmPerFt": 20.2,
            "co2Ppm": 431.7
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 79.0,
            "ambientTempF": 69.0,
            "maturityFHours": 3892.8,
            "moisturePct": 92.1,
            "relativeHumidityPct": 93.4,
            "resistivityOhmPerFt": 20.7,
            "co2Ppm": 436.8
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 78.2,
            "ambientTempF": 67.7,
            "maturityFHours": 4831.2,
            "moisturePct": 90.6,
            "relativeHumidityPct": 96.4,
            "resistivityOhmPerFt": 21.69,
            "co2Ppm": 435.7
        },
        {
            "specimenId": 7,
            "specimenLabel": "D28-1",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 75.5,
            "ambientTempF": 66.5,
            "maturityFHours": 5741.4,
            "moisturePct": 92.2,
            "relativeHumidityPct": 93.2,
            "resistivityOhmPerFt": 24.34,
            "co2Ppm": 425.3
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 79.3,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 93.4,
            "relativeHumidityPct": 95.9,
            "resistivityOhmPerFt": 14.22,
            "co2Ppm": 425.6
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.4,
            "ambientTempF": 69.9,
            "maturityFHours": 964.2,
            "moisturePct": 90.8,
            "relativeHumidityPct": 94.8,
            "resistivityOhmPerFt": 16.69,
            "co2Ppm": 423.5
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 82.2,
            "ambientTempF": 71.3,
            "maturityFHours": 1947.6,
            "moisturePct": 90.3,
            "relativeHumidityPct": 95.2,
            "resistivityOhmPerFt": 18.38,
            "co2Ppm": 416.1
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 80.8,
            "ambientTempF": 70.3,
            "maturityFHours": 2930.4,
            "moisturePct": 91.1,
            "relativeHumidityPct": 95.9,
            "resistivityOhmPerFt": 19.25,
            "co2Ppm": 434.7
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 79.6,
            "ambientTempF": 69.0,
            "maturityFHours": 3894.8,
            "moisturePct": 92.9,
            "relativeHumidityPct": 94.9,
            "resistivityOhmPerFt": 21.35,
            "co2Ppm": 403.5
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 76.3,
            "ambientTempF": 67.7,
            "maturityFHours": 4816.6,
            "moisturePct": 91.8,
            "relativeHumidityPct": 93.7,
            "resistivityOhmPerFt": 22.01,
            "co2Ppm": 420.7
        },
        {
            "specimenId": 8,
            "specimenLabel": "D28-2",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 75.1,
            "ambientTempF": 66.5,
            "maturityFHours": 5724.0,
            "moisturePct": 92.6,
            "relativeHumidityPct": 96.5,
            "resistivityOhmPerFt": 24.48,
            "co2Ppm": 422.4
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-01T12:00:00Z",
            "internalTempF": 80.6,
            "ambientTempF": 68.9,
            "maturityFHours": 0.0,
            "moisturePct": 90.2,
            "relativeHumidityPct": 93.2,
            "resistivityOhmPerFt": 14.55,
            "co2Ppm": 406.4
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-02T00:00:00Z",
            "internalTempF": 81.4,
            "ambientTempF": 69.9,
            "maturityFHours": 974.4,
            "moisturePct": 90.6,
            "relativeHumidityPct": 93.3,
            "resistivityOhmPerFt": 15.84,
            "co2Ppm": 426.6
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-02T12:00:00Z",
            "internalTempF": 81.6,
            "ambientTempF": 71.3,
            "maturityFHours": 1950.4,
            "moisturePct": 91.1,
            "relativeHumidityPct": 93.3,
            "resistivityOhmPerFt": 17.51,
            "co2Ppm": 428.3
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-03T00:00:00Z",
            "internalTempF": 80.9,
            "ambientTempF": 70.3,
            "maturityFHours": 2933.0,
            "moisturePct": 90.6,
            "relativeHumidityPct": 96.8,
            "resistivityOhmPerFt": 18.79,
            "co2Ppm": 403.9
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-03T12:00:00Z",
            "internalTempF": 78.7,
            "ambientTempF": 69.0,
            "maturityFHours": 3886.6,
            "moisturePct": 91.0,
            "relativeHumidityPct": 93.4,
            "resistivityOhmPerFt": 21.18,
            "co2Ppm": 419.1
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-04T00:00:00Z",
            "internalTempF": 78.3,
            "ambientTempF": 67.7,
            "maturityFHours": 4825.2,
            "moisturePct": 90.2,
            "relativeHumidityPct": 93.3,
            "resistivityOhmPerFt": 21.9,
            "co2Ppm": 429.2
        },
        {
            "specimenId": 9,
            "specimenLabel": "D28-3",
            "time": "2026-06-04T12:00:00Z",
            "internalTempF": 75.2,
            "ambientTempF": 66.5,
            "maturityFHours": 5732.8,
            "moisturePct": 93.6,
            "relativeHumidityPct": 95.2,
            "resistivityOhmPerFt": 23.95,
            "co2Ppm": 413.3
        }
    ]
}
```

### Get At-Risk Batches

Use to get the batches that are predicted to be at-risk.

**Endpoint:** `GET /api/warnings/early`

**Request Parameters:**

* `includeSynthetic`: boolean from query. Default is true. Set to false to have the server omit synthetic data in the response.

**Response Status Codes:**

- `400`: if the request was malformed
- `200`: if the server was able to return a list of at-risk batches

**Response Schema:**

```json
{
    "title": "GetBatchStrengthPredictionWarningsResponse",
    "type": "array",
    "items": {
        "type": "object",
        "properties": {
            "id": {
                "type": "integer"
            },
            "batchNumber": {
                "type": "string"
            },
            "isSynthetic": {
                "type": "boolean"
            },
            "atRisk": {
                "type": "boolean"
            },
            "projected28DayPsi": {
                "type": ["integer", "null"]
            },
            "shortfallPsi": {
                "type": ["integer", "null"]
            },
            "shortfallPct": {
                "type": ["number", "null"]
            },
            "earlyWarnings": {
                "type": "array",
                "default": [],
                "items": {
                    "type": "object",
                    "properties": {
                        "warning": {
                            "type": "string",
                            "description": "Warning message"
                        },
                        "field": {
                            "type": "string",
                            "description": "What field the warning refers to"
                        },
                        "value": {
                            "type": "number",
                            "description": "The value that triggered the warning"
                        },
                        "limitKey": {
                            "type": "string",
                        },
                        "limit": {
                            "type": "number"
                        }
                    },
                    "required": [
                        "warning",
                        "field",
                        "value",
                        "limitKey",
                        "limit"
                    ]
                }
            },
        },
        "required": [
            "id",
            "batchNumber",
            "atRisk",
            "isSynthetic",
            "earlyWarnings"
        ]
    }
}
```

**Example Response:**

Status Code: `200`

```json
[
    {
        "id": 1,
        "batchNumber": "VM-SYN-001",
        "isSynthetic": true,
        "atRisk": true,
        "projected28DayPsi": 4450,
        "shortfallPsi": 1750,
        "shortfallPct": 28.23,
        "earlyWarnings": [
            {
                "warning": "Water-cement ratio of 0.603 is above the expected maximum of 0.60",
                "field": "waterCementRatio",
                "value": 0.603,
                "limitKey": "Composition.WaterCementRatio",
                "limit": 0.6
            }
        ]
    }
]
```
