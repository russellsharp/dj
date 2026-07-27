[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [switch]$AwsDeploy
)

dotnet clean

docker build -f docker\DockerFile.release -t dj-api-image .

if ($AwsDeploy) {
    
    aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 648807276746.dkr.ecr.us-east-1.amazonaws.com

    docker tag dj-api-image:latest 648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest

    docker push 648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest
}