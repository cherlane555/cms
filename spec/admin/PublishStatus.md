# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

## Summary

`PublishStatus` is a small **lookup / status table** that enumerates the publishing lifecycle
states an item can be in (draft, published, discontinued). It is referenced by `Course`
(`Course.PublishStatus_pkid` → `PublishStatus.pkid`) and is expected to back other content
entities as they are built. It has **no foreign keys and no N-N relationships**.

> **Non-obvious:** the primary key `pkid` is `tinyint NOT NULL` and is **NOT an IDENTITY column**.
> Unlike every other table in this system (where `pkid` is an IDENTITY surrogate), here the code is
> **user-assigned** on create — it must be supplied in the INSERT, is editable in the create form,
> disabled in the edit form, and checked for a duplicate (409) on create.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint, user-assigned (NOT IDENTITY)** → C# `byte` |
| Foreign Keys | None |
| Required Fields | `pkid` (user-assigned), `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid` references this table (Course feature not yet built — see note) |
| Query Filters | keyword (Description), IsDraft / IsPublished / IsDiscontinued tri-state bools |
| Default Sort | `pkid ASC` (no DisplayOrder column; the tinyint code is the natural order) |

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼（草稿／已發布／已停用）

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態描述
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已停用

---

## Required Fields

Required (NOT NULL — all columns):
- `pkid` — user-assigned tinyint code (required in **new** mode; disabled/read-only in **edit** mode)
- `Description`
- `IsDraft` (bit, defaults false)
- `IsPublished` (bit, defaults false)
- `IsDiscontinued` (bit, defaults false)

Optional (nullable): none.

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns — no outbound navigation.

**N/A**

---

## Primary-Foreign Links

`Course.PublishStatus_pkid` references `PublishStatus.pkid` (`FK_Course_PublishStatus`). In principle
the list/detail could link to `/courses?publishStatusPkid={pkid}`.

**Deferred:** the Course feature is not built yet (only AppRole and now PublishStatus exist). Emitting
a nav button to a non-existent `/courses` route would be a dead link, so **no Primary-Foreign link
button is generated in this run.** Add it when the Course feature lands.

---

## N-N Relationships

No junction tables reference `PublishStatus`.

**N/A**

---

## Query Filters

`POST /api/publish-statuses/query` accepts:

- **keyword**: string — LIKE on `Description` (the only string column).
- **IsDraft**: bool? — exact match on `IsDraft`. Tri-state: null = no filter, true = 是, false = 否.
- **IsPublished**: bool? — exact match on `IsPublished`. Tri-state.
- **IsDiscontinued**: bool? — exact match on `IsDiscontinued`. Tri-state.

No FK filters (no FKs). No date-range filters (no date columns).

---

## Lookup Endpoints Required

`PublishStatus` is itself an FK target (used by `Course`), so it needs a slim lookup endpoint for
future features' FK dropdowns. This run **creates** it.

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** | `{ pkid, description }[]` ordered by `pkid ASC` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publish-statuses/{id}` | Get by pkid (tinyint) |
| `POST` | `/api/publish-statuses` | Create (pkid supplied in body; 409 if it already exists) |
| `PUT` | `/api/publish-statuses` | Update (pkid from body) |
| `DELETE` | `/api/publish-statuses/{id}` | Delete |
| `GET` | `/api/lookups/publish-statuses` | Slim lookup list (new) |

No auth exceptions.

---

## Backend Notes

### Models

```csharp
public class PublishStatus
{
    public byte Pkid { get; set; }            // tinyint PK, user-assigned (NOT IDENTITY)
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

public class PublishStatusRequest
{
    public byte Pkid { get; set; }            // supplied on create (it's the PK), immutable on update
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

public class PublishStatusQuery
{
    public string? Keyword { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
```

Lookup projection:

```csharp
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### SQL — SELECT

```sql
SELECT s.pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued
FROM PublishStatus s
-- list/query: ORDER BY s.pkid ASC ; get-by-id: WHERE s.pkid = @Pkid
```

No JOINs (no FKs), no `RTRIM()` (no `nchar` columns), no multi-map.

### SQL — INSERT

`pkid` **is** written (user-assigned PK), so there is **no `SCOPE_IDENTITY()`**:

```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

### SQL — UPDATE

`pkid` is the immutable key (WHERE only):

```sql
UPDATE PublishStatus
SET Description = @Description, IsDraft = @IsDraft,
    IsPublished = @IsPublished, IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM PublishStatus WHERE pkid = @Pkid;
```

### Repository

`IPublishStatusRepository` + `PublishStatusRepository` (Dapper, `IDbConnectionFactory`). Methods take
`byte pkid`. No transaction needed (no junction rows). `ExistsAsync(byte)` backs the create 409 check.

### Controller

`PublishStatusesController`, route `api/publish-statuses`. `PUT` takes pkid from the body.
Create validation (→ 400 `ValidationProblem`): `Pkid` must be non-zero **and** `Description` non-empty;
then 409 `Conflict` if `ExistsAsync(pkid)`. Numeric PK → route `{id}` binds to `byte` (no `:int`
constraint needed; no `encodeURIComponent` on the client).

### Special Column Notes

- **`pkid` is a user-assigned `tinyint` PK (NOT IDENTITY)** — the single most important deviation from
  the AppRole reference. Insert includes it; no `SCOPE_IDENTITY()`; create checks for duplicates.
- No `nchar`, `DateOnly`, `TimeOnly`, or computed columns.
- No RowAudit (the AppRole reference feature does not use it; not wired in this codebase).

---

## Frontend Notes

### Model (`publish-status.model.ts`)

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}
export interface PublishStatusRequest { /* same shape */ }
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
export interface PublishStatusLookup { pkid: number; description: string; }
```

### Route Table (`app.routes.ts`)

| Path | Component |
|------|-----------|
| `publish-statuses` | `PublishStatusList` |
| `publish-statuses/new` | `PublishStatusForm` (before `:id`) |
| `publish-statuses/:id` | `PublishStatusDetail` |
| `publish-statuses/:id/edit` | `PublishStatusForm` |

### List component

- Columns: 主代碼 (pkid), 狀態描述 (description), 草稿 / 已發布 / 已停用 (bool → 是/否 `p-tag`), 操作.
- Sort default `pkid ASC`; sessionStorage keys `publish-status-list-{filters,sort,page}`.
- Filter drawer: keyword `pInputText`; three tri-state `p-select`s (不限 / 是 / 否) with `appendTo="body"`.
- `confirmDelete`: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？`

### Detail component

- Simple `getById` (no lookups / no forkJoin — no FKs). Shows all five fields; bools as 是/否 `p-tag`.
- No Primary-Foreign link buttons (Course feature not built — see Primary-Foreign Links).

### Form component

- Reactive Forms. `pkid` `p-inputNumber` (required, min 1) — **editable in new mode, disabled in edit**
  (`getRawValue()` still includes the disabled PK). `description` `pInputText` (required). Three bool
  `p-checkbox [binary]="true"`. Branch create vs update into separate `.subscribe()` calls. 409 → 「主代碼已存在」.

### Service (`publish-status.service.ts`)

Standard six methods against `/api/publish-statuses`. PK is numeric — **no `encodeURIComponent`**.
`getById(pkid: number)`, `delete(pkid: number)`.

### Sidebar placement

Existing group **系統管理 Admin** (already in `app.ts` `navGroups`). Add
`{ label: '發布狀態 PublishStatus', route: '/publish-statuses' }` after the AppRole entry.

---

## Files to Create / Modify

### Backend (`src/CMS.API`)
| File | Action |
|------|--------|
| `Models/PublishStatus.cs` | create |
| `Models/PublishStatusRequest.cs` | create |
| `Models/PublishStatusQuery.cs` | create |
| `Models/PublishStatusLookup.cs` | create |
| `Repositories/IPublishStatusRepository.cs` | create |
| `Repositories/PublishStatusRepository.cs` | create |
| `Controllers/PublishStatusesController.cs` | create |
| `Repositories/ILookupRepository.cs` | modify (add `GetPublishStatusesAsync`) |
| `Repositories/LookupRepository.cs` | modify (impl) |
| `Controllers/LookupsController.cs` | modify (add `publish-statuses` endpoint) |
| `Program.cs` | modify (register `IPublishStatusRepository`) |

### Frontend (`src/CMS.NG`)
| File | Action |
|------|--------|
| `core/models/publish-status.model.ts` | create |
| `core/services/publish-status.service.ts` | create |
| `core/services/lookup.service.ts` | modify (add `getPublishStatuses`) |
| `features/publish-statuses/publish-status-list/*` (ts/html/scss) | create |
| `features/publish-statuses/publish-status-detail/*` (ts/html/scss) | create |
| `features/publish-statuses/publish-status-form/*` (ts/html/scss) | create |
| `app.routes.ts` | modify (4 routes) |
| `app.ts` / `app.html` | modify (sidebar entry) |

### Tests
| File | Action |
|------|--------|
| `CMS.API.Tests/PublishStatusesControllerTests.cs` | create (list, query, get found/404, create new/409/400, update ok/404/400, delete ok/404) |
| `core/services/publish-status.service.spec.ts` | create |
| `core/services/lookup.service.spec.ts` | modify (add `getPublishStatuses` test) |
| `features/publish-statuses/*/publish-status-{list,detail,form}.spec.ts` | create |
