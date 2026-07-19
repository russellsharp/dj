## Cert Management
dotnet dev-certs https --clean
dotnet dev-certs https --trust

## Store secret key for dev to use Auth
### Generate symmetric key
-join ((1..32) | ForEach-Object { [char](Get-Random -Min 65 -Max 91) }) > secret_key_file

dotnet user-secrets set "HostConfiguration:Jwt:Key" "PASTE_YOUR_GENERATED_KEY_HERE"

Store key in environment variable DJ_SECURITY_KEY