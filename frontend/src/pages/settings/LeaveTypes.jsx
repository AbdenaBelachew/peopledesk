import { useState } from 'react'
import { Plus, Pencil, Palmtree } from 'lucide-react'
import { useLeaveTypes, useSaveLeaveType } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import Modal from '../../components/ui/Modal'
import EmptyState from '../../components/ui/EmptyState'
import Spinner from '../../components/ui/Spinner'

export default function LeaveTypes() {
  const types = useLeaveTypes()
  const save = useSaveLeaveType()
  const toast = useToast()
  const [editor, setEditor] = useState(null) // null | {} | type

  async function onSave(values) {
    try {
      await save.mutateAsync(values)
      toast.success(editor?.id ? 'Leave type updated' : 'Leave type created')
      setEditor(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <div>
      <PageHeader
        title="Leave types & policies"
        subtitle="Entitlements, accrual and approval rules per leave type."
        actions={<Button onClick={() => setEditor({})}><Plus className="h-4 w-4" aria-hidden="true" /> Add leave type</Button>}
      />
      <Card bodyClassName="!p-0">
        {types.isLoading ? (
          <div className="py-12 text-center"><Spinner className="mx-auto" /></div>
        ) : !types.data?.length ? (
          <EmptyState icon={Palmtree} title="No leave types" description="Create your first leave type, e.g. Annual Leave." />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                  <th className="px-3 py-2">Type</th>
                  <th className="px-3 py-2 text-right">Entitlement</th>
                  <th className="px-3 py-2">Accrual</th>
                  <th className="px-3 py-2 text-right">Carry over</th>
                  <th className="px-3 py-2 text-right">Notice</th>
                  <th className="px-3 py-2 text-right">Max consec.</th>
                  <th className="px-3 py-2">Flags</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {types.data.map((t) => (
                  <tr key={t.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/60">
                    <td className="px-3 py-2">
                      <span className="flex items-center gap-2">
                        <span className="h-3 w-3 rounded-full border border-white/50" style={{ backgroundColor: t.colorHex || '#2563eb' }} aria-hidden="true" />
                        <span>
                          <span className="block font-medium text-slate-900 dark:text-slate-100">{t.name}</span>
                          <span className="font-mono text-xs text-slate-400">{t.code}</span>
                        </span>
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right">{t.annualEntitlementDays}d</td>
                    <td className="px-3 py-2">{t.accrualFrequency === 'Monthly' ? 'Monthly' : 'Yearly upfront'}</td>
                    <td className="px-3 py-2 text-right">{t.carryOverMaxDays}d</td>
                    <td className="px-3 py-2 text-right">{t.minNoticeDays}d</td>
                    <td className="px-3 py-2 text-right">{t.maxConsecutiveDays}d</td>
                    <td className="px-3 py-2">
                      <span className="flex flex-wrap gap-1">
                        <Badge tone={t.isPaid ? 'green' : 'slate'}>{t.isPaid ? 'Paid' : 'Unpaid'}</Badge>
                        {t.requiresApproval && <Badge tone="amber">Approval</Badge>}
                        {t.allowHalfDays && <Badge tone="blue">Half days</Badge>}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right">
                      <Button variant="ghost" size="icon" aria-label={`Edit ${t.name}`} onClick={() => setEditor(t)}>
                        <Pencil className="h-4 w-4" aria-hidden="true" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {editor !== null && (
        <LeaveTypeModal
          open
          onClose={() => setEditor(null)}
          type={editor.id ? editor : null}
          onSave={onSave}
          saving={save.isPending}
        />
      )}
    </div>
  )
}

function LeaveTypeModal({ open, onClose, type, onSave, saving }) {
  const [values, setValues] = useState({
    name: type?.name || '',
    code: type?.code || '',
    colorHex: type?.colorHex || '#2563eb',
    isPaid: type?.isPaid ?? true,
    requiresApproval: type?.requiresApproval ?? true,
    description: type?.description || '',
    annualEntitlementDays: type?.annualEntitlementDays ?? 20,
    accrualFrequency: type?.accrualFrequency || 'YearlyUpfront',
    carryOverMaxDays: type?.carryOverMaxDays ?? 0,
    minNoticeDays: type?.minNoticeDays ?? 0,
    maxConsecutiveDays: type?.maxConsecutiveDays ?? 30,
    allowHalfDays: type?.allowHalfDays ?? false,
  })
  const [errors, setErrors] = useState({})

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  function submit(e) {
    e.preventDefault()
    const next = {}
    if (!values.name.trim()) next.name = 'Name is required'
    if (!values.code.trim()) next.code = 'Code is required'
    if (Number(values.annualEntitlementDays) < 0) next.annualEntitlementDays = 'Must be 0 or more'
    setErrors(next)
    if (Object.keys(next).length) return
    onSave({
      ...(type?.id ? { id: type.id } : {}),
      name: values.name.trim(),
      code: values.code.trim().toUpperCase(),
      colorHex: values.colorHex || undefined,
      isPaid: values.isPaid,
      requiresApproval: values.requiresApproval,
      description: values.description.trim() || undefined,
      annualEntitlementDays: Number(values.annualEntitlementDays),
      accrualFrequency: values.accrualFrequency,
      carryOverMaxDays: Number(values.carryOverMaxDays) || 0,
      minNoticeDays: Number(values.minNoticeDays) || 0,
      maxConsecutiveDays: Number(values.maxConsecutiveDays) || 0,
      allowHalfDays: values.allowHalfDays,
    })
  }

  return (
    <Modal open={open} onClose={onClose} title={type ? `Edit ${type.name}` : 'New leave type'} size="lg"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Save</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Name" value={values.name} onChange={(e) => set('name', e.target.value)} error={errors.name} autoFocus />
        <Input label="Code" hint="Short unique code, e.g. AL" value={values.code} onChange={(e) => set('code', e.target.value)} error={errors.code} />
        <Input label="Calendar color" type="color" className="!mt-0 h-10" value={values.colorHex} onChange={(e) => set('colorHex', e.target.value)} />
        <Select
          label="Accrual"
          value={values.accrualFrequency}
          onChange={(e) => set('accrualFrequency', e.target.value)}
          options={[{ value: 'YearlyUpfront', label: 'Yearly upfront' }, { value: 'Monthly', label: 'Monthly accrual' }]}
        />
        <Input label="Annual entitlement (days)" type="number" min="0" step="0.5" value={values.annualEntitlementDays} onChange={(e) => set('annualEntitlementDays', e.target.value)} error={errors.annualEntitlementDays} />
        <Input label="Carry-over cap (days)" type="number" min="0" step="0.5" value={values.carryOverMaxDays} onChange={(e) => set('carryOverMaxDays', e.target.value)} />
        <Input label="Min notice (days)" type="number" min="0" value={values.minNoticeDays} onChange={(e) => set('minNoticeDays', e.target.value)} />
        <Input label="Max consecutive (days)" type="number" min="0" value={values.maxConsecutiveDays} onChange={(e) => set('maxConsecutiveDays', e.target.value)} />
        <div className="space-y-2.5 sm:col-span-2">
          <Checkbox label="Paid leave" checked={values.isPaid} onChange={(e) => set('isPaid', e.target.checked)} />
          <Checkbox label="Requires approval" checked={values.requiresApproval} onChange={(e) => set('requiresApproval', e.target.checked)} />
          <Checkbox label="Allow half days" checked={values.allowHalfDays} onChange={(e) => set('allowHalfDays', e.target.checked)} />
        </div>
        <Textarea label="Description (optional)" value={values.description} onChange={(e) => set('description', e.target.value)} className="sm:col-span-2" />
      </form>
    </Modal>
  )
}
