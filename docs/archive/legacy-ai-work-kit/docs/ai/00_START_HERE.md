# 00 — Start Here

## Mục tiêu

Bảo đảm mọi AI agent làm việc dựa trên source code hiện tại, ticket rõ ràng và bằng chứng build/test.

## Thứ tự đọc bắt buộc

1. `AI_START_HERE.md`
2. `DEEPSEEK.md`
3. `docs/ai/01_AI_RUNBOOK.md`
4. `docs/ai/02_PROJECT_STATE.md`
5. `docs/ai/03_ARCHITECTURE_RULES.md`
6. `docs/ai/04_ARAS_SCHEMA_MAP.md`
7. `docs/ai/05_TESTING_GUIDE.md`
8. Ticket đang làm trong `tasks/ai/tickets/`
9. Prompt vai trò trong `docs/ai/prompts/`

## Source of truth

Theo thứ tự ưu tiên:

1. Source code và compile-time contracts hiện tại.
2. Schema Aras đã xác nhận trên live/test server.
3. Test đang chạy.
4. `docs/ai/`.
5. Tài liệu cũ trong repo.
6. README và mockup.

Nếu README mâu thuẫn với code, code thắng. Nếu code đang giả định schema chưa được xác nhận, agent phải dừng.

## Chế độ làm việc

```text
Planner → Implementer → Reviewer → Verifier → Merge
```

Không dùng cùng một context session cho cả bốn vai trò khi có thể tránh.
