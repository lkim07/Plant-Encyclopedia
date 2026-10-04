1. 지금까지 우리가 한 것

큰 흐름으로 보면 이렇게 왔어.

아이디어
  ↓
제품 정의
  ↓
UX 설계
  ↓
중요한 결정 정리
  ↓
시스템 아키텍처
  ↓
DB 설계
  ↓
API 설계
  ↓
개발 로드맵
  ↓
AI/Cursor 작업 규칙
  ↓
현재 상태 / TODO
  ↓
▶ 이제 실제 구현

각각을 보면:

//////////////////////////////////////////////////////
① Product Requirements

PRODUCT_REQUIREMENTS.md

"무엇을 만들 것인가?"

를 정했어.

예를 들어:

식물 백과사전이라는 제품의 목적
Identify
Explore
Search
Favorites
Plant Detail
Where to Buy
Similar Plants
추천 시스템
AI 식물 식별
MVP에서 제외할 것

등을 정했지.

//////////////////////////////////////////////////////
② UX Specification

UX_SPECIFICATION.md

"사용자가 이 제품을 어떻게 사용할 것인가?"

를 정했어.

예를 들어:

Explore
  ↓
Plant Group
  ↓
Plant
  ↓
Detail

또는

Camera
  ↓
Photo Preview
  ↓
Identify
  ↓
Identification Results
  ↓
Plant Detail

같은 사용자 흐름.

그리고 모바일/데스크톱, sidebar, masonry grid, bookmark, loading, error, animation까지 정의했어.


//////////////////////////////////////////////////////
③ Decisions

DECISIONS.md

이건 **"왜 이렇게 결정했는가?"**를 저장하는 문서야.

이게 생각보다 중요해.

예를 들어 우리가 나중에 Cursor에게:

"왜 mobile bottom navigation을 안 만들었지?"

라고 물었을 때,

그냥 "그냥 안 만들기로 했어요"가 아니라

기존 navigation 구조와 UX 방향을 고려해서 slide-in sidebar를 사용하기로 결정했다.

라는 식으로 결정의 이유를 보존하는 거야.


//////////////////////////////////////////////////////
④ Architecture

ARCHITECTURE.md

"이 시스템을 기술적으로 어떻게 만들 것인가?"

를 정했어.

큰 구조는:

Angular
   ↓
ASP.NET Core API
   ↓
Application / Domain
   ↓
Infrastructure
   ↓
PostgreSQL

그리고:

iNaturalist / GBIF
        ↓
External Provider Layer
        ↓
Application
        ↓
PostgreSQL

AI도:

Image
 ↓
AI Provider
 ↓
Candidate
 ↓
DB validation
 ↓
Identification Result

처럼 만들기로 했어.

그리고 중요한 결정:

처음부터 microservices로 만들지 않는다.

즉, portfolio라고 해서 괜히 Kubernetes, Kafka, microservices를 붙이지 않는 거야.



//////////////////////////////////////////////////////
⑤ Database Design

DATABASE_DESIGN.md

"데이터를 어떻게 저장할 것인가?"

를 정했어.

예를 들어:

Plant
 ├── Names
 ├── Cultivar
 ├── Category
 ├── Plant Group
 ├── Images
 ├── Care
 ├── Blooming
 ├── Health
 └── Sources

그리고 나중에는:

User
 ├── Favorites
 ├── Search History
 ├── View History
 └── Identification Requests

같은 구조가 들어가게 돼.

특히 중요한 건:

iNaturalist나 GBIF가 우리 DB가 아니라 외부 데이터 공급원이라는 것.

우리 서비스의 source of truth는 우리 DB로 두기로 했어.

//////////////////////////////////////////////////////
⑥ API Specification

API_SPEC.md

"Frontend와 Backend가 어떻게 대화할 것인가?"

를 정했어.

예:

GET /api/plants/{plantId}
GET /api/explore
GET /api/search
POST /api/identification
GET /api/favorites

이렇게 API 계약을 먼저 정해둔 거야.
//////////////////////////////////////////////////////
⑦ Development Roadmap

DEVELOPMENT_ROADMAP.md

"이걸 어떤 순서로 만들 것인가?"

를 정했어.

대략:

Foundation
 ↓
Backend
 ↓
Database
 ↓
Frontend
 ↓
Plant Detail
 ↓
Search
 ↓
External Data
 ↓
Auth/Favorites
 ↓
Explore/Recommendation
 ↓
AI Identification
 ↓
Where to Buy
 ↓
Testing
 ↓
Docker/CI/CD/AWS
 ↓
Polish
//////////////////////////////////////////////////////
⑧ Project Instructions

PROJECT_INSTRUCTIONS.md

이건 조금 성격이 달라.

"Cursor/AI가 이 프로젝트에서 어떻게 행동해야 하는가?"

를 정했어.

예를 들어 Cursor에게:

기존 UX 결정을 마음대로 바꾸지 마라.

전체 프로젝트를 한 번에 만들려고 하지 마라.

먼저 repository를 조사해라.

작은 단위로 구현해라.

테스트해라.

문서와 실제 구현 상태가 다르면 업데이트해라.

등을 알려주는 거야.

이 문서가 AI coding agent의 헌법이라고 생각하면 돼.
//////////////////////////////////////////////////////
⑨ Current State

CURRENT_STATE.md

"지금 실제로 어디까지 왔는가?"

를 기록하는 문서야.

현재는:

Product/UX/Architecture/Database/API 설계는 되어 있지만, 실제 구현은 본격적으로 시작되지 않았다.

라고 되어 있어.

굉장히 중요한 구분이야.
//////////////////////////////////////////////////////
⑩ TODO

TODO.md

마지막으로:

"그래서 이제 뭘 해야 하는가?"

를 정리했어.

현재 가장 앞부분은:

Repository
 ↓
ASP.NET Core
 ↓
PostgreSQL + EF Core
 ↓
Plant Model
 ↓
Migration
 ↓
Angular
 ↓
Plant Detail

이야.
//////////////////////////////////////////////////////






2. 그러면 지금 이 문서들의 목적은 뭐냐?

핵심은 AI가 프로젝트를 기억하도록 만드는 것이야.

네가 처음에 GardenOps에서 겪었던 문제가 있었잖아.

긴 하나의 ChatGPT conversation에서는 앞에서 결정한 걸 기억하는데, 새 chat을 열면:

"어? 이거 전에 뭐라고 결정했지?"

가 되는 문제.

Plant Encyclopedia에서는 그걸 문서 시스템으로 해결하려는 것이야.

각각 한 문장으로 기억하면 돼
문서	질문
PRODUCT_REQUIREMENTS.md	무엇을 만들지?
UX_SPECIFICATION.md	사용자가 어떻게 사용할지?
DECISIONS.md	왜 이렇게 결정했지?
ARCHITECTURE.md	시스템을 어떻게 만들지?
DATABASE_DESIGN.md	데이터를 어떻게 저장하지?
API_SPEC.md	Frontend와 Backend가 어떻게 통신하지?
DEVELOPMENT_ROADMAP.md	어떤 순서로 만들지?
PROJECT_INSTRUCTIONS.md	AI/Cursor가 어떻게 작업해야 하지?
CURRENT_STATE.md	지금 실제로 어디까지 됐지?
TODO.md	다음에 뭘 해야 하지?

이렇게 생각하면 돼.

3. 그런데 중요한 문제가 하나 있어

문서를 너무 많이 만들었다고 이제 계속 문서만 만들면 안 돼.

여기서 멈춰야 해.

지금부터는:

Documentation → Implementation

으로 넘어가야 해.

문서를 더 정교하게 만드는 것보다 실제 코드를 만들면서 문서가 현실과 맞는지 검증하는 것이 중요해.

4. 앞으로 실제로 어떻게 진행할까?

내가 너라면 이렇게 할 거야.

STEP 0 — 문서 정리

먼저 딱 한 번 확인.

Project 안에:

PROJECT_INSTRUCTIONS.md
PRODUCT_REQUIREMENTS.md
UX_SPECIFICATION.md
DECISIONS.md
ARCHITECTURE.md
DATABASE_DESIGN.md
API_SPEC.md
DEVELOPMENT_ROADMAP.md
CURRENT_STATE.md
TODO.md

가 있는지 확인.

그리고 ARCHITECTURE.md 중복 파일은 하나만 남겨.

우리가 마지막에 만든 버전을 기준으로.

여기까지가 준비 단계의 마지막이야.

5. STEP 1 — 실제 Repository 만들기

이제 Cursor를 사용하기 시작해.

우선 아무 기능도 만들지 말고:

"프로젝트 구조를 먼저 만들어줘."

부터 하는 게 좋아.

예를 들어 최종적으로:

plant-encyclopedia/
│
├── backend/
│
├── frontend/
│
├── mobile/
│
├── docs/
│
├── README.md
└── .gitignore

정도의 구조를 만들게 될 거야.

하지만 Cursor에게 바로 실행시키기 전에 나한테 먼저 물어봐도 돼.

내가:

"이 구조로 가자."

라고 확인해주고 Cursor에게 보내는 식으로 진행하면 돼.

6. STEP 2 — Backend부터 만든다

우리가 C#/.NET을 배우려는 목적도 있으니까 여기서부터 천천히 가자.

처음부터 Plant API 20개 만들지 않아.

먼저:

ASP.NET Core

프로젝트 하나를 실행시키는 것부터.

목표:

Browser
   ↓
ASP.NET Core
   ↓
Hello / Health response

정도.

이때 내가 너에게 설명해줄 것:

.NET이 뭐인지
ASP.NET Core가 뭐인지
Solution이 뭔지
Project가 뭔지
Controller가 뭔지
Dependency Injection이 뭔지
Program.cs가 뭔지

즉, Cursor가 코드를 만들어도 네가 무슨 일이 일어나는지 이해하도록 할 거야.

7. STEP 3 — PostgreSQL + EF Core

그다음:

ASP.NET Core
      ↓
EF Core
      ↓
PostgreSQL

을 연결해.

여기서 네가 배우게 될 핵심:

Database
Table
Row
Primary Key
Foreign Key
Entity
DbContext
Migration
ORM

그리고 첫 번째 Entity를 만들자.

아마 가장 먼저:

Plant

부터.

8. STEP 4 — 첫 번째 진짜 API

그다음:

GET /api/plants/{plantId}

를 만든다.

예를 들어 DB에:

Plant
------------------
id: 1
name: Peace Rose
scientificName: Rosa × hybrida

가 있다면,

Frontend가:

GET /api/plants/1

했을 때 JSON을 받는 거야.

이게 굉장히 중요한 순간이야.

왜냐하면 이때 처음으로:

Database
    ↓
Backend
    ↓
API

가 연결되기 때문이야.

9. STEP 5 — Angular 시작

그 다음 frontend.

Angular

프로젝트를 만들고:

Angular
   ↓
API
   ↓
ASP.NET Core
   ↓
PostgreSQL

를 연결해.

처음에는 예쁜 UI를 만들지 않아도 돼.

그냥:

Peace Rose

가 화면에 뜨면 성공이야.

10. STEP 6 — 첫 번째 Vertical Slice

이제 정말 중요한 단계야.

Plant Detail 하나를 완성한다.

예를 들어:

URL
/api/plants/1

→ Backend

→ PostgreSQL

→ Angular

→ Plant Detail

그리고:

Hero
Name
Scientific Name
Quick Care
Blooming
Health
About

을 실제 DB 데이터로 보여줘.

이걸 Vertical Slice라고 생각하면 돼.

11. 왜 Plant Detail부터 하냐?

처음부터:

Explore + Search + AI + Favorites + AWS

를 만들면 문제가 생겨.

각각 따로 만들어놓고 마지막에 연결하려 하면 어디가 잘못됐는지 찾기가 어려워.

반면:

DB
 ↓
API
 ↓
Frontend
 ↓
User

하나를 처음부터 끝까지 만들어 보면,

"아, 이게 실제 application이 연결되는 방식이구나."

를 배우게 돼.

그리고 우리가 만든 ARCHITECTURE.md가 실제 코드에서도 제대로 작동하는지 검증할 수 있어.

12. STEP 7 — Search

Plant Detail 하나가 되면:

Search

를 만든다.

User types "rose"
        ↓
Angular
        ↓
GET /api/search?q=rose
        ↓
ASP.NET Core
        ↓
PostgreSQL
        ↓
Results

이렇게.

여기서 SQL/EF Core의 실제 활용을 배우게 돼.

13. STEP 8 — Category / Plant Group

그다음:

Explore
 ↓
Flowers
 ↓
Roses
 ↓
Peace Rose

를 구현.

이때 DB relationship이 실제로 필요해지기 때문에,

Category → Plant Group → Plant

구조를 제대로 이해하게 돼.

14. STEP 9 — External Data

이제서야 iNaturalist/GBIF를 붙여.

중요한 순서야.

처음부터 외부 API에 의존하지 않는 이유는:

우리 DB

가 먼저 있어야 외부 데이터를 가져와도

External API
 ↓
Normalize
 ↓
Validate
 ↓
Our DB

라는 구조를 이해할 수 있기 때문이야.

15. STEP 10 — Authentication + Favorites

그 다음:

User
 ↓
Login
 ↓
Favorite
 ↓
PostgreSQL

을 구현.

이때 authentication/authorization을 실제로 배우게 돼.

16. STEP 11 — Explore Recommendation

이제 사용자의:

View History
Search History
Favorites

가 생겼으니까 추천을 만들 수 있어.

처음부터 AI 추천 시스템을 만들 필요 없어.

우리가 정한:

60% related
20% adjacent
20% novel

원칙을 간단한 rule-based system으로 먼저 구현.

17. STEP 12 — AI Identification

이제야 AI.

왜냐하면 그때쯤이면:

Plant Database
+
Search
+
Plant Detail
+
External Data

가 있기 때문이야.

그래야 AI가:

"이거 Peace Rose 같아요."

라고 했을 때,

우리가:

AI Candidate
     ↓
Our Database
     ↓
Validated?
     ↓
Identification Result

할 수 있어.

이게 그냥 Gemini API 하나 호출하는 것보다 훨씬 좋은 portfolio 프로젝트가 되는 부분이야.

18. STEP 13 — Where to Buy

그다음 Vancouver 중심으로:

Plant
 ↓
Nearby Nurseries
 ↓
Flower Shops
 ↓
Garden Centres

를 구현.

이때 location permission, distance calculation, retailer data 등의 실제 문제를 다루게 돼.

19. STEP 14 — Testing

그 다음 테스트를 강화해.

Unit Test
Integration Test
E2E Test

특히:

Search
Plant Detail
Favorite
Identification

같은 핵심 flow를 테스트.

20. STEP 15 — Docker / GitHub Actions / AWS

마지막 쪽에서:

Docker
 ↓
GitHub Actions
 ↓
CI
 ↓
AWS
 ↓
Production

으로 가져가.

이때부터 프로젝트가 단순히:

"내 컴퓨터에서 실행되는 앱"

이 아니라

"실제로 배포된 소프트웨어 시스템"

이 돼.

21. STEP 16 — UI Polish

마지막에 우리가 이야기했던:

Lando Norris 스타일의 인터랙티브한 느낌
Botanical cursor
Falling leaves
Hero scroll transition
subtle hover
Bookmark animation

등을 넣어.

순서를 뒤로 미룬 이유가 있어.

예쁜 animation을 먼저 만들면:

"와, 예쁘다."

는 되지만,

"이 사람이 backend/API/database/cloud를 실제로 할 줄 아는구나."

는 증명하지 못하거든.

반대로:

Working Product
+
Good Architecture
+
Database
+
API
+
Tests
+
AI
+
Cloud
+
Polished UX

까지 가면 훨씬 강한 portfolio가 돼.

22. 앞으로 나와 Cursor를 어떻게 같이 사용할까?

이 부분이 네 프로젝트에서 굉장히 중요해.

나(ChatGPT)

너와 함께:

무엇을 만들지 결정
개념 설명
architecture 검토
DB/API 설계
Cursor에게 줄 prompt 작성
Cursor 결과 리뷰
에러 분석
면접에서 설명할 수 있도록 정리

를 담당.

Cursor

실제 repository를 보고:

코드 작성
파일 수정
refactoring
test 작성
실행
compiler error 수정

을 담당.

즉:

너
 ↓
"이 기능을 만들고 싶어."

ChatGPT
 ↓
설계 + 설명 + 구현 계획
 ↓
Cursor
 ↓
실제 코드
 ↓
ChatGPT
 ↓
Review / 이해
 ↓
너
 ↓
승인

이 workflow를 사용하는 거야.

23. 그리고 앞으로 내가 가이드할 때의 원칙

너는 C#/.NET을 처음부터 배우는 상태이기 때문에 내가 그냥:

"Cursor에게 이 prompt 넣으세요."

만 하지는 않을 거야.

각 단계마다:

① 먼저 개념

"오늘 우리가 뭘 배우는가?"

② 왜 필요한지

"왜 Plant Encyclopedia에서 필요한가?"

③ 구조
A → B → C
④ Cursor prompt

Cursor에게 실제로 무엇을 시킬지.

⑤ Cursor 결과 확인

네가 결과를 가져오면 내가 리뷰.

⑥ 직접 이해

"이 코드에서 Controller가 왜 필요한가?"

같은 질문을 통해 네가 이해.

⑦ 테스트

실제로 실행.

⑧ 다음 단계

문제 없으면 다음 단계로 이동.

이 방식으로 갈 거야.

24. 그래서 지금 당장 할 일

오늘 당장 코딩을 시작해도 돼.

순서는 아주 단순하게 잡자.

NOW
│
├─ 1. Project 문서 10개 정리
│
├─ 2. Repository 확인
│
├─ 3. Backend 생성
│
├─ 4. ASP.NET Core 실행
│
├─ 5. PostgreSQL 연결
│
├─ 6. Plant Entity
│
├─ 7. Migration
│
├─ 8. Seed Data
│
├─ 9. Plant Detail API
│
├─ 10. Angular 연결
│
└─ 11. 첫 Plant Detail 완성

그리고 여기서 한 번 멈춰서 architecture를 검증한다.

그 이후에 Search → Category → External Data → Auth → Favorites → Explore → AI → Where to Buy → Testing → Docker → AWS 순으로 가면 돼.

가장 중요한 것 하나

지금 만든 문서들은 “앞으로 우리가 절대로 문서를 다시 수정하지 않는다”는 뜻이 아니야.

오히려 반대야.

실제 코딩하다 보면 반드시 이런 일이 생겨.

"어? 이 DB 구조보다 다른 구조가 더 낫네."

그러면 그냥 코드를 막 바꾸는 게 아니라:

실제 구현 중 발견
       ↓
문제/대안 검토
       ↓
DECISIONS.md 수정
       ↓
DATABASE_DESIGN.md 수정
       ↓
ARCHITECTURE.md 수정
       ↓
TODO 수정
       ↓
구현

이렇게 문서 ↔ 실제 코드가 계속 서로 맞춰지게 하는 거야.

그래서 지금 문서 작업은 끝났지만, 프로젝트의 문서는 앞으로도 살아 있는 문서가 돼.

이제 진짜 Plant Encyclopedia를 만들기 시작하면 돼.
다음 단계부터는 내가 STEP 1부터 한 단계씩 끊어서, 네가 직접 이해하면서 Cursor와 함께 구현할 수 있게 가이드하면 된다.


///////
1. Plant Encyclopedia repository 위치
2. Git 상태
3. .NET SDK
4. Node.js / npm
5. Angular CLI
6. Docker