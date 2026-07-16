# Thiết kế migration workflow Spec Kit

## Mục tiêu

Chuyển `ArasPlugin` sang GitHub Spec Kit làm workflow canonical cho feature
development, đồng thời phân định rõ vai trò hỗ trợ của Matt Pocock Skills,
OpenCode và các artifact AI legacy. Mục tiêu là bảo toàn mã nguồn, lịch sử Git,
domain knowledge và khả năng build/test hiện tại.

## Phạm vi

### Trong phạm vi

- Chuẩn hóa root tại Git repository `ArasPlugin/`.
- Đưa Spec Kit configuration, feature artifacts và AI instructions cần thiết vào
  cùng repository với source.
- Thiết lập constitution và canonical AI instructions.
- Phân loại/migration domain, architecture, ADR, development, security và workflow
  documentation.
- Migration feature spec, plan và task theo từng feature.
- Cập nhật OpenCode agent, command, `opencode.json`, entry points và instruction paths.
- Kiểm tra/cập nhật `.gitignore` để artifact mới được Git theo dõi.
- Lựa chọn issue tracker.
- Thu hồi workflow legacy sau khi có artifact thay thế.

### Ngoài phạm vi

- Di chuyển hoặc đổi tên `src/`, `tests/`, solution và project files.
- Đổi namespace, assembly name hoặc package/deployment path.
- Refactor business code hoặc thay đổi behavior sản phẩm.
- Thay đổi Aras schema hoặc IronCAD integration.
- Chuyển hàng loạt ticket bằng suy đoán.
- Xóa artifact còn inbound reference.
- Sửa code để ép build baseline pass.
- Triển khai feature trong phase migration tài liệu.

## Phân định trách nhiệm

### GitHub Spec Kit

Spec Kit sở hữu workflow feature chính và các artifact canonical:

```text
.specify/
└── memory/
    └── constitution.md

specs/
└── <###-feature-slug>/
    ├── spec.md
    ├── plan.md
    ├── tasks.md
    ├── research.md
    ├── data-model.md
    ├── quickstart.md
    └── contracts/
```

Không phải feature nào cũng cần mọi artifact phụ; chúng được tạo theo nhu cầu và
template của Spec Kit.

Core feature workflow:

```text
/speckit.constitution
→ /speckit.specify
→ /speckit.plan
→ /speckit.tasks
→ /speckit.implement
→ /speckit.converge
```

Quality và refinement:

```text
/speckit.clarify
/speckit.checklist
/speckit.analyze
```

Issue integration:

```text
/speckit.taskstoissues
```

`/speckit.taskstoissues` chỉ được dùng nếu chọn GitHub Issues. `tasks.md` vẫn là
feature task canonical; GitHub Issues chỉ là execution/tracking projection, không
thay thế requirement, plan hoặc task source. Không tạo issue trước khi `tasks.md`
được review và không dùng command này cho completed legacy tickets.

Nguồn sự thật của feature:

- `.specify/memory/constitution.md`: nguyên tắc phát triển và quality gates.
- `specs/<feature>/spec.md`: requirements.
- `specs/<feature>/plan.md`: technical implementation plan.
- `specs/<feature>/tasks.md`: implementation task breakdown.
- Các file còn lại trong cùng feature directory: research, design, model, contract
  và quickstart khi feature cần.

Spec Kit không sử dụng `.scratch/` làm nơi lưu spec, plan hoặc task canonical.

### Matt Pocock Skills

Matt Pocock Skills là tập skill hỗ trợ kỹ thuật, không thay thế workflow feature
của Spec Kit. Các trách nhiệm gồm grilling requirements, khám phá codebase, domain
modeling, ADR, TDD, debugging, code review, QA và handoff.

`CONTEXT.md` và `docs/adr/` được giữ theo domain-document convention của skills.
`.scratch/` chỉ là một lựa chọn local Markdown issue tracker; chỉ tồn tại khi
repository có quyết định chính thức sử dụng nó.

### OpenCode

`.opencode/` chỉ chứa agent definitions, command definitions và adapter/wrapper để
gọi Spec Kit hoặc skills. OpenCode không sở hữu workflow feature cạnh tranh và
không được tạo plan/task riêng song song với `specs/<feature>/`.

### Legacy AI Work Kit

Các khu vực sau được xem là legacy trong giai đoạn chuyển tiếp:

```text
tasks/ai/
docs/ai/
.opencode/commands/ticket-*
.opencode/agents/idea-*
docs/superpowers/
.superpowers/
```

Không mặc định xóa toàn bộ. Mỗi artifact phải được phân loại thành canonical
knowledge cần giữ, adapter cần thiết, historical evidence, legacy workflow, exact
duplicate, functional overlap hoặc artifact không còn tham chiếu.

## Nguyên tắc nguồn sự thật

1. Source code, compile-time contracts và tests là nguồn sự thật về behavior hiện tại.
2. `.specify/memory/constitution.md` là nguồn sự thật về nguyên tắc phát triển và
   quality gates.
3. `specs/<feature>/spec.md` là nguồn sự thật về requirement của feature.
4. `specs/<feature>/plan.md` là nguồn sự thật về technical implementation plan.
5. `specs/<feature>/tasks.md` là nguồn sự thật về implementation task breakdown.
6. `CONTEXT.md` và `docs/domain/` là nguồn sự thật về thuật ngữ và domain context.
7. `docs/adr/` là nguồn sự thật về các quyết định kiến trúc đã được chấp nhận.
8. `docs/architecture/` mô tả kiến trúc hiện tại.
9. `docs/development/` chứa hướng dẫn build, test và workflow cho developer.
10. `AGENTS.md` chứa instruction canonical dành cho AI agent.
11. `.scratch/` chỉ là issue tracker phụ nếu local Markdown issue tracking được chọn.
12. `tasks/ai/`, `docs/plans/` và `docs/superpowers/` là legacy hoặc historical cho
    tới khi được phân loại rõ.

## Cấu trúc repository đích

```text
ArasPlugin/
├── .specify/
│   ├── memory/constitution.md
│   ├── scripts/
│   └── templates/
├── specs/
│   └── <###-feature-slug>/
│       ├── spec.md
│       ├── plan.md
│       ├── tasks.md
│       ├── research.md
│       ├── data-model.md
│       ├── quickstart.md
│       └── contracts/
├── .agents/skills/
├── .opencode/
├── opencode.json
├── .superpowers/
├── docs/
│   ├── adr/
│   ├── agents/
│   ├── architecture/
│   ├── domain/
│   ├── development/
│   ├── deployment/
│   ├── security/
│   ├── plans/
│   └── archive/
├── tasks/ai/
├── scripts/
├── src/
├── tests/
├── AGENTS.md
├── CONTEXT.md
├── README.md
└── IdeaCadConnector.sln
```

`.scratch/` không được tạo tự động. Chỉ bổ sung khi đã xác nhận local Markdown là
issue tracker chính thức cho các bug/chore/maintenance độc lập.

`opencode.json` phải nằm tại root repository `ArasPlugin/` và chỉ load canonical
instruction hoặc adapter cần thiết. Trong giai đoạn chuyển tiếp, các khu vực sau
được đánh dấu rõ là không canonical:

```text
.superpowers/       # transitional; không phải canonical
tasks/ai/           # transitional; không nhận feature ticket mới
docs/plans/         # historical hoặc migration pending
docs/superpowers/   # migration pending
```

## Skill routing policy

Skill routing phải dựa trên các skill thực tế được cài trong `.agents/skills/` của
repository, không khóa cứng theo tên skill trong tài liệu bên ngoài. Mỗi skill phải
được inventory từ `SKILL.md`, xác định loại invocation, artifact đọc/ghi và nguy cơ
tạo nguồn cạnh tranh trước khi được route vào workflow.

Các capability cần phân loại trong inventory gồm: clarification/grilling, domain
modeling, research, codebase design, TDD, bug diagnosis, code review, handoff,
implementation, ticket generation, triage và wayfinding. Tên skill trong routing
chỉ được dùng nếu tồn tại thực tế trong `.agents/skills/`.

Các skill cần được routing hoặc giới hạn:

| Skill | Hành vi mặc định | Nguy cơ cạnh tranh | Chính sách trong ArasPlugin |
|---|---|---|---|
| Skill có capability tạo spec | Chuyển ý tưởng thành spec | Tạo spec ngoài `specs/` | Chỉ route nếu artifact được ghi vào `specs/<feature>/spec.md` |
| Skill có capability tạo ticket | Sinh ticket từ yêu cầu | Tạo task cạnh tranh với `tasks.md` | Không dùng cho feature; chỉ route issue ngoài feature nếu được duyệt |
| Skill có capability implement | Kỷ luật triển khai | Bỏ qua Spec Kit artifact | Chỉ dùng sau khi đã có artifact Spec Kit canonical |
| Skill có capability wayfinding | Investigation/work map | Bị dùng thay cho feature plan | Chỉ dùng cho investigation, không thay thế `plan.md` |
| Skill có capability triage | Phân loại issue | Tạo tracker ngoài policy | Chỉ dùng với issue tracker đã được phê duyệt |

Không sửa upstream skill. Dùng routing instruction, adapter hoặc project-local
customization để điều chỉnh hành vi.

## Workflow routing matrix

| Loại công việc | Spec Kit flow | Issue tracker | Matt Pocock Skills | OpenCode role | Artifact canonical | Quality gate |
|---|---|---|---|---|---|---|
| Feature lớn | Đầy đủ | Không bắt buộc | grill, domain, design, TDD, review | Planner readiness; implementer theo tasks | `specs/<feature>/` | analyze, build/test, review, verify |
| Feature nhỏ thay đổi behavior | Rút gọn: spec/plan/tasks khi cần | Không bắt buộc | TDD, review | Không tạo plan riêng | Artifact Spec Kit tương ứng | Test và review |
| Bug | Không tạo feature giả | Tracker đã chọn | diagnosing-bugs, TDD, review | Implementer theo approved bug issue | Bug issue + evidence | Reproduction, test, verify |
| Hotfix | Không tạo feature giả nếu nhỏ | Tracker đã chọn | diagnosing-bugs, TDD, review | Implementer theo approved issue | Hotfix issue + evidence | Regression test, verify |
| Refactor lớn | Spec Kit nếu scope lớn | Có thể dùng | codebase-design, TDD, review | Planner kiểm consistency | `specs/<feature>/` hoặc refactor issue | Tests, review |
| Refactor nhỏ | Không bắt buộc | Có thể dùng | codebase-design, TDD | Không tạo artifact cạnh tranh | Issue hoặc task đã duyệt | Tests, review |
| Research cho feature | `research.md` | Không bắt buộc | research, grilling, domain-modeling | Planner đọc research | `specs/<feature>/research.md` | Research review |
| Research độc lập | Không bắt buộc | Tracker đã chọn | research, wayfinder | Không tạo feature plan | Research issue/map | Evidence review |
| Documentation-only | Chỉ khi thay đổi lớn | Có thể dùng | domain-modeling, review | Agent cập nhật docs đã chỉ định | Docs + issue/task | Link/reference check |
| Architecture decision | Không thay thế ADR | Không bắt buộc | codebase-design, domain-modeling | Planner link ADR | `docs/adr/` | ADR review |
| Maintenance/chore | Không tạo feature giả | Tracker đã chọn | diagnosing-bugs hoặc TDD | Implementer theo approved issue | Issue + evidence | Build/test nếu ảnh hưởng |

Reviewer và verifier là quality gates; họ không tạo nguồn plan/task cạnh tranh với
`specs/<feature>/plan.md` và `specs/<feature>/tasks.md`.

Vai trò OpenCode được giới hạn như sau:

- `idea-planner` kiểm tra readiness, codebase evidence và plan consistency; không
  tạo `plan.md` thứ hai.
- `idea-implementer` chỉ triển khai từ `tasks.md` đã duyệt hoặc bug issue đã duyệt.
- `idea-reviewer` review diff đối chiếu với spec/issue; không tự sửa source.
- `idea-verifier` chạy build/test và ghi evidence; không tự sửa source.
- Agent được cập nhật spec, plan, tasks, ADR và canonical documentation phải được
  chỉ rõ trong routing; không có agent nào tự tạo nguồn canonical cạnh tranh.

## Customization policy

Project-local customization, nếu cần, được đặt tại:

```text
.specify/templates/overrides/
.specify/presets/
.specify/extensions/
```

- Không sửa trực tiếp generated Spec Kit core command nếu override giải quyết được.
- Dùng override cho thay đổi chỉ áp dụng cho ArasPlugin.
- Chỉ dùng preset khi cần thay đổi format hoặc policy có hệ thống.
- Chỉ dùng extension khi bổ sung phase hoặc capability mới.
- OpenCode wrapper không sao chép toàn bộ logic command của Spec Kit.
- Mọi customization phải được document và có owner.

## Traceability convention

Artifact Spec Kit chuyển từ legacy phải ghi traceability thay vì sao chép mù quáng:

```markdown
## Legacy traceability

- Legacy feature documents:
  - `docs/superpowers/specs/...`
  - `docs/plans/...`
- Legacy tickets:
  - `tasks/ai/tickets/...`
- Completion evidence:
  - `docs/part-library/...`
- Migration status: Partially migrated
```

Ticket đã hoàn tất không được biến thành task mở. Ticket bug/chore độc lập không bị
ép vào một feature Spec Kit giả; giữ tạm trong `tasks/ai/tickets/` hoặc chuyển sang
issue tracker đã được chọn sau khi có quyết định riêng.

## Migration mapping

| Artifact hiện tại | Phân loại | Artifact canonical mới | Cách xử lý |
|---|---|---|---|
| `docs/superpowers/specs/*` | Legacy/historical spec | `specs/<feature>/spec.md` nếu còn hiệu lực | Chuyển requirement hiệu lực, giữ traceability |
| `docs/superpowers/plans/*` | Legacy plan | `specs/<feature>/plan.md` nếu còn hiệu lực | Không chuyển kế hoạch obsolete |
| `docs/plans/*` | Design/historical plan | `specs/<feature>/plan.md` hoặc giữ lịch sử | Quyết định từng file |
| `tasks/ai/tickets/*` | Legacy ticket | `specs/<feature>/tasks.md`, issue tracker hoặc archive | Quyết định từng ticket |
| `tasks/ai/BACKLOG.*` | Legacy backlog/index | Migration index hoặc issue tracker | Không dùng làm Spec Kit map |
| `docs/ai/03_ARCHITECTURE_RULES.md` | Architecture knowledge | `docs/architecture/` | Giữ nội dung canonical |
| `docs/ai/04_ARAS_SCHEMA_MAP.md` | Domain knowledge | `docs/domain/` | Giữ nội dung canonical |
| `docs/ai/06_DECISIONS.md` | ADR collection | `docs/adr/` | Tách theo từng quyết định |
| `docs/ai/05_TESTING_GUIDE.md` | Development guide | `docs/development/` | Không đưa vào feature Spec Kit |
| `docs/ai/09_SECURITY_AND_DATA_SAFETY.md` | Rule/security guide | Constitution, `AGENTS.md` hoặc `docs/security/` | Phân chia theo trách nhiệm |
| `.opencode/commands/ticket-*` | Legacy command | Wrapper Spec Kit, issue command hoặc archive | Quyết định từng command |

## Các phase migration

### Phase 0 — Xác minh root và baseline

- Xác định `.git`, `.specify`, `.agents/skills` và `.opencode` nằm ở đâu.
- Xác định file nào đang ở ngoài repository.
- Ghi nhận Git status, build/test baseline.
- Xác minh integration của Spec Kit bằng:

  ```powershell
  specify --version
  specify check
  specify self check
  specify integration list
  ```

- Ghi nhận version Spec Kit dự kiến, integration hỗ trợ môi trường AI hiện tại,
  đường dẫn command/skill do integration tạo và file có nguy cơ xung đột với
  `.opencode/` hiện có.
- Không mặc định OpenCode integration tồn tại nếu `specify integration list` chưa
  xác nhận.
- Không sửa repository.

### Phase 1 — Thiết lập Spec Kit canonical

- Khởi tạo hoặc hoàn thiện Spec Kit tại root `ArasPlugin/`.
- Tạo hoặc hoàn thiện constitution.
- Đảm bảo `specs/` được Spec Kit sử dụng.
- Không tạo feature migration trong `.scratch/`.
- Chưa loại bỏ workflow cũ.

Việc chạy `specify init` chỉ thực hiện trong implementation phase sau khi plan được
phê duyệt.

Trước `specify init`, phải có baseline commit hoặc working tree sạch, danh sách file
có khả năng bị merge/overwrite, command init cụ thể và kế hoạch rollback dựa trên Git
state. Sau init phải kiểm tra `git status` và `git diff` trước khi chỉnh file khác.

### Phase 2 — Thiết lập supporting context

- Hoàn thiện `AGENTS.md`, `CONTEXT.md`, `docs/adr/` và các thư mục domain,
  architecture, development.
- Xác định issue tracker chính thức: GitHub, GitLab, local Markdown hoặc hệ thống
  khác.
- Chỉ tạo `.scratch/` nếu chọn local Markdown.

### Phase 3 — Pilot một feature

- Chọn một feature đang hoạt động và có đủ dữ liệu.
- Tạo `specs/<###-feature>/spec.md`, `plan.md` và `tasks.md`.
- Gắn traceability tới tài liệu cũ.
- Chạy `/speckit.analyze`.
- Không chạy `/speckit.implement` nếu mục tiêu chỉ là migration tài liệu.

### Phase 4 — Cập nhật entry points

- Cập nhật `AGENTS.md`, `AI_START_HERE.md` và `DEEPSEEK.md` để trỏ tới Spec Kit.
- Chuyển OpenCode command thành wrapper/adaptor mỏng.
- Ngăn agent tạo feature ticket mới trong `tasks/ai/`.
- Route bug/chore vào issue tracker đã chọn.
- Cập nhật `opencode.json` và `.gitignore`, sau đó dùng `git check-ignore -v` để
  xác nhận từng canonical path được theo dõi.

### Phase 5 — Migration theo feature

- Chuyển từng feature riêng, mỗi feature có checklist migration.
- Xác minh spec, plan, tasks và traceability.
- Không chuyển historical task thành task mở.
- Không suy luận trạng thái khi thiếu bằng chứng.

### Phase 6 — Thu hồi legacy

Chỉ archive hoặc xóa khi có artifact canonical thay thế, inbound references đã cập
nhật, Git history vẫn truy xuất được, không còn script/agent đọc đường dẫn cũ,
build/test không suy giảm và review độc lập đã hoàn tất.

## Tiêu chí nghiệm thu

- `.specify/` và `specs/` nằm trong cùng Git repository với source.
- Feature mới được tạo dưới `specs/`, không dưới `.scratch/`.
- `/speckit.tasks` tạo `tasks.md` canonical và `/speckit.implement` đọc chính file đó.
- `.scratch/` không tồn tại hoặc chỉ chứa issue khi local Markdown tracker được chọn.
- `CONTEXT.md` không chứa feature requirement tạm thời.
- `AGENTS.md` chứa instruction canonical nhưng không trở thành onboarding dài.
- OpenCode agent không tạo plan cạnh tranh với `plan.md`.
- Ticket legacy không bị biến thành task mở bằng suy đoán.
- Một feature pilot được phân tích bằng `/speckit.analyze`.
- Không có source/test file bị thay đổi trong documentation migration.
- `specify integration list` đã được ghi nhận.
- Không có hai `.specify/` root hoạt động.
- `.gitignore` theo dõi được `.specify/`, `specs/`, `AGENTS.md`, `CONTEXT.md` và
  tài liệu canonical mới.
- Không có command/agent đọc path legacy ngoài adapter đã được ghi nhận.
- `opencode.json` chỉ load canonical instruction hoặc adapter cần thiết.
- Constitution và `AGENTS.md` không mâu thuẫn.
- Skill routing ngăn `to-spec` và `to-tickets` tạo nguồn cạnh tranh.
- Nếu dùng GitHub Issues, issue chỉ được sinh từ `tasks.md` đã review.
- Nếu không dùng GitHub Issues, `/speckit.taskstoissues` không được gọi.
- Baseline build/test trước và sau được so sánh, phân biệt lỗi môi trường với regression.
- Mỗi file thu hồi có reference check và rollback path.

## Risk register

| ID | Rủi ro | Khả năng | Ảnh hưởng | Dấu hiệu | Giảm thiểu | Rollback |
|---|---|---:|---:|---|---|---|
| R1 | Hai Spec Kit root | Trung bình | Cao | Có nhiều `.specify/` root | Xác minh root ở Phase 0 | Giữ một root, loại thay đổi ngoài root |
| R2 | `specify init` overwrite/merge file | Trung bình | Cao | `git diff` có file không dự kiến | Dry-run bằng Git state và init command cụ thể | Revert commit init |
| R3 | `.gitignore` bỏ sót artifact mới | Trung bình | Trung bình | Canonical path bị ignore | Kiểm tra `git check-ignore -v` | Sửa ignore rule |
| R4 | OpenCode đọc đường dẫn cũ | Cao | Cao | Agent load `tasks/ai` hoặc prompt cũ | Audit `opencode.json`, command và agent | Khôi phục adapter path |
| R5 | Hai workflow cùng tạo task | Cao | Cao | Có task ngoài `tasks.md` | Routing policy và entry point duy nhất | Dừng migration, giữ artifact canonical |
| R6 | Matt `to-spec` tạo spec cạnh tranh | Trung bình | Cao | Spec ngoài `specs/<feature>/` | Project-local routing, không sửa upstream | Xóa duplicate sau reference check |
| R7 | Matt `to-tickets` tạo ticket cạnh tranh | Trung bình | Trung bình | Ticket không có source từ `tasks.md` | Chỉ dùng cho issue ngoài feature | Đóng ticket duplicate |
| R8 | Constitution mâu thuẫn `AGENTS.md` | Trung bình | Cao | Hai quality gate khác nhau | Cross-review trước commit | Sửa instruction layer thấp hơn |
| R9 | Context window overload | Cao | Trung bình | Agent đọc toàn bộ docs legacy | Index và progressive disclosure | Giảm context entry points |
| R10 | Completed ticket thành task mở | Trung bình | Cao | Task không có acceptance/evidence mới | Traceability và phân loại trạng thái | Archive task tạo nhầm |
| R11 | Mất domain knowledge | Thấp | Rất cao | Link tới schema/ADR bị hỏng | Migration index và inbound reference check | Khôi phục artifact lịch sử |
| R12 | Pilot không đại diện | Trung bình | Trung bình | Pilot không chạm workflow chính | Chọn feature có artifact hiện hữu | Chọn pilot khác |
| R13 | Build environment failure bị hiểu là regression | Cao | Cao | Lỗi dependency/tool ngoài diff | Baseline trước/sau và phân loại failure | Không rollback docs vì lỗi môi trường |
| R14 | Adapter bị xóa quá sớm | Trung bình | Cao | DeepSeek không tìm thấy instruction | Giữ adapter qua pilot và reference check | Khôi phục adapter |
| R15 | Traceability bị đứt | Trung bình | Cao | Không tìm được legacy source | Bắt buộc section traceability | Khôi phục link/index |
| R16 | Spec Kit upgrade ghi đè customization | Trung bình | Cao | Override/preset mất sau upgrade | Owner, override path và upgrade check | Khôi phục customization |

## Các quyết định cần phê duyệt

| ID | Quyết định | Phương án A | Phương án B | Khuyến nghị | Hệ quả |
|---|---|---|---|---|---|
| D1 | Root | Repository-centric `ArasPlugin/` | Workspace-centric | Repository-centric | `.specify/`, `specs/`, instructions cùng source |
| D2 | Domain context | Giữ `CONTEXT.md` + `docs/domain/` | Chỉ dùng `docs/domain/` | Giữ cả hai | Context ngắn, docs chi tiết |
| D3 | Issue tracker | GitHub Issues | Local Markdown/khác | GitHub Issues | Issue execution/tracking ngoài repository artifact |
| D4 | `.scratch` | Không tạo | Dùng cho local Markdown | Không tạo | Không thuộc cây canonical |
| D5 | `taskstoissues` | Dùng với GitHub Issues | Không dùng | Cho phép sau khi reviewed `tasks.md` | Issue chỉ là projection từ feature task |
| D6 | `idea-planner` | Planner độc lập | Readiness/consistency checker | Readiness checker | Không có `plan.md` cạnh tranh |
| D7 | `idea-implementer` | Workflow riêng | Thực thi approved artifact | Approved artifact | Đọc `tasks.md` hoặc bug issue |
| D8 | Reviewer/verifier | Giữ agent | Thay bằng skill | Giữ trong transition | Quality gate độc lập trước khi đơn giản hóa |
| D9 | DeepSeek adapter | Giữ qua pilot | Archive ngay | Giữ qua pilot | Tránh gián đoạn entry point |
| D10 | ADR path | `docs/adr/` | `docs/decisions/` | `docs/adr/` | Phù hợp domain convention hiện tại |
| D11 | Traceability | Trong feature artifact | Migration index | Trong feature, index bổ trợ | Gần requirement/plan/tasks |
| D12 | Pilot feature | Feature gần đây có đủ artifact | Feature mới | Feature gần đây | Có evidence để đối chiếu |
| D13 | Template customization | Không override | `overrides/presets/extensions` khi cần | Chỉ dùng khi cần | Tránh fork Spec Kit |
| D14 | Matt restricted skills | Cho phép tự do | Routing/giới hạn local | Routing/giới hạn local | Không sửa upstream, không tạo nguồn cạnh tranh |

Các quyết định D1–D11, D13 và D14 ở trên là quyết định nền đã được chấp thuận cho
technical plan. D12 (pilot cụ thể) vẫn cần approval riêng sau candidate matrix.

Trong cây repository, các khu vực sau chỉ là transitional/historical, không phải
canonical dài hạn:

```text
.superpowers/       # transitional only
tasks/ai/           # transitional only
docs/plans/         # migration pending hoặc historical
docs/superpowers/   # migration pending
```

## Điều kiện trước khi triển khai

- Không chạy `specify init` trong bước thiết kế này.
- Không tạo `.scratch/`.
- Không chuyển ticket, sửa command, archive hoặc xóa artifact.
- Không thay đổi source code và không commit trong bước review thiết kế.
- Sau khi thiết kế được duyệt, technical plan phải phân tách riêng việc thiết lập
  Spec Kit, supporting context, pilot feature và thu hồi legacy.
