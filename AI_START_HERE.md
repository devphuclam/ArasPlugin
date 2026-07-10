# IDEA CAD Connector — AI Work Kit

Bộ này tổ chức công việc để AI coding agent làm dự án theo từng ticket nhỏ, có review và kiểm chứng độc lập.

## Vị trí đúng sau khi cài

File này phải nằm tại:

```text
ARAS-Plugin/
└── IdeaCadConnector/
    ├── AI_START_HERE.md
    ├── DEEPSEEK.md
    ├── IdeaCadConnector.sln
    ├── docs/ai/
    ├── tasks/ai/
    └── scripts/ai/
```

Nếu bạn thấy đường dẫn `IdeaCadConnector/IdeaCadConnector/AI_START_HERE.md`, bạn đã giải nén sai một cấp.

## Cài lần đầu

Mở PowerShell tại thư mục `ARAS-Plugin\IdeaCadConnector`, sau đó chạy:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\ai\Initialize-AiWorkKit.ps1
```

Script chỉ:

- kiểm tra đang đứng đúng repo;
- thêm khối unignore cần thiết vào `.gitignore`;
- tạo `.ai-work` để lưu context tạm;
- không sửa source code;
- không tự commit.

Sau đó review và commit riêng bộ AI Work Kit:

```powershell
git status --short
git add AI_START_HERE.md DEEPSEEK.md .gitignore.ai-workkit-snippet docs/ai tasks/ai scripts/ai .github .gitignore
git commit -m "chore: add AI development work kit"
```

Nếu repo đang có thay đổi code chưa commit, **không chạy AI sửa tiếp**. Đọc ticket `BASE-00` trước.

## Thứ tự bắt đầu

1. `tasks/ai/tickets/BASE-00-clean-baseline.md`
2. `tasks/ai/tickets/BASE-01-build-baseline.md`
3. `tasks/ai/tickets/BASE-02-test-baseline.md`
4. `tasks/ai/tickets/BASE-04-aras-schema-map.md`
5. Chỉ sau đó mới bắt đầu `DOC-01`.

## Bắt đầu một ticket

```powershell
.\scripts\ai\Start-AiTicket.ps1 -TicketId BASE-00
```

Script sẽ:

- từ chối chạy nếu working tree không sạch;
- tìm đúng ticket;
- tạo branch `ai/<ticket-id>-...`;
- tạo prompt tại `.ai-work/current-prompt.md`;
- in hướng dẫn tiếp theo.

## Quy tắc quan trọng nhất

- Một ticket = một branch = một PR.
- Không giao cả Epic cho một agent.
- AI viết code không được tự review và tự xác nhận hoàn thành.
- Không cho AI đoán schema Aras.
- Không merge nếu không có build/test output.
- Không sửa Pull, Branch và Document Vault trong cùng PR.

Đọc tiếp: `DEEPSEEK.md` và `docs/ai/00_START_HERE.md`.
