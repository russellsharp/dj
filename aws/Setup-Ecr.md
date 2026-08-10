# aws ecr create-repository --repository-name dj/api-repo --region us-east-1




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