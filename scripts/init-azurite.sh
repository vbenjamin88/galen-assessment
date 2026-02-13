#!/bin/bash
# Initialize Azurite blob containers - run after Azurite is up
# Requires: az (Azure CLI) or curl

BLOB_ENDPOINT="http://127.0.0.1:10000/devstoreaccount1"

# Create inbound container via REST
curl -X PUT "${BLOB_ENDPOINT}/inbound?restype=container" \
  -H "x-ms-version: 2020-10-02" \
  -H "x-ms-blob-public-access: container" \
  -v 2>/dev/null || echo "Container may already exist or Azurite not running"
