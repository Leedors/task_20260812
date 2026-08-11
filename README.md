# Employee Directory API

직원 긴급 연락망 백엔드 API. csv/json 업로드로 직원 연락처를 등록하고, 목록(페이징)과 상세를 조회합니다.

- **Framework**: .NET 8 (C# 12), ASP.NET Core
- **구조**: Clean Architecture + CQRS
- **영속성**: EF Core 8 + SQLite (별도 설치 불필요)
- **문서**: Swagger UI 내장

---

## 1. 빠른 시작

### 요구 환경

- .NET SDK 8.0 이상 (9, 10 에서도 그대로 빌드됩니다)
- 그 외 DB 서버, Docker 등 **추가 설치가 필요한 것은 없습니다**

### 실행

```bash
dotnet run --project src/EmployeeDirectory.Api
```

실행하면 브라우저가 Swagger UI(`http://localhost:5080/swagger`)로 열립니다.
포트를 바꾸려면 `--urls` 를 쓰면 됩니다.

```bash
dotnet run --project src/EmployeeDirectory.Api --urls http://localhost:8080
```

### 처음부터 바로 확인 가능한 상태

- DB 스키마는 **기동 시 자동 생성**됩니다. 마이그레이션 명령이 필요 없습니다.
- DB 가 비어 있으면 `samples/` 의 csv·json 을 **자동으로 적재**합니다. 그래서 clone 직후
  `GET /api/employee` 를 호출하면 곧바로 6건이 조회됩니다.
- SQLite 파일(`employee-directory.db`)은 실행 디렉터리에 생성되며 `.gitignore` 에 포함되어 있습니다.

시드가 필요 없으면 `src/EmployeeDirectory.Api/appsettings.json` 에서 끕니다.

```json
{ "Seed": { "Enabled": false } }
```

### 테스트

```bash
dotnet test
```

---

## 2. API

Base URL: `/api/employee`

### 2.1 직원 목록 조회 (페이징)

```
GET /api/employee?page={page}&pageSize={pageSize}
```

| 파라미터 | 기본값 | 제약 |
| --- | --- | --- |
| `page` | 1 | 1 이상 |
| `pageSize` | 20 | 1 ~ 200 |

`200 OK`

```json
{
  "items": [
    { "id": 1, "name": "홍길동", "email": "gildong@example.com", "tel": "010-1234-5678", "joined": "2015-08-15" }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 6,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

전체 건수와 페이지 계산 결과를 함께 내려주므로 Front-end 는 **추가 호출 없이** 페이지네이션 UI를 그릴 수 있습니다.

### 2.2 직원 상세 조회

```
GET /api/employee/{name}
```

`200 OK`

```json
{ "id": 1, "name": "홍길동", "email": "gildong@example.com", "tel": "010-1234-5678", "joined": "2015-08-15" }
```

없으면 `404 Not Found` + `application/problem+json`.

### 2.3 직원 등록 (csv / json)

```
POST /api/employee
```

**요구된 4가지 입력을 모두 지원합니다.**

| # | 방식 | 요청 형태 |
| --- | --- | --- |
| 1 | csv 파일 업로드 | `multipart/form-data`, `file` 필드 (`<input type="file">`) |
| 2 | json 파일 업로드 | `multipart/form-data`, `file` 필드 |
| 3 | body 에 csv 직접 입력 | `Content-Type: text/csv`, 본문에 원시 텍스트 (`<textarea>`) |
| 4 | body 에 json 직접 입력 | `Content-Type: application/json`, 본문에 원시 텍스트 |

`Content-Type` 이 `text/plain` 이거나 없어도 **본문 내용으로 형식을 추론**합니다.
파일 대신 폼 필드(`content`, `data`, `text`, `payload`, `body`)로 텍스트를 보내도 동작합니다.

`201 Created`

```json
{ "format": "csv", "created": 2, "updated": 1, "totalProcessed": 3 }
```

실패 시 `400 Bad Request` + `application/problem+json`. **실패한 항목을 모두** 알려줍니다.

```json
{
  "title": "요청이 올바르지 않습니다.",
  "status": 400,
  "detail": "[2행] 이메일 형식이 올바르지 않습니다: '이메일아님'",
  "errors": [
    { "code": "employee.email_invalid", "message": "[2행] 이메일 형식이 올바르지 않습니다: '이메일아님'" },
    { "code": "employee.tel_invalid",   "message": "[3행] 전화번호 형식이 올바르지 않습니다: '123'" }
  ]
}
```

### 2.4 입력 데이터 형식

**csv** — 헤더 없이 `이름, 이메일, 전화번호, 입사일` 순서

```csv
홍길동, gildong@example.com, 01012345678, 2015.08.15
성춘향, chunhyang@example.com, 010-2222-3333, 2021.04.28
```

- 헤더 행이 있으면 **컬럼명으로 매핑**합니다 (`name/이름`, `email/이메일`, `tel/전화번호/연락처`, `joined/입사일` 등).
- 필수 4개 외의 **추가 컬럼은 무시**합니다.
- 따옴표로 감싼 필드와 `""` 이스케이프, 빈 줄, `#` 주석을 지원합니다.

**json** — 배열 또는 단일 객체

```json
[
  { "name": "홍길동", "email": "gildong@example.com", "tel": "010-1234-5678", "joined": "2015-08-15" }
]
```

- 프로퍼티 이름은 **대소문자를 구분하지 않고**, 별칭(`phone`, `joinDate` 등)도 허용합니다.
- 필수 4개 외의 프로퍼티는 무시합니다.

**공통 규칙**

| 항목 | 규칙 |
| --- | --- |
| 전화번호 | `01012345678`, `010-1234-5678`, `+82-10-1234-5678` 모두 허용. 저장은 숫자만, 응답은 하이픈 표기 |
| 입사일 | `yyyy-MM-dd`, `yyyy.MM.dd`, `yyyy/MM/dd`, `yyyyMMdd` 및 ISO 8601. **미래 날짜 불가** |
| 이메일 | 소문자로 정규화. **직원을 식별하는 자연 키** |

### 2.5 curl 예시

```bash
# 1) csv 파일 업로드
curl -F "file=@samples/employees.csv;type=text/csv" http://localhost:5080/api/employee

# 2) json 파일 업로드
curl -F "file=@samples/employees.json;type=application/json" http://localhost:5080/api/employee

# 3) body 에 csv 직접 입력
curl -X POST -H "Content-Type: text/csv" \
     --data-binary '홍길동, gildong@example.com, 01012345678, 2015.08.15' \
     http://localhost:5080/api/employee

# 4) body 에 json 직접 입력
curl -X POST -H "Content-Type: application/json" \
     --data-binary '[{"name":"홍길동","email":"gildong@example.com","tel":"010-1234-5678","joined":"2015-08-15"}]' \
     http://localhost:5080/api/employee

# 조회
curl "http://localhost:5080/api/employee?page=1&pageSize=20"
curl "http://localhost:5080/api/employee/홍길동"
```

`docs/api-examples.http` 파일로 IDE(Visual Studio / Rider / VS Code REST Client)에서 바로 실행할 수도 있습니다.

---

## 3. 요구사항 체크리스트

### 필수

| 요구사항 | 구현 |
| --- | --- |
| 직원의 기본 연락 정보를 알 수 있어야 함 | `GET /api/employee`, `GET /api/employee/{name}` |
| csv 파일 업로드시 작동 | `POST /api/employee` (multipart) |
| json 파일 업로드시 작동 | `POST /api/employee` (multipart) |
| body 에 csv 직접 입력시 작동 | `POST /api/employee` (`text/csv`) |
| body 에 json 직접 입력시 작동 | `POST /api/employee` (`application/json`) |
| `GET /api/employee?page=&pageSize=` → 200, 전체 데이터 + 페이징 | `PagedResult` 로 항목 + `totalCount`/`totalPages` 반환 |
| `GET /api/employee/{name}` → 200, 상세 반환 | 이름 일치 시 상세, 없으면 404 |
| `POST /api/employee` → 201 | 신규/갱신 건수 요약 반환 |
| .NET 8 이상 (C#) | net8.0 / C# 12 |
| CQRS 패턴 | Command/Query 분리 + 전용 디스패처 + 파이프라인 |
| 성공·실패 케이스 테스트 코드 | 단위 119개 + 통합 20개 |

### Optional

| 요구사항 | 구현 |
| --- | --- |
| 로그 기능 | 요청 로깅 미들웨어(상관관계 ID·상태코드·소요시간) + CQRS `LoggingBehavior` |
| OpenAPI 로 API spec 노출 | Swagger UI + XML 주석 + POST 본문 4가지 방식 문서화 |
| 설계 변경 반영이 쉬운 코드 | 계층 간 의존성 역전, 파서 플러그인 구조, 파이프라인 behavior, 읽기/쓰기 모델 분리 |

---

## 4. 아키텍처

```
src/
├── EmployeeDirectory.Domain          비즈니스 규칙 (외부 의존성 0)
│   ├── Common/                       Result, Error
│   └── Employees/                    Employee, EmailAddress, PhoneNumber, IEmployeeRepository
├── EmployeeDirectory.Application     유스케이스 (CQRS)
│   ├── Abstractions/                 메시징·검증·영속성·파싱·시간 추상화
│   ├── Behaviors/                    LoggingBehavior, ValidationBehavior
│   └── Employees/                    Commands / Queries / Dtos
├── EmployeeDirectory.Infrastructure  바깥 세계 구현
│   ├── Parsing/                      CsvEmployeeParser, JsonEmployeeParser, Resolver
│   ├── Persistence/                  DbContext, Repository, ReadStore, DatabaseInitializer
│   └── Time/                         SystemDateTimeProvider
└── EmployeeDirectory.Api             HTTP 경계
    ├── Controllers/                  EmployeeController
    ├── Common/                       ProblemDetails 변환, 전역 예외 처리
    ├── Middleware/                   요청 로깅
    └── Swagger/                      POST 본문 스키마 문서화

tests/
├── EmployeeDirectory.UnitTests        도메인·파서·핸들러·디스패처
└── EmployeeDirectory.IntegrationTests 실제 HTTP 요청 기반 종단 테스트
```

의존성 방향은 항상 **바깥 → 안쪽**입니다. Domain 은 아무것도 참조하지 않고, Application 은 인터페이스만 알며,
구현체는 Infrastructure 가 제공합니다. 덕분에 SQLite 를 다른 DB 로 바꾸거나 파서를 추가해도
도메인/유스케이스 코드는 그대로입니다.

### 요청 흐름

```
HTTP 요청
  → EmployeeController         (전송 방식 흡수: 파일/본문 → EmployeePayload)
  → ICommandDispatcher         (요청 타입에 맞는 핸들러 탐색)
  → LoggingBehavior            (시작·종료·소요시간)
  → ValidationBehavior         (입력 계약 검증)
  → RegisterEmployeesCommandHandler
       → IEmployeeSourceParserResolver → Csv/Json Parser   (형식 판별·파싱)
       → Employee.Create                                   (도메인 규칙 검증)
       → IEmployeeRepository + IUnitOfWork                 (단일 트랜잭션 저장)
  → Result → ProblemDetails 또는 200/201
```

---

## 5. 설계 결정과 근거

### 5.1 CQRS 를 라이브러리 없이 직접 구현했습니다

`ICommand`/`IQuery`, 각각의 핸들러, 그리고 이를 연결하는 디스패처와 파이프라인을 직접 만들었습니다
(`Application/Abstractions/Messaging`).

- 필요한 기능이 "요청 → 핸들러 + 파이프라인"뿐이라 코드가 200줄이면 충분했습니다.
- 외부 라이브러리의 라이선스·버전 정책 변화에 과제가 묶이지 않습니다.
- 무엇보다 **동작 원리를 코드로 직접 설명할 수 있습니다.** 라이브러리를 쓰면 "왜 이렇게 동작하는가"가
  블랙박스로 남습니다.

리플렉션은 요청 타입당 한 번만 수행하고 래퍼 인스턴스를 캐시하므로 호출마다 비용이 들지 않습니다.

### 5.2 읽기와 쓰기 모델을 실제로 분리했습니다

CQRS 를 "폴더만 나누는 것"으로 끝내지 않았습니다.

- **쓰기**: `IEmployeeRepository` → 애그리게이트(`Employee`)를 통해서만 상태를 바꿉니다.
- **읽기**: `IEmployeeReadStore` → 애그리게이트를 거치지 않고 EF 투영으로 DTO 를 바로 만듭니다.

조회에 불필요한 변경 추적이 없고, 나중에 조회 전용 저장소(캐시·검색엔진 등)로 교체할 때
쓰기 모델을 건드릴 필요가 없습니다.

### 5.3 예상 가능한 실패는 예외가 아니라 `Result` 로 표현합니다

업로드 한 번에 **여러 건의 검증 실패**를 모아서 돌려줘야 합니다. 예외는 첫 실패에서 멈추기 때문에
이 요구를 만족시킬 수 없습니다. 또한 실패 가능성이 메서드 시그니처에 드러나 호출자가 처리를 강제받습니다.
예외는 "버그"에만 사용하고, 전역 예외 핸들러가 500 으로 변환하면서 내부 정보를 노출하지 않습니다.

### 5.4 이메일·전화번호는 값 객체로, 매핑은 Owned Type 으로

`string email` 대신 `EmailAddress` 를 쓰면 "검증되지 않은 이메일"이라는 상태가 도메인에 들어올 수 없습니다.
전화번호는 표기가 제각각(`01075312468`, `010-1111-2424`)이라 **정규화**가 특히 중요합니다.

EF 매핑에는 `ValueConverter` 대신 **Owned Type** 을 썼습니다. 컨버터로 매핑하면 `e.Email.Value` 같은
표현이 SQL 로 번역되지 않아, 업로드 시 필요한 "이메일 IN 절" 조회를 만들 수 없기 때문입니다.
Owned Type 은 소유자 테이블의 컬럼으로 펼쳐지므로 스키마는 단일 테이블 그대로입니다.

### 5.5 업로드는 "전부 성공 아니면 전부 실패"

한 건이라도 유효하지 않으면 **아무것도 저장하지 않고** 실패 항목을 모두 반환합니다.
긴급 연락망이 "절반만 반영된" 상태로 남는 것이, 사용자에게 다시 올리게 하는 것보다 훨씬 위험합니다.

### 5.6 같은 이메일은 갱신(upsert)합니다

연락처 파일을 다시 올리는 것은 실무에서 가장 흔한 갱신 시나리오입니다. 409 로 막기보다
최신 값으로 덮어쓰는 편이 자연스럽습니다. 응답에 `created`/`updated` 를 나눠 담아
호출자가 무슨 일이 일어났는지 알 수 있게 했습니다.
단, **같은 요청 안에서의 이메일 중복은 오류**입니다. 사용자의 실수일 가능성이 높기 때문입니다.

### 5.7 엔드포인트는 하나, 입력 방식은 4가지

요구된 4가지를 `2 × 2`(형식 × 전송수단)로 분해했습니다.

- "어떻게 도착했는가"(파일/본문)는 **Api 계층이 흡수**해 `EmployeePayload` 하나로 정규화합니다.
- "무슨 형식인가"는 **파서 전략**이 판단합니다. 선언된 형식(확장자·Content-Type)이 있으면 그것을 신뢰하고,
  없으면 내용으로 추론합니다(`<textarea>` 입력에는 형식 정보가 없을 수 있기 때문).

새 형식(xml, xlsx …)을 추가하려면 `IEmployeeSourceParser` 구현 하나를 만들어 DI 에 등록하면 되고,
컨트롤러·핸들러·리졸버는 **수정할 필요가 없습니다**(OCP).

### 5.8 영속성: EF Core + SQLite, `EnsureCreated`

과제 조건이 "clone 후 빌드 성공 및 결과 확인 가능"이므로 **설치·설정이 필요 없는 것**이 최우선이었습니다.
SQLite 는 파일 하나로 동작하면서도 실제 관계형 DB 라 인덱스·유니크 제약·트랜잭션을 그대로 검증할 수 있습니다.

마이그레이션 대신 `EnsureCreated` 를 쓴 것도 같은 이유입니다(도구 설치·추가 명령 불필요).
스키마 이력 관리가 필요한 운영 환경에서는 `Migrate()` 로 바꾸면 되고, **교체 지점은
`DatabaseInitializer` 한 곳으로 고립**되어 있습니다.

### 5.9 시드도 실제 API 경로를 그대로 사용합니다

초기 데이터 적재는 별도 코드가 아니라 `RegisterEmployeesCommand` 를 그대로 호출합니다.
시드 경로에서만 통하는 "특별한 검증"이 생기지 않게 하기 위해서입니다.

---

## 6. 해석이 갈릴 수 있는 부분 (명시적 가정)

과제 안내에 따라, 여러 해석이 가능한 지점은 아래와 같이 정하고 그 이유를 남깁니다.

| 쟁점 | 선택 | 이유 |
| --- | --- | --- |
| `GET /{name}` 에서 **동명이인** | 단건(가장 먼저 등록된 사람) 반환 | 명세가 "직원의 상세 연락정보"로 단수를 지칭합니다. 목록 형태로 바꾸면 응답 스키마가 흔들려 Front-end 계약이 불안정해집니다. 대신 저장은 동명이인을 허용하고 이메일로 식별합니다 |
| 직원 **식별자** | 이메일 | 이름은 중복될 수 있고 전화번호는 바뀝니다. 예제 데이터에서 유일성이 보장되는 값은 이메일뿐입니다 |
| `pageSize` **상한** | 200 | 상한이 없으면 한 번의 호출로 전체 테이블을 끌어올 수 있습니다. 기본값 20 |
| 범위를 벗어난 `page` | 빈 배열 + 200 | "데이터가 없음"은 오류가 아닙니다. `totalCount` 로 클라이언트가 보정할 수 있습니다 |
| 잘못된 `page`/`pageSize` | 400 | 0 이나 음수는 클라이언트 버그이므로 조용히 보정하지 않고 알려줍니다 |
| csv **헤더** 유무 | 자동 판별 | 필수 4개 컬럼명을 모두 찾을 수 있을 때만 헤더로 인정합니다. 일부만 맞으면 데이터 행일 가능성이 높아 위치 기반으로 처리합니다 |
| 이름 조회 매칭 | 공백 제거 후 정확히 일치 | 부분 일치는 "상세 조회"의 의미와 맞지 않고, 의도치 않은 다중 매칭을 만듭니다 |
| 오류 응답 개수 | 최대 50건 + 요약 | 대량 파일이 전부 잘못됐을 때 응답이 무한정 커지는 것을 막습니다 |
| 입사일 `07/03/2018` | 거부 | 일/월 순서가 모호합니다. 서버 로캘에 따라 다르게 해석되어 **조용히 틀린 날짜**가 저장되는 것이 가장 나쁜 결과입니다 |

---

## 7. 테스트

```bash
dotnet test                                    # 전체
dotnet test tests/EmployeeDirectory.UnitTests  # 단위만
```

### 단위 테스트 (119개)

| 대상 | 검증 내용 |
| --- | --- |
| `EmailAddress`, `PhoneNumber` | 정규화, 표시 형식, 동등성, 실패 케이스 |
| `Employee` | 생성 규칙, 미래 입사일 거부, **여러 오류 동시 수집**, 갱신 시 이메일 불변 |
| `JoinedDate` | 허용 포맷 전부, 모호한 표기 거부 |
| `CsvEmployeeParser` | 예제 데이터, 헤더/한글 헤더, 추가 컬럼, 따옴표·이스케이프, BOM·CRLF, 빈 줄·주석, **행 번호 보존**, 컬럼 부족 |
| `JsonEmployeeParser` | 배열·단일 객체, 대소문자 무시, 별칭, 숫자 tel, 깨진 json, 잘못된 요소 |
| `EmployeeSourceParserResolver` | 선언 형식 우선, 내용 추론, 미등록 형식 |
| `RegisterEmployeesCommandHandler` | 신규/갱신 집계, **부분 저장 금지**, 오류 일괄 수집, 요청 내 중복 이메일 |
| 조회 핸들러·검증기 | 페이징 계산, NotFound, 파라미터 범위 |
| `Dispatcher` | 핸들러 라우팅, **behavior 실행 순서**, 캐시 경로 |

### 통합 테스트 (20개)

실제 HTTP 요청으로 라우팅 → 컨트롤러 → CQRS → EF Core/SQLite 전 구간을 검증합니다.
DI 구성을 테스트용으로 뜯어고치지 않고 **설정만 덮어써서** 프로덕션과 동일한 경로를 지나가게 했습니다.
따라서 DI 구성 자체의 실수도 테스트가 잡아냅니다.

- 필수 4가지 입력 방식 각각 201 + 조회까지 확인
- Content-Type 이 `text/plain` 일 때 내용 추론
- 재업로드 시 갱신 동작
- 잘못된 행이 있을 때 400 + **정상 행도 저장되지 않음**
- 페이징 계산, 기본값, 범위 초과, 잘못된 파라미터 400
- 없는 이름 404 + `application/problem+json`
- 응답의 상관관계 ID 헤더

---

## 8. 로깅

- **요청 단위**: `X-Correlation-Id`(없으면 발급) 를 로그 스코프에 넣고 응답 헤더로 돌려줍니다.
  메서드·경로·상태코드·소요시간을 남기며, 4xx 는 Warning, 5xx 는 Error 로 레벨을 올립니다.
- **유스케이스 단위**: `LoggingBehavior` 가 모든 커맨드/쿼리의 시작·성공·실패·소요시간을 남깁니다.
  핸들러 본문에는 로깅 코드가 없습니다.
- **도메인 이벤트성 로그**: 등록 완료 시 형식·신규·갱신 건수·원본 이름을 구조적 로그로 남깁니다.

로그 레벨은 `appsettings.json` 에서 조정합니다. EF Core 가 실행하는 SQL 을 보려면
`Microsoft.EntityFrameworkCore.Database.Command` 를 `Information` 으로 낮추면 됩니다.

---

## 9. 설정

| 키 | 기본값 | 설명 |
| --- | --- | --- |
| `ConnectionStrings:Default` | `Data Source=employee-directory.db` | SQLite 파일 경로 |
| `Seed:Enabled` | `true` | DB 가 비어 있을 때 샘플 적재 여부 |
| `Seed:Directory` | `samples` | 시드 파일 디렉터리(상대 경로면 실행 파일 기준) |

환경 변수로도 덮어쓸 수 있습니다.

```bash
ConnectionStrings__Default="Data Source=/var/data/directory.db" Seed__Enabled=false \
  dotnet run --project src/EmployeeDirectory.Api
```

---

## 10. 실행 환경 관련 알려진 제약

프로젝트는 `net8.0` 을 타겟으로 하되, **.NET 8 런타임이 없고 9/10 만 설치된 환경**에서도 실행되도록
`RollForward=LatestMajor` 를 설정했습니다.

이 조합에서 한 가지 알려진 제약이 있습니다. `Microsoft.AspNetCore.Mvc.Testing` 8.x 의 테스트 서버는
.NET 9 부터 바뀐 `PipeWriter.UnflushedBytes` 규약을 구현하지 않아, **.NET 8 런타임이 없는 환경에서는
통합 테스트 20개가 실패**합니다(단위 테스트 119개와 애플리케이션 실행에는 영향이 없습니다).

- .NET 8 런타임이 설치된 환경에서는 통합 테스트도 정상 통과합니다.
- 테스트 패키지를 9.x 로 올리려면 타겟을 `net9.0` 으로 올려야 하는데, 그러면 .NET 8 SDK 만 있는 환경에서
  **빌드 자체가 실패**합니다. 과제의 "clone 후 빌드 성공" 조건을 우선해 `net8.0` 을 유지했습니다.

---

## 11. 확장 시나리오

| 하고 싶은 것 | 바꿔야 하는 곳 |
| --- | --- |
| 새 입력 형식(xml, xlsx) 지원 | `IEmployeeSourceParser` 구현 1개 추가 + DI 등록 |
| DB 를 PostgreSQL 등으로 교체 | `AddInfrastructure` 의 `UseSqlite` 한 줄 |
| 마이그레이션 기반 스키마 관리 | `DatabaseInitializer` 의 `EnsureCreated` → `Migrate` |
| 새 유스케이스 추가 | Command/Query + Handler 파일 추가 (DI 는 자동 스캔) |
| 모든 요청에 공통 동작 추가 | `IPipelineBehavior` 구현 1개 추가 |
| 조회 성능 최적화(캐시 등) | `IEmployeeReadStore` 구현 교체 (쓰기 모델 무영향) |
