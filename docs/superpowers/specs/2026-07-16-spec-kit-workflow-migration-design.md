# Thiết kế migration workflow Spec Kit

## Mục tiêu

Chuyển `ArasPlugin` từ workflow AI tự xây dựng dựa trên `tasks/ai`, `.opencode` và
`docs/ai` sang Spec Kit làm workflow chính, đồng thời bảo toàn mã nguồn, lịch sử
Git, domain knowledge và khả năng build/test hiện tại.

## Phạm vi

Trong phạm vi:

- Chuẩn hóa project root tại `ArasPlugin/`.
- Thiết lập `CONTEXT.md`, `docs/adr/` và `.scratch/` theo quy ước của workspace.
- Lập bản đồ các spec, plan và ticket hiện có sang cấu trúc Spec Kit.
- Chuyển tài liệu hướng dẫn AI thành tài liệu hỗ trợ, không còn là workflow nguồn.
- Đánh dấu và loại bỏ dần các artifact legacy sau khi đã chuyển ngữ cảnh.
- Cập nhật hướng dẫn bắt đầu, lệnh và agent để không tạo thêm ticket ngoài Spec Kit.

Ngoài phạm vi:

- Di chuyển hoặc đổi tên `src/`, `tests/`, solution và project files.
- Refactor business code.
- Thay đổi behavior của sản phẩm.
- Xóa tài liệu cũ trước khi có bản đồ chuyển đổi và kiểm tra tham chiếu.

## Nguyên tắc nguồn sự thật

1. Source code, compile-time contracts và test là nguồn sự thật về behavior.
2. `CONTEXT.md` và `docs/adr/` là nguồn sự thật về thuật ngữ domain và quyết định
   kiến trúc.
3. `.scratch/<feature>/spec.md` là nguồn sự thật về yêu cầu của feature.
4. `.scratch/<feature>/issues/` là nguồn sự thật về các task triển khai.
5. `docs/plans/` và `docs/superpowers/plans/` chỉ giữ các kế hoạch lịch sử hoặc
   tham chiếu, không dùng để tạo task mới.
6. `docs/ai/` chỉ chứa quy tắc vận hành, context và tài liệu domain bổ trợ.

## Cấu trúc đích

```text
ArasPlugin/
├── CONTEXT.md
├── docs/
│   ├── adr/
│   ├── ai/
│   ├── domain/
│   ├── plans/
│   └── superpowers/
├── .scratch/
│   └── <feature-slug>/
│       ├── spec.md
│       └── issues/
│           └── NN-<ticket-slug>.md
├── .agents/skills/
├── .opencode/
├── src/
└── tests/
```

`.opencode/` được giữ trong giai đoạn chuyển tiếp vì DeepSeek đang sử dụng các
agent và command hiện có. Các command phải trỏ tới artifact Spec Kit; sau khi
workflow mới được kiểm chứng, các command trùng chức năng sẽ bị loại bỏ hoặc
chuyển thành wrapper mỏng.

## Bản đồ chuyển đổi

| Hiện tại | Đích | Cách xử lý |
|---|---|---|
| `tasks/ai/tickets/<id>.md` | `.scratch/<feature>/issues/NN-*.md` | Chuyển nội dung, giữ ID cũ trong metadata hoặc phần liên kết |
| `tasks/ai/BACKLOG.*` | `.scratch/<feature>/map.md` hoặc index domain | Chuyển các mục còn mở; không sao chép backlog đã hoàn tất |
| `docs/superpowers/specs/*` | `.scratch/<feature>/spec.md` | Gắn feature slug và lưu link tới tài liệu lịch sử |
| `docs/superpowers/plans/*` | `.scratch/<feature>/plan.md` hoặc `docs/plans/` | Chỉ chuyển kế hoạch còn hiệu lực |
| `docs/plans/*` | `docs/plans/` | Giữ làm lịch sử, thêm trạng thái và link tới spec |
| `docs/ai/00_START_HERE.md` | `AI_START_HERE.md` | Viết lại để chỉ dẫn workflow Spec Kit |
| `docs/ai/01_AI_RUNBOOK.md` và prompts | `docs/ai/` | Rút gọn, loại bỏ quy trình tạo ticket cũ |
| `.opencode/commands/ticket-*.md` | `.opencode/commands/` | Cập nhật thành wrapper hoặc đánh dấu legacy |
| `.superpowers/sdd` | `docs/ai/` hoặc bỏ sau kiểm chứng | Không để tồn tại workflow cạnh tranh |

Không thực hiện chuyển đổi hàng loạt bằng suy đoán. Mỗi feature được chuyển khi
đã xác định được spec, trạng thái và các ticket liên quan.

## Các phase thực hiện

### Phase 1 — Baseline và cấu trúc nền

- Ghi nhận trạng thái Git, build và test hiện tại.
- Tạo `CONTEXT.md`, `docs/adr/` và quy ước `.scratch/`.
- Tạo tài liệu chỉ mục migration, không xóa artifact nào.

### Phase 2 — Chuyển feature đang hoạt động

- Ưu tiên feature có spec/plan gần đây nhất và còn ticket liên quan.
- Tạo một feature directory trong `.scratch/`.
- Liên kết spec, plan, issue, source area và test evidence.
- Chạy kiểm tra liên kết tài liệu sau từng feature.

### Phase 3 — Cập nhật AI entry points

- Sửa `AI_START_HERE.md`, `DEEPSEEK.md` và `docs/ai/`.
- Cập nhật agent/command để đọc Spec Kit trước khi lập task.
- Thêm quy tắc không tạo ticket mới trong `tasks/ai/`.

### Phase 4 — Thu hồi legacy workflow

- Đánh dấu `tasks/ai/` và các command cũ là legacy trong thời gian chuyển tiếp.
- Xóa chỉ các file đã có artifact thay thế và không còn tham chiếu.
- Chạy build/test và code review sau mỗi nhóm thay đổi.

## Tiêu chí nghiệm thu

- Có một project root rõ ràng tại `ArasPlugin/`.
- Agent mới có thể tìm được context, ADR, spec và task mà không đọc nhiều nguồn
  workflow cạnh tranh.
- Không còn quy trình mới nào yêu cầu tạo ticket trong `tasks/ai/`.
- Mỗi artifact được chuyển có link ngược tới nguồn cũ hoặc ghi rõ lý do không cần
  chuyển.
- Không có file source/test nào bị thay đổi trong migration tài liệu.
- `dotnet build IdeaCadConnector.sln` và bộ test baseline được chạy, kết quả được
  ghi lại trước và sau migration.
- Sau từng nhóm thay đổi có review độc lập theo quy tắc của repository.

## Rủi ro và biện pháp giảm thiểu

- **Mất ngữ cảnh lịch sử:** giữ tài liệu cũ cho tới khi artifact mới có link và
  trạng thái rõ ràng.
- **Hai workflow cùng tồn tại:** cập nhật entry point trước, sau đó mới thu hồi
  command/ticket legacy.
- **Chuyển sai trạng thái ticket:** không suy luận trạng thái; lấy từ nội dung,
  Git history và test evidence.
- **Ảnh hưởng build:** tách migration tài liệu khỏi thay đổi code và chạy baseline
  trước khi commit.

