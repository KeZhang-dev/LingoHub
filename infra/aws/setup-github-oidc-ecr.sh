#!/usr/bin/env bash
set -euo pipefail

REGION="ap-southeast-2"
GITHUB_REPO="KeZhang-dev/LingoHub"
GITHUB_BRANCH="main"
ROLE_NAME="lingohub-github-actions-ecr-push"
POLICY_NAME="lingohub-ecr-push"
REPOSITORIES=("lingohub-api" "lingohub-web")
OIDC_HOST="token.actions.githubusercontent.com"

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
OIDC_PROVIDER_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${OIDC_HOST}"
WORK_DIR="$(mktemp -d)"

echo "Account: ${ACCOUNT_ID}  Region: ${REGION}  Repo: ${GITHUB_REPO} (${GITHUB_BRANCH})"

echo "==> ECR repositories"
cat > "${WORK_DIR}/lifecycle.json" <<'JSON'
{
  "rules": [
    {
      "rulePriority": 1,
      "description": "Keep the 5 most recent images",
      "selection": { "tagStatus": "any", "countType": "imageCountMoreThan", "countNumber": 5 },
      "action": { "type": "expire" }
    }
  ]
}
JSON
REPO_ARNS=()
for repo in "${REPOSITORIES[@]}"; do
  if ! aws ecr describe-repositories --region "${REGION}" --repository-names "${repo}" >/dev/null 2>&1; then
    aws ecr create-repository --region "${REGION}" --repository-name "${repo}" \
      --image-scanning-configuration scanOnPush=true --image-tag-mutability MUTABLE >/dev/null
    echo "    created ${repo}"
  else
    echo "    ${repo} already exists"
  fi
  aws ecr put-lifecycle-policy --region "${REGION}" --repository-name "${repo}" \
    --lifecycle-policy-text "file://${WORK_DIR}/lifecycle.json" >/dev/null
  REPO_ARNS+=("\"arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/${repo}\"")
done

echo "==> GitHub OIDC identity provider"
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "${OIDC_PROVIDER_ARN}" >/dev/null 2>&1; then
  echo "    already exists"
else
  aws iam create-open-id-connect-provider \
    --url "https://${OIDC_HOST}" \
    --client-id-list "sts.amazonaws.com" \
    --thumbprint-list "6938fd4d98bab03faadb97b34396831e3780aea1" >/dev/null
  echo "    created"
fi

echo "==> IAM role ${ROLE_NAME}"
cat > "${WORK_DIR}/trust.json" <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "${OIDC_PROVIDER_ARN}" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "${OIDC_HOST}:aud": "sts.amazonaws.com",
          "${OIDC_HOST}:sub": "repo:${GITHUB_REPO}:ref:refs/heads/${GITHUB_BRANCH}"
        }
      }
    }
  ]
}
JSON
if aws iam get-role --role-name "${ROLE_NAME}" >/dev/null 2>&1; then
  aws iam update-assume-role-policy --role-name "${ROLE_NAME}" --policy-document "file://${WORK_DIR}/trust.json"
  echo "    exists, trust policy updated"
else
  aws iam create-role --role-name "${ROLE_NAME}" \
    --description "GitHub Actions (${GITHUB_REPO}@${GITHUB_BRANCH}) pushes images to LingoHub ECR repositories" \
    --assume-role-policy-document "file://${WORK_DIR}/trust.json" --max-session-duration 3600 >/dev/null
  echo "    created"
fi

cat > "${WORK_DIR}/permissions.json" <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "GetEcrLoginToken",
      "Effect": "Allow",
      "Action": "ecr:GetAuthorizationToken",
      "Resource": "*"
    },
    {
      "Sid": "PushToLingoHubRepositories",
      "Effect": "Allow",
      "Action": [
        "ecr:BatchCheckLayerAvailability",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload",
        "ecr:PutImage"
      ],
      "Resource": [$(IFS=,; echo "${REPO_ARNS[*]}")]
    }
  ]
}
JSON
aws iam put-role-policy --role-name "${ROLE_NAME}" --policy-name "${POLICY_NAME}" \
  --policy-document "file://${WORK_DIR}/permissions.json"
echo "    inline policy ${POLICY_NAME} applied"

rm -rf "${WORK_DIR}"

echo
echo "Done. Add this GitHub repository variable (Settings > Secrets and variables > Actions > Variables):"
echo "    AWS_ROLE_ARN = arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"
