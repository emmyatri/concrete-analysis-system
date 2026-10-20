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
        "expectedFinalBreakPsi": {
            "type": "integer",
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
        "estimates": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
                    "specimenId": {
                        "type": "integer"
                    },
                    "horizonDays": {
                        "enum": ["3", "7", "28"]
                    },
                    "estimatedPsi": {
                        "type": "integer"
                    },
                    "equivalentAgeDays": {
                        "enum": ["Day3", "Day7", "Day28"]
                    },
                    "adjustmentStage": {
                        "enum": ["none", "3day", "3and7day"]
                    },
                    "unadjustedPsi": {
                        "type": "integer"
                    },
                    "basis": {
                        "type": "string"
                    },
                    "calibratedVersionId": {
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
                        "type": "string",
                    },
                },
                "required": [
                    "horizonDays",
                    "estimatedPsi",
                    "equivalentAgeDays",
                    "adjustmentStage",
                    "unadjustedPsi",
                    "basis",
                    "calibratedVersionId",
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
                        "type": "integer",
                    },
                    "ageDays": {
                        "type": "number"
                    },
                    "psi": {
                        "type": "number"
                    },
                    "calibratedVersionId": {
                        "type": "string"
                    }
                },
                "required": [
                    "specimenId",
                    "ageDays",
                    "calibratedVersionId"
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
                    }
                },
                "required": ["warning", "field", "value"]
            }
        },
        "curingPoints": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
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
                    "maturity": {
                        "type": "number"
                    }
                },
                "required": [
                    "time",
                    "internalTempF",
                    "ambientTempF",
                    "maturity"
                ]
            }
        }
    },
    "required": [
        "id",
        "batchNumber",
        "isSynthetic",
        "createdAtUtc",
        "createdBy",
        "expectedFinalBreakPsi",
        "measuredBreaks",
        "estimates",
        "earlyWarnings",
        "curingPoints"
    ]
}
```

### Get At-Risk Batches

Use to get the batches that are predicted to be at-risk.

**Endpoint:** `GET /api/warnings/early`

**Request Parameters:**

* `includeSynthetic`: boolean from query. Default is false. Set to true to have the server include synthetic data in the response.

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
                "type": "integer",
            },
            "batchNumber": {
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
                "type": "string",
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
                        }
                    },
                    "required": ["warning", "field", "value"]
                }
            },
        },
        "required": [
            "id",
            "batchNumber",
            "isSynthetic",
            "createdAt",
            "createdBy",
            "earlyWarnings"
        ]
    }
}
```
