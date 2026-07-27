# aws ecr create-repository --repository-name dj/api-repo --region us-east-1

## Cloudwatch

Create the log group as the AWS task creation process does not necessarily creat them.

Log group is in the task definition: 

```jsonpath
.containerDefinitions[0].logConfiguration.options.awslogs-group
```

## Users and Roles
### Permissions

Policies are in .\aws\ecsTask*Role*.json file.

Some key permissions:
- Trust Relationships: Statement.Principe.Service: "ecs-tasks.amazonaws.com, Statement.Action: "sts:AssumeRole"
- ecsTaskExecutionRole
  - logs:CreateGroup
  - secretsmanager:GetSecretValue, Resource: dev/dj/*
- ecsTaskRole
  - logs:CreateGroup
  - sts:AssumeRole for relevant roles
  - S3, SecretsManager, DynamoDb, and other AWS resources being used by the application.

## Setup Image in ECR

Can run the script to build, publish, and deploy the image to ECR.

```shell .\docker-build.ps1 -awsDeploy```

Or run the following:

From ECR Push Commands

```shell
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 648807276746.dkr.ecr.us-east-1.amazonaws.com.com
```
  
Note: If you receive an error using the AWS TOOLS for PowerShell, make sure that you have the latest version of the AWS TOOLS for PowerShell and Docker installed.

Build your Docker image using the following command. For information on building a Docker file from scratch see the instructions here . You can skip this step if your image is already built:
  
```shell docker build -t dj/api-repo . ```
  
After the build completes, tag your image so you can push the image to this repository:

```shell docker tag dj/api-repo:latest 648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest ```
  
Run the following command to push this image to your newly created AWS repository:
```shell docker push 648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest ```

## Setup Task Definition

Use the task defintion in \aws
```shell
aws ecs register-task-definition --cli-input-json file://aws/ecs-taskdef.json --region us-east-1
```

## Setup Service

Using the following command or AWS Console.

```shell
aws ecs create-service \
    --cluster default \
    --service-name dj-api-service \
    --task-definition aspnetcore-net11-app:latest \
    --desired-count 1 \
    --launch-type FARGATE \
    --platform-version LATEST
```