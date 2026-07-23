# Productization Checklist for DJ API

## Goals
- Hostable in AWS ECS
- No additional login integration beyond existing token flow
- Focus on `src/api`, `src/api.core`, and `src/shared`

---


## 1. Deployment & Hosting
- [x] Add a `Dockerfile` for the API service
  - Build from .NET 11 runtime
  - Publish `src/api`
  - Expose the HTTP port used by the app
- [ ] Add a task definition / ECS deployment guide
  - Define container port mapping
  - Configure CPU / memory sizing
  - Configure health check path: `/api/health`
- [ ] Use ECS-friendly environment variables
  - `DJ_SECURITY_KEY`
  - `HostConfiguration:Jwt:Issuer`
  - `HostConfiguration:Jwt:Audience`
  - `MediaCollectionConfiguration:BaseDirectory`
  - `DatabaseConfiguration:DataFile`
  - ~~`TMDB:ApiKey`~~
  - `TMDB:DatabasePath`
- [ ] Avoid hardcoded local Windows paths in production config
  - Current `appsettings.json` uses `//fatty/Existing/`
- [ ] Ensure app supports running behind a load balancer / ingress
  - Validate `ASPNETCORE_URLS` or Kestrel binding
  - Ensure `AllowedHosts: "*"` is acceptable

## 2. Secrets & Configuration

- [x] Remove or stop using local dev-only secret files like `super_secret_key.secret`
- [ ] Use ECS secrets manager / Parameter Store for:
  - `DJ_SECURITY_KEY`
  - `TMDB.ApiKey`
- [ ] Ensure JWT configuration is fully environment-driven
  - The app currently reads `HostConfiguration:Jwt` from config
  - `SecurityConfiguration.AddSecurityConfiguration()` requires `DJ_SECURITY_KEY` env var
- [ ] Confirm `HostConfiguration:AllowedHosts` is used correctly if desired

## 3. Security

- [ ] Verify authentication behavior for product use case
  - Token endpoint exists at `/api/token/scoped`
  - Anonymous token endpoint exists at `/api/token/anonymous`
  - API routes require `ReadScope` policy
- [ ] Confirm OpenIddict and JWT validation are production-suitable
  - Ensure signing key is injected from environment
  - Check if `options.AddEphemeralSigningKey()` is still used in production path
- [ ] Validate CORS policy
  - Currently hardcoded to `https://127.0.0.1`
  - Needs config for production origins or allowlist
- [ ] Ensure health endpoint is anonymous and unprotected
  - `/api/health` is `AllowAnonymous`

## 4. Storage & Persistence

- [ ] Validate SQLite persistence in ECS
  - `DatabaseConfiguration.DataFile` currently relative to process path
  - Use writable volume mount for `data/media.db`
- [ ] Validate TMDB cache DB path
  - `EndpointConfig.DatabasePath` currently `data/tmdb.db`
  - Also needs writable volume or persistent storage
- [ ] Ensure local media base directory path is injectable
  - `MediaCollectionConfiguration.BaseDirectory` must be configurable
  - ECS task should mount the media path or use S3/AWS FS if needed
- [ ] Avoid assuming local file shares / Windows network paths

## 5. Runtime Behavior

- [ ] Ensure startup initialization is safe in container
  - `media.Initialize(cts.Token)` is called before `app.Run()`
  - Check if media initialization can block startup too long
- [ ] Check if `IMediaCollection.UpdateRepos` is safe for concurrency
  - It uses static state and may behave oddly across requests
- [ ] Consider startup failure modes
  - If DB / media base directory is missing, app should fail clearly

## 6. Observability & Production Readiness

- [ ] Add structured logging for important startup / health events
- [ ] Ensure errors are not silently swallowed
- [ ] Add metrics / logging for:
  - request count
  - auth failures
  - rate limit hits
- [ ] Confirm `UseDeveloperExceptionPage()` is only enabled in development

## 7. API Surface & Docs

- [ ] Verify OpenAPI is available in production if desired
  - Currently only mapped in Development mode
- [ ] Document API routes and auth flow
  - `/api/health`
  - `/api/token/anonymous`
  - `/api/token/scoped`
  - `/api/media/*`
- [ ] Confirm token flow for product consumers
  - client credentials via `/api/token/scoped`
  - anonymous JWT via `/api/token/anonymous`

## 8. Tests & Validation

- [ ] Add integration tests for API startup and auth in production-like config
- [ ] Add ECS-compatible smoke test plan
  - health endpoint
  - token endpoint
  - one protected media call
- [ ] Validate DB file cleanup / initialization semantics in container

---

## Notes from current code

- `src/api/Program.cs`:
  - Uses `builder.AddConfiguration().AddServices().AddSecurity().AddRateLimiter()`
  - Uses `app.SetupSecurity()` and `app.MapControllers()`
- `src/shared/ApplicationExtensions.cs`:
  - Reads `DJ_SECURITY_KEY` from env var
  - Configures CORS only for `https://127.0.0.1`
  - Adds OpenIddict with in-memory OAuth DB
- `src/shared/http/security/OpenIddict.cs`:
  - In-memory DB for OAuth and user data
  - Seeds test clients and scopes
- `src/api/core` controllers:
  - `DjController` requires `[Authorize(Policy = "ReadScope")]`
  - `HealthController` is anonymous
  - `TokenController` supports scoped tokens and anonymous token generation
