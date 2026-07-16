# Technical Migration Plan — ArasPlugin sang GitHub Spec Kit

Implementation chỉ được bắt đầu sau approval gate tương ứng và phải sử dụng workflow
canonical được ghi trong `AGENTS.md` cùng Spec Kit artifact đã phê duyệt. Không bắt
buộc một skill chưa được chứng minh tồn tại.

**Goal:** Chuyển workflow feature của ArasPlugin sang GitHub Spec Kit theo mô hình repository-centric, bảo toàn source/behavior và thu hồi legacy có kiểm chứng.

**Architecture:** ArasPlugin là Git root canonical. .specify/ và specs/ sở hữu constitution/spec/plan/tasks; CONTEXT.md ngắn và docs/domain/ chi tiết sở hữu domain context; GitHub Issues là projection từ tasks.md đã review. OpenCode và skill chỉ là adapter/support.

**Tech Stack:** Git, GitHub Spec Kit CLI 0.12.16, OpenCode, PowerShell, .NET Framework net48, Visual Studio/MSBuild hoặc .NET SDK, GitHub Issues.

## Global Constraints

- D1 repository-centric tại ArasPlugin/.
- D2 dùng CONTEXT.md ngắn và docs/domain/ chi tiết.
- D3 GitHub Issues là issue tracker mục tiêu.
- D4 không tạo .scratch/.
- D5 chỉ dùng /speckit.taskstoissues sau khi tasks.md được review.
- D6 idea-planner là readiness/consistency checker.
- D7 idea-implementer chỉ thực thi approved artifact.
- D8 giữ idea-reviewer và idea-verifier qua pilot.
- D9 giữ DeepSeek adapter qua pilot.
- D10 dùng docs/adr/.
- D11 traceability nằm trong feature artifact; migration index chỉ hỗ trợ.
- D13 chỉ dùng override/preset/extension khi nhu cầu đã chứng minh.
- D14 routing dựa trên skill thực tế đã cài; không sửa upstream.
- Plan-only phase không chạy specify init, không sửa .gitignore, không tạo issue,
  không migration/archive/xóa artifact và không thay đổi source/tests.

---

## 1. Evidence đã xác minh

- Git root: C:/Users/TD-999/Research/ArasInnovator/copilot-worktrees/Workspace/ArasPlugin.
- Git directory: ArasPlugin/.git; branch main.
- Remote: https://github.com/devphuclam/ArasPlugin.git.
- HEAD: 9ec5fd1 docs: design Spec Kit workflow migration.
- Working tree có design migration chưa commit; không reset/checkout/restore thay đổi này.
- ArasPlugin/.specify/ và ArasPlugin/.agents/skills/ hiện chưa tồn tại.
- Workspace cha có .specify/ và .agents/, nhưng không phải canonical của repo.
- ArasPlugin/opencode.json đã tồn tại tại root và hiện đọc AI_START_HERE.md cùng nhiều docs/ai/* legacy.
- Windows 10.0.19045; .NET SDK 10.0.300; MSBuild 18.6.3.
- Solution IdeaCadConnector.sln; test project, OcrTool và CreateIronCadTestFiles target net48.
- Solution dùng Aras IOM, IronCAD interop, WPF/WinForms, COM/strong-name và Windows references.
- Build/test baseline chưa chạy trong plan-only phase.

### Tooling evidence

Đã chạy read-only:

    specify --version        # specify 0.12.16
    specify check            # pass; opencode CLI available
    specify self check       # pass; up to date 0.12.16
    specify integration list # không trả integration

Không suy đoán integration name hoặc command init từ output rỗng.

### Skills evidence

Skill discovery hiện nằm ở workspace cha ../.agents/skills/, gồm:
grill-with-docs, grilling, domain-modeling, codebase-design, tdd,
diagnosing-bugs, code-review, setup-matt-pocock-skills.

Không thấy ArasPlugin/.agents/skills/, .opencode/skills/ hoặc skills/. Các tên
to-spec, to-tickets, triage, wayfinder, implement, research, handoff chưa có bằng
chứng đã cài; implementation phải inventory lại từ SKILL.md thực tế.

## 2. Root verification

### Task 1: Reconfirm root trước implementation

Files: none.

    Get-Location
    Get-ChildItem -Force
    Get-ChildItem .. -Force
    git rev-parse --show-toplevel
    git rev-parse --git-dir
    git status --short
    git branch --show-current
    git log -1 --oneline

Expected: Git root là ArasPlugin/; design diff vẫn được bảo toàn.

- Scenario A (hiện tại): giữ ArasPlugin/; không copy parent .specify/ hoặc skills.
- Scenario B: nếu canonical dirs đã vào repo, không tạo bản thứ hai.
- Scenario C: nếu có hai .specify/ hoặc skill sets, dừng và lập collision report.
- Agent-root check phải chạy từ ArasPlugin/ và kiểm tra path instruction thực tế.

## 3. Tooling verification

### Task 2: Verify CLI và integration

Files: none.

    specify --version
    specify check
    specify self check
    specify integration list

Ghi version, pin decision, integration hỗ trợ, slash-command/skills mode, paths được
tạo và xung đột với .opencode/ hoặc .agents/skills/. Chỉ đề xuất init command sau
khi integration list trả integration cụ thể; command ví dụ không phải quyết định.

## 4. Installed skills inventory

### Task 3: Inventory skill theo SKILL.md

Files: none trong plan-only phase.

    rg --files .agents/skills
    Get-ChildItem .agents/skills -Recurse -Filter SKILL.md
    Get-ChildItem .opencode/skills, .claude/skills, skills -ErrorAction SilentlyContinue

Đọc từng skill thực tế và lập bảng:

| Skill | Invocation | Artifact đọc | Artifact ghi | Cạnh tranh Spec Kit? | Routing | Hành động |
|---|---|---|---|---|---|---|

Phân loại supporting/restricted/explicit-only/safe automatic/legacy/unknown; kiểm tra
capability tạo spec, ticket, implement, triage, wayfinding/planning, domain, review,
TDD, diagnosis và handoff. Không dùng tên skill online nếu không tồn tại.

## 5. Git tracking baseline

### Task 4: Kiểm tra .gitignore read-only

Files: none.

    git check-ignore -v --no-index .specify/memory/constitution.md
    git check-ignore -v --no-index specs/001-example/spec.md
    git check-ignore -v --no-index AGENTS.md
    git check-ignore -v --no-index CONTEXT.md
    git check-ignore -v --no-index docs/adr/0001-example.md
    git check-ignore -v --no-index .agents/skills/example/SKILL.md
    Get-Content .gitignore

Current .gitignore broadly ignores *.md, có unignore cho AI Work Kit/OpenCode,
nhưng chưa chứng minh canonical .specify/, specs/, AGENTS.md, CONTEXT.md, docs/adr/
và repo-local skills được track. Phân loại generated/secret trước khi đề xuất
minimal unignore; không sửa trong plan phase.

## 6. Build/test baseline

### Task 5: Establish baseline sau approval

Files: none intentionally; giữ bin/obj ignored.

    dotnet --info
    dotnet build IdeaCadConnector.sln
    dotnet test IdeaCadConnector.sln
    msbuild IdeaCadConnector.sln

Chọn command thực tế sau khi xác minh MSBuild Developer PowerShell, NuGet, .NET
Framework reference assemblies, Windows SDK, Aras IOM, IronCAD assemblies, COM,
strong-name và WPF packs. Ghi ma trận project/target/dependency/requirement/testability/
baseline. Phân biệt code failure, missing dependency/tooling, machine integration,
existing baseline và regression; không sửa source để ép pass.

## 7. Initialization collision

### Task 6: Dry-run inventory trước specify init

Approval gate: user phải duyệt integration, command và collision table.

Kiểm tra các paths: .specify/, .specify/memory/, .specify/scripts/,
.specify/templates/, .specify/templates/overrides/, specs/, .agents/skills/,
AGENTS.md, CONTEXT.md, .opencode/, opencode.json.

Lập bảng path dự kiến, tồn tại, owner hiện tại/sau migration, collision risk, xử lý,
verification và rollback. Không copy parent artifact tự động.

## 8. Canonical instruction design

### Task 7: Lập ownership map trước khi tạo file

Future files: AGENTS.md, CONTEXT.md, .specify/memory/constitution.md,
docs/domain/, docs/adr/.

- AGENTS.md: scope, source order, feature/issue routing, safety, build/test,
  review/verify và links; không là onboarding dài.
- CONTEXT.md: glossary, entities/relationships, preferred/forbidden terms, stable
  invariants và links; không chứa feature/task/session/completion.
- Constitution: principles, architecture, testing, data safety, compatibility,
  no-guess Aras schema, quality gates, documentation sync.
- docs/domain/ chi tiết domain; docs/adr/ accepted decisions.

Lập rule ownership matrix; tạo constitution chỉ sau approval gate riêng.

## 9. Documentation taxonomy

### Task 8: Inventory từng file legacy

Paths: docs/ai/**, docs/superpowers/**, docs/plans/**, .superpowers/**,
AI_START_HERE.md, DEEPSEEK.md, OPENCODE_START_HERE.md.

    rg --files docs/ai docs/superpowers docs/plans .superpowers

Lập bảng ID/current path/content type/canonical target/action/inbound references/link
updates/verification/rollback. Action chỉ có thể là giữ, git mv, tách, hợp nhất phần,
adapter, deprecate, archive sau pilot hoặc điều tra. Foundation phase không delete;
completed work không thành open task.

## 10. Pilot feature

### Task 9: Chọn pilot bằng evidence

Đánh giá candidates theo spec/plan/ticket/trạng thái có evidence/test evidence/đại
diện/rủi ro. Ưu tiên feature gần đây có source area rõ, scope vừa, test/verification
evidence và không historical thuần túy. Không chọn chỉ vì tên file mới nhất; pilot
migration artifact không sửa product code.

### Task 10: Tạo artifact pilot sau approval

Future path:

    specs/<###-pilot-feature>/
    ├── spec.md
    ├── plan.md
    ├── tasks.md
    ├── research.md        # chỉ khi cần
    └── migration-notes.md # chỉ khi cần, không sửa core template

Phân loại legacy thành requirement, plan, tasks, traceability, historical evidence
hoặc manual conflict. Chạy /speckit.analyze; chỉ dùng taskstoissues sau review
tasks.md. Không biến completed work thành open task.

## 11. OpenCode adaptation

### Task 11: Audit adapter sau pilot approval

Files: .opencode/agents/**, .opencode/commands/**, opencode.json.

Lập bảng file/role hiện tại/role đích/artifact đọc/artifact ghi/thay đổi/thu hồi.
Planner chỉ readiness/consistency; implementer chỉ approved tasks.md/issue; reviewer
đối chiếu diff; verifier chạy baseline và ghi evidence; wrapper không copy logic Spec
Kit; DeepSeek adapter giữ qua pilot; opencode.json không load toàn bộ legacy docs mỗi
session.

## 12. GitHub Issues projection

### Task 12: Projection sau khi tasks.md được review

Định nghĩa duplicate prevention, link hai chiều task/issue, task split sau projection,
đóng issue, bug/chore ngoài feature, legacy open ticket và completed evidence. GitHub
Issue chỉ là execution/tracking projection; tasks.md vẫn là canonical.

## 13. Commit sequence và gates

### Task 13: Commit/rollback sequence

Các commit tương lai: baseline evidence/index; Spec Kit root; constitution; AGENTS;
CONTEXT; taxonomy; domain/architecture/development/security; ADR; OpenCode paths;
pilot artifacts; issue projection; legacy deprecation; final verification.

Approval gate trước integration selection, init, .gitignore, constitution, pilot,
OpenCode edit, issue projection, deprecation, archive hoặc delete. Mỗi gate có
evidence, approver, action sau approve và action khi reject.

## 14. Verification và rollback

### Task 14: Verify từng phase

    git status --short
    git diff
    git diff --check
    rg -n "tasks/ai|docs/ai|docs/superpowers|\.superpowers" .opencode opencode.json AGENTS.md
    git check-ignore -v --no-index <canonical-path>

Mỗi phase kiểm tra Git state, references/Markdown links, agent root/instruction path,
Spec Kit artifact lookup và build/test comparison. Mỗi file thu hồi phải có inbound
reference check và rollback path.

Rollback không dùng destructive reset/checkout. Nếu init overwrite ngoài collision
table: dừng, lưu status/diff và revert commit init theo gate. Nếu adapter path sai:
khôi phục adapter commit, không xóa legacy. Nếu issue duplicate: xử lý từng issue,
không đóng/xóa hàng loạt. Build failure phải phân loại môi trường trước rollback.

## Các câu hỏi chỉ trả lời trong implementation phase

1. specify integration list có trả integration hợp lệ sau khi chạy đúng repo root?
2. Command init cụ thể tạo những file nào và collision nào?
3. Skill nào thực sự được cài trong ArasPlugin/.agents/skills/?
4. Build command nào pass với dependency .NET Framework/Aras/IronCAD?
5. Pilot nào có evidence tốt nhất sau inventory từng file?
6. .gitignore cần unignore tối thiểu paths nào?
7. OpenCode paths nào chuyển được mà không gián đoạn DeepSeek?

## Plan self-review

- Root repo được phân biệt với workspace parent.
- Không giả định integration name khi output hiện tại rỗng.
- Routing không khóa cứng theo skill không tồn tại trong repo.
- Không tạo .scratch/ và không dùng nó cho Spec Kit artifact.
- Build/test plan tính đến net48, Windows/COM/Aras/IronCAD.
- Có collision, approval, verification và rollback cho bước overwrite/archive.
- Không task nào sửa source hoặc ép baseline pass.

---

## Evidence completion và state classification

Mọi kết luận trong plan dùng ba trạng thái:

- `Verified`: có command output hoặc nội dung file chứng minh.
- `Inferred`: suy luận từ evidence, cần xác minh thêm.
- `Pending`: chỉ xác định được trong implementation phase.

### Git state — Verified

| Path | State | Tracked/untracked | Thuộc | Hành động trước init |
|---|---|---|---|---|
| `docs/superpowers/specs/2026-07-16-spec-kit-workflow-migration-design.md` | modified | tracked | design | bảo toàn diff, không reset |
| `docs/plans/2026-07-16-spec-kit-migration-technical-plan.md` | ignored untracked | untracked/ignored bởi `*.md` | technical plan | không sửa ignore trong plan-only |
| source/test paths | unchanged | tracked | product | không chạm |

Cached diff: không có output. HEAD gần nhất: `9ec5fd1`, `bc3b2a1`, `07cf495`.

### Worktree — Verified

| Thuộc tính | Kết quả | Ý nghĩa | Rủi ro |
|---|---|---|---|
| inside work tree | `true` | Git hoạt động | thấp |
| worktree root | `.../Workspace/ArasPlugin` | đúng D1 | thấp |
| git dir | `.git` | repository không phải linked worktree | thấp |
| common dir | `.git` | không có common-dir ngoài repo | thấp |
| worktree list | một worktree, branch `main` | không có worktree cạnh tranh | trung bình do đang trên main |
| migration branch | chưa tạo | chưa triển khai | không triển khai trực tiếp trên main |

Khuyến nghị implementation tạo branch riêng `chore/spec-kit-workflow-migration` sau
approval; bước review hiện tại không tạo branch.

### Spec Kit command matrix — Verified/Blocked

| Command | Exit | Output tóm tắt | Kết luận |
|---|---:|---|---|
| `specify version` | 0 | CLI `0.12.16`, Python `3.14.6`, Windows AMD64 | Verified |
| `specify version --features --json` | 0 | có workflow catalog, bundled templates, integration features | Verified |
| `specify check` | 0 | CLI ready; opencode available | Verified |
| `specify self check` | 0 | Up to date `0.12.16` | Verified |
| `specify integration list` | 1 | không hiển thị catalog/output | **Blocked** |
| `specify integration list --help` | 0 | hỗ trợ `--catalog` và `--help` | Verified; cần điều tra catalog |

`specify init` đang `BLOCKED`: không suy đoán integration, không reinstall/upgrade tự
động và không đề xuất init command cuối cùng. Bước tiếp theo chỉ là điều tra read-only
CLI/source nếu được duyệt.

### Phase 0 integration catalog investigation

Các command read-only phải chạy trong Phase 0:

    specify integration list --catalog
    specify integration list --catalog --help
    specify version --features --json

Evidence appendix phải ghi riêng standard output và standard error, không thay bằng
summary:

| Command | Exit code | Standard output | Standard error | Kết luận |
|---|---:|---|---|---|
| `specify integration list` | 1 | rỗng trong lần chạy hiện tại | rỗng trong lần chạy hiện tại | catalog chưa được giải quyết |
| `specify integration list --help` | 0 | hiển thị `--catalog`, `--help` | rỗng | catalog investigation khả dụng |
| `specify integration list --catalog` | Pending | chưa chạy | chưa chạy | cần chạy Phase 0 |
| `specify integration list --catalog --help` | Pending | chưa chạy | chưa chạy | cần chạy Phase 0 |
| `specify version --features --json` | 0 | JSON có workflow catalog, bundled templates và integration features | rỗng | CLI feature flags đã xác minh |

Không suy đoán integration name, không chạy `specify init`, không reinstall/upgrade CLI
tự động. Nếu catalog hợp lệ, lập danh sách integration và xác định integration phù
hợp OpenCode; nếu vẫn lỗi, ghi nguyên output/lỗi và giữ gate integration resolution ở
trạng thái blocked.

### Installed skills — Verified

| Path | Skill | Nguồn | Invocation | Artifact đọc | Artifact ghi | Cạnh tranh Spec Kit | Routing | Migration action |
|---|---|---|---|---|---|---|---|---|
| `../.agents/skills/grill-with-docs/SKILL.md` | grill-with-docs | workspace | explicit-only | domain docs | domain/ADR qua domain modeling | không | clarification | giữ workspace, không copy |
| `../.agents/skills/grilling/SKILL.md` | grilling | workspace | explicit/user | context | không canonical feature | không | clarification | giữ workspace |
| `../.agents/skills/domain-modeling/SKILL.md` | domain-modeling | workspace | skill | CONTEXT/ADR | CONTEXT/ADR | không | domain support | giữ workspace; copy chỉ sau approval |
| `../.agents/skills/codebase-design/SKILL.md` | codebase-design | workspace | skill | codebase/docs | design guidance | không | plan support | giữ workspace |
| `../.agents/skills/tdd/SKILL.md` | tdd | workspace | skill | CONTEXT/ADR | tests/source | không | implementation support | giữ workspace |
| `../.agents/skills/diagnosing-bugs/SKILL.md` | diagnosing-bugs | workspace | skill | CONTEXT/ADR | repro/test/evidence | không | bug support | giữ workspace |
| `../.agents/skills/code-review/SKILL.md` | code-review | workspace | skill | diff/spec/standards | review report | không | quality gate | giữ workspace |
| `../.agents/skills/setup-matt-pocock-skills/SKILL.md` | setup-matt-pocock-skills | workspace | explicit-only | repo docs/remote | AGENTS/docs/agents | có thể ảnh hưởng issue/domain config | chỉ sau D3 approval | không tự chạy |

Không có path verified trong `ArasPlugin/.agents/skills/`, `.opencode/skills/`,
`.claude/skills/` hoặc `skills/`. Không có evidence cho `to-spec`, `to-tickets`,
`triage`, `wayfinder`, `implement`, `research` hoặc `handoff`.

| Capability | Skill thực tế | Tự động | Explicit | Hạn chế | Lý do |
|---|---|---:|---:|---|---|
| clarification/domain | grill-with-docs, grilling, domain-modeling | không | có | không sở hữu spec | tránh tạo artifact cạnh tranh |
| technical design | codebase-design | không | có | không sở hữu plan | hỗ trợ Spec Kit |
| TDD | tdd | không | có | sau approved task | không sửa behavior ngoài scope |
| bug diagnosis | diagnosing-bugs | không | có | issue/repro trước | không tạo feature giả |
| review | code-review | không | có | quality gate | không tự sửa source |
| setup | setup-matt-pocock-skills | không | có | D3 đã duyệt | không tạo `.scratch` vì D4 |

Recommendation: giữ skills ở workspace trong transition, không tự động copy/cài vào
repository; chỉ copy có kiểm chứng sau khi có quyết định source/version và owner.

### Git tracking — Verified

| Path | Ignored? | Matching rule | Minimal change đề xuất |
|---|---:|---|---|
| `.specify/memory/constitution.md` | yes | `.gitignore:9:*.md` | unignore exact canonical path |
| `specs/001-example/spec.md` | yes | `.gitignore:9:*.md` | unignore `specs/` |
| `AGENTS.md` | yes | `.gitignore:9:*.md` | unignore exact path |
| `CONTEXT.md` | yes | `.gitignore:9:*.md` | unignore exact path |
| `docs/adr/0001-example.md` | yes | `.gitignore:9:*.md` | unignore `docs/adr/` |
| `.agents/skills/example/SKILL.md` | yes | `.gitignore:9:*.md` | only if repo-local skills approved |
| migration design basename | yes | `.gitignore:9:*.md` | preserve existing explicit tracking mechanism |
| migration technical plan basename | yes | `.gitignore:9:*.md` | preserve ignored status until commit gate |

No `.gitignore` change is made now. Generated/secret environment files remain ignored.

### Build dependency matrix — Verified from files; baseline Pending

`Directory.Build.props` supplies `net48`, deterministic build, strong-name signing and
shared `ICApiAddin.snk` to projects that inherit it.

| Project | Output | Target | Project refs | External refs/deps | Machine dependency | Build/test |
|---|---|---|---|---|---|---|
| Core | library | net48 via props | none | Newtonsoft.Json 13.0.4 | .NET Framework refs | build |
| Aras | library | net48 via props | Core, Workspace | Aras.IOM 15.1.2, Logging.Abstractions 8.0.2 | Aras/IOM | build; integration pending |
| Workspace | library | net48 via props | Core | Newtonsoft.Json 13.0.4 | filesystem/domain data | build/test |
| Ui | library | net48 via props | Core, Aras, Workspace | WPF/WinForms/System.Net.Http | Windows desktop targeting | build |
| IronCAD | library | net48 via props | Core, Aras, Workspace, Ui | IronCAD interop, WPF/WinForms, stdole | IronCAD install/COM, x64 | build pending |
| Desktop | WinExe | net48 via props | Core, Aras, Workspace, Ui | Newtonsoft.Json 13.0.4, WPF | Windows desktop/x64 | build pending |
| OcrTool | Exe | explicit net48 | none | WindowsRuntime/Windows | Windows runtime | build pending |
| Tests | test | explicit net48 | Core, Workspace, Aras, Desktop, IronCAD | xUnit 2.4.2, Test SDK 17.0.0, coverlet | IronCAD/WPF refs | test pending |
| CreateIronCadTestFiles | Exe | explicit net48 | none | IronCAD interop at ProgramFiles path | IronCAD 2025 install | build pending |

Baseline status: `not established`. Không dùng pass/fail/regression trước khi build/test
được chạy.

### Collision table — Verified current state / Pending target ownership

| Path | Tồn tại | Owner hiện tại | Owner đích | Collision | Xử lý đề xuất | Gate |
|---|---:|---|---|---|---|---|
| `.specify/` | no | workspace parent | Spec Kit repo root | high | init only after integration resolution | integration/init |
| `.specify/memory/` | no | workspace parent | Spec Kit | high | inspect parent; do not copy automatically | init |
| `.specify/scripts/` | no | workspace parent | Spec Kit | medium | use verified init output | init |
| `.specify/templates/` | no | workspace parent | Spec Kit | medium | preserve core; overrides only if needed | init |
| `.specify/templates/overrides/` | no | none | project customization | low | create only proven need | customization |
| `.specify/presets/` | no | none | project customization | low | create only proven need | customization |
| `.specify/extensions/` | no | none | project customization | low | create only proven need | customization |
| `specs/` | no | none | Spec Kit | low | create via approved workflow | foundation |
| `.agents/` | no | workspace parent | repo-local skills if approved | high | decide copy vs workspace | skills |
| `.agents/skills/` | no | workspace parent | repo-local skills if approved | high | inventory first | skills |
| `AGENTS.md` | no | workspace parent only | repo instructions | high | create after ownership approval | foundation |
| `CONTEXT.md` | no | workspace parent only | repo domain context | high | create after domain approval | foundation |
| `.opencode/` | yes | legacy OpenCode | transitional adapter | medium | audit; do not overwrite | OpenCode |
| `opencode.json` | yes | legacy OpenCode | adapter config | medium | update only after pilot | OpenCode |

### Rule ownership matrix

| Rule/knowledge | Constitution | AGENTS.md | CONTEXT.md | Detailed docs | Canonical owner |
|---|---|---|---|---|---|
| architecture boundaries | principle | routing pointer | no | docs/architecture | constitution + architecture docs |
| Aras schema no-guess | principle | safety instruction | no | docs/domain | constitution + domain docs |
| data safety | principle | operational rule | no | docs/security | constitution + security docs |
| compatibility | principle | gate pointer | no | docs/development | constitution |
| build/test gates | quality rule | commands/pointer | no | docs/development | constitution + development docs |
| review gate | quality rule | workflow pointer | no | review checklist | constitution + AGENTS |
| domain terminology | no | link only | glossary/invariants | docs/domain | CONTEXT + domain docs |
| current project state | no | link only | no | docs/development | detailed state doc |
| feature requirements | no | routing only | no | feature spec | specs/<feature>/spec.md |
| feature tasks | no | routing only | no | feature tasks | specs/<feature>/tasks.md |
| security details | high-level principle | safety pointer | no | docs/security | docs/security |
| deployment guidance | no | link only | no | docs/deployment | docs/deployment |

### Migration map — each current file

All current paths below were enumerated read-only. No action is executed in this plan.

| ID | Current path | Type | Target/action | Gate | Verification/rollback |
|---|---|---|---|---|---|
| M01 | `.superpowers/sdd/task-4-report.md` | historical evidence | preserve; no canonical copy | archive | inbound refs; restore |
| M02 | `AI_START_HERE.md` | entry guide | update to Spec Kit pointer | entry point | smoke test; restore |
| M03 | `DEEPSEEK.md` | adapter guide | update after pilot; keep adapter | DeepSeek | smoke test; restore |
| M04 | `OPENCODE_START_HERE.md` | adapter guide | update after OpenCode audit | OpenCode | path check; restore |
| M05 | `opencode.json` | adapter config | update instruction paths after pilot | OpenCode | config/path check; restore |
| M06 | `.opencode/agents/idea-implementer.md` | agent | route to approved tasks/issues | OpenCode | prompt audit; restore |
| M07 | `.opencode/agents/idea-planner.md` | agent | readiness checker only | OpenCode | prompt audit; restore |
| M08 | `.opencode/agents/idea-reviewer.md` | agent | review diff/spec, no source edit | OpenCode | prompt audit; restore |
| M09 | `.opencode/agents/idea-verifier.md` | agent | build/test evidence, no source edit | OpenCode | prompt audit; restore |
| M10 | `.opencode/commands/ticket-fix-review.md` | command | adapter or deprecate | OpenCode | command path; restore |
| M11 | `.opencode/commands/ticket-implement.md` | command | adapter or deprecate | OpenCode | command path; restore |
| M12 | `.opencode/commands/ticket-plan.md` | command | adapter or deprecate | OpenCode | no competing plan; restore |
| M13 | `.opencode/commands/ticket-review.md` | command | adapter or deprecate | OpenCode | command path; restore |
| M14 | `.opencode/commands/ticket-status.md` | command | GitHub Issues adapter | issue tracker | command smoke; restore |
| M15 | `.opencode/commands/ticket-verify.md` | command | verification adapter | OpenCode | command smoke; restore |
| M16 | `docs/ai/00_START_HERE.md` | AI guide | merge into developer/AI guide | taxonomy | link check; restore |
| M17 | `docs/ai/01_AI_RUNBOOK.md` | workflow | extract canonical routing | taxonomy | reference check; restore |
| M18 | `docs/ai/02_PROJECT_STATE.md` | project state | detailed development doc | taxonomy | link check; restore |
| M19 | `docs/ai/03_ARCHITECTURE_RULES.md` | architecture | docs/architecture | taxonomy | link check; restore |
| M20 | `docs/ai/04_ARAS_SCHEMA_MAP.md` | domain | docs/domain | taxonomy | schema links; restore |
| M21 | `docs/ai/05_TESTING_GUIDE.md` | development | docs/development | taxonomy | command check; restore |
| M22 | `docs/ai/06_DECISIONS.md` | ADR collection | split into docs/adr | ADR | ADR links; restore |
| M23 | `docs/ai/07_KNOWN_LIMITATIONS.md` | limitation | domain/development as classified | taxonomy | link check; restore |
| M24 | `docs/ai/08_DEFINITION_OF_DONE.md` | quality gate | constitution/AGENTS pointer | foundation | rule ownership; restore |
| M25 | `docs/ai/09_SECURITY_AND_DATA_SAFETY.md` | security | docs/security + constitution principle | foundation | safety review; restore |
| M26 | `docs/ai/10_DEEPSEEK_WORKFLOW.md` | adapter | retain through pilot, then classify | DeepSeek | smoke test; restore |
| M27 | `docs/ai/11_CONTEXT_PACK_RULES.md` | context guide | AGENTS/development pointer | foundation | agent smoke; restore |
| M28 | `docs/ai/12_REVIEW_CHECKLIST.md` | review guide | AGENTS/development/review | review | checklist check; restore |
| M29 | `docs/ai/audit/REPOSITORY_AUDIT_2026-07-10.md` | audit evidence | archive/reference | archive | inbound refs; restore |
| M30 | `docs/ai/audit/REPOSITORY_MANIFEST_2026-07-10.csv` | manifest evidence | archive/reference | archive | path check; restore |
| M31 | `docs/ai/bom/BOM-00-ICAPI-CAPABILITY-REPORT.md` | capability evidence | docs/domain or archive | taxonomy | link check; restore |
| M32 | `docs/ai/prompts/00_PROJECT_BOOTSTRAP.md` | prompt | adapter/reference | OpenCode | prompt audit; restore |
| M33 | `docs/ai/prompts/01_PLANNER.md` | prompt | adapter/reference | OpenCode | plan ownership; restore |
| M34 | `docs/ai/prompts/02_IMPLEMENTER.md` | prompt | adapter/reference | OpenCode | task ownership; restore |
| M35 | `docs/ai/prompts/03_REVIEWER.md` | prompt | adapter/reference | OpenCode | review ownership; restore |
| M36 | `docs/ai/prompts/04_VERIFIER.md` | prompt | adapter/reference | OpenCode | verification; restore |
| M37 | `docs/ai/prompts/05_FIX_REVIEW_FINDINGS.md` | prompt | adapter/reference | OpenCode | prompt audit; restore |
| M38 | `docs/ai/prompts/06_BLOCKER_REPORT.md` | prompt | adapter/reference | OpenCode | prompt audit; restore |
| M39 | `docs/ai/prompts/07_SESSION_HANDOFF.md` | prompt | adapter/reference | handoff if installed | no skill evidence; restore |
| M40 | `docs/ai/prompts/08_PR_DESCRIPTION.md` | prompt | GitHub workflow reference | issue tracker | link check; restore |
| M41 | `docs/ai/roadmap/AI_IMPLEMENTATION_ROADMAP.md` | roadmap | archive/reference | archive | link check; restore |
| M42 | `docs/ai/roadmap/DEPENDENCY_MAP.md` | dependency | docs/architecture/development | taxonomy | link check; restore |
| M43 | `docs/plans/2026-07-15-clone-package-round-trip-design.md` | historical plan | preserve or feature spec if active | pilot | traceability; restore |
| M44 | `docs/plans/2026-07-16-spec-kit-migration-technical-plan.md` | technical plan | this plan | plan review | diff check; restore |
| M45 | `docs/superpowers/plans/2026-07-13-sec-00-hotfix-config-regressions.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M46 | `docs/superpowers/plans/2026-07-14-pdm-readable-export-names.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M47 | `docs/superpowers/plans/2026-07-14-pdm-read-studycase.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M48 | `docs/superpowers/plans/2026-07-15-clone-package-round-trip.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M49 | `docs/superpowers/plans/2026-07-15-ironcad-executable-resolution.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M50 | `docs/superpowers/plans/2026-07-15-pdm-cad-launch-action.md` | historical plan | preserve/feature mapping | pilot | traceability; restore |
| M51 | `docs/superpowers/specs/2026-07-13-sec-00-hotfix-config-regressions-design.md` | historical spec | preserve/feature mapping | pilot | traceability; restore |
| M52 | `docs/superpowers/specs/2026-07-14-pdm-readable-export-names-design.md` | historical spec | preserve/feature mapping | pilot | traceability; restore |
| M53 | `docs/superpowers/specs/2026-07-14-pdm-read-studycase-design.md` | historical spec | preserve/feature mapping | pilot | traceability; restore |
| M54 | `docs/superpowers/specs/2026-07-15-ironcad-executable-resolution-design.md` | historical spec | preserve/feature mapping | pilot | traceability; restore |
| M55 | `docs/superpowers/specs/2026-07-15-pdm-cad-launch-action-design.md` | historical spec | preserve/feature mapping | pilot | traceability; restore |
| M56 | `docs/superpowers/specs/2026-07-16-spec-kit-workflow-migration-design.md` | current design | preserve as design evidence | design | diff check; restore |

### Pilot candidate matrix — Inferred, approval pending

| Candidate | Legacy spec | Legacy plan | Ticket/evidence | Source area | Test evidence | Active/historical | Representation | Risk | Score |
|---|---|---|---|---|---|---|---|---|---:|
| PDM CAD launch action | `docs/superpowers/specs/2026-07-15-pdm-cad-launch-action-design.md` | `docs/superpowers/plans/2026-07-15-pdm-cad-launch-action.md` | commits `2dfc5f2`, `edc316b` | Workspace/CAD launch | existing tests/review evidence to verify | active | high | medium | 5 |
| Clone package round trip | `docs/superpowers/specs/2026-07-15-clone-package-round-trip-design.md` | `docs/superpowers/plans/2026-07-15-clone-package-round-trip.md` | recent HEAD history | Workspace clone | evidence to verify | active | high | high | 4 |
| IronCAD executable resolution | `docs/superpowers/specs/2026-07-15-ironcad-executable-resolution-design.md` | `docs/superpowers/plans/2026-07-15-ironcad-executable-resolution.md` | recent plan | IronCAD | evidence to verify | active | medium | high | 3 |

Recommendation: PDM CAD launch action, pending user approval and exact reference
verification. No pilot artifact is created now.

### Pilot artifact mapping — Pending approval

| Legacy section/artifact | Destination | Transfer | Status |
|---|---|---|---|
| user-visible requirement | `spec.md` | content | pending |
| current design decisions | `plan.md` | content after review | pending |
| remaining implementation work | `tasks.md` | only open tasks | pending |
| research/evidence | `research.md` | content if needed | pending |
| legacy links/completion | traceability | link only | pending |
| completed work | historical | link only | pending |

### OpenCode file audit — Verified current role / Pending target role

| File | Current role | Reads | Writes | Target role | Action |
|---|---|---|---|---|---|
| `.opencode/agents/idea-planner.md` | planner | legacy docs/tickets | plan prompt | readiness checker | audit after pilot |
| `.opencode/agents/idea-implementer.md` | implementer | ticket/prompt | source | approved tasks/issue only | audit after pilot |
| `.opencode/agents/idea-reviewer.md` | reviewer | diff/review docs | review output | spec/issue review | keep through pilot |
| `.opencode/agents/idea-verifier.md` | verifier | build/test guide | evidence | verify evidence | keep through pilot |
| `.opencode/commands/ticket-plan.md` | planning command | legacy ticket | plan prompt | adapter | no competing plan |
| `.opencode/commands/ticket-implement.md` | implementation command | legacy ticket | source | approved artifact adapter | gate |
| `.opencode/commands/ticket-review.md` | review command | diff/ticket | review | quality adapter | gate |
| `.opencode/commands/ticket-verify.md` | verify command | ticket | evidence | verification adapter | gate |
| `.opencode/commands/ticket-status.md` | status command | ticket state | status | GitHub issue adapter | gate |
| `.opencode/commands/ticket-fix-review.md` | fix command | review | source | approved finding adapter | gate |
| `opencode.json` | instruction loader | `docs/ai/*` | none | canonical/adapter loader | update after pilot |

### GitHub Issues tooling — Verified blocker

| Check | Result | Blocker | Action |
|---|---|---|---|
| `gh --version` | command not found | CLI unavailable | install/configure only after approval |
| `gh auth status` | not executable | auth unknown | verify after CLI exists |
| `gh repo view` | not executable | repository access unknown | verify after CLI exists |
| `git remote -v` | GitHub remote verified | none | proceed only after gh gate |

Projection design must still define idempotency marker, task/issue links, split,
cancel, close/reopen, standalone bug/chore, open legacy ticket and completed evidence.

### Concrete commit sequence — Pending implementation

| Commit | Files added | Files moved | Files modified | Untouched | Verification | Rollback | Runtime |
|---|---|---|---|---|---|---|---|
| 1 | design, plan | none | two docs | source/tests | diff/check | revert docs commit | none |
| 2 | baseline evidence/index | none | approved docs | source/tests | Git/tool output | revert commit | none |
| 3 | `.specify/`, `specs/` | none | `.gitignore` only if approved | source/tests | init collision | revert init | none |
| 4 | constitution/AGENTS/CONTEXT | none | canonical docs | source/tests | ownership/link check | revert docs | none |
| 5 | domain/architecture/ADR docs | selected files | links | source/tests | reference check | revert moves | none |
| 6 | pilot artifacts | selected legacy docs | adapter paths | source/tests | analyze/pilot review | restore paths | none |
| 7 | issue projection | none | task links | source/tests | idempotency/issue check | close/reopen review | none |
| 8 | deprecation markers | none | legacy entry points | source/tests | no old path reads | restore adapter | none |

### Approval gates — Current state

| Gate | State | Evidence | Approver | If approved | If rejected |
|---|---|---|---|---|---|
| working-tree cleanup | pending | exact Git state | user | prepare branch | keep main untouched |
| migration branch | pending | clean/intentional diff | user | create branch | no implementation |
| integration resolution | blocked | catalog output absent | user | choose verified init | investigate CLI |
| specify init | blocked | collision table + integration | user | init on branch | no init |
| .gitignore | pending | check-ignore table | user | minimal unignore | keep unchanged |
| skill installation | pending | skill inventory/source | user | install/copy | keep workspace |
| constitution/AGENTS/CONTEXT | pending | ownership matrix | user | create docs | no files |
| documentation moves | pending | per-file map/links | user | move selected files | preserve legacy |
| pilot selection | pending | candidate matrix | user | create pilot artifacts | choose another |
| OpenCode adaptation | pending | file audit | user | edit adapters | keep transition |
| GitHub projection | blocked | gh/auth/idempotency | user | project reviewed tasks | no issues |
| deprecate/archive/delete | pending | references/rollback | user | retire one file/group | retain artifact |

### Verification matrix

| Phase | Git | Tracking | Docs/links | Agent | Spec Kit | Build/test | Rollback trigger |
|---|---|---|---|---|---|---|---|
| root/tooling | root/status/log/worktree | none | evidence file | root path check | CLI outputs | none | wrong root |
| tracking | diff/check-ignore | each canonical path | ignore rule review | instruction path | no init | none | canonical ignored |
| foundation | commit/diff/check | tracked docs | ownership/link check | source-order smoke | artifact lookup | baseline compare | contradiction |
| pilot | branch/diff | specs tracked | traceability/analyze | planner/agent smoke | analyze | no source regression | conflict |
| projection | issue links | tasks unchanged | task/issue refs | command smoke | tasks source | none | duplicate issue |
| retirement | inbound refs | archive state | no broken links | no old-path reads | artifacts intact | baseline preserved | reference break |

## Implementation readiness

**READY FOR PHASE 0 ONLY.** Repository root, worktree, Git state, installed skills,
tracking rules, collision paths và candidate pilot đã được khảo sát. Foundation
implementation chưa được thực hiện.

| Area | Readiness | Reason |
|---|---|---|
| Phase 0 investigation | **READY** | root/worktree/state/skills/tracking/collision/pilot đã có evidence |
| Foundation implementation | **BLOCKED** | chưa có approval và chưa hoàn tất Phase 0 |
| Spec Kit init | **BLOCKED** | integration catalog chưa được giải quyết |
| GitHub Issues projection | **BLOCKED** | GitHub CLI và authentication chưa có |
| Legacy retirement | **BLOCKED** | cần canonical replacements, reference check và approval |

`specify init` vẫn bị chặn cho tới khi integration catalog được giải quyết. GitHub
Issues projection bị chặn vì `gh` chưa có và authentication chưa được xác minh.
Build/test baseline vẫn phải được thiết lập trong Phase 0.

## First permitted action sequence

### Action 1 — Tạo migration branch

Sau user approval, xác nhận lại `git status --short --untracked-files=all`, bảo đảm
modified design document không mất, rồi mới chạy:

    git switch -c chore/spec-kit-workflow-migration

Không chạy reset, checkout file hoặc restore.

### Action 2 — Commit hai tài liệu được duyệt

Files:

    docs/superpowers/specs/2026-07-16-spec-kit-workflow-migration-design.md
    docs/plans/2026-07-16-spec-kit-migration-technical-plan.md

Technical plan đang bị `*.md` ignore; command dự kiến sau khi branch được tạo và user
approval tiếp theo:

    git add docs/superpowers/specs/2026-07-16-spec-kit-workflow-migration-design.md
    git add -f docs/plans/2026-07-16-spec-kit-migration-technical-plan.md
    git diff --cached

Không sửa `.gitignore` trong commit này. Commit message đề xuất:

    docs: finalize Spec Kit migration design and technical plan

Các command trên chưa được thực hiện trong bước cập nhật tài liệu này.

### Action 3 — Thực hiện Phase 0

Sau khi commit hai tài liệu: điều tra integration catalog bằng các command read-only,
thiết lập build/test baseline, ghi baseline evidence và chưa chạy `specify init`.

## Approval gates — final state

| Gate | Trạng thái |
|---|---|
| Tạo migration branch | Ready, cần user approval |
| Commit design và technical plan | Ready sau khi tạo branch |
| Phase 0 tooling investigation | Ready sau docs commit |
| Build/test baseline | Ready trong Phase 0 |
| Spec Kit integration resolution | Blocked, cần catalog output |
| `specify init` | Blocked |
| `.gitignore` migration | Blocked cho tới foundation approval |
| GitHub CLI installation | Blocked, cần approval riêng |
| GitHub Issues projection | Blocked |
| Pilot artifact creation | Blocked |
| Legacy deprecation/removal | Blocked |

## Prohibitions for this revision

- Do not run `specify init`.
- Do not create/switch a branch.
- Do not commit.
- Do not edit `.gitignore`, OpenCode, source or tests.
- Do not install/copy skills.
- Do not create `.specify/`, `specs/`, `AGENTS.md`, `CONTEXT.md` or issues.
- Do not move, archive, delete or migrate files.
