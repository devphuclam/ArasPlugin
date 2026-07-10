# 01 — AI Runbook

## Hard rules

1. Một ticket, một branch, một PR.
2. Không sửa ngoài phạm vi ticket.
3. Không broad refactor trong feature ticket.
4. Không đổi tên public contract nếu ticket không yêu cầu.
5. Không đoán ItemType, RelationshipType, property, lifecycle hoặc permission Aras.
6. Không tạo API IronCAD giả.
7. Không sửa binary, `.dll`, `.snk`, ảnh, build output hoặc `.vs`.
8. Không ghi password, token, API key hoặc file content nhạy cảm vào log.
9. Phải truyền `CancellationToken` xuyên suốt async path mới.
10. Không trả `Success = true` khi một bước bắt buộc thất bại.
11. Không cập nhật manifest/head commit trước khi operation hoàn tất nguyên tử.
12. Không ghi đè file local có thay đổi mà chưa có backup hoặc quyết định conflict.
13. Không tuyên bố Done nếu build/test chưa chạy; phải ghi `NOT VERIFIED`.
14. Không sửa test chỉ để hợp thức hóa behavior sai.
15. Mọi TODO mới phải có ticket follow-up.

## Giới hạn scope

Agent phải dừng và đề xuất chia nhỏ nếu:

- dự kiến sửa hơn 15 file;
- chạm hơn hai module chính;
- vừa đổi schema, backend, workspace và UI trong cùng ticket;
- thay đổi hơn một public workflow;
- cần migration nhưng ticket không có migration plan.

## Báo cáo cuối bắt buộc

- Behavior before.
- Behavior after.
- Files changed.
- Commands executed.
- Build result.
- Test result.
- Acceptance criteria mapping.
- Known limitations.
- Schema/manual steps.
- Follow-up tickets.
