## Setup Docker CLI in WSL
- Switch default distro for wsl to ubuntu
- wsl terminal
```shell
sudo nano /etc/wsl.conf
```
```ini
[boot]
systemd=true
```
  - Restart wsl
  - Download and execute the installer script (no desktop) in WSL
  ```shell
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
  ```
  - Manage Docker Without Sudo
  ```shell
  sudo usermod -aG docker $USER
  ```
  - Verify CLI works in WSL
```shell
docker run hello-world
```
  - Expose Docker daemon inside WSL
  ```shell
sudo mkdir -p /etc/docker
sudo nano /etc/docker/daemon.json
  ```
  ```json
{
  "hosts": ["unix:///var/run/docker.sock", "tcp://127.0.0.1:2375"]
}
  ```
  - sudo systemctl edit docker.service
  ```ini
[Service]
ExecStart=
ExecStart=/usr/bin/dockerd
  ```
  - Reload the systemd configuration and restart docker service
```shell
sudo systemctl daemon-reload
sudo systemctl restart docker.service
```
  - Install Docker CLI on Windows
```shell
winget install Docker.DockerCLI
```
  - Direct windows to WSL Daemon
```ps1
[Environment]::SetEnvironmentVariable("DOCKER_HOST", "tcp://127.0.0.1:2375", "User")
```
  - Reload terminals and run docker ps




## Setup Cert for Container

```ps1
mkdir %USERPROFILE%\.aspnet\https
dotnet dev-certs https -ep ${env:USERPROFILE}\.aspnet\https\aspnetapp.pfx -p YourSecurePassword123!
dotnet dev-certs https --trust
```
You should be prompted to approve the cert.

Add parameters to docker run command or set environment variables when building the container.

```
docker run `
    -e TMDB_API_KEY=$env:DJ_TMDB_API_KEY `
    -e DJ_SECURITY_KEY=$env:DJ_SECURITY_KEY `
    -e "ASPNETCORE_ENVIRONMENT=Development" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Password=$env:ASPNETCORE_Kestrel__Certificates__Default__Password" `
    -e "ASPNETCORE_Kestrel__Certificates__Default__Path=/root/https/aspnetapp.pfx" `
    -d `
    -p 7132:7132 `
    -p 5282:5282 `
    -v "${env:USERPROFILE}\.aspnet\https:/root/https:ro" `
    --name dj-api-container `
    dj-api-image
```

