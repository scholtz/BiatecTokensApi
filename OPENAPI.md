# OpenAPI Contract Documentation

## Overview

The Biatec Tokens API provides a comprehensive OpenAPI (Swagger) specification that documents all available endpoints, request/response schemas, and authentication requirements.

## Accessing the OpenAPI Specification

### During Development (Local)

When running the API locally, the OpenAPI specification is available at:

- **Interactive Swagger UI**: https://localhost:7000/swagger
- **OpenAPI JSON**: https://localhost:7000/swagger/v1/swagger.json

### From CI/CD Pipeline

The OpenAPI specification is automatically generated and published as a GitHub Actions artifact on every pull request and push to main/master branches.

#### Downloading from GitHub Actions:

1. Navigate to the repository's Actions tab
2. Click on the latest successful workflow run
3. Download the `openapi-specification` artifact
4. Extract the `openapi.json` file

### Using the OpenAPI Specification

#### For Frontend Development

1. **Mock API Server**: Use tools like [Prism](https://github.com/stoplightio/prism) to create a mock API server:
   ```bash
   npm install -g @stoplight/prism-cli
   prism mock openapi.json
   ```

2. **TypeScript/JavaScript Client Generation**: Generate type-safe API clients:
   ```bash
   npm install -g @openapitools/openapi-generator-cli
   openapi-generator-cli generate -i openapi.json -g typescript-fetch -o ./src/api-client
   ```

3. **Validation**: Validate requests and responses against the schema:
   ```bash
   npm install ajv
   # Use the schemas from openapi.json to validate your data
   ```

#### For Testing

1. **Postman**: Import the OpenAPI specification directly into Postman
   - Open Postman
   - Click Import
   - Select the openapi.json file
   - All endpoints will be available as a collection

2. **Insomnia**: Import the specification into Insomnia
   - Open Insomnia
   - Click Import/Export
   - Select "From File"
   - Choose the openapi.json file

## API Endpoints Overview

### ERC20 Tokens (Base Blockchain)

- **POST** `/api/v1/token/erc20-mintable/create` - Deploy mintable ERC20 token
- **POST** `/api/v1/token/erc20-preminted/create` - Deploy preminted ERC20 token

### Algorand Standard Assets (ASA)

- **POST** `/api/v1/token/asa-ft/create` - Create fungible token
- **POST** `/api/v1/token/asa-nft/create` - Create NFT
- **POST** `/api/v1/token/asa-fnft/create` - Create fractional NFT

### ARC3 Tokens (Algorand with Metadata)

- **POST** `/api/v1/token/arc3-ft/create` - Create ARC3 fungible token
- **POST** `/api/v1/token/arc3-nft/create` - Create ARC3 NFT
- **POST** `/api/v1/token/arc3-fnft/create` - Create ARC3 fractional NFT

### ARC200 Tokens (Algorand Smart Contracts)

- **POST** `/api/v1/token/arc200-mintable/create` - Create mintable ARC200 token
- **POST** `/api/v1/token/arc200-preminted/create` - Create preminted ARC200 token

### ARC1400 Security Tokens (Algorand)

- **POST** `/api/v1/token/arc1400-mintable/create` - Create ARC1400 security token

## Authentication

All endpoints require **ARC-0014 Algorand Authentication**.

### Authentication Flow

1. Create an authentication transaction on Algorand
2. Sign the transaction with your private key
3. Include the signed transaction in the Authorization header:
   ```
   Authorization: SigTx <base64-encoded-signed-transaction>
   ```

### Example Authentication Header

```
Authorization: SigTx gqNzaWfEQE...
```

For detailed authentication implementation, refer to the [ARC-0014 specification](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md).

## Sample Request Data

Sample request payloads for all endpoints are available in `sample-seed-data.json`. These can be used as templates for creating tokens.

## Schema Definitions

The OpenAPI specification includes comprehensive schema definitions for:

- Request models (e.g., `ERC20MintableTokenDeploymentRequest`)
- Response models (e.g., `EVMTokenDeploymentResponse`)
- Metadata structures (e.g., `ARC3Metadata`)
- Configuration models

All schemas include:
- Property descriptions
- Data types
- Required fields
- Validation rules
- Example values

## Validation Rules

The API enforces validation rules documented in the OpenAPI specification:

### ERC20 Tokens
- Name: 1-50 characters
- Symbol: 1-20 characters
- Decimals: 0-18
- Initial supply and cap must be positive numbers

### Algorand Tokens
- Asset name: 1-32 characters
- Unit name: 1-8 characters
- Total supply based on token type
- Network must be valid (mainnet, testnet, betanet, etc.)

## Error Responses

All endpoints return standardized error responses:

```json
{
  "success": false,
  "errorMessage": "Detailed error description"
}
```

Status codes:
- `200 OK` - Successful operation
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Missing or invalid authentication
- `500 Internal Server Error` - Server error during processing

## Change Log

The OpenAPI specification version follows the API version. Breaking changes will be clearly documented in:
- Commit messages
- Pull request descriptions
- Release notes

## Support

For questions or issues with the OpenAPI specification:
1. Check the Swagger UI for interactive documentation
2. Review `sample-seed-data.json` for example requests
3. Open an issue in the GitHub repository
