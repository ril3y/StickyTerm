# StickyTerm API Documentation

StickyTerm provides a local HTTP API that allows AI coding assistants and other tools to query COM port information.

## Quick Start

1. Enable the API server in StickyTerm: **Settings > API Server > Enable**
2. Query your ports: `curl http://localhost:27182/api/ports`

## Base URL

```
http://localhost:27182
```

The default port is `27182`. You can change this in Settings.

## Endpoints

### GET /api/ports

Returns all connected COM ports with their aliases and notes. **This is the recommended endpoint for AI assistants.**

```bash
curl http://localhost:27182/api/ports
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "comPort": "COM3",
      "alias": "Meshtastic 1",
      "notes": "Primary Meshtastic device for mesh network testing",
      "friendlyName": "USB Serial Device (COM3)",
      "deviceType": "USB",
      "vid": "1A86",
      "pid": "7523",
      "serialNumber": "5&12345678&0&1",
      "isConnected": true,
      "stability": "Stable",
      "lastSeen": "2024-01-15T10:30:00Z"
    },
    {
      "comPort": "COM5",
      "alias": "Arduino Mega",
      "notes": "Main development board",
      "friendlyName": "Arduino Mega 2560 (COM5)",
      "deviceType": "USB",
      "vid": "2341",
      "pid": "0042",
      "serialNumber": "95530343834351E0B151",
      "isConnected": true,
      "stability": "Stable",
      "lastSeen": "2024-01-15T10:30:00Z"
    }
  ],
  "timestamp": "2024-01-15T10:30:05Z"
}
```

### GET /api/devices

Returns all known devices, including disconnected ones.

```bash
curl http://localhost:27182/api/devices
```

### GET /api/rules

Returns all port tracking rules configured by the user.

```bash
curl http://localhost:27182/api/rules
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "abc-123-def",
      "name": "Track Meshtastic",
      "notes": "Primary Meshtastic device",
      "enabled": true,
      "targetComPort": "COM3",
      "vid": "1A86",
      "pid": "7523",
      "serialNumber": "5&12345678&0&1",
      "matchType": "VidPidSerial"
    }
  ],
  "timestamp": "2024-01-15T10:30:05Z"
}
```

### GET /api/status

Returns server status and health information.

```bash
curl http://localhost:27182/api/status
```

**Response:**
```json
{
  "success": true,
  "data": {
    "appName": "StickyTerm",
    "version": "1.0.0",
    "deviceCount": 3,
    "ruleCount": 2,
    "trackedPortCount": 2,
    "isRunningAsAdmin": true,
    "watchModeEnabled": true,
    "uptime": "01:30:00"
  },
  "timestamp": "2024-01-15T10:30:05Z"
}
```

### GET /api or GET /

Returns API discovery information with available endpoints.

```bash
curl http://localhost:27182/api
```

## For AI Assistants

When you need to open a serial connection to a device, query `/api/ports` to find the correct COM port:

```python
import requests
import serial

# Get available ports from StickyTerm
response = requests.get("http://localhost:27182/api/ports")
ports = response.json()["data"]

# Find the Meshtastic device by alias
meshtastic = next((p for p in ports if p["alias"] == "Meshtastic 1"), None)
if meshtastic:
    ser = serial.Serial(meshtastic["comPort"], 115200)
```

## Error Responses

```json
{
  "success": false,
  "error": "Endpoint not found: /api/unknown",
  "timestamp": "2024-01-15T10:30:05Z"
}
```

## Security

- The API only listens on `127.0.0.1` (localhost) by default
- No authentication required (localhost only)
- Read-only access (no modification endpoints)
- CORS headers included for browser-based tools
