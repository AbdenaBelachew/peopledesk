import { useState } from 'react'
import { Plus, Trash2, Palmtree } from 'lucide-react'
import { useHolidays, useSaveHoliday, useDeleteHoliday } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, todayInput } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import Select from '../../components/ui/Select'
import Modal from '../../components/ui/Modal'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import EmptyState from '../../components/ui/EmptyState'
import Spinner from '../../components/ui/Spinner'

export default function Holidays() {
  const year = new Date().getFullYear()
  const [selectedYear, setSelectedYear] = useState(year)
  const holidays = useHolidays(selectedYear)
  const save = useSaveHoliday()
  const remove = useDeleteHoliday()
  const toast = useToast()
  const [editor, setEditor] = useState(null) // null | { } | holiday
  const [deleteFor, setDeleteFor] = useState(null)

  async function onConfirmDelete() {
    try {
      await remove.mutateAsync(deleteFor.id)
      toast.success('Holiday deleted')
      setDeleteFor(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
      setDeleteFor(null)
    }
  }

  return (
    <div>
      <PageHeader
        title="Holidays"
        subtitle="Public holidays excluded from working days and attendance."
        actions={(
          <div className="flex items-center gap-2">
            <Select
              aria-label="Year"
              value={selectedYear}
              onChange={(e) => setSelectedYear(Number(e.target.value))}
              className="!mt-0 w-28"
              options={Array.from({ length: 5 }, (_, i) => ({ value: year - i + 1, label: String(year - i + 1) }))}
            />
            <Button onClick={() => setEditor({})}><Plus className="h-4 w-4" aria-hidden="true" /> Add holiday</Button>
          </div>
        )}
      />

      <Card bodyClassName="!p-0">
        {holidays.isLoading ? (
          <div className="py-12 text-center"><Spinner className="mx-auto" /></div>
        ) : !holidays.data?.length ? (
          <EmptyState icon={Palmtree} title={`No holidays in ${selectedYear}`} description="Add public holidays your organization observes." />
        ) : (
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {holidays.data.map((h) => (
              <li key={h.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">{h.name}</p>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {formatDate(h.date)}{h.description ? ` · ${h.description}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {h.recurringYearly && <Badge tone="blue">Recurring yearly</Badge>}
                  <Button variant="ghost" size="sm" onClick={() => setEditor(h)}>Edit</Button>
                  <Button variant="ghost" size="icon" aria-label={`Delete ${h.name}`} onClick={() => setDeleteFor(h)}>
                    <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      {editor !== null && (
        <HolidayModal
          open
          onClose={() => setEditor(null)}
          holiday={editor.id ? editor : null}
          defaultYear={selectedYear}
          onSave={async (values) => {
            try {
              await save.mutateAsync(values)
              toast.success(editor.id ? 'Holiday updated' : 'Holiday added')
              setEditor(null)
            } catch (e) {
              toast.error(await apiErrorMessage(e))
            }
          }}
          saving={save.isPending}
        />
      )}

      <ConfirmDialog
        open={!!deleteFor}
        onClose={() => setDeleteFor(null)}
        onConfirm={onConfirmDelete}
        danger
        loading={remove.isPending}
        title={`Delete “${deleteFor?.name || ''}”?`}
        confirmLabel="Delete"
      />
    </div>
  )
}

function HolidayModal({ open, onClose, holiday, defaultYear, onSave, saving }) {
  const [values, setValues] = useState({
    name: holiday?.name || '',
    date: holiday?.date ? String(holiday.date).slice(0, 10) : `${defaultYear}-01-01`,
    recurringYearly: holiday?.recurringYearly ?? false,
    description: holiday?.description || '',
  })

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  function submit(e) {
    e.preventDefault()
    if (!values.name.trim() || !values.date) return
    onSave({
      ...(holiday?.id ? { id: holiday.id } : {}),
      name: values.name.trim(),
      date: values.date,
      recurringYearly: values.recurringYearly,
      description: values.description.trim() || undefined,
    })
  }

  return (
    <Modal open={open} onClose={onClose} title={holiday ? `Edit ${holiday.name}` : 'Add holiday'}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Save</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Name" value={values.name} onChange={(e) => set('name', e.target.value)} required autoFocus className="sm:col-span-2" />
        <Input label="Date" type="date" value={values.date} onChange={(e) => set('date', e.target.value)} required />
        <div className="flex items-end pb-2">
          <Checkbox label="Repeats every year" checked={values.recurringYearly} onChange={(e) => set('recurringYearly', e.target.checked)} />
        </div>
        <Textarea label="Description (optional)" value={values.description} onChange={(e) => set('description', e.target.value)} className="sm:col-span-2" />
      </form>
    </Modal>
  )
}
