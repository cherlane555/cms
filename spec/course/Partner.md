# Build Spec for Partner
- database schema: `.\database\course.sql`

## Summary

`Partner` is a **flat parent table** in the course sub-system representing a training partner /
vendor (合作廠商). It has **no foreign keys of its own** — every column is a plain scalar field. It is
scaffolded early because it is the *referenced* (parent) side of FKs from `Course`
(`Course.Partner_pkid`), `Certification` (`Certification.Partner_pkid`), and `PartnerCourseGroup`
(`PartnerCourseGroup.Partner_pkid`), so it must exist before those features to avoid lookup gaps.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY** (DB-assigned surrogate) → C# `short` |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | N/A (`PartnerCourseGroup` is a payload-bearing entity, not a pure junction — managed separately) |
| Primary-Foreign Links | `Course`, `Certification`, `PartnerCourseGroup` reference `Partner` (none built yet — see note) |
| Query Filters | keyword (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage) |
| Default Sort | `DisplayOrder ASC` |

---

## Localization

### Chinese Table Name

- Partner: 合作廠商
- Description: 訓練合作廠商主資料

### Chinese Column Names

- pkid: 主代碼
- Name: 廠商名稱
- AppKey: 關鍵字
- NameOnPartnerMenu: 選單顯示名稱
- NameOnCourseDetailPage: 課程頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

---

## Required Fields

Required (NOT NULL):
- `Name` (nvarchar 50)
- `AppKey` (varchar 10)
- `NameOnPartnerMenu` (nvarchar 200)
- `NameOnCourseDetailPage` (nvarchar 50)
- `DisplayOrder` (int)

Optional (nullable):
- `ImageFilename` (varchar 50)

`pkid` is IDENTITY — DB-assigned, excluded from INSERT and from the create form.

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns — no outbound navigation.

**N/A**

---

## Primary-Foreign Links

These tables reference `Partner.pkid`:
- `Course.Partner_pkid` (`FK_Course_Partner`)
- `Certification.Partner_pkid` (`FK_Certification_Partner`)
- `PartnerCourseGroup.Partner_pkid` (`FK_PartnerCourseGroup_Partner`)

**Deferred:** none of those features are built yet (only AppRole, PublishStatus, and now Partner exist).
Emitting nav buttons to `/courses`, `/certifications`, `/partner-course-groups` would be dead links, so
**no Primary-Foreign link buttons are generated in this run.** Add them when those features land.

---

## N-N Relationships

No pure junction table (exactly two FK columns) links `Partner`. `PartnerCourseGroup` carries its own
`pkid`, `DisplayOrder`, and `Description`, so it is an association entity managed on its own, not an
n-n multi-select on the Partner form.

**N/A**

---

## Query Filters

`POST /api/partners/query` accepts:

- **keyword**: string — LIKE across `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`
  (`ImageFilename` excluded — it's a file path, not an identifying field).

No FK filters (no FKs), no bool filters (no bit columns), no date filters (no date columns).

---

## Lookup Endpoints Required

`Partner` is an FK target for `Course` and `Certification`, so it needs a slim lookup endpoint. This
run **creates** it.

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | `{ pkid, name }[]` ordered by `DisplayOrder ASC` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id}` | Get by pkid (smallint) |
| `POST` | `/api/partners` | Create (pkid is IDENTITY — assigned by DB) |
| `PUT` | `/api/partners` | Update (pkid from body) |
| `DELETE` | `/api/partners/{id}` | Delete |
| `GET` | `/api/lookups/partners` | Slim lookup list (new) |

No auth exceptions.

---

## Backend Notes

### Models

```csharp
public class Partner
{
    public short Pkid { get; set; }           // smallint IDENTITY
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}

public class PartnerRequest
{
    public short Pkid { get; set; }           // 0 on create (IDENTITY), the key on update
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}

public class PartnerQuery
{
    public string? Keyword { get; set; }
}
```

Lookup projection:

```csharp
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

### SQL — SELECT

```sql
SELECT p.pkid, p.Name, p.AppKey, p.NameOnPartnerMenu, p.NameOnCourseDetailPage,
       p.DisplayOrder, p.ImageFilename
FROM Partner p
-- list/query: ORDER BY p.DisplayOrder ASC ; get-by-id: WHERE p.pkid = @Pkid
```

No JOINs (no FKs), no `RTRIM()` (no `nchar` columns).

### SQL — INSERT

`pkid` is IDENTITY — excluded; read it back via `SCOPE_IDENTITY()`:

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

```sql
UPDATE Partner
SET Name = @Name, AppKey = @AppKey, NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage, DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM Partner WHERE pkid = @Pkid;
```

### Repository / Controller

`IPartnerRepository` + `PartnerRepository` (Dapper, `IDbConnectionFactory`); methods take `short pkid`.
No transaction (no junction rows). `PartnersController` route `api/partners`, `PUT` takes pkid from body.
Create validation (→ 400): `Name` and `AppKey` non-empty. **No 409 check** — pkid is IDENTITY and there
is no unique business key in the schema. Numeric PK → route `{id}` binds to `short`.

### Special Column Notes

- **`pkid` is `smallint IDENTITY`** — DB-assigned; excluded from INSERT (`SCOPE_IDENTITY()`), no pkid
  field in the create form (contrast PublishStatus, whose tinyint PK is user-assigned).
- No `nchar`, `date`, `time`, bit, or computed columns. No RowAudit (not in this codebase).

---

## Frontend Notes

### Model (`partner.model.ts`)

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}
export interface PartnerRequest { /* same shape */ }
export interface PartnerQuery { keyword?: string | null; }
export interface PartnerLookup { pkid: number; name: string; }
```

### Route Table (`app.routes.ts`)

| Path | Component |
|------|-----------|
| `partners` | `PartnerList` |
| `partners/new` | `PartnerForm` (before `:id`) |
| `partners/:id` | `PartnerDetail` |
| `partners/:id/edit` | `PartnerForm` |

### List component

- Columns: 主代碼, 廠商名稱, 關鍵字, 選單顯示名稱, 顯示順序, 操作.
- Sort default `DisplayOrder ASC`; sessionStorage keys `partner-list-{filters,sort,page}`.
- Filter drawer: single keyword `pInputText` (no FK/bool/date filters).
- `confirmDelete`: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？`

### Detail component

- Simple `getById` (no lookups / no forkJoin — no FKs). Shows all fields.
- No Primary-Foreign link buttons (referencing features not built — see Primary-Foreign Links).

### Form component

- Reactive Forms, **plain inputs only — no FK dropdowns**. Fields: `name`, `appKey`,
  `nameOnPartnerMenu`, `nameOnCourseDetailPage` (`pInputText`, required); `displayOrder`
  (`p-inputNumber`, required); `imageFilename` (`pInputText`, optional).
- **No pkid field** — it's IDENTITY. Carry pkid in a private field (0 on new, loaded value on edit) and
  send it in the request; the backend ignores it on create and uses it in the WHERE on update. (Same as
  the AppRole surrogate-pkid pattern.)
- Branch create vs update into separate `.subscribe()` calls.

### Service (`partner.service.ts`)

Standard six methods against `/api/partners`. PK is numeric — **no `encodeURIComponent`**.

### Sidebar placement

Group **課程管理 Course** (exists in `app.ts` `navGroups`, currently with `items: []`). Add
`{ label: '合作廠商 Partner', route: '/partners' }` as its first item.

---

## Files to Create / Modify

### Backend (`src/CMS.API`)
`Models/Partner.cs`, `PartnerRequest.cs`, `PartnerQuery.cs`, `PartnerLookup.cs`;
`Repositories/IPartnerRepository.cs`, `PartnerRepository.cs`; `Controllers/PartnersController.cs`;
modify `Repositories/ILookupRepository.cs` + `LookupRepository.cs` (`GetPartnersAsync`),
`Controllers/LookupsController.cs` (`partners` endpoint), `Program.cs` (register `IPartnerRepository`).

### Frontend (`src/CMS.NG`)
`core/models/partner.model.ts`, `core/services/partner.service.ts`; modify `core/services/lookup.service.ts`;
`features/partners/partner-{list,detail,form}/*` (ts/html/scss); modify `app.routes.ts`, `app.ts`.

### Tests
`CMS.API.Tests/PartnersControllerTests.cs`; `core/services/partner.service.spec.ts`;
modify `core/services/lookup.service.spec.ts`; `features/partners/*/partner-{list,detail,form}.spec.ts`.
