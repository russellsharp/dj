docker container stop dj-api-container
docker container rm dj-api-container
 
# powershell command

docker run `
    -e "DJ_TMDB_API_KEY=$env:DJ_TMDB_API_KEY" `
    -e "DJ_SECURITY_KEY=$env:DJ_SECURITY_KEY" `
    -e "ASPNETCORE_ENVIRONMENT=Development" `
    -e "APSNETCORE_URLS=https://+:7132;http://+:5282" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Password=$env:ASPNETCORE_Kestrel__Certificates__Default__Password" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Path=/root/https/aspnetapp.pfx" `
    -d `
    -p 7132:7132 `
    -p 5282:5282 `
    -v "${env:USERPROFILE}\.aspnet\https:/root/https:ro" `
    --name dj-api-container `
    dj-api-image