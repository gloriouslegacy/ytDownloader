# 📌 버전 관리 규칙 (ytDownloader)

이 프로젝트는 **GitHub Actions** 및 **커밋 메시지 규칙**에 따라 자동으로 버전을 증가시키고 릴리스를 생성합니다.

---

## 🚀 버전 증가 규칙

| 커밋 메시지 패턴        | 의미              | 버전 증가 방식 |
|--------------------------|-------------------|----------------|
| `fix: ...`, `chore: ...` | 버그 수정 / 유지보수 | 패치(Patch) 증가 |
| `feat: ...`              | 새 기능 추가        | 마이너(Minor) 증가 |
| `BREAKING: ...`          | 호환성 깨짐 변경     | 메이저(Major) 증가 |

---

## 🛠 예시 (이전 버전 = `0.3.5` , 오늘 날짜 = `20250919`)

| 커밋 메시지 예시                           | 결과 버전             |
|--------------------------------------------|-----------------------|
| `fix: update release.yml quoting`          | **0.3.6-20250919**    |
| `chore: update docs`                       | **0.3.6-20250919**    |
| `feat: add updater silent mode`            | **0.4.0-20250919**    |
| `feat: support GitHub Actions auto update` | **0.4.0-20250919**    |
| `BREAKING: migrate UI to new framework`    | **1.0.0-20250919**    |

---

## 📦 Release 정책

- **main 브랜치 push**
  - 커밋 메시지 규칙에 따라 버전 증가
  - 날짜(`-YYYYMMDD`) 가 붙은 **Pre-release** 로 생성됨  
  - 예: `0.4.0-20250919`

- **태그 push (vX.Y.Z)**
  - 지정한 버전 그대로 릴리스
  - 정식 Release 로 표시됨  
  - 예: `git tag v1.0.0 && git push origin v1.0.0`

---

## ✅ 요약

- **패치**: `fix:` 또는 `chore:`  
- **마이너**: `feat:`  
- **메이저**: `BREAKING:`  
- **main push** → Pre-release (날짜 포함)  
- **태그 push** → 정식 Release (날짜 없음)  

---
