# OAuth2/OIDC Identity Provider with Firebase Integration

This solution demonstrates a complete OAuth2/OIDC authentication flow using:
- **IdentityServer**: A .NET 8 Identity Provider using Duende IdentityServer
- **MvcClient**: A .NET 8 MVC application that authenticates via OIDC and mints Firebase custom tokens
- **FirebaseApp**: A Firebase micro-frontend that uses custom tokens from the backend

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                       MVC Client (Port 5002)                        │
│                                                                     │
│  ┌──────────────────────┐    ┌──────────────────────────────────┐  │
│  │  FirebaseAuthController│    │    Firebase Micro-Frontend       │  │
│  │  - OAuth2 Auth Code   │◄───│    (Embedded via iframe)         │  │
│  │  - Token Exchange     │    │    - signInWithCustomToken       │  │
│  │  - Mint Custom Token  │────►    - Firestore Demo             │  │
│  └──────────────────────┘    └──────────────────────────────────┘  │
│            │                                   │                    │
└────────────│───────────────────────────────────│────────────────────┘
             │                                   │
             ▼                                   ▼
┌──────────────────────┐              ┌──────────────────────┐
│    IdentityServer    │              │   Firebase Emulators │
│    (Port 5001)       │              │   - Auth (9099)      │
│    recruiting.       │              │   - Firestore (8080) │
│    acme.com       │              │   - UI (4000)        │
└──────────────────────┘              └──────────────────────┘
```

## Authentication Flow

```
┌──────────┐      ┌─────────────┐      ┌──────────────┐      ┌─────────────┐
│  Browser │      │  MVC Client │      │IdentityServer│      │Firebase Auth│
└────┬─────┘      └──────┬──────┘      └──────┬───────┘      └──────┬──────┘
     │                   │                    │                     │
     │ 1. Click Login    │                    │                     │
     │──────────────────►│                    │                     │
     │                   │                    │                     │
     │ 2. Redirect with PKCE                  │                     │
     │◄──────────────────│                    │                     │
     │                   │                    │                     │
     │ 3. Login Page     │                    │                     │
     │───────────────────────────────────────►│                     │
     │                   │                    │                     │
     │ 4. Submit Credentials                  │                     │
     │───────────────────────────────────────►│                     │
     │                   │                    │                     │
     │ 5. Auth Code Redirect                  │                     │
     │◄──────────────────────────────────────│                     │
     │                   │                    │                     │
     │ 6. Auth Code      │                    │                     │
     │──────────────────►│                    │                     │
     │                   │                    │                     │
     │                   │ 7. Exchange Code   │                     │
     │                   │───────────────────►│                     │
     │                   │                    │                     │
     │                   │ 8. Access Token    │                     │
     │                   │◄───────────────────│                     │
     │                   │                    │                     │
     │                   │ 9. Create Custom Token                   │
     │                   │─────────────────────────────────────────►│
     │                   │                    │                     │
     │                   │ 10. Custom Token   │                     │
     │                   │◄────────────────────────────────────────│
     │                   │                    │                     │
     │ 11. Return Token  │                    │                     │
     │◄──────────────────│                    │                     │
     │                   │                    │                     │
     │ 12. signInWithCustomToken              │                     │
     │─────────────────────────────────────────────────────────────►│
     │                   │                    │                     │
     │ 13. Authenticated │                    │                     │
     │◄────────────────────────────────────────────────────────────│
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Firebase CLI](https://firebase.google.com/docs/cli) (for emulators)
- [Node.js](https://nodejs.org/) (required for Firebase CLI)

## Quick Start with Firebase Emulators

### Step 1: Configure Hosts File

Add the following entry to your hosts file:

**Windows**: `C:\Windows\System32\drivers\etc\hosts`
**macOS/Linux**: `/etc/hosts`

```
127.0.0.1 myidprovider.acme.com
```

### Step 2: Trust Development Certificates

```bash
dotnet dev-certs https --trust
```

### Step 3: Install Firebase CLI

```bash
npm install -g firebase-tools
```

### Step 4: Start Firebase Emulators

```bash
cd src/FirebaseApp
firebase emulators:start --project demo-oauth-firebase
```

This will start:
- **Auth Emulator**: http://localhost:9099
- **Firestore Emulator**: http://localhost:8080
- **Emulator UI**: http://localhost:4000

### Step 5: Start the .NET Applications

**Terminal 1 - Identity Server:**
```bash
cd src/IdentityServer
dotnet restore
dotnet run --launch-profile https
```

**Terminal 2 - MVC Client:**
```bash
cd src/MvcClient
dotnet restore
dotnet run --launch-profile https
```

### Step 6: Access the Applications

| Application | URL |
|-------------|-----|
| MVC Client | https://localhost:5002 |
| Firebase App (standalone) | https://localhost:5002/firebase/index.html |
| Firebase App (embedded) | https://localhost:5002/Home/Firebase |
| Firebase Emulator UI | http://localhost:4000 |
| Identity Server | https://myidprovider.acme.com:5001 |

## Test Accounts

| Username | Password | Email |
|----------|----------|-------|
| alice | Pass123! | alice@example.com |
| bob | Pass123! | bob@example.com |

## Project Structure

```
OAuthFireBase/
├── OAuthFireBase.sln
├── README.md
├── start.sh / start.bat
└── src/
    ├── IdentityServer/              # Duende IdentityServer (.NET 8)
    │   ├── Config.cs                # OIDC clients and resources
    │   ├── Program.cs               # Application entry point
    │   ├── Data/                    # EF Core DbContext & seed data
    │   ├── Models/                  # ApplicationUser model
    │   └── Pages/                   # Login/Logout Razor Pages
    │
    ├── MvcClient/                   # MVC Client Application (.NET 8)
    │   ├── Program.cs               # OIDC + Firebase setup
    │   ├── Controllers/
    │   │   ├── HomeController.cs    # MVC pages
    │   │   └── FirebaseAuthController.cs  # OAuth2 token exchange
    │   └── Views/                   # Razor Views
    │
    └── FirebaseApp/                 # Firebase Micro-Frontend
        ├── index.html               # Main app with Firebase SDK
        ├── firebase.json            # Emulator configuration
        └── firestore.rules          # Security rules
```

## Key Components

### FirebaseAuthController

The controller handles the OAuth2 Authorization Code flow with PKCE:

- `GET /api/firebase-auth/login` - Initiates the OAuth2 flow
- `GET /api/firebase-auth/callback` - Handles the callback, exchanges code for tokens, mints Firebase custom token
- `POST /api/firebase-auth/token` - API endpoint for SPA token exchange

### Custom Token Minting

The backend uses Firebase Admin SDK to mint custom tokens that include:
- `email` - User's email from IdentityServer
- `name` - User's display name
- `identity_provider` - Set to "acme"
- `idp_sub` - Original subject ID from IdentityServer

### Firebase App

The frontend uses `signInWithCustomToken()` to authenticate with Firebase using the custom token from the backend. This allows:
- Full Firebase Auth integration
- Access to Firestore with user context
- Custom claims from the identity provider

## OAuth2/OIDC Configuration

### Identity Server Endpoints

| Endpoint | URL |
|----------|-----|
| Discovery | https://myidprovider.acme.com:5001/.well-known/openid-configuration |
| Authorize | https://myidprovider.acme.com:5001/connect/authorize |
| Token | https://myidprovider.acme.com:5001/connect/token |
| UserInfo | https://myidprovider.acme.com:5001/connect/userinfo |

### Configured Clients

| Client ID | Type | Redirect URI |
|-----------|------|--------------|
| mvc-client | Confidential | https://localhost:5002/signin-oidc |
| firebase-client | Public (PKCE) | https://localhost:5002/api/firebase-auth/callback |

## Switching to Production Firebase

To use a real Firebase project instead of emulators:

1. **Update FirebaseApp/index.html**:
   ```javascript
   const USE_EMULATORS = false;

   const firebaseConfig = {
       apiKey: "your-actual-api-key",
       authDomain: "your-project.firebaseapp.com",
       projectId: "your-project-id",
       // ... rest of config
   };
   ```

2. **Update MvcClient/appsettings.json**:
   ```json
   {
     "Firebase": {
       "UseEmulator": false,
       "ServiceAccountPath": "path/to/serviceAccountKey.json"
     }
   }
   ```

3. Download your Firebase service account key from Firebase Console > Project Settings > Service Accounts

## Troubleshooting

### SSL Certificate Errors

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### Firebase Emulator Not Starting

Ensure you have Java installed (required for Firestore emulator):
```bash
java -version
```

### Custom Token Errors

If you see "The custom token corresponds to a different Firebase project", ensure:
- The `projectId` in `firebase.json` matches the one in `index.html`
- When using emulators, use `demo-` prefixed project IDs

### CORS Errors

The Identity Server allows CORS from:
- http://localhost:5002
- https://localhost:5002
- http://localhost:3000

## License

This project uses Duende IdentityServer which has specific licensing requirements. For production use, please review the [Duende licensing](https://duendesoftware.com/products/identityserver#pricing).
