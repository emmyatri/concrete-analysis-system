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
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["title", "status", "errors"]
}
```

## Batch

### Create Batch

Use to create a batch.

**Endpoint:** `POST /api/batches`

**Request Schema:**

```json
{
    "title": "CreateBatchRequest",
    "type": "object",
    "properties": {
        "batchNumber": {
            "type": "string",
            "pattern": "^[A-Za-z0-9-]+$"
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
            "type": "boolean",
            "default": false
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
            }
        },
        "oxides": {
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
            }
        }
    },
    "required": [
        "batchNumber",
        "castAt",
        "finalWeightLb",
        "cementType",
        "mixWaterPh"
    ]
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
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["id", "batchNumber"]
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
        "isSynthetic": {
            "type": "boolean",
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
            }
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
            }
        },
    }
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
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                }
            }
        }
    },
    "required": ["id", "batchNumber"]
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
        "batchNumber": {
            "type": "string",
            "pattern": "^[A-Za-z0-9-]+$" 
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
            "type": "boolean",
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
        "specimens": {
            "type": "array",
            "default": [],
            "items": {
                "type": "object",
                "properties": {
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
                            "brokenAt": {
                                "type": "string",
                                "format": "date-time"
                            },
                            "strengthPsi": {
                                "type": "integer"
                            }
                        },
                        "required": ["brokenAt", "strengthPsi"]
                    },
                    "curingReadings": {
                        "type": "array",
                        "items": {
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
                    }
                },
                "required": [
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
        "batchNumber",
        "castAt",
        "finalWeightLb",
        "cementType",
        "mixWaterPh"
    ]
}
```


### Get Batch List

Returns a paged list of batches.

**Endpoint:** `GET /api/batches`

**Request Parameters:**

* `q`: from query. The batch search query.
* `page`: from query. The page of data to fetch on. Starts from 1.

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
            "batchNumber": {
                "type": "string",
                "pattern": "^[A-Za-z0-9-]+$" 
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
                "type": "boolean",
            },
            "specimens": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
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
                                "brokenAt": {
                                    "type": "string",
                                    "format": "date-time"
                                },
                                "strengthPsi": {
                                    "type": "integer"
                                }
                            },
                            "required": ["brokenAt", "strengthPsi"]
                        },
                        "curingReadings": {
                            "type": "array",
                            "items": {
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
                        }
                    },
                    "required": [
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
            "batchNumber",
            "castAt",
            "finalWeightLb",
            "cementType",
            "mixWaterPh",
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
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["id", "label", "batchNumber"]
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
            "type": "number",
            "description": "Break result id"
        },
        "specimen_id": {
            "type": "number",
        },
        "warnings": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["id", "specimen_id"]
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
            "type": "number",
            "description": "Curing reading id"
        },
        "specimen_id": {
            "type": "number"
        },
        "warnings": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string"
                    },
                    "value": {
                        "type": "string"
                    }
                },
                "required": ["key", "value"]
            }
        }
    },
    "required": ["id", "specimen_id"]
}
```
