# WpfApp1

간단한 WPF 데모 애플리케이션입니다. 이 리포지토리는 MVVM 패턴을 사용하는 예제 프로젝트로, 고객/비디오/대여 관련 간단한 도메인 모델과 ViewModel, View를 포함합니다.

## 주요 정보
- 플랫폼: Windows (WPF)
- 타겟 프레임워크: .NET 10
- 프로젝트 구조: MVVM (Models / ViewModels / Views)

## 스크린샷
스크린샷은 리포지토리의 docs/images 또는 assets/images 같은 폴더에 업로드하세요. 예:

![앱 스크린샷](docs/images/screenshot.png)

스크린샷을 추가하려면:
1. 프로젝트 루트에 `docs/images/` 폴더를 만들고 이미지를 넣습니다.
2. README에서 상대 경로로 참조합니다: `![설명](docs/images/파일명.png)`

> 참고: 스크린샷은 사용자가 직접 찍어 올리시면 됩니다. 이미지 파일은 저장소 용량을 고려해 최적화(png/jpg, 적절한 해상도)하세요.

## 기능
- 고객 목록 및 간단 데이터 모델
- 비디오 및 대여 모델 예시
- MVVM 기반 바인딩과 커맨드(RelayCommand)

## 요구사항
- Windows
- .NET 10 SDK
- Visual Studio 2022 이상 또는 Visual Studio 2026 권장

## 빠른 시작

Visual Studio
1. 솔루션 폴더(C:\...\WpfApp1)를 엽니다.
2. `WpfApp1.slnx`를 엽니다.
3. 시작 프로젝트가 `WpfApp1`인지 확인하고 F5로 실행합니다.

명령행
1. 저장소 루트로 이동
   dotnet restore
2. 빌드
   dotnet build
3. 실행 (Windows 환경)
   dotnet run --project WpfApp1.csproj

## 프로젝트 구조
- App.xaml / App.xaml.cs: 애플리케이션 진입점
- Views/: 뷰(예: MainWindow.xaml)
- ViewModels/: 뷰 모델(예: CustomerViewModel.cs, VideoViewModel.cs)
- Models/: 데이터 모델(예: CustomerModel.cs, VideoModel.cs, RentalModel.cs)
- Base/: 공통 유틸(예: RelayCommand, ViewModelBase)

## 아키텍처 요약
- 패턴: MVVM
- 역할: Models는 데이터 구조, ViewModels는 UI 로직과 바인딩, Views는 XAML로 UI를 정의합니다.

## 개발 규칙 / 컨벤션
- C# 코딩 스타일: 기본 .NET 스타일(명명: PascalCase, private 필드: _camelCase)
- 브랜치: 기능별 브랜치는 `feature/` 접두사, 버그는 `bugfix/`
- 커밋 메시지: 영어 또는 한글 상관없으나 요약과 상세를 구분하세요. 예: `feat: 고객 목록 바인딩 추가`

## 기여 가이드 (간단)
1. Issue를 생성하여 변경 이유를 설명합니다.
2. 기능 추가/수정은 새 브랜치 생성(`feature/이름`).
3. PR 템플릿에 변경 요약, 영향 범위, 테스트 방법을 기입합니다.
4. 코드 스타일을 준수하고 가능한 단위 테스트를 추가하세요.

원하시면 이 저장소에 CONTRIBUTING.md와 ISSUE_TEMPLATE을 추가해 드릴 수 있습니다.

## 라이선스
이 저장소에 라이선스를 명시하세요. 권장: MIT

예: 루트에 `LICENSE` 파일을 생성하려면 아래 MIT 텍스트를 사용하세요.

## GitHub에 올리는 방법
1. 로컬에서 초기화 및 커밋:
   git init
   git add .
   git commit -m "Initial commit"
2. GitHub에서 새 저장소 생성
3. 원격 추가 및 푸시:
   git remote add origin https://github.com/사용자명/저장소명.git
   git branch -M main
   git push -u origin main

## 연락/문의
문제나 개선 제안은 Issue로 남겨 주세요.

---
간단한 추가 요청(스크린샷 업로드, LICENSE 파일 자동 생성, CONTRIBUTING.md 생성 등)을 해주시면 내가 파일을 생성해 드리겠습니다.
