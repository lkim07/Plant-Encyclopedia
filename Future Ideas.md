Future Ideas

1. 더 똑똑한 개인화 ⭐

현재는:

Search + View + Bookmark + 이미 본 식물 제외/감점

정도의 rule-based recommendation.

나중에는 사용자가 충분히 쌓이면:

어떤 식물을 오래 봤는지
어떤 식물을 반복해서 Detail까지 들어갔는지
어떤 식물을 Bookmark했는지
어떤 Category를 많이 탐색하는지
어떤 식물에서 More를 많이 눌렀는지

등을 조합할 수 있어.

더 나중에는 embeddings / semantic similarity 같은 것도 고려할 수 있고.

하지만 처음부터 ML recommendation을 만들지는 않음.

2. Seasonal / Local Explore

나중에:

🌸 What's blooming now?

또는

🌱 Plants for Vancouver this season

같은 discovery를 넣을 수 있어.

사용자의 지역과 계절을 이용해서:

봄에 볼 만한 꽃
현재 재배하기 좋은 식물
Vancouver에서 흔히 볼 수 있는 식물

등을 Explore에 섞을 수 있어.

다만 이런 기능은 정확한 지역/기후 데이터와 신뢰할 수 있는 출처가 필요하므로 나중에.

3. "Recently Viewed"

현재는 View History를 추천 시스템 내부 데이터로만 사용.

나중에 사용자가:

"아까 봤던 그 꽃 어디 있었지?"

라고 할 수 있으므로 Favorites 안에:

Recently Viewed

를 추가하는 것도 가능.

4. Favorites → Folder / Collection

현재:

Bookmark → Favorites

나중에는:

Favorites
├── All
├── Wishlist
├── Roses
├── My Garden
├── To Buy
└── + New Folder

처럼 발전 가능.

사용자가 식물을 저장하는 이유도:

사고 싶어서
키워보고 싶어서
그냥 마음에 들어서
나중에 다시 보고 싶어서

다 다를 수 있으니까.

5. Explore에서 "For You"

현재는 그냥 Explore.

나중에 개인화가 충분히 발전하면:

For You

같은 별도의 영역을 만들 수도 있어.

예:

Explore

Categories
[Flowers] [Trees] [Fruits] ...

For You
────────────────
개인화된 새로운 식물들

Discover
────────────────
다양한 새로운 식물들

다만 지금은 굳이 섹션을 나누지 않고 하나의 자연스러운 feed로 시작하는 게 좋아.

6. Search의 자연어 검색

현재:

rose

→ DB keyword matching.

나중에는:

"빨간색이고 꽃이 크면서 향이 강한 장미"

"집 안에서 키우기 쉬운 큰 잎 식물"

같은 검색을 지원할 수 있어.

처음에는 keyword/attribute extraction으로 시작하고, 필요하면 semantic search로 발전.

7. AI Identification 고도화

현재 핵심:

사진 → AI → 후보 → 우리 DB matching

나중에는:

cultivar-level identification 개선
여러 사진 입력
꽃/잎/줄기 등 추가 사진 요청
식물의 특정 부위를 자동 분석
식별할 수 없는 경우 명확하게 안내

등을 추가할 수 있어.

특히 **"모르면 모른다고 말하는 identification"**이 장기적으로 중요한 품질 원칙이 될 것 같아.

8. Where to Buy 고도화

현재 Vancouver/BC 중심으로 시작.

나중에는:

위치 기반 검색
거리
평점
온라인 구매
가격
retailer availability
가격 변동
affiliate/referral

등으로 발전 가능.

단, 실시간 재고가 없는 경우에는 있는 것처럼 표현하지 않는 것을 원칙으로.

9. Care 정보의 신뢰도 시스템

이건 개인적으로 장기적으로 꽤 중요한 기능이라고 봐.

예:

Watering
Source-backed

Temperature
Source-backed

AI identification
AI-generated candidate

처럼 정보의 성격을 구분할 수 있어.

나중에는 각 정보 옆에:

Source
Updated
Verified

등을 보여줄 수도 있어.

Plant Encyclopedia의 핵심 가치 중 하나가 "그럴듯한 AI 답변"이 아니라 믿을 수 있는 식물 정보가 되는 것이기 때문이야.

10. 이미지 품질 / 성능 개선

이미지 중심 앱이니까 나중에 상당히 중요해.

WebP / AVIF
thumbnail
responsive image
lazy loading
CDN
image caching
적절한 image size
placeholder / blur loading
prefetching

등.

특히 네가 원하는 parallax Detail page + Pinterest-style masonry + infinite scroll은 잘못 구현하면 모바일에서 무거워질 수 있어.

그래서 나중에는 "예쁜 UI"와 "성능"을 함께 테스트해야 해.







/////////
그리고 지금부터 특히 중요하게 생각할 제품 원칙

나는 이 프로젝트에서 아래 네 가지를 PROJECT_REQUIREMENTS나 DECISIONS에 명시적으로 기록해두는 걸 추천해.

1. Discovery over repetition
사용자의 관심사를 반영하되 이미 본 콘텐츠의 반복 노출을 최소화한다.

2. Personalization without losing exploration
개인화는 Explore를 사용자의 검색 결과 페이지로 만들기 위한 것이 아니라 새로운 식물을 발견하도록 돕기 위한 것이다.

3. Search history is not activity history
사용자가 검색한 것과 사용자가 본/저장한 것은 서로 다른 데이터다.

4. Trust over AI fluency
AI가 그럴듯하게 답하는 것보다 출처가 확인된 식물 정보를 우선한다.

이 네 가지가 나중에 Cursor가 코드를 만들 때도 꽤 중요한 제품의 기준점이 될 거야.

/////////////
현재 구현하지 않고 "Future"로 남겨둘 것

정리하면:

Later / Optional

고급 recommendation / ML personalization
Seasonal / local recommendations
Recently Viewed UI
Favorites folders / collections
For You 별도 섹션
자연어 / semantic search
고급 AI plant identification
다중 사진 identification
Care 정보 provenance UI
Vancouver 외 지역 확대
실시간 retailer inventory
가격/affiliate 기능
이미지 CDN 및 고급 최적화
Android 전용 UX 개선
알림 / plant reminders
My Plants / gardening journal
향후 business model

///////
이 프로젝트의 목표를 단순히 "AI로 코딩해서 앱 하나 만들기"로 잡지 않고, 실제 industry-level 개발 프로세스를 AI와 함께 연습하는 프로젝트로 잡자.

즉 앞으로 내가 Cursor에게 넘길 코드를 만들 때도 단순히:

"이 기능 구현해."

가 아니라,

요구사항 → 설계 → trade-off → 작은 구현 단위 → 테스트 → 코드 리뷰 → 보안/성능 검토 → CI → 배포 → 관찰 → 개선

의 흐름으로 가르칠게.

그리고 AI를 쓸 때도 AI가 대신 개발하는 것과 AI를 engineering tool로 사용하는 것을 구분해서 알려줄게. 특히 네가 junior SWE 취업을 준비하고 있으니까, 나중에 면접에서 "AI로 만들었습니다"가 아니라 **"AI를 활용하면서도 내가 설계·검증·테스트·디버깅을 통제했습니다"**라고 설명할 수 있게 만드는 게 중요해.
//////////////

나는 이 결정을 DECISIONS.md에 나중에 기록할 만한 좋은 제품 결정이라고 봐.

We intentionally excluded an extensive growing guide from the MVP to keep the encyclopedia focused on identification, essential care, and discovery. More detailed cultivation guidance may be added based on user needs.

////////

PROJECT_INSTRUCTIONS.md
ChatGPT / Cursor가 이 프로젝트에서 따라야 할 원칙
기술 스택
AI-assisted development workflow
코드 작성/리뷰 원칙
PRODUCT_REQUIREMENTS.md
Plant Encyclopedia가 해결하려는 문제
target user
핵심 user journey
기능 요구사항
Explore 60/20/20 등 제품 규칙

UX_SPECIFICATION.md
Sidebar
Explore
Category
Search
Identification
Plant Detail
Favorites
Where to Buy
Mobile UX
Loading/Empty/Error
Accessibility
Botanical interactions
ARCHITECTURE.md
Angular
ASP.NET Core
PostgreSQL
EF Core
AWS
S3
AI service
web/mobile architecture
data flow

DATABASE_DESIGN.md
Plant
Cultivar
Taxonomy
Images
Care information
Favorites
Search History
View History
Recommendation signals
Retailers
Provenance/source data
API_SPEC.md
REST endpoints
request/response
authentication
error format
pagination
image identification
search
favorites

DEVELOPMENT_ROADMAP.md
Phase 0 → foundation
Backend
Frontend
Database
AI
Testing
Docker
CI/CD
AWS
Android/MAUI

CURRENT_STATE.md
지금 실제로 구현된 것이 무엇인지
아직 아무것도 구현하지 않은 부분
현재 phase
다음 작업

DECISIONS.md
우리가 왜 이런 UX/architecture 결정을 했는지
예: “Why Category does not recommend plants outside its category”
60/20/20의 이유
AI는 care information의 authority가 아니라 identification에 사용한다는 원칙 등
TODO.md
아직 결정/구현해야 할 사항
우선순위
나중에 추가할 기능
/////////////
문서 작성 원칙

각 문서에는 필요하면 다음을 명확하게 구분할게.

Goals
Non-goals
Requirements
Constraints
User flows
Rules / invariants
Implementation notes
Open questions
//////////
문서 작성 순서

나는 이렇게 진행할게.

Phase 1 — Product & UX

PRODUCT_REQUIREMENTS.md
UX_SPECIFICATION.md
DECISIONS.md

↓

Phase 2 — Engineering

ARCHITECTURE.md
DATABASE_DESIGN.md
API_SPEC.md

↓

Phase 3 — Development Management

DEVELOPMENT_ROADMAP.md
PROJECT_INSTRUCTIONS.md
CURRENT_STATE.md
TODO.md

이 순서가 좋은 이유는 제품 → UX → 기술 → 구현 순서를 그대로 문서에도 반영하기 때문이야.
//////////
앞으로 새로운 UX 아이디어가 나올 때마다 무조건 기존 문서를 수정하는 방식으로 관리하자.

즉:

New idea
   ↓
Does it conflict with an existing decision?
   ↓
Yes → discuss + update DECISIONS.md
No  → update the appropriate spec
   ↓
Implementation
/////////
