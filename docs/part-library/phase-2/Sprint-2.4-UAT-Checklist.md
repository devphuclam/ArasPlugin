# Sprint 2.4 UAT Checklist

## Build Verification

- [ ] Debug build: 0 warnings, 0 errors
- [ ] Release build: 0 warnings, 0 errors
- [ ] Full tests: 403/403 passed

## Filter Functionality

### Entry Status Filter
- [ ] Filter ComboBox shows "All Entry Statuses", "Draft", "Pending Review", "Published", "Deprecated"
- [ ] "All Entry Statuses" shows all entries
- [ ] "Draft" shows only entries with `EntryStatus == Draft`
- [ ] "Pending Review" shows only entries with `EntryStatus == PendingReview`
- [ ] "Published" shows only entries with `EntryStatus == Published`
- [ ] "Deprecated" shows only entries with `EntryStatus == Deprecated`
- [ ] Switching filter re-filters immediately

### CAD Status Filter
- [ ] Filter ComboBox shows "All CAD Statuses", "Available", "No CAD", "No native file", "CAD lookup unavailable"
- [ ] "All CAD Statuses" shows all entries
- [ ] "Available" shows only entries with an existing CAD that has a native file
- [ ] "No CAD" shows only entries without any primary CAD
- [ ] "No native file" shows only entries with a CAD but no native file on Aras
- [ ] "CAD lookup unavailable" shows only entries where CAD status was not resolved
- [ ] Switching filter re-filters immediately

### Text Search
- [ ] Search text box filters by item_number substring
- [ ] Search text box filters by name substring
- [ ] Clearing text shows all entries matching other active filters
- [ ] Search works in combination with Entry Status filter
- [ ] Search works in combination with CAD Status filter

### Archived Libraries
- [ ] Archived Libraries hidden by default
- [ ] Archived entries do not appear in any filtered view

## Sort Functionality

- [ ] Sort By ComboBox shows: Item Number, Name, Entry Status, Revision Policy, CAD Status, Usage Count, Last Used On
- [ ] Sort Direction ComboBox shows: Ascending, Descending
- [ ] Default: Item Number Ascending
- [ ] Item Number Ascending sorts correctly
- [ ] Item Number Descending sorts correctly
- [ ] Name Ascending sorts correctly
- [ ] Name Descending sorts correctly
- [ ] Entry Status sorts by status ordinal
- [ ] Revision Policy sorts alphabetically
- [ ] CAD Status sorts by status ordinal
- [ ] Usage Count Descending sorts correctly (null counts come last)
- [ ] Usage Count Ascending sorts correctly (null counts come last)
- [ ] Last Used On sorts chronologically (no-op when data unavailable)
- [ ] Switching sort column re-sorts immediately
- [ ] Switching sort direction re-sorts immediately

## Detail Status UX Hardening

- [ ] Loading state shows "Loading details..." message while detail tabs are fetching
- [ ] Permission denied shows clear diagnostic message
- [ ] Server unavailable shows: "Server unavailable. Check connection and retry."
- [ ] Operation cancelled shows: "Operation was cancelled."
- [ ] Empty states: No CAD / No BOM / No Revisions / No Where Used show appropriate messages
- [ ] Error state shows formatted error: "Failed to load details: {message}"

## Command State Hardening

### NVTKC (contributor — Nhân viên thiết kế cơ)
- [ ] Cannot Move Entry
- [ ] Cannot Pin Revision
- [ ] Can view Revision Browser
- [ ] Can view Library

### TNTKC (reviewer — Trưởng nhóm thiết kế cơ)
- [ ] Can Move Entry
- [ ] Can Pin Revision
- [ ] Can view Revision Browser
- [ ] Can view Library

### TPTKC (manager — Trưởng phòng thiết kế cơ)
- [ ] Can Move Entry
- [ ] Can Pin Revision
- [ ] Can manage Libraries
- [ ] Can view Revision Browser

### NVLCR (assembly viewer — Nhân viên lắp ráp cơ)
- [ ] View-only Library
- [ ] Cannot Add/Move/Pin/Admin

### PM (project viewer — Quản lý dự án)
- [ ] View-only Library
- [ ] Cannot Add/Move/Pin/Admin

### Khách hàng (external viewer)
- [ ] Move Entry blocked
- [ ] Revision Browser hidden
- [ ] Library view-only

## Localization

- [ ] All 25 new keys display in en-US
- [ ] All 25 new keys display in vi-VN
- [ ] All 25 new keys display in ja-JP
- [ ] No missing translation fallback to key name

## Regression

- [ ] Login/logout works
- [ ] Part search works
- [ ] Checkout/check-in/cancel checkout/read-only open works
- [ ] Native .ics upload/download works
- [ ] PDM analyze, branch, commit, clone, preview, push all work
- [ ] Root Assembly CAD gating and current revision behavior unchanged
- [ ] Existing Phase 1 save/reuse flows not broken
- [ ] CAD tab loads
- [ ] BOM tab loads
- [ ] Revisions tab loads
- [ ] Where Used tab loads
- [ ] Open in Aras works
- [ ] Download CAD works
- [ ] Open in IronCAD works

## Tester

| Date | Tester | Result | Notes |
|---|---|---|---|
| | | | |
