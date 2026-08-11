# Employee Directory API

직원 긴급 연락망 백엔드 API. csv/json 업로드로 직원 연락처를 등록하고, 목록(페이징·검색)·상세 조회와 개별 정정을 제공합니다.

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

### 2.1 직원 목록 조회 (페이징 + 검색)

```
GET /api/employee?page={page}&pageSize={pageSize}&q={keyword}
```

| 파라미터 | 기본값 | 설명 |
| --- | --- | --- |
| `page` | 1 | 1 이상 |
| `pageSize` | 20 | 1 ~ 200 |
| `q` | (없음) | 이름·이메일·전화번호 **부분 일치** 검색어. 최대 100자 |

`200 OK`

```json
{
  "items": [
    {
      "id": 1,
      "name": "홍길동",
      "email": "gildong@example.com",
      "tel": "010-1234-5678",
      "joined": "2015-08-15",
      "createdAt": "2026-08-11T05:12:33.1210000+00:00",
      "updatedAt": "2026-08-11T05:12:33.1210000+00:00"
    }
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

검색은 다음을 지원합니다.

- 이름 부분 일치 (`?q=홍길`)
- 이메일 부분 일치, **대소문자 무시** (`?q=GILDONG`)
- 전화번호 부분 일치, **하이픈 유무 무관** (`?q=010-1234` → `01012345678` 매칭)
- `%`, `_` 는 와일드카드가 아니라 **글자로 취급**합니다

### 2.2 직원 상세 조회

```
GET /api/employee/{name}
```

`200 OK` — 항목 하나. 없으면 `404 Not Found` + `application/problem+json`.

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
{ "format": "csv", "created": 2, "updated": 1, "restored": 0, "totalProcessed": 3 }
```

`created + updated + restored = totalProcessed` 가 항상 성립합니다.
`restored` 는 연락망에서 제외됐던 사람이 되살아난 건수입니다(§2.5 참고).

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

### 2.4 직원 수정 (단건)

```
PUT /api/employee/{id}
Content-Type: application/json

{ "name": "홍길동", "email": "gildong@example.com", "tel": "010-9999-8888", "joined": "2015-08-15" }
```

PUT 시맨틱이라 **모든 필드를 보냅니다.** `200 OK` 로 수정된 항목을 반환합니다.

| 상태 | 상황 |
| --- | --- |
| `400` | 입력값 검증 실패 (실패 항목 전체 반환) |
| `404` | 해당 직원이 없거나 이미 제외됨 |
| `409` | 이메일을 **다른 직원이 사용 중** |

### 2.5 직원 제외 (단건, soft delete)

```
DELETE /api/employee/{id}
```

`204 No Content`. 없거나 이미 제외됐으면 `404`.

**물리 삭제가 아닙니다.** 행은 남고 제외 표시(`deletedAt`)만 붙습니다. 조회에서는 즉시 사라집니다.
같은 이메일로 다시 업로드하면 **복구**되며, 그 건수는 `restored` 로 따로 집계됩니다.

### 2.6 헬스체크

```
GET /health
```

프로세스 생존뿐 아니라 **저장소 연결까지** 확인합니다.

```json
{
  "status": "Healthy",
  "totalDurationMs": 1.24,
  "checks": [
    { "name": "database", "status": "Healthy", "description": "데이터베이스에 연결할 수 있습니다.", "durationMs": 1.1 }
  ]
}
```

운영 확인용 경로라 Swagger 문서에는 노출하지 않습니다.

### 2.7 입력 데이터 형식

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

### 2.8 curl 예시

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

# 조회 / 검색
curl "http://localhost:5080/api/employee?page=1&pageSize=20"
curl "http://localhost:5080/api/employee?q=010-1234"
curl "http://localhost:5080/api/employee/홍길동"

# 수정 / 제외
curl -X PUT -H "Content-Type: application/json" \
     -d '{"name":"홍길동","email":"gildong@example.com","tel":"010-9999-8888","joined":"2015-08-15"}' \
     http://localhost:5080/api/employee/1
curl -X DELETE http://localhost:5080/api/employee/1

# 헬스체크
curl http://localhost:5080/health
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
| `POST /api/employee` → 201 | 신규/갱신/복구 건수 요약 반환 |
| .NET 8 이상 (C#) | net8.0 / C# 12 |
| CQRS 패턴 | Command/Query 분리 + 전용 디스패처 + 파이프라인 |
| 성공·실패 케이스 테스트 코드 | 단위 153개 + 통합 39개 |

### Optional

| 요구사항 | 구현 |
| --- | --- |
| 로그 기능 | 요청 로깅 미들웨어(상관관계 ID·상태코드·소요시간) + CQRS `LoggingBehavior` |
| OpenAPI 로 API spec 노출 | Swagger UI + XML 주석 + POST 본문 4가지 방식 문서화 |
| 설계 변경 반영이 쉬운 코드 | 계층 간 의존성 역전, 파서 플러그인 구조, 파이프라인 behavior, 읽기/쓰기 모델 분리 |

### 필수 요구를 넘어 추가한 것

문제 정의가 *"주어진 정보를 **빠르게 확인**할 수 있도록"* 이고 *"실제 서비스로 배포한다는 생각으로"* 였기에,
연락망으로서 최소한 갖춰야 한다고 판단한 범위까지 넣었습니다.

| 추가 | 이유 |
| --- | --- |
| 이름·이메일·전화번호 **검색** | 긴급 상황에서 정확한 이름을 모른 채 찾는 경우가 대부분입니다 |
| **등록·수정 시각** 노출 | 연락처는 시간이 지나면 저절로 틀려집니다. "이 정보가 얼마나 오래됐나"가 번호만큼 중요합니다 |
| **단건 수정** API | 전화번호 한 자리 고치려고 파일 전체를 다시 올리는 것은 실제 운용에서 부담입니다 |
| **제외(soft delete)** API | 퇴사자가 연락망에 남아 있으면 안 되지만, 물리 삭제는 오삭제 복구와 감사 추적을 막습니다 |
| **헬스체크** | 긴급 연락망은 정작 비상시에 살아 있어야 하는 시스템입니다 |

더 필요하다고 판단했으나 **의도적으로 제외한 것**은 §7 에 근거와 함께 정리했습니다.

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
    ├── Diagnostics/                  헬스체크
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

무엇을 골랐는지보다 **무엇을 포기했는지**를 같이 적었습니다.
3개월 뒤에 다시 열어봐도 "왜 이렇게 돼 있지"에 답할 수 있어야 한다고 생각했습니다.

### 5.1 CQRS 디스패처를 직접 만들었습니다

MediatR을 쓰지 않았습니다. 필요한 게 "요청 → 핸들러 → 파이프라인"뿐이라 40줄로 충분했고,
v13부터 상용 라이선스로 바뀐 것도 확인했습니다. FluentAssertions를 7.x에 고정한 것과 같은 기준입니다.

리플렉션은 쓰지 않았습니다. 처음에는 `SendAsync(ICommand<TResponse>)` 형태로 받았는데,
구체 타입을 런타임에 알아내야 해서 `Activator.CreateInstance`와 타입 캐시, 래퍼 클래스가 딸려왔습니다.
호출부가 타입을 명시하는 방식으로 바꾸니 전부 사라졌습니다.

대신 호출부가 길어집니다.

```csharp
await queryDispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(query, ct);
```

짧은 호출과 컴파일 시점에 드러나는 것 중에 후자를 골랐습니다.

### 5.2 읽기와 쓰기를 실제로 분리했습니다

폴더만 나누면 CQRS라고 하기 어렵다고 봤습니다. 저장소를 두 개로 나눴습니다.

- 쓰기 `IEmployeeRepository` — 애그리게이트를 통해서만 상태를 바꿉니다. 변경 추적이 필요합니다.
- 읽기 `IEmployeeReadStore` — `AsNoTracking` 으로 필요한 컬럼만 투영합니다.

같은 테이블을 보지만 두 경로의 코드가 만나지 않습니다. 나중에 조회에 캐시를 붙여도 쓰기 쪽은 건드릴 일이 없습니다.

솔직히 엔드포인트 5개 규모에 이 구조는 과합니다. CQRS가 필수 요구사항이라 적용했고,
이왕 하는 김에 형식만 갖추지는 말자고 생각해서 여기까지 왔습니다.
규모가 더 작았다면 디스패처는 빼고 컨트롤러가 핸들러를 직접 주입받게 했을 것 같습니다.

### 5.3 실패는 예외가 아니라 `Result` 로 돌려줍니다

csv 100줄 중 3줄이 틀렸으면 3줄을 한 번에 알려줘야 합니다. 예외는 첫 실패에서 멈추니 이 요구를 못 맞춥니다.

`Result<T>` 는 실패 가능성이 시그니처에 드러나서 호출자가 처리를 건너뛸 수 없다는 점도 좋았습니다.

예외는 버그에만 씁니다. 여기까지 올라온 예외는 전역 핸들러가 500으로 바꾸고 내부 메시지는 감춥니다.

### 5.4 이메일·전화번호는 값 객체로, 매핑은 Owned Type 으로

`string` 으로 두면 "검증 안 된 이메일"이 도메인 안으로 들어옵니다. 생성자를 닫고 `Create()` 만 열어뒀습니다.

전화번호는 특히 필요했습니다. csv는 `01075312468`, json은 `010-1111-2424` 로 들어옵니다.
정규화하는 곳이 한 군데여야 표기가 달라도 같은 번호로 비교됩니다.

EF 매핑은 처음에 `ValueConverter` 로 했다가 바꿨습니다. 컨버터를 쓰면 `e.Email.Value` 가 SQL로 번역되지 않아서,
업로드에 필요한 `IN` 절과 검색의 `LIKE` 절을 만들 수 없었습니다.
Owned Type은 소유자 테이블 컬럼으로 펼쳐지니 조건절에 그대로 씁니다. 테이블은 하나 그대로입니다.

### 5.5 업로드는 전부 성공 아니면 전부 실패입니다

한 줄이라도 유효하지 않으면 아무것도 저장하지 않고 실패한 항목을 전부 돌려줍니다.

연락망이 절반만 반영된 채로 남는 게, 사용자에게 다시 올리라고 하는 것보다 위험하다고 봤습니다.
"몇 건이 들어갔지"를 사용자가 추적하게 만들고 싶지 않았습니다.

수만 건짜리 배치라면 부분 성공에 실패 리포트를 주는 쪽이 맞을 겁니다. 여기서는 수십~수백 건을 가정했습니다.

### 5.6 같은 이메일은 갱신합니다

연락처 파일을 다시 올리는 건 흔한 갱신 방식이라, 409로 막지 않고 최신 값으로 덮어씁니다.

대신 `created` / `updated` / `restored` 를 나눠서 돌려줍니다. 무슨 일이 일어났는지 감추지 않으려고요.
셋을 더하면 항상 `totalProcessed` 가 됩니다.

같은 요청 안에서 이메일이 중복되는 건 오류로 처리합니다. 이건 사용자 실수일 가능성이 높습니다.

### 5.7 엔드포인트는 하나, 입력 방식은 네 가지

요구된 네 가지를 형식(csv/json) × 전송수단(파일/본문)으로 나눠서 봤습니다.

"어떻게 도착했는가"는 HTTP 문제라 컨트롤러가 흡수해서 `EmployeePayload` 하나로 만듭니다.
Application 아래로는 `IFormFile` 도 `HttpRequest` 도 없습니다.

"무슨 형식인가"는 파서가 판단합니다. 확장자나 Content-Type이 있으면 그걸 믿고, 없으면 내용을 봅니다.
`<textarea>` 입력에는 형식 정보가 없을 수 있기 때문입니다.

xlsx를 추가한다면 `IEmployeeSourceParser` 구현 하나를 만들어 DI에 등록하면 됩니다.
컨트롤러·핸들러·리졸버는 건드리지 않습니다.

### 5.8 삭제는 soft delete 입니다

`DELETE` 는 행을 지우지 않고 `deleted_at` 만 채웁니다.
연락망에서 사람이 빠진 건 기록해야 할 사건이고, 잘못 지웠을 때 되돌릴 수 있어야 합니다.

제외 조건을 쿼리마다 적으면 언젠가 빠뜨립니다. EF 전역 쿼리 필터로 모델 레벨에서 걸러내고,
삭제된 행까지 봐야 하는 두 곳에서만 `IgnoreQueryFilters()` 로 명시적으로 풉니다.

**여기에 아직 남은 위험이 있습니다.** 이메일 유니크 인덱스는 삭제된 행도 계속 점유합니다.
그래서 같은 이메일을 다시 올리면 신규 추가가 안 되고 복구 + 갱신이 됩니다.
재입사나 오삭제 복구에는 맞지만, 오래된 파일을 다시 올렸을 때 퇴사자가 조용히 살아나는 문제가 있습니다.
지금은 `restored` 카운트로 드러내는 선에서 멈췄습니다.
제대로 하려면 유니크 인덱스를 `WHERE deleted_at IS NULL` 부분 인덱스로 바꾸고,
복구를 `POST /{id}/restore` 같은 별도 API로 빼야 합니다.

### 5.9 감사 시각은 저장 직전에 한 번에 찍습니다

`created_at` / `updated_at` 은 도메인 메서드마다 시간을 넘기지 않고 `SaveChanges` 직전에 처리합니다.
메서드를 새로 만들 때 갱신을 빠뜨릴 여지를 없애려고 그렇게 했습니다.

여기에 함정이 하나 있었습니다. 값 객체를 Owned Type으로 매핑했기 때문에,
**전화번호만 바뀌면 소유자 엔트리는 `Unchanged` 로 남고 owned 엔트리만 `Modified`** 가 됩니다.
소유자 상태만 보면 이 변경을 놓칩니다. 참조 엔트리까지 확인하도록 했고 통합 테스트로 고정했습니다.

`deleted_at` 은 다르게 처리합니다. 기계적인 갱신이 아니라 도메인 행위의 결과라서 시각을 명시적으로 받습니다.

### 5.10 검색은 `LIKE` 를 쓰고 이스케이프합니다

`string.Contains` 는 SQLite에서 대소문자를 구분하는 `instr()` 로 번역됩니다.
이메일 검색은 대소문자를 안 가리는 게 자연스러워서 `EF.Functions.Like` 를 씁니다.

사용자가 입력한 `%` 와 `_` 는 반드시 이스케이프해야 합니다.
빠뜨리면 `%` 한 글자를 검색했을 때 전 직원이 나옵니다. 테스트로 고정해뒀습니다.

전화번호는 저장 값이 숫자열이라 검색어에서도 숫자만 뽑아 비교합니다.
`010-7531` 로 검색해도 `01075312468` 이 걸립니다.

### 5.11 EF Core + SQLite, 스키마는 `EnsureCreated`

"clone 후 빌드 성공, 결과 확인 가능"이 실격 조건이라 **설치나 추가 명령이 필요 없는 것**을 최우선으로 봤습니다.

SQLite는 파일 하나로 돌면서도 실제 관계형 DB라, 유니크 제약·인덱스·트랜잭션을 그대로 확인할 수 있습니다.

마이그레이션 대신 `EnsureCreated` 를 쓴 것도 같은 이유입니다.
운영이라면 `Migrate()` 가 맞고, 바꿀 자리는 `DatabaseInitializer` 한 곳입니다.

### 5.12 시드도 실제 API 경로를 그대로 씁니다

초기 데이터 적재는 별도 코드가 아니라 `RegisterEmployeesCommand` 를 그대로 호출합니다.
시드에서만 통하는 검증이 생기지 않게 하려고요.

### 5.13 로그에 개인정보를 남기지 않습니다

이 시스템이 다루는 값은 전부 개인정보입니다.
로그는 보존 기간이 길고 접근 통제가 응답보다 느슨해서, 한 번 남으면 지우기 어렵습니다.

세 군데를 막았습니다.

| 지점 | 문제 | 처리 |
| --- | --- | --- |
| 요청 경로 | `GET /api/employee/김철수` → 이름이 그대로 쌓임 | 라우트 템플릿 `/api/employee/{name}` 으로 기록 |
| 쿼리스트링 | `?q=김철수` → 검색어에도 개인정보 | 키만 기록 (`?page&pageSize&q`) |
| 오류 메시지 | 잘못된 이메일 원문이 들어 있음 | 로그에는 오류 코드만, 상세는 응답으로만 |

직원을 특정해야 하는 로그는 `EmailAddress.Masked`(`ch***@example.com`),
`PhoneNumber.Masked`(`010-****-2468`) 를 씁니다.

**네 번째는 조치한 뒤에 로그를 열어보고 찾았습니다.**
ASP.NET Core 기본 요청 로거가 원본 URL을 따로 찍고 있었습니다.

```
GET /api/employee/{name} → 404 (36.9ms)              ← 직접 만든 미들웨어
Request starting HTTP/1.1 GET .../api/employee/김철수  ← 프레임워크
```

운영 설정에서는 해당 카테고리가 `Warning` 이라 안 나오지만 개발 설정에서는 노출됐습니다.
`appsettings.Development.json` 에서 그 카테고리만 `Warning` 으로 낮췄습니다.
직접 만든 코드만 봐서는 못 잡는 종류였습니다.

## 6. 해석이 갈릴 수 있는 부분 (명시적 가정)

과제 안내에 따라, 여러 해석이 가능한 지점은 아래와 같이 정하고 그 이유를 남깁니다.

| 쟁점 | 선택 | 이유 |
| --- | --- | --- |
| `GET /{name}` 에서 **동명이인** | 단건(가장 먼저 등록된 사람) 반환 | 명세가 "직원의 상세 연락정보"로 단수를 지칭합니다. 목록 형태로 바꾸면 응답 스키마가 흔들려 Front-end 계약이 불안정해집니다. 대신 저장은 동명이인을 허용하고 이메일로 식별합니다 |
| 직원 **식별자** | 이메일(자연 키) / API 는 `id` | 이름은 중복될 수 있고 전화번호는 바뀝니다. 다만 수정·삭제 API 의 키로 이메일을 쓰면 URL 에 개인정보가 노출되므로 `id` 를 씁니다 |
| `pageSize` **상한** | 200 | 상한이 없으면 한 번의 호출로 전체 테이블을 끌어올 수 있습니다. 기본값 20 |
| 범위를 벗어난 `page` | 빈 배열 + 200 | "데이터가 없음"은 오류가 아닙니다. `totalCount` 로 클라이언트가 보정할 수 있습니다 |
| 잘못된 `page`/`pageSize` | 400 | 0 이나 음수는 클라이언트 버그이므로 조용히 보정하지 않고 알려줍니다 |
| **수정** 방식 | PUT (전체 교체) | PATCH 부분 수정은 "필드를 안 보낸 것"과 "null 로 지운 것"을 구분해야 해 계약이 복잡해집니다. 연락처는 필드가 4개뿐이라 전체 교체가 단순하고 안전합니다 |
| 수정 시 **이메일 충돌** | 409 | DB 유일 제약에 걸려 500 이 나기 전에 의미 있는 상태 코드로 알려줍니다 |
| **삭제된 직원** 재업로드 | 복구 후 갱신 | 이메일 유일 인덱스 때문에 신규 추가가 불가능하고, 재입사·오삭제 복구가 실제 시나리오입니다. 대신 `restored` 로 드러냅니다 |
| csv **헤더** 유무 | 자동 판별 | 필수 4개 컬럼명을 모두 찾을 수 있을 때만 헤더로 인정합니다. 일부만 맞으면 데이터 행일 가능성이 높아 위치 기반으로 처리합니다 |
| 이름 조회 매칭 | 공백 제거 후 정확히 일치 | 부분 일치는 "상세 조회"의 의미와 맞지 않습니다. 부분 검색은 목록의 `q` 로 분리했습니다 |
| 오류 응답 개수 | 최대 50건 + 요약 | 대량 파일이 전부 잘못됐을 때 응답이 무한정 커지는 것을 막습니다 |
| 입사일 `07/03/2018` | 거부 | 일/월 순서가 모호합니다. 서버 로캘에 따라 다르게 해석되어 **조용히 틀린 날짜**가 저장되는 것이 가장 나쁜 결과입니다 |

---

## 7. 제품 관점에서 다음에 해야 할 것

이 API 는 요구된 기능을 만족하지만, **"직원 명부"와 "긴급 연락망"은 요구가 다릅니다.**
아래는 실제 연락망으로 쓰려면 필요하다고 판단했으나 과제 범위를 고려해 **의도적으로 제외한** 항목입니다.
시스템이 실패하는 시나리오 순으로 정리했습니다.

### 7.1 아무나 전 직원 연락처를 수집할 수 있습니다 — 가장 큰 구멍

현재 API 에는 **인증이 없습니다.** URL 만 알면 페이지를 넘겨가며 전 직원의 이름·전화번호·이메일·입사일을
통째로 가져갈 수 있습니다. 이건 개인정보이고, 실서비스라면 "있으면 좋은 것"이 아니라 없으면 안 되는 것입니다.

- 인증·인가 (사내 SSO, 일반 사용자 / 관리자 역할 분리)
- **접근 로그** — 누가 언제 누구의 연락처를 조회했는지
- **마스킹** — 일반 사용자에게는 `010-****-5678`, 전체 열람은 권한자만
- 대량 조회 제한 (rate limit). 현재는 `pageSize` 상한만 있고 페이지를 돌리면 전부 가져갈 수 있습니다

### 7.2 조직 단위로 부를 수 없습니다

실제 긴급 연락망은 평면 목록이 아니라 **전파 트리**입니다. *"A팀 전원"*, *"각 팀장에게만 1차 전파"* 가
실제 사용 패턴입니다.

- 부서/팀, 직급, **보고 라인(상급자)**
- 부서별 조회, 1차/2차 전파 담당자

### 7.3 연락 수단이 하나뿐입니다

현재 한 사람당 전화번호 1개, 이메일 1개입니다. 긴급 연락망에서 이건 **실패 대비가 없다**는 뜻입니다.

- 휴대폰 / 내선 / **비상연락처(가족)** / 메신저 ID
- 연락 **우선순위** — 1순위가 안 되면 2순위
- 비상연락처는 특히 중요합니다. 본인이 사고 당사자면 본인 번호는 의미가 없습니다

### 7.4 데이터가 썩는 것을 막을 장치가 없습니다

연락처는 시간이 지나면 **자동으로 틀려집니다.** 평소엔 아무도 모르다가 정작 필요한 순간에만 드러납니다.
`updatedAt` 을 노출한 것은 첫 걸음일 뿐입니다.

- `verifiedAt` — "이 번호가 유효함을 마지막으로 확인한 시점"
- "6개월 이상 미확인" 조회 → 관리자가 갱신 요청
- 이메일 반송·통화 실패 기록

### 7.5 정작 "연락"은 못 합니다

조회만 되고 발송이 없습니다.

- SMS/푸시 일괄 발송, 전파 대상 그룹 지정
- **수신 확인(ACK)** — 긴급 상황에서는 "연락했다"보다 "확인됐다"가 중요합니다
- 미확인자 자동 재시도

### 7.6 비상시 가용성

긴급 연락망은 **다른 시스템이 같이 흔들리는 상황에서** 살아 있어야 합니다.
헬스체크는 넣었지만 그 다음이 남아 있습니다.

- **오프라인 대비 export** (CSV/PDF) — 네트워크가 죽어도 연락망은 살아 있어야 합니다.
  이 도메인에서는 실제 요구사항인 경우가 많습니다
- 조회 트래픽 급증 대비 캐시, 다중화

### 왜 지금 구현하지 않았는가

위 항목들은 과제의 필수 요구 범위를 넘어서고, 특히 인증·조직도·발송은 각각이 별도의 설계 결정을 요구합니다.
과제 안내에 *"추가로 인해서 과제가 실행이 안 될 경우는 실격"* 이라고 명시돼 있어,
**동작 확인 가능성을 해치지 않는 선**에서 멈추고 나머지는 근거와 함께 남기는 쪽을 택했습니다.

---

## 8. 테스트

```bash
dotnet test                                    # 전체
dotnet test tests/EmployeeDirectory.UnitTests  # 단위만
```

단언은 **FluentAssertions** 로 통일했습니다. 라이선스가 8.0.0 부터 상용으로 바뀌어
마지막 Apache-2.0 버전인 **7.x 로 고정**했습니다.

전체 **192개(단위 153 + 통합 39)** 이며, .NET 8 SDK 단독 환경에서 모두 통과하는 것을 확인했습니다(11절).

### 단위 테스트 (153개)

| 대상 | 검증 내용 |
| --- | --- |
| `EmailAddress`, `PhoneNumber` | 정규화, 표시 형식, 동등성, 실패 케이스, **마스킹**, 검색어 숫자 추출 |
| `Employee` | 생성 규칙, 미래 입사일 거부, **여러 오류 동시 수집**, 전체 교체, 실패 시 부분 적용 없음, 제외/복구 |
| `JoinedDate` | 허용 포맷 전부, 모호한 표기 거부 |
| `CsvEmployeeParser` | 예제 데이터, 헤더/한글 헤더, 추가 컬럼, 따옴표·이스케이프, BOM·CRLF, 빈 줄·주석, **행 번호 보존**, 컬럼 부족 |
| `JsonEmployeeParser` | 배열·단일 객체, 대소문자 무시, 별칭, 숫자 tel, 깨진 json, 잘못된 요소 |
| `EmployeeSourceParserResolver` | 선언 형식 우선, 내용 추론, 미등록 형식 |
| `RegisterEmployeesCommandHandler` | 신규/갱신/**복구** 집계, 집계 합 = 전체 건수, **부분 저장 금지**, 오류 일괄 수집, 요청 내 중복 이메일 |
| `UpdateEmployeeCommandHandler` | 전체 교체, 404, 409(이메일 선점), 자기 이메일 유지, 검증 실패 시 미저장 |
| `DeleteEmployeeCommandHandler` | 제외 후 조회 불가, 이중 삭제 404, 식별자 검증 |
| 조회 핸들러·검증기 | 페이징 계산, 검색어 필터, NotFound, 파라미터 범위, 검색어 길이 |
| `Dispatcher` | 핸들러 라우팅, **behavior 실행 순서**, 반복 호출 |

### 통합 테스트 (39개)

실제 HTTP 요청으로 라우팅 → 컨트롤러 → CQRS → EF Core/SQLite 전 구간을 검증합니다.
DI 구성을 테스트용으로 뜯어고치지 않고 **설정만 덮어써서** 프로덕션과 동일한 경로를 지나가게 했습니다.
따라서 DI 구성 자체의 실수도 테스트가 잡아냅니다.

- 필수 4가지 입력 방식 각각 201 + 조회까지 확인
- Content-Type 이 `text/plain` 일 때 내용 추론
- 재업로드 시 갱신 / 제외 후 재업로드 시 **복구**
- 잘못된 행이 있을 때 400 + **정상 행도 저장되지 않음**
- 페이징 계산, 기본값, 범위 초과, 잘못된 파라미터 400
- **검색**: 이름 부분 일치, 이메일 대소문자 무시, 하이픈 넣은 전화번호, **LIKE 와일드카드 이스케이프**, 검색+페이징 조합
- **수정**: 성공, 수정 시각 갱신, **전화번호만 바뀐 경우에도 갱신**(owned type 함정), 404, 409, 400
- **제외**: 204, 조회에서 사라짐, 이중 삭제 404
- 없는 이름 404 + `application/problem+json`
- 헬스체크 상태·저장소 검사 항목, Swagger 문서 미노출
- 응답의 상관관계 ID 헤더

---

## 9. 로깅

- **요청 단위**: `X-Correlation-Id`(없으면 발급) 를 로그 스코프에 넣고 응답 헤더로 돌려줍니다.
  메서드·**라우트 템플릿**·상태코드·소요시간을 남기며, 4xx 는 Warning, 5xx 는 Error 로 레벨을 올립니다.
- **유스케이스 단위**: `LoggingBehavior` 가 모든 커맨드/쿼리의 시작·성공·실패·소요시간을 남깁니다.
  핸들러 본문에는 로깅 코드가 없습니다.
- **도메인 이벤트성 로그**: 등록 완료 시 형식·신규·갱신·복구 건수를, 제외 시 대상 직원을 남깁니다.
  연락망에서 사람이 빠진 것은 사후 추적이 필요한 사건이기 때문입니다.
- **개인정보는 남기지 않습니다**: 경로·쿼리·오류 메시지 처리 방식은 5.13 참고.

로그 레벨은 `appsettings.json` 에서 조정합니다. EF Core 가 실행하는 SQL 을 보려면
`Microsoft.EntityFrameworkCore.Database.Command` 를 `Information` 으로 낮추면 됩니다.

---

## 10. 설정

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

## 11. 실행 환경 — 검증 결과

### 검증한 환경

빈 디렉터리에 **저장소를 새로 clone 해서** 아래 두 환경에서 확인했습니다.

| 환경 | 빌드 | 단위 153 | 통합 39 | 실행 |
| --- | :---: | :---: | :---: | :---: |
| **.NET 8 SDK 8.0.423** (런타임 8.0.29 단독) | ✅ 경고 0 | ✅ | ✅ | ✅ |
| .NET 10 SDK preview (런타임 9.0 / 10.0) | ✅ 경고 0 | ✅ | ⚠️ 아래 참고 | ✅ |

.NET 8 만 설치된 환경에서 **192개 테스트가 모두 통과**하고, 필수 4가지 입력·검색·수정·제외·헬스체크·Swagger 가
모두 정상 동작하는 것까지 확인했습니다.

### 상위 런타임에서의 알려진 제약

`net8.0` 을 타겟으로 하되, **.NET 8 런타임이 없고 9/10 만 설치된 환경**에서도 실행되도록
`RollForward=LatestMajor` 를 설정했습니다.

이 조합에서 통합 테스트만 영향을 받습니다. `Microsoft.AspNetCore.Mvc.Testing` 8.x 의 테스트 서버가
.NET 9 부터 바뀐 `PipeWriter.UnflushedBytes` 규약을 구현하지 않기 때문입니다.
**빌드·단위 테스트·애플리케이션 실행에는 영향이 없습니다.**

테스트 패키지를 9.x 로 올리려면 타겟을 `net9.0` 으로 올려야 하는데, 그러면 .NET 8 SDK 만 있는 환경에서
**빌드 자체가 실패**합니다. 과제의 "clone 후 빌드 성공" 조건을 우선해 `net8.0` 을 유지했습니다.

---

## 12. 확장 시나리오

| 하고 싶은 것 | 바꿔야 하는 곳 |
| --- | --- |
| 새 입력 형식(xml, xlsx) 지원 | `IEmployeeSourceParser` 구현 1개 추가 + DI 등록 |
| DB 를 PostgreSQL 등으로 교체 | `AddInfrastructure` 의 `UseSqlite` 한 줄 |
| 마이그레이션 기반 스키마 관리 | `DatabaseInitializer` 의 `EnsureCreated` → `Migrate` |
| 새 유스케이스 추가 | Command/Query + Handler 파일 추가 (DI 는 자동 스캔) |
| 모든 요청에 공통 동작 추가 | `IPipelineBehavior` 구현 1개 추가 |
| 조회 성능 최적화(캐시 등) | `IEmployeeReadStore` 구현 교체 (쓰기 모델 무영향) |
| 헬스체크 항목 추가 | `IHealthCheck` 구현 + `AddCheck` 한 줄 |
