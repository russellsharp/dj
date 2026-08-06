. "aws/aws_constants.ps1"

aws ecs update-service --cluster $CLUSTER_NAME --service $SERVICE_NAME --desired-count 1 > start_service_last.json