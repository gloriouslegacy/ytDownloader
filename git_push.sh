#!/bin/bash

# $1은 스크립트 실행 시 첫 번째 인자로 전달되는 값을 의미합니다.
commit_message="$1"

# 커밋 메시지가 비어있는지 확인
if [ -z "$commit_message" ]; then
  echo "커밋 메시지를 입력해주세요."
  echo "사용법: ./git_commit.sh \"커밋 메시지\""
  exit 1
fi

git add .
git commit -m "$commit_message"
git push origin main

echo "-----------------------------------"
echo "Git 자동화 완료!"
echo "커밋 메시지: $commit_message"
echo "-----------------------------------"