New-Variable -Name "CONTAINER_NAME" -VALUE "dj-api-container" -Option Constant

if ($id = docker ps -aq --filter "name=^$CONTAINER_NAME") { 
    docker container stop $id
    docker container rm $id
}
 
# powershell command

docker run `
    -e "DJ_TMDB_API_KEY=$env:DJ_TMDB_API_KEY" `
    -e "DJ_SECURITY_KEY=$env:DJ_SECURITY_KEY" `
    -e "ASPNETCORE_ENVIRONMENT=Development" `
    -e "APSNETCORE_URLS=https://+:80" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Password=$env:ASPNETCORE_Kestrel__Certificates__Default__Password" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Path=/root/https/aspnetapp.pfx" `
    -e "DJ_HOST_ALLOWED_CORS_URL=127.0.0.1" `
    -e "DJ_MEDIA_DATABASE_PATH": "data/media.db", `
    -e "DJ_OPENIDDICT_DATABASE_PATH": "data/openiddict.db", `
    -e "DJ_USER_DATABASE_PATH": "data/testusers.db", `
    -e "TMDB:DatabasePath": "data/tmdb.db", `
    -d `
    -p 7132:7132 `
    -p 5282:5282 `
    -v "/mnt/c/Users/rufford/.aspnet/https:/root/https:ro" `
    --name dj-api-container `
    dj-api-image