[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [switch]$AwsDeploy
)

New-Variable -Name "IMAGE_NAME" -VALUE "dj-api-image" -Option Constant
New-Variable -Name "TASK_FAMILY" -VALUE "aspnetcore-net11-app" -Option Constant
New-Variable -Name "TASK_DEF_FILE" -VALUE "file://aws/ecs-taskdef.json" -Option Constant
New-Variable -Name "AWS_REGION" -VALUE "us-east-1" -Option Constant
New-Variable -Name "AWS_IMAGE_TAG" -VALUE "648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest" -Option Constant
New-Variable -Name "AWS_ECR_REPO" -VALUE "648807276746.dkr.ecr.us-east-1.amazonaws.com" -Option Constant
New-Variable -Name "SERVICE_NAME" -VALUE "dj-api-service-http" -Option Constant
dotnet clean

docker build -f docker\DockerFile.release -t dj-api-image .

if ($AwsDeploy) {
    
    aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $AWS_ECR_REPO

    docker tag $IMAGE_NAME:latest $AWS_IMAGE_TAG\

    docker push AWS_IMAGE_TAG

    aws ecs register-task-definition --cli-input-json $TASK_DEF_FILE --region $AWS_REGION > task_def_last.json

    aws ecs update-service --cluster default --service $SERVICE_NAME --task-definition $TASK_FAMILY --health-check-grace-period-seconds 120 --force-new-deployment > update_service_last.json 
}