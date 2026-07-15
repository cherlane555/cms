/** One audit-trail entry for a record, from GET /api/rowaudit. */
export interface RowAuditEntry {
  dateTime: string;
  userName: string;
  actionType: string;
  actionDesc: string | null;
}
