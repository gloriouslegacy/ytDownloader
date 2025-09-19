param (
    [string]$prev = "0.1.0",   # 최신 릴리스 버전 (예: GitHub API로 가져온 값)
    [string]$lastCommit = "fix: update workflow" # 최근 커밋 메시지
)

Write-Output "Previous version: $prev"
Write-Output "Last commit: $lastCommit"

# 따옴표 제거
$prev = $prev.Trim("'").Trim('"')

# 버전 분리
$parts = $prev.Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

# 규칙에 따라 버전 증가
if ($lastCommit -match "BREAKING") {
    $major++
    $minor = 0
    $patch = 0
}
elseif ($lastCommit -match "^feat:") {
    $minor++
    $patch = 0
}
else {
    $patch++
}

# 새 버전 + 날짜
$newVersion = "$major.$minor.$patch"
$date = Get-Date -Format "yyyyMMdd"
$finalVersion = "$newVersion-$date"

Write-Output "👉 Calculated version: $finalVersion"
