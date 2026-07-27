. "aws/aws_constants.ps1"

aws ecs update-service --cluster $CLUSTER_NAME --service $SERVICE_NAME --desired-count 0 > stop_service_last.json