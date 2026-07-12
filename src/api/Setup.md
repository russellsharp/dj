## Cert Management
dotnet dev-certs https --clean
dotnet dev-certs https --trust

## Store secret key for dev to use Auth
dotnet user-secrets set "Jwt:Key" "PASTE_YOUR_GENERATED_KEY_HERE"