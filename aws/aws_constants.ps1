New-Variable -Name 'IMAGE_NAME' -Value 'dj-api-image' -Option Constant
New-Variable -Name 'TASK_FAMILY' -Value 'aspnetcore-net11-app' -Option Constant
New-Variable -Name 'TASK_DEF_FILE' -Value 'file://aws/ecs-taskdef.json' -Option Constant
New-Variable -Name 'AWS_REGION' -Value 'us-east-1' -Option Constant
New-Variable -Name 'AWS_IMAGE_TAG' -Value '648807276746.dkr.ecr.us-east-1.amazonaws.com/dj/api-repo:latest' -Option Constant
New-Variable -Name 'AWS_ECR_REPO' -Value '648807276746.dkr.ecr.us-east-1.amazonaws.com' -Option Constant
New-Variable -Name 'SERVICE_NAME' -Value 'dj-api-service-http' -Option Constant
New-Variable -Name 'CLUSTER_NAME' -VALUE 'default' -Option Constant