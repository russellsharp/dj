[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [switch]$AwsDeploy
)

. ".\aws\aws_constants.ps1"

Set-PSDebug -Trace 1

docker ps

dotnet clean

docker build -f docker\DockerFile.release -t dj-api-image .

if ($AwsDeploy) {
    
    aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $AWS_ECR_REPO

    docker tag $IMAGE_NAME:latest $AWS_IMAGE_TAG\

    docker push AWS_IMAGE_TAG

    aws ecs register-task-definition --cli-input-json $TASK_DEF_FILE --region $AWS_REGION > task_def_last.json

    aws ecs update-service --cluster default --service $SERVICE_NAME --task-definition $TASK_FAMILY --health-check-grace-period-seconds 120 --force-new-deployment > update_service_last.json 
}