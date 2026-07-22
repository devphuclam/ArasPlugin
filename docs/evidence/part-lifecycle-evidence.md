# Part Lifecycle Evidence (GATE-A)

**Task**: T001

**Requirement**: Capture verified Part ItemType lifecycle state names, transitions, and semantic roles from the Aras environment.

## Status

**PASS for the bounded Feature 003 MVP scope - recorded from read-only live inspection and product-owner confirmation on 2026-07-20.** The MVP lifecycle intentionally ends at `Released`. The four in-scope state identities, semantic roles, and permitted transitions are retained below. States after `Released` remain present in Aras but are outside this feature and are not used by the MVP policy.

## Observed live configuration

- Part ItemType: `Part`
- Active lifecycle map: `Custom Part`
- Observed states: `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `Che tao`, `Nhan hang`, `In Change`, `Superseded`, `Obsolete`
- Product owner confirmed the MVP core path is `Khoi tao` -> `Thiet ke chi tiet` -> `In Review` -> `Released` and that `Released` is not edited directly; further design work starts as a new revision.

### Feature 003 lifecycle boundary

Feature 003 uses only this lifecycle segment:

`Khoi tao` -> `Thiet ke chi tiet` -> `In Review` -> `Released`

The accepted rework edge is `In Review` -> `Thiet ke chi tiet`. The states and transitions after `Released` (`In Change`, `Che tao`, `Nhan hang`, `Superseded`, `Obsolete`) are outside the MVP lifecycle contract and are not enabled or modeled by this feature.

### Bounded MVP semantic roles

| State | MVP role | Basis |
|---|---|---|
| `Khoi tao` | Initial working state | Live state identity and transition graph |
| `Thiet ke chi tiet` | Editable design state | Live state identity/transition graph and product-owner confirmation |
| `In Review` | Review state; direct design modification is not the normal review operation | Live state identity/transition graph and existing review workflow |
| `Released` | Released/read-only state; further design work requires a new revision | Live `set_is_released=1` flag and product-owner confirmation |

### Retained read-only OData evidence

The following requests were executed against the configured live environment on 2026-07-20 using a short-lived authenticated session. No add, update, delete, promote, version, checkout, check-in, or business Server Method operation was invoked.

1. `GET /server/odata/ItemType?$filter=name eq 'Part'&$select=id,name`
   - Result: `Part` ItemType id `4F1AC04A2B484F3ABA4E20DB63808A88`.
2. `GET /server/odata/ItemType('4F1AC04A2B484F3ABA4E20DB63808A88')/ItemType%20Life%20Cycle`
   - Result: the active Part lifecycle relationship resolves to `Custom Part`.
3. `GET /server/odata/Life%20Cycle%20Map('BD56E2EE6F6245AF926EF02C5FDE7334')/Life%20Cycle%20State?$select=id,name,sort_order,is_released,set_is_released,not_lockable,item_behavior`
   - Result: nine current states were returned. `set_is_released=1` was returned only for `Released`. The runtime `is_released` value also returned `1` for `Nhan hang`; this discrepancy is retained as an unresolved authority semantic and is not interpreted by the client.

| State name returned by Aras | State id | Sort order | `is_released` | `set_is_released` | `not_lockable` |
|---|---|---:|---:|---:|---:|
| `Khoi tao` | `EC7313A7F7984309B308EBCC08D9A2F7` | 1 | 0 | 0 | 0 |
| `Thiet ke chi tiet` | `E388A74C4B1946359E78BB2684D40BDE` | 2 | 0 | 0 | 0 |
| `In Review` | `5010183127F74376B8588F528602F5E8` | 3 | 0 | 0 | 0 |
| `Released` | `3D0D40076E0B407CAA0DA5ED9AD524E3` | 4 | 1 | 1 | 0 |
| `Che tao` | `AE17BAF432C8431EBC65872741599BC8` | 5 | 0 | 0 | 0 |
| `Nhan hang` | `E5DCF4685F5B4A179E63EF1F2F14965C` | 6 | 1 | 0 | 0 |
| `In Change` | `C89E580006CE4D88BCBFCD678B0BC15A` | 7 | 0 | 0 | 0 |
| `Superseded` | `90F3E88296754408AA01821E1D3759B8` | 8 | 0 | 0 | 0 |
| `Obsolete` | `9242DBCAA57440E596385D05B3F46F7F` | 9 | 0 | 0 | 0 |

4. `GET /server/odata/Life%20Cycle%20Map('BD56E2EE6F6245AF926EF02C5FDE7334')/Life%20Cycle%20Transition?$select=id,from_state,to_state,sort_order,execute_post_in_main_txn,get_comment`
   - Result: these edges were returned from the active map:

| From state | To state |
|---|---|
| `Khoi tao` | `Thiet ke chi tiet` |
| `Thiet ke chi tiet` | `In Review` |
| `In Review` | `Released` |
| `In Review` | `Thiet ke chi tiet` |
| `Released` | `In Change` |
| `Released` | `Che tao` |
| `Released` | `Superseded` |
| `Che tao` | `Nhan hang` |
| `Che tao` | `Obsolete` |
| `Che tao` | `Released` |
| `Nhan hang` | `Obsolete` |
| `Nhan hang` | `Released` |
| `In Change` | `Superseded` |
| `In Change` | `Thiet ke chi tiet` |
| `Superseded` | `Obsolete` |

5. Permission metadata was queried read-only for the lifecycle map permission (`Life Cycle Map`) and the Part ItemType permission (`ItemType`). The lifecycle state records point to the map-level permission, not to distinct state permissions. Sampled map-level access rows include `Administrators`, `Creator`, and `World`, but this does not establish state-specific edit/review authorization for the Part business item. No permission claim is made from this metadata alone.

The queries prove the active Part vocabulary and retain the in-scope transition graph. The editable/read-only semantic roles above additionally rely on product-owner confirmation. The client must not generalize these roles to post-`Released` states.

## Checklist

- [x] Part ItemType name in Aras: `Part`
- [x] Lifecycle map name for Part: `Custom Part`
- [x] State display names captured from the active live map
- [x] Internal authority state identities captured as Aras `id` + `name` pairs for the four MVP states
- [x] Lifecycle flags captured: `Released` has `set_is_released=1`; `Nhan hang` also returned `is_released=1` while `set_is_released=0`
- [x] Semantic role captured for each MVP state: initial, editable design, review, and released/read-only
- [x] In-scope transition edges captured from the active `Life Cycle Transition` collection
- [x] MVP semantic roles recorded for the four-state scope; post-`Released` roles intentionally excluded
- [ ] State-specific permission behavior beyond the accepted MVP semantic roles verified by an authorized transition/edit test
- [x] Read-only OData query evidence of the lifecycle map configuration
- [x] Date recorded: 2026-07-20; environment: configured live Aras Innovator instance

## Current conclusion

The live Part map and product-owner confirmation establish the bounded MVP vocabulary and path through `Released`. The application must still keep Part and CAD policies separate, because the maps have additional states and may diverge later. T001 is complete for Feature 003's four-state scope; post-`Released` actions remain outside this feature.

## Template: State Entry

| Display Name | Internal Name | Semantic Role | Editable | Reviewable | Releasable | Released |
|---|---|---|---|---|---|---|
| (from Aras) | (from Aras) | (e.g. design, review, released) | Yes/No | Yes/No | Yes/No | Yes/No |

**Note**: These state names are separately mapped from CAD lifecycle states per ADR-0009, even though the current IDEA profile uses the same core path.
