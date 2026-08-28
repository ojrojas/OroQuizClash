#!/usr/bin/env bash
# Registers the quizarena-admin OIDC client (confidential, authorization_code + refresh_token,
# PKCE) in OroIdentityServer via its master-admin API (contracts/oidc-config.md §1).
#
# Usage:
#   IDP_URL=https://localhost:5086 IDP_ADMIN_USER=admin IDP_ADMIN_PASSWORD='...' \
#   ADMIN_REDIRECT_URI=https://localhost:7172/signin-oidc \
#   ADMIN_POST_LOGOUT_URI=https://localhost:7172/signout-callback-oidc \
#   ADMIN_CLIENT_SECRET="$(openssl rand -hex 32)" \
#   ./scripts/register-admin-oidc-client.sh
#
# The printed ADMIN_CLIENT_SECRET must be stored as the Aspire parameter
# `quizarena-admin-oidc-secret` (AppHost user secrets) or Identity__ClientSecret env var.
set -euo pipefail

IDP_URL="${IDP_URL:-https://localhost:5086}"
IDP_ADMIN_USER="${IDP_ADMIN_USER:-admin}"
: "${IDP_ADMIN_PASSWORD:?IDP_ADMIN_PASSWORD is required (master admin password)}"
CLIENT_ID="${CLIENT_ID:-quizarena-admin}"
ADMIN_REDIRECT_URI="${ADMIN_REDIRECT_URI:-https://localhost:7172/signin-oidc}"
ADMIN_POST_LOGOUT_URI="${ADMIN_POST_LOGOUT_URI:-https://localhost:7172/signout-callback-oidc}"
ADMIN_CLIENT_SECRET="${ADMIN_CLIENT_SECRET:-$(openssl rand -hex 32)}"

cookies="$(mktemp)"
trap 'rm -f "$cookies"' EXIT

echo "-> Signing in to $IDP_URL as $IDP_ADMIN_USER"
curl -sk -c "$cookies" -o /dev/null -w "   login: HTTP %{http_code}\n" \
  -X POST "$IDP_URL/auth/login" \
  --data-urlencode "loginIdentifier=$IDP_ADMIN_USER" \
  --data-urlencode "password=$IDP_ADMIN_PASSWORD"

echo "-> Checking existing registration"
status=$(curl -sk -b "$cookies" -o /dev/null -w "%{http_code}" "$IDP_URL/api/applications/$CLIENT_ID")
if [ "$status" = "200" ]; then
  echo "   client '$CLIENT_ID' already exists — nothing to do (update it via PUT if needed)"
  exit 0
fi

echo "-> Creating client '$CLIENT_ID'"
curl -sk -b "$cookies" -H "Content-Type: application/json" \
  -o /dev/null -w "   create: HTTP %{http_code}\n" \
  -X POST "$IDP_URL/api/applications" -d "{
  \"clientId\": \"$CLIENT_ID\",
  \"clientSecret\": \"$ADMIN_CLIENT_SECRET\",
  \"displayName\": \"QuizArena Administration (BFF)\",
  \"clientType\": \"confidential\",
  \"applicationType\": \"web\",
  \"consentType\": \"implicit\",
  \"permissions\": [\"ept:authorization\", \"ept:token\", \"ept:end_session\", \"ept:userinfo\", \"gt:authorization_code\", \"gt:refresh_token\", \"rst:code\", \"scp:openid\", \"scp:profile\", \"scp:email\", \"scp:roles\", \"scp:offline_access\", \"scp:admin\"],
  \"requirements\": [\"ft:pkce\"],
  \"redirectUris\": [\"$ADMIN_REDIRECT_URI\"],
  \"postLogoutRedirectUris\": [\"$ADMIN_POST_LOGOUT_URI\"]
}"

echo "-> Done. Store this secret as quizarena-admin-oidc-secret (Aspire user secrets):"
echo "   $ADMIN_CLIENT_SECRET"
