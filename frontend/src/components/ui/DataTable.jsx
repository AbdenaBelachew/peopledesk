import { ChevronDown, ChevronUp } from 'lucide-react'
import Spinner from './Spinner'
import EmptyState from './EmptyState'
import Button from './Button'

/**
 * Server-friendly data table: sortable headers, pagination footer, loading and
 * empty states. Pass page/pageSize/totalCount/onPageChange to enable pagination.
 */
export default function DataTable({
  columns,
  rows,
  rowKey = 'id',
  loading = false,
  empty,
  sortBy,
  sortDesc,
  onSortChange,
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onRowClick,
  stickyHeader = false,
  zebra = false,
  className = '',
}) {
  const showPager = typeof page === 'number' && typeof totalCount === 'number'
  const from = totalCount === 0 ? 0 : (page - 1) * (pageSize || 10) + 1
  const to = Math.min((page || 1) * (pageSize || 10), totalCount || 0)

  function handleSort(col) {
    if (!col.sortable || !onSortChange) return
    if (sortBy === col.sortBy || sortBy === col.key) {
      onSortChange({ sortBy: col.sortBy || col.key, sortDesc: !sortDesc })
    } else {
      onSortChange({ sortBy: col.sortBy || col.key, sortDesc: false })
    }
  }

  return (
    <div className={`overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900 ${className}`}>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
          <thead className={stickyHeader ? 'sticky top-0 z-10 bg-slate-50 dark:bg-slate-800' : 'bg-slate-50 dark:bg-slate-800/60'}>
            <tr>
              {columns.map((col) => (
                <th
                  key={col.key}
                  scope="col"
                  className={`whitespace-nowrap px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400 ${col.headerClassName || ''}`}
                >
                  {col.sortable ? (
                    <button
                      type="button"
                      onClick={() => handleSort(col)}
                      className="inline-flex items-center gap-1 hover:text-slate-700 dark:hover:text-slate-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 rounded"
                    >
                      {col.header}
                      {sortBy === col.sortBy || sortBy === col.key ? (
                        sortDesc ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronUp className="h-3.5 w-3.5" />
                      ) : null}
                    </button>
                  ) : (
                    col.header
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
            {loading ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-12 text-center">
                  <Spinner className="mx-auto" />
                </td>
              </tr>
            ) : !rows || rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-4">
                  {empty || <EmptyState title="Nothing here" description="No records found." />}
                </td>
              </tr>
            ) : (
              rows.map((row, idx) => (
                <tr
                  key={row[rowKey] ?? idx}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={`border-slate-100 dark:border-slate-800 ${zebra && idx % 2 === 1 ? 'bg-slate-50/60 dark:bg-slate-800/40' : ''} ${onRowClick ? 'cursor-pointer' : ''} hover:bg-slate-50 dark:hover:bg-slate-800/60`}
                >
                  {columns.map((col) => (
                    <td key={col.key} className={`px-3 py-2.5 text-slate-700 dark:text-slate-300 ${col.className || ''}`}>
                      {col.render ? col.render(row) : row[col.key] ?? '—'}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      {showPager && (
        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 px-3 py-2 dark:border-slate-800">
          <p className="text-xs text-slate-500 dark:text-slate-400">
            Showing <span className="font-medium">{from}</span>–<span className="font-medium">{to}</span> of{' '}
            <span className="font-medium">{totalCount}</span>
          </p>
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
              aria-label="Previous page"
            >
              Previous
            </Button>
            <span className="text-xs text-slate-500 dark:text-slate-400">
              Page {page} of {totalPages || 1}
            </span>
            <Button
              variant="secondary"
              size="sm"
              disabled={page >= (totalPages || 1)}
              onClick={() => onPageChange(page + 1)}
              aria-label="Next page"
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
