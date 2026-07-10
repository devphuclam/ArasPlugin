# Dùng DeepSeek để làm repo này

## Cách khuyến nghị

Dùng **DeepSeek API làm model backend cho một coding agent có quyền đọc/sửa file và chạy terminal**. Bộ này cung cấp script cho Claude Code vì DeepSeek có hướng dẫn tích hợp chính thức qua Anthropic-compatible API.

Web chat thông thường chỉ phù hợp để phân tích hoặc review đoạn code bạn tải lên. Nó không tự truy cập toàn bộ folder, không tự chạy build và không tự tạo commit trên máy bạn.

## Chuẩn bị

Cần có:

- Windows PowerShell;
- Git for Windows;
- Node.js 18 trở lên;
- Claude Code CLI;
- DeepSeek API key.

Cài Claude Code:

```powershell
npm install -g @anthropic-ai/claude-code
claude --version
```

Không ghi API key vào repo, `.env`, prompt, ảnh chụp hoặc log.

## Khởi động DeepSeek coding agent

Đứng tại `ARAS-Plugin\IdeaCadConnector`, chạy:

```powershell
.\scripts\ai\Start-DeepSeekClaudeCode.ps1
```

Script sẽ hỏi API key ở dạng secure prompt, đặt biến môi trường **chỉ cho process hiện tại**, rồi mở coding agent tại repo.

## Luồng làm một ticket

### Bước 1 — Tạo branch và prompt

```powershell
.\scripts\ai\Start-AiTicket.ps1 -TicketId DOC-01
```

### Bước 2 — Mở agent

```powershell
.\scripts\ai\Start-DeepSeekClaudeCode.ps1
```

Trong agent, gửi:

```text
Read .ai-work/current-prompt.md and follow it exactly.
Start in PLANNER mode. Do not edit code until the plan is approved.
```

### Bước 3 — Duyệt kế hoạch

Không trả lời “cứ làm đi” ngay. Kiểm tra:

- file dự kiến sửa có đúng scope không;
- có tự đặt tên property/ItemType Aras không;
- có sửa hơn 15 file không;
- có gộp nhiều feature vào một ticket không;
- test nào sẽ chứng minh behavior.

Sau khi ổn, gửi:

```text
Plan approved. Switch to IMPLEMENTER mode. Implement only the approved plan, add tests, then run verification. Stop on schema uncertainty or destructive risk.
```

### Bước 4 — Review bằng phiên agent mới

Đóng session implementer. Mở session mới và dùng prompt:

```text
Read docs/ai/prompts/03_REVIEWER.md, the ticket, and the complete git diff. Do not modify code. Return findings classified BLOCKER/HIGH/MEDIUM/LOW.
```

### Bước 5 — Verify

```powershell
.\scripts\ai\Verify-AiTicket.ps1 -TicketId DOC-01
```

Lưu output build/test vào PR.

## Chọn model

Dùng model reasoning/pro cho Planner, thiết kế, Aras workflow, Pull và Branch. Dùng model flash cho tác vụ nhỏ như test bổ sung, format hoặc review đơn giản. Script mặc định chọn model pro và không lưu key.

Tên model/API có thể thay đổi theo DeepSeek. Khi script báo model không hợp lệ, kiểm tra tài liệu DeepSeek hiện hành rồi chỉ sửa các biến model trong script; không sửa source app.

## Khi DeepSeek phải dừng

Bắt nó trả `BLOCKED` và không sửa code nếu:

- chưa xác nhận relationship gắn File vào Document;
- cần tạo/chỉnh ItemType hoặc property ngoài ticket;
- working tree không sạch;
- build baseline đang lỗi chưa phân loại;
- cần xóa hoặc ghi đè dữ liệu local;
- cần gọi API Aras/IronCAD không tồn tại trong code/docs;
- phải sửa ngoài hai module chính;
- acceptance criteria không thể kiểm chứng.


## Tài liệu chính thức tham khảo

- DeepSeek API quick start: `https://api-docs.deepseek.com/`
- DeepSeek integration với Claude Code: `https://api-docs.deepseek.com/quick_start/agent_integrations/claude_code/`

Các tích hợp coding agent là công cụ bên thứ ba; luôn review quyền truy cập filesystem/terminal và chính sách dữ liệu của công ty trước khi dùng.
