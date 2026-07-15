# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

## Summary

`CourseGroup` is a small **lookup / grouping table** in the course sub-system. It holds the course
group categories (課程群組) that courses are filed under. It is the narrowest table scaffolded so far —
a surrogate PK plus a single description column, with **no foreign keys and no N-N relationships**.

It is scaffolded **before `Course`** because `Course.CourseGroup_pkid` references it, so the parent and
its lookup endpoint must exist before the Course feature is generated.

> **Non-obvious (important):** `FK_Course_CourseGroup` is declared **`ON DELETE CASCADE`**. Deleting a
> `CourseGroup` row will **silently delete every `Course` filed under it** — this is enforced by the DB,
> not by application code. See [Delete Cascade Warning](#delete-cascade-warning) below.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY** (DB-assigned surrogate) → C# `short` |
| Foreign Keys | None |
| Required Fields | `Description` |
| N-N Relationships | N/A (`PartnerCourseGroup` is a payload-bearing entity, not a pure junction) |
| Primary-Foreign Links | `Course`, `PartnerCourseGroup` reference `CourseGroup` (neither built yet — deferred) |
| Query Filters | keyword (Description) |
| Default Sort | `Description ASC` (no `DisplayOrder` column — see note) |

**Non-obvious decision — default sort.** The convention prefers `DisplayOrder ASC`, then `pkid DESC`.
`CourseGroup` has no `DisplayOrder` and no date column, and its `pkid` is a meaningless IDENTITY
surrogate, so `pkid DESC` (newest-first) would be arbitrary for a small reference table. Sorting by
`Description ASC` matches how the existing `AppUser` lookup falls back to its label column
(`ORDER BY UserName ASC`). `PublishStatus` set the precedent for choosing the natural order per table
rather than applying `pkid DESC` blindly.

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程所屬群組分類

### Chinese Column Names

- pkid: 主代碼
- Description: 群組描述

---

## Required Fields

Required (NOT NULL):
- `Description` (nvarchar 100)

Optional (nullable): none.

`pkid` is IDENTITY — DB-assigned, excluded from INSERT and from the create form.

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns — no outbound navigation.

**N/A**

---

## Primary-Foreign Links

These tables reference `CourseGroup.pkid`:
- `Course.CourseGroup_pkid` (`FK_Course_CourseGroup`, **ON DELETE CASCADE**, nullable FK)
- `PartnerCourseGroup.CourseGroup_pkid` (`FK_PartnerCourseGroup_CourseGroup`, no cascade)

**Deferred:** neither feature is built yet (only AppRole, PublishStatus, and Partner exist). Emitting
nav buttons to `/courses` or `/partner-course-groups` would be dead links, so **no Primary-Foreign link
buttons are generated in this run.** Add them when those features land — this follows the same
precedent set by `spec/course/Partner.md` and `spec/admin/PublishStatus.md`.

---

## N-N Relationships

No pure junction table (exactly two FK columns and nothing else) links `CourseGroup`.
`PartnerCourseGroup` has two FK columns but also carries its own `pkid`, `DisplayOrder`, and
`Description`, so it is an association entity managed on its own — **not** an n-n multi-select on the
CourseGroup form. This is the same ruling `spec/course/Partner.md` made for the same table.

**N/A**

---

## Query Filters

`POST /api/course-groups/query` accepts:

- **keyword**: string — LIKE on `Description` (the only string column).

No FK filters (no FKs), no bool filters (no bit columns), no date filters (no date columns).

---

## Lookup Endpoints Required

`CourseGroup` is an FK target for `Course` and `PartnerCourseGroup`, so it needs a slim lookup
endpoint. This run **creates** it.

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** | `{ pkid, description }[]` ordered by `Description ASC` |

> **Route naming note.** The lab handout refers to this endpoint as `GET /api/Lookups/CourseGroup`.
> The established convention in this codebase is **kebab-case plural** (`/api/lookups/app-users`,
> `/api/lookups/publish-statuses`, `/api/lookups/partners`), so this spec uses
> `/api/lookups/course-groups`. ASP.NET routing is case-insensitive, but the segment name still has to
> match — `/api/Lookups/course-groups` resolves, `/api/Lookups/CourseGroup` does not.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/course-groups/{id}` | Get by pkid (smallint) |
| `POST` | `/api/course-groups` | Create (pkid is IDENTITY — assigned by DB) |
| `PUT` | `/api/course-groups` | Update (pkid from body) |
| `DELETE` | `/api/course-groups/{id}` | Delete (**cascades to Course** — see warning) |
| `GET` | `/api/lookups/course-groups` | Slim lookup list (new) |

No auth exceptions.

---

## Backend Notes

### Models

```csharp
public class CourseGroup
{
    public short Pkid { get; set; }            // smallint IDENTITY
    public string Description { get; set; } = string.Empty;
}

public class CourseGroupRequest
{
    public short Pkid { get; set; }            // 0 on create (IDENTITY), the key on update
    public string Description { get; set; } = string.Empty;
}

public class CourseGroupQuery
{
    public string? Keyword { get; set; }
}
```

Lookup projection:

```csharp
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### SQL — SELECT

```sql
SELECT g.pkid, g.Description
FROM CourseGroup g
-- list/query: ORDER BY g.Description ASC ; get-by-id: WHERE g.pkid = @Pkid
```

No JOINs (no FKs), no `RTRIM()` (no `nchar` columns), no multi-map.

### SQL — INSERT

`pkid` is IDENTITY — excluded; read it back via `SCOPE_IDENTITY()`:

```sql
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

```sql
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid;
```

### Delete Cascade Warning

`FK_Course_CourseGroup` is `ON DELETE CASCADE`. Deleting a `CourseGroup` **deletes every `Course` row
referencing it**, at the database level. `Course.CourseGroup_pkid` is nullable, but the constraint is
CASCADE (not SET NULL), so the child rows are removed rather than unlinked.

`FK_PartnerCourseGroup_CourseGroup` has **no** cascade, so a `CourseGroup` still referenced by
`PartnerCourseGroup` will fail to delete with `SqlException` 547 (FK violation) → surfaces as a 500.

**Scope decision for this run:** neither behaviour is special-cased in the repository. The existing
`Partner` / `PublishStatus` repositories do not catch 547 either, and adding cascade-guard logic here
would diverge from the established pattern. Instead the risk is surfaced in the UI copy: the delete
confirmation warns that filed courses are removed with the group (see Frontend Notes). Revisit when the
`Course` feature lands and a real dependency check is possible.

### Repository / Controller

`ICourseGroupRepository` + `CourseGroupRepository` (Dapper, `IDbConnectionFactory`); methods take
`short pkid`. No transaction (no junction rows). `CourseGroupsController` route `api/course-groups`,
`PUT` takes pkid from body. Create validation (→ 400): `Description` non-empty. **No 409 check** — pkid
is IDENTITY and there is no unique business key in the schema (same as `Partner`). Numeric PK → route
`{id}` binds to `short`.

### Special Column Notes

- **`pkid` is `smallint IDENTITY`** — DB-assigned; excluded from INSERT (`SCOPE_IDENTITY()`), no pkid
  field in the create form (same as `Partner`; contrast `PublishStatus`, whose tinyint PK is
  user-assigned).
- `Description` is the only editable column — this is the smallest feature in the codebase so far.
- No `nchar`, `date`, `time`, bit, or computed columns. No RowAudit (not wired in this codebase).

---

## Frontend Notes

### Model (`course-group.model.ts`)

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
}
export interface CourseGroupRequest { pkid: number; description: string; }
export interface CourseGroupQuery { keyword?: string | null; }
export interface CourseGroupLookup { pkid: number; description: string; }
```

### Route Table (`app.routes.ts`)

| Path | Component |
|------|-----------|
| `course-groups` | `CourseGroupList` |
| `course-groups/new` | `CourseGroupForm` (before `:id`) |
| `course-groups/:id` | `CourseGroupDetail` |
| `course-groups/:id/edit` | `CourseGroupForm` |

### List component

- Columns: 主代碼 (pkid), 群組描述 (description), 操作.
- Sort default `Description ASC`; sessionStorage keys `course-group-list-{filters,sort,page}`.
- Filter drawer: single keyword `pInputText` (no FK/bool/date filters).
- `confirmDelete`: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？<br><small>此群組下的所有課程將一併刪除。</small>`
  — the standard copy plus a cascade warning line (see Delete Cascade Warning).

### Detail component

- Simple `getById` (no lookups / no forkJoin — no FKs). Shows both fields.
- No Primary-Foreign link buttons (referencing features not built — see Primary-Foreign Links).

### Form component

- Reactive Forms, **plain input only — no FK dropdowns**. Single field: `description`
  (`pInputText`, required, maxlength 100).
- **No pkid field** — it's IDENTITY. Carry pkid in a private field (0 on new, loaded value on edit) and
  send it in the request; the backend ignores it on create and uses it in the WHERE on update. (Same as
  the `Partner` / AppRole surrogate-pkid pattern.)
- Branch create vs update into separate `.subscribe()` calls.

### Service (`course-group.service.ts`)

Standard six methods against `/api/course-groups`. PK is numeric — **no `encodeURIComponent`**.
`getById(pkid: number)`, `delete(pkid: number)`.

### Sidebar placement

Existing group **課程管理 Course** (already in `app.ts` `navGroups`, currently holding the Partner
entry). Add `{ label: '課程群組 CourseGroup', route: '/course-groups' }` after the Partner entry.

---

## Files to Create / Modify

### Backend (`src/CMS.API`)
| File | Action |
|------|--------|
| `Models/CourseGroup.cs` | create |
| `Models/CourseGroupRequest.cs` | create |
| `Models/CourseGroupQuery.cs` | create |
| `Models/CourseGroupLookup.cs` | create |
| `Repositories/ICourseGroupRepository.cs` | create |
| `Repositories/CourseGroupRepository.cs` | create |
| `Controllers/CourseGroupsController.cs` | create |
| `Repositories/ILookupRepository.cs` | modify (add `GetCourseGroupsAsync`) |
| `Repositories/LookupRepository.cs` | modify (impl) |
| `Controllers/LookupsController.cs` | modify (add `course-groups` endpoint) |
| `Program.cs` | modify (register `ICourseGroupRepository`) |

### Frontend (`src/CMS.NG`)
| File | Action |
|------|--------|
| `core/models/course-group.model.ts` | create |
| `core/services/course-group.service.ts` | create |
| `core/services/lookup.service.ts` | modify (add `getCourseGroups`) |
| `features/course-groups/course-group-list/*` (ts/html/scss) | create |
| `features/course-groups/course-group-detail/*` (ts/html/scss) | create |
| `features/course-groups/course-group-form/*` (ts/html/scss) | create |
| `app.routes.ts` | modify (4 routes) |
| `app.ts` / `app.html` | modify (sidebar entry) |

### Tests
| File | Action |
|------|--------|
| `CMS.API.Tests/CourseGroupsControllerTests.cs` | create (list, query, get found/404, create ok/400, update ok/404/400, delete ok/404) |
| `core/services/course-group.service.spec.ts` | create |
| `core/services/lookup.service.spec.ts` | modify (add `getCourseGroups` test) |
| `features/course-groups/*/course-group-{list,detail,form}.spec.ts` | create |
