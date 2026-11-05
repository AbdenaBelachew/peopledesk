import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  Pencil, Trash2, UserMinus, Download, Upload, Plus, Star, ChevronLeft,
  FileText, Mail, Phone, Landmark, Briefcase, CalendarDays, Clock, Wallet, Users,
} from 'lucide-react'
import {
  useEmployee, useEmployeeContacts, useSaveContact, useDeleteContact,
  useEmployeeDocuments, useUploadDocument, useDeleteDocument, useEmployeeHistory,
  useSalaryStructures, useCreateSalaryStructure, useDeleteSalaryStructure,
  useLeaveBalances, useLeaveTransactions, useAdjustBalance,
  useAttendanceSummary, useOffboardEmployee, useDeleteEmployee, useStructure,
  useOrg,
} from '../../api/queries'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import {
  money, formatDate, formatDateTime, formatTime, duration, statusTone, humanize, dateInput, todayInput,
} from '../../lib/format'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import Modal from '../../components/ui/Modal'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import Tabs from '../../components/ui/Tabs'
import Avatar from '../../components/ui/Avatar'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'
import PageHeader from '../../components/ui/PageHeader'

export default function EmployeeDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { hasPerm, user } = useAuth()
  const toast = useToast()
  const employee = useEmployee(id)
  const [tab, setTab] = useState('overview')

  const canSensitive = hasPerm('employees.viewSensitive')
  const e = employee.data

  const tabs = [
    { key: 'overview', label: 'Overview' },
    { key: 'contacts', label: 'Contacts' },
    { key: 'documents', label: 'Documents' },
    { key: 'history', label: 'Employment History' },
    ...(canSensitive ? [{ key: 'salary', label: 'Salary' }] : []),
    { key: 'leave', label: 'Leave Balances' },
    { key: 'attendance', label: 'Attendance Summary' },
  ]

  if (employee.isLoading) return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
  if (!e) return <EmptyState icon={Users} title="Employee not found" description="This record may have been deleted." />

  return (
    <div>
      <Link to="/employees" className="mb-3 inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200">
        <ChevronLeft className="h-4 w-4" aria-hidden="true" /> Back to directory
      </Link>

      <PageHeader
        title={e.fullName || `${e.firstName} ${e.lastName}`}
        subtitle={`${e.employeeNumber} · ${e.department || 'No department'} · ${e.position || 'No position'}`}
        actions={
          <>
            {hasPerm('employees.update') && (
              <Link to={`/employees/${id}/edit`}><Button variant="secondary"><Pencil className="h-4 w-4" aria-hidden="true" /> Edit</Button></Link>
            )}
            {hasPerm('employees.update') && ['Active', 'Probation', 'Suspended', 'OnLeave'].includes(e.status) && (
              <OffboardButton employee={e} />
            )}
            {hasPerm('employees.delete') && <DeleteButton employee={e} />}
          </>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Avatar name={e.fullName || `${e.firstName} ${e.lastName}`} size="xl" />
        <div className="flex flex-wrap items-center gap-2">
          <Badge tone={statusTone(e.status)}>{e.status}</Badge>
          {e.employmentType && <Badge tone="sky">{e.employmentType}</Badge>}
          {e.branch && <Badge tone="slate">{e.branch}</Badge>}
          {e.hasUserAccount && <Badge tone="purple">User account</Badge>}
        </div>
      </div>

      <Tabs tabs={tabs} active={tab} onChange={setTab} className="mb-4" />

      {tab === 'overview' && <OverviewTab employee={e} canSensitive={canSensitive} />}
      {tab === 'contacts' && <ContactsTab employeeId={id} />}
      {tab === 'documents' && <DocumentsTab employeeId={id} />}
      {tab === 'history' && <HistoryTab employee={e} />}
      {tab === 'salary' && canSensitive && <SalaryTab employeeId={id} />}
      {tab === 'leave' && <LeaveTab employeeId={id} canAdjust={hasPerm('leave.manage')} />}
      {tab === 'attendance' && <AttendanceTab employeeId={id} />}
    </div>
  )
}

// ------------------------------- Overview -------------------------------

function Field({ label, value }) {
  return (
    <div>
      <p className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">{label}</p>
      <p className="mt-0.5 text-sm text-slate-800 dark:text-slate-200">{value ?? '—'}</p>
    </div>
  )
}

function OverviewTab({ employee: e, canSensitive }) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
      <Card title="Personal">
        <div className="grid grid-cols-2 gap-4">
          <Field label="Full name" value={`${e.firstName} ${e.middleName ? e.middleName + ' ' : ''}${e.lastName}`} />
          <Field label="Gender" value={humanize(e.gender)} />
          <Field label="Date of birth" value={formatDate(e.dateOfBirth)} />
          {canSensitive && <Field label="National ID" value={e.nationalId} />}
          <Field label="Phone" value={e.phone} />
          <Field label="Work email" value={e.workEmail} />
          <Field label="Personal email" value={e.personalEmail} />
          <Field label="Address" value={[e.addressLine, e.city].filter(Boolean).join(', ')} />
        </div>
      </Card>
      <Card title="Employment">
        <div className="grid grid-cols-2 gap-4">
          <Field label="Employee number" value={e.employeeNumber} />
          <Field label="Status" value={<Badge tone={statusTone(e.status)}>{e.status}</Badge>} />
          <Field label="Hire date" value={formatDate(e.hireDate)} />
          <Field label="Contract end" value={formatDate(e.contractEndDate)} />
          <Field label="Department" value={e.department} />
          <Field label="Position" value={e.position} />
          <Field label="Branch" value={e.branch} />
          <Field label="Employment type" value={e.employmentType} />
          {e.terminationDate && <Field label="Termination date" value={formatDate(e.terminationDate)} />}
          {e.terminationReason && <Field label="Termination reason" value={e.terminationReason} />}
        </div>
      </Card>
      <Card title="Payroll">
        <div className="grid grid-cols-2 gap-4">
          <Field label="Payment method" value={humanize(e.paymentMethod)} />
          <Field label="Bank" value={e.bankName} />
          <Field label="Account" value={e.bankAccountMasked} />
          {canSensitive && <Field label="TIN" value={e.taxIdentificationNumber} />}
        </div>
      </Card>
    </div>
  )
}

// ------------------------------- Contacts -------------------------------

function ContactsTab({ employeeId }) {
  const toast = useToast()
  const contacts = useEmployeeContacts(employeeId)
  const save = useSaveContact(employeeId)
  const remove = useDeleteContact(employeeId)
  const [modalOpen, setModalOpen] = useState(false)

  async function onDelete(c) {
    try {
      await remove.mutateAsync(c.id)
      toast.success('Contact removed')
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  return (
    <Card
      title="Emergency & personal contacts"
      actions={<Button size="sm" onClick={() => setModalOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" /> Add contact</Button>}
    >
      {!contacts.data?.length ? (
        <EmptyState icon={Phone} title="No contacts" description="Add people to reach in case of emergency." />
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {contacts.data.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-3 py-3">
              <div>
                <p className="flex items-center gap-1.5 text-sm font-medium text-slate-900 dark:text-slate-100">
                  {c.name}
                  {c.isPrimary && <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" aria-label="Primary contact" />}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400">{c.relationship}</p>
                <p className="mt-0.5 flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400">
                  <span className="inline-flex items-center gap-1"><Phone className="h-3 w-3" aria-hidden="true" />{c.phone}</span>
                  {c.email && <span className="inline-flex items-center gap-1"><Mail className="h-3 w-3" aria-hidden="true" />{c.email}</span>}
                </p>
              </div>
              <Button variant="ghost" size="icon" aria-label={`Delete contact ${c.name}`} onClick={() => onDelete(c)}>
                <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <ContactModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onSave={async (values) => {
          try {
            await save.mutateAsync(values)
            toast.success('Contact saved')
            setModalOpen(false)
          } catch (e) { toast.error(await apiErrorMessage(e)) }
        }}
        saving={save.isPending}
      />
    </Card>
  )
}

function ContactModal({ open, onClose, onSave, saving }) {
  const [values, setValues] = useState({ name: '', relationship: '', phone: '', email: '', isPrimary: false })
  const [errors, setErrors] = useState({})

  useEffect(() => {
    if (open) {
      setValues({ name: '', relationship: '', phone: '', email: '', isPrimary: false })
      setErrors({})
    }
  }, [open])

  function submit(ev) {
    ev.preventDefault()
    const next = {}
    if (!values.name.trim()) next.name = 'Name is required'
    if (!values.relationship.trim()) next.relationship = 'Relationship is required'
    if (!values.phone.trim()) next.phone = 'Phone is required'
    setErrors(next)
    if (Object.keys(next).length) return
    onSave({ ...values, name: values.name.trim(), relationship: values.relationship.trim(), phone: values.phone.trim(), email: values.email.trim() || undefined })
  }

  return (
    <Modal open={open} onClose={onClose} title="Add contact"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Save</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Name" value={values.name} onChange={(e) => setValues((v) => ({ ...v, name: e.target.value }))} error={errors.name} />
        <Input label="Relationship" placeholder="Spouse, sibling…" value={values.relationship} onChange={(e) => setValues((v) => ({ ...v, relationship: e.target.value }))} error={errors.relationship} />
        <Input label="Phone" value={values.phone} onChange={(e) => setValues((v) => ({ ...v, phone: e.target.value }))} error={errors.phone} />
        <Input label="Email (optional)" type="email" value={values.email} onChange={(e) => setValues((v) => ({ ...v, email: e.target.value }))} />
        <Checkbox label="Primary contact" checked={values.isPrimary} onChange={(e) => setValues((v) => ({ ...v, isPrimary: e.target.checked }))} />
      </form>
    </Modal>
  )
}

// ------------------------------- Documents -------------------------------

const DOC_KINDS = ['Contract', 'Identification', 'Certificate', 'Letter', 'Payslip', 'Other']

function DocumentsTab({ employeeId }) {
  const toast = useToast()
  const documents = useEmployeeDocuments(employeeId)
  const upload = useUploadDocument(employeeId)
  const remove = useDeleteDocument(employeeId)
  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState('Contract')
  const [name, setName] = useState('')
  const [file, setFile] = useState(null)

  async function onUpload(ev) {
    ev.preventDefault()
    if (!file || !name.trim()) {
      toast.error('Choose a file and give it a name')
      return
    }
    try {
      await upload.mutateAsync({ file, kind, name: name.trim() })
      toast.success('Document uploaded')
      setOpen(false); setFile(null); setName('')
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  async function onDownload(doc) {
    try {
      const { fetchBlob } = await import('../../api/client')
      const res = await fetchBlob(`/employees/${employeeId}/documents/${doc.id}/download`)
      const url = URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = doc.fileName || doc.name
      document.body.appendChild(a); a.click(); a.remove()
      setTimeout(() => URL.revokeObjectURL(url), 10_000)
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  return (
    <Card
      title="Documents"
      actions={<Button size="sm" onClick={() => setOpen(true)}><Upload className="h-4 w-4" aria-hidden="true" /> Upload</Button>}
    >
      {!documents.data?.length ? (
        <EmptyState icon={FileText} title="No documents" description="Upload contracts, IDs and certificates (max 10MB)." />
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {documents.data.map((d) => (
            <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
              <div className="flex min-w-0 items-center gap-3">
                <span className="rounded-md bg-slate-100 p-2 dark:bg-slate-800">
                  <FileText className="h-4 w-4 text-slate-500" aria-hidden="true" />
                </span>
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">{d.name}</p>
                  <p className="text-xs text-slate-400">
                    {d.kind} · {d.fileName} · {(d.sizeBytes / 1024).toFixed(0)} KB · {formatDate(d.uploadedAtUtc)}
                  </p>
                </div>
              </div>
              <div className="flex gap-1">
                <Button variant="ghost" size="icon" aria-label={`Download ${d.name}`} onClick={() => onDownload(d)}>
                  <Download className="h-4 w-4" aria-hidden="true" />
                </Button>
                <Button
                  variant="ghost" size="icon" aria-label={`Delete ${d.name}`}
                  onClick={async () => {
                    try { await remove.mutateAsync(d.id); toast.success('Document deleted') }
                    catch (e) { toast.error(await apiErrorMessage(e)) }
                  }}
                >
                  <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <Modal open={open} onClose={() => setOpen(false)} title="Upload document"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button onClick={onUpload} loading={upload.isPending}>Upload</Button></>}>
        <form onSubmit={onUpload} className="space-y-4">
          <Input label="Display name" value={name} onChange={(e) => setName(e.target.value)} required />
          <Select label="Kind" value={kind} onChange={(e) => setKind(e.target.value)} options={DOC_KINDS.map((k) => ({ value: k, label: k }))} />
          <Input
            label="File"
            type="file"
            accept=".pdf,.png,.jpg,.jpeg,.webp,.doc,.docx"
            onChange={(e) => setFile(e.target.files?.[0] || null)}
            hint="PDF, image or Word document up to 10 MB"
          />
        </form>
      </Modal>
    </Card>
  )
}

// ------------------------------- History -------------------------------

function HistoryTab({ employee: e }) {
  const history = useEmployeeHistory(e.id)
  const departments = useStructure('departments')
  const positions = useStructure('positions')
  const branches = useStructure('branches')

  const nameOf = (list, idv) => (list || []).find((x) => x.id === idv)?.name || null

  return (
    <Card title="Employment history">
      {history.isLoading ? (
        <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
      ) : !history.data?.length ? (
        <EmptyState icon={Briefcase} title="No history yet" description="Changes to department, position, branch or status are tracked here." />
      ) : (
        <ol className="relative space-y-4 border-l border-slate-200 pl-5 dark:border-slate-700">
          {history.data.map((h) => (
            <li key={h.id} className="relative">
              <span className="absolute -left-[26px] top-1 h-3 w-3 rounded-full border-2 border-white bg-blue-500 dark:border-slate-900" aria-hidden="true" />
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm font-medium text-slate-900 dark:text-slate-100">
                  {formatDate(h.effectiveFrom)}
                  {h.effectiveTo ? ` → ${formatDate(h.effectiveTo)}` : ' → present'}
                </p>
                {h.status && <Badge tone={statusTone(h.status)}>{h.status}</Badge>}
              </div>
              <p className="mt-0.5 text-sm text-slate-600 dark:text-slate-300">
                {[nameOf(departments.data, h.departmentId) || '—', nameOf(positions.data, h.positionId) || '—', nameOf(branches.data, h.branchId) || '—'].join(' · ')}
              </p>
              {h.changeReason && <p className="mt-0.5 text-xs text-slate-400">Reason: {h.changeReason}</p>}
              {h.notes && <p className="text-xs text-slate-400">{h.notes}</p>}
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}

// ------------------------------- Salary -------------------------------

function SalaryTab({ employeeId }) {
  const toast = useToast()
  const structures = useSalaryStructures(employeeId)
  const create = useCreateSalaryStructure(employeeId)
  const remove = useDeleteSalaryStructure(employeeId)
  const org = useOrg()
  const [open, setOpen] = useState(false)
  const [confirmId, setConfirmId] = useState(null)

  async function onDelete() {
    try {
      await remove.mutateAsync(confirmId)
      toast.success('Salary structure deleted')
      setConfirmId(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
      setConfirmId(null)
    }
  }

  return (
    <Card
      title="Salary structures"
      subtitle="Historical structures with effective dating. Exactly one BASIC earning is required per structure."
      actions={<Button size="sm" onClick={() => setOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" /> New structure</Button>}
    >
      {structures.isLoading ? (
        <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
      ) : !structures.data?.length ? (
        <EmptyState icon={Wallet} title="No salary structures" description="Add a structure so this employee can be included in payroll." />
      ) : (
        <div className="space-y-4">
          {structures.data.map((s) => (
            <div key={s.id} className="rounded-lg border border-slate-200 dark:border-slate-700">
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-2.5 dark:border-slate-800">
                <div>
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    Effective {formatDate(s.effectiveFrom)}{s.effectiveTo ? ` → ${formatDate(s.effectiveTo)}` : ' → present'}
                  </p>
                  <p className="text-xs text-slate-400">{s.currency}{s.notes ? ` · ${s.notes}` : ''}</p>
                </div>
                <Button variant="ghost" size="sm" aria-label="Delete structure" onClick={() => setConfirmId(s.id)}>
                  <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
                </Button>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                      <th className="px-3 py-2">Kind</th>
                      <th className="px-3 py-2">Code</th>
                      <th className="px-3 py-2">Label</th>
                      <th className="px-3 py-2">Type</th>
                      <th className="px-3 py-2">Calculation</th>
                      <th className="px-3 py-2 text-right">Amount</th>
                      <th className="px-3 py-2">Taxable</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {(s.components || []).map((c) => (
                      <tr key={c.id}>
                        <td className="px-3 py-2"><Badge tone={c.kind === 'Earning' ? 'green' : 'red'}>{c.kind}</Badge></td>
                        <td className="px-3 py-2 font-mono text-xs">{c.code}</td>
                        <td className="px-3 py-2">{c.label}</td>
                        <td className="px-3 py-2">{c.type}</td>
                        <td className="px-3 py-2">{humanize(c.calculationType)}</td>
                        <td className="px-3 py-2 text-right font-medium">
                          {c.calculationType === 'PercentOfBasic' ? `${c.amount}%` : money(c.amount, s.currency)}
                        </td>
                        <td className="px-3 py-2">{c.isTaxable ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </div>
      )}

      <SalaryStructureModal
        open={open}
        onClose={() => setOpen(false)}
        defaultCurrency={org.data?.currency || 'ETB'}
        onSave={async (payload) => {
          try {
            await create.mutateAsync(payload)
            toast.success('Salary structure added')
            setOpen(false)
          } catch (e) { toast.error(await apiErrorMessage(e)) }
        }}
        saving={create.isPending}
      />

      <ConfirmDialog
        open={!!confirmId}
        onClose={() => setConfirmId(null)}
        onConfirm={onDelete}
        danger
        loading={remove.isPending}
        title="Delete salary structure?"
        message="Structures used by finalized payslips cannot be deleted."
        confirmLabel="Delete"
      />
    </Card>
  )
}

function SalaryStructureModal({ open, onClose, onSave, saving, defaultCurrency }) {
  const [effectiveFrom, setEffectiveFrom] = useState(todayInput())
  const [currency, setCurrency] = useState(defaultCurrency)
  const [notes, setNotes] = useState('')
  const [components, setComponents] = useState([{ kind: 'Earning', code: 'BASIC', label: 'Basic Salary', type: 'Basic', calculationType: 'FixedAmount', amount: '', isTaxable: true, isActive: true }])
  const [error, setError] = useState('')

  useEffect(() => {
    if (open) {
      setEffectiveFrom(todayInput())
      setCurrency(defaultCurrency)
      setNotes('')
      setComponents([{ kind: 'Earning', code: 'BASIC', label: 'Basic Salary', type: 'Basic', calculationType: 'FixedAmount', amount: '', isTaxable: true, isActive: true }])
      setError('')
    }
  }, [open, defaultCurrency])

  function updateComp(i, key, value) {
    setComponents((c) => c.map((row, idx) => (idx === i ? { ...row, [key]: value } : row)))
  }

  function submit(ev) {
    ev.preventDefault()
    setError('')
    const clean = components
      .filter((c) => c.code.trim() && c.label.trim() && c.amount !== '' && c.amount !== null)
      .map((c) => ({ ...c, code: c.code.trim().toUpperCase(), label: c.label.trim(), amount: Number(c.amount) }))
    if (!clean.length) { setError('Add at least one component'); return }
    if (!clean.some((c) => c.kind === 'Earning' && c.code === 'BASIC')) {
      setError('Exactly one BASIC earning is required')
      return
    }
    onSave({ effectiveFrom, currency, notes: notes.trim() || undefined, components: clean })
  }

  return (
    <Modal open={open} onClose={onClose} title="New salary structure" size="lg"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Create structure</Button></>}>
      <form onSubmit={submit} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-3">
          <Input label="Effective from" type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} required />
          <Input label="Currency" value={currency} onChange={(e) => setCurrency(e.target.value)} required />
          <Input label="Notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>

        {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-600 dark:bg-red-900/30 dark:text-red-300">{error}</p>}

        <div className="space-y-3">
          {components.map((c, i) => (
            <div key={i} className="rounded-md border border-slate-200 p-3 dark:border-slate-700">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Select label="Kind" value={c.kind} onChange={(e) => updateComp(i, 'kind', e.target.value)}
                  options={[{ value: 'Earning', label: 'Earning' }, { value: 'Deduction', label: 'Deduction' }]} />
                <Input label="Code" value={c.code} onChange={(e) => updateComp(i, 'code', e.target.value)} placeholder="BASIC" />
                <Input label="Label" value={c.label} onChange={(e) => updateComp(i, 'label', e.target.value)} placeholder="Basic Salary" />
                <Input label="Type" value={c.type} onChange={(e) => updateComp(i, 'type', e.target.value)} placeholder="Basic / Allowance / Loan" />
                <Select label="Calculation" value={c.calculationType} onChange={(e) => updateComp(i, 'calculationType', e.target.value)}
                  options={[{ value: 'FixedAmount', label: 'Fixed amount' }, { value: 'PercentOfBasic', label: '% of basic' }]} />
                <Input label={c.calculationType === 'PercentOfBasic' ? 'Percent' : 'Amount'} type="number" step="0.01" value={c.amount}
                  onChange={(e) => updateComp(i, 'amount', e.target.value)} />
                <Checkbox label="Taxable" checked={c.isTaxable} onChange={(e) => updateComp(i, 'isTaxable', e.target.checked)} className="mt-6" />
                <Button variant="ghost" size="icon" aria-label="Remove component" className="mt-6 justify-self-end"
                  onClick={() => setComponents((comps) => (comps.length > 1 ? comps.filter((_, idx) => idx !== i) : comps))}>
                  <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
                </Button>
              </div>
            </div>
          ))}
        </div>
        <button type="button" onClick={() => setComponents((c) => [...c, { kind: 'Earning', code: '', label: '', type: '', calculationType: 'FixedAmount', amount: '', isTaxable: true, isActive: true }])}
          className="inline-flex items-center gap-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
          <Plus className="h-4 w-4" aria-hidden="true" /> Add component
        </button>
      </form>
    </Modal>
  )
}

// ------------------------------- Leave -------------------------------

function LeaveTab({ employeeId, canAdjust }) {
  const toast = useToast()
  const [year, setYear] = useState(new Date().getFullYear())
  const balances = useLeaveBalances(employeeId, year)
  const transactions = useLeaveTransactions(employeeId, { year })
  const adjust = useAdjustBalance(employeeId)
  const [adjustFor, setAdjustFor] = useState(null) // balance row
  const [days, setDays] = useState('')
  const [note, setNote] = useState('')

  async function submitAdjust(ev) {
    ev.preventDefault()
    try {
      await adjust.mutateAsync({ leaveTypeId: adjustFor.leaveTypeId, days: Number(days), note: note.trim() || undefined })
      toast.success('Balance adjusted')
      setAdjustFor(null); setDays(''); setNote('')
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  return (
    <div className="space-y-4">
      <Card
        title="Leave balances"
        actions={
          <Select aria-label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))} className="!mt-0 w-28"
            options={Array.from({ length: 5 }, (_, i) => ({ value: new Date().getFullYear() - i, label: String(new Date().getFullYear() - i) }))} />
        }
      >
        {balances.isLoading ? (
          <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
        ) : !balances.data?.length ? (
          <EmptyState icon={CalendarDays} title="No balances" description="This employee has no leave entitlements for the selected year." />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                  <th className="px-3 py-2">Type</th>
                  <th className="px-3 py-2 text-right">Entitled</th>
                  <th className="px-3 py-2 text-right">Carried</th>
                  <th className="px-3 py-2 text-right">Accrued</th>
                  <th className="px-3 py-2 text-right">Used</th>
                  <th className="px-3 py-2 text-right">Pending</th>
                  <th className="px-3 py-2 text-right">Adjusted</th>
                  <th className="px-3 py-2 text-right">Available</th>
                  {canAdjust && <th className="px-3 py-2" />}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {balances.data.map((b) => (
                  <tr key={b.leaveTypeId} className="hover:bg-slate-50 dark:hover:bg-slate-800/60">
                    <td className="px-3 py-2 font-medium text-slate-900 dark:text-slate-100">
                      <span className="inline-flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: b.colorHex || '#2563eb' }} aria-hidden="true" />
                        {b.typeName}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right">{b.entitledDays}</td>
                    <td className="px-3 py-2 text-right">{b.carriedOverDays}</td>
                    <td className="px-3 py-2 text-right">{b.accruedDays}</td>
                    <td className="px-3 py-2 text-right">{b.usedDays}</td>
                    <td className="px-3 py-2 text-right">{b.pendingDays}</td>
                    <td className="px-3 py-2 text-right">{b.adjustedDays}</td>
                    <td className="px-3 py-2 text-right font-bold text-slate-900 dark:text-white">{b.available}</td>
                    {canAdjust && (
                      <td className="px-3 py-2 text-right">
                        <Button variant="ghost" size="sm" onClick={() => setAdjustFor(b)}>Adjust</Button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Card title="Leave transactions">
        {transactions.isLoading ? (
          <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
        ) : !transactions.data?.length ? (
          <EmptyState icon={Clock} title="No transactions" description="Accruals, usage and adjustments appear here." />
        ) : (
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {transactions.data.map((t) => (
              <li key={t.id} className="flex flex-wrap items-center justify-between gap-2 py-2.5">
                <div>
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">{humanize(t.type)}</p>
                  <p className="text-xs text-slate-400">{t.description || ''}{t.actor ? ` · by ${t.actor}` : ''} · {formatDateTime(t.createdAtUtc)}</p>
                </div>
                <p className="text-sm">
                  <span className={`font-semibold ${t.days >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}`}>
                    {t.days >= 0 ? '+' : ''}{t.days}d
                  </span>
                  <span className="ml-2 text-xs text-slate-400">bal. {t.balanceAfter}</span>
                </p>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Modal open={!!adjustFor} onClose={() => setAdjustFor(null)} title={`Adjust ${adjustFor?.typeName || ''} balance`} size="sm"
        footer={<><Button variant="secondary" onClick={() => setAdjustFor(null)}>Cancel</Button><Button onClick={submitAdjust} loading={adjust.isPending}>Apply</Button></>}>
        <form onSubmit={submitAdjust} className="space-y-4">
          <Input label="Days" type="number" step="0.5" value={days} onChange={(e) => setDays(e.target.value)} required
            hint="Negative to reduce, positive to add" autoFocus />
          <Textarea label="Note (optional)" value={note} onChange={(e) => setNote(e.target.value)} />
        </form>
      </Modal>
    </div>
  )
}

// ------------------------------- Attendance -------------------------------

function AttendanceTab({ employeeId }) {
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const summary = useAttendanceSummary(employeeId, year, month)

  const s = summary.data
  const chips = s && [
    { label: 'Working days', value: s.workingDays },
    { label: 'Present', value: s.daysPresent },
    { label: 'Late', value: s.daysLate },
    { label: 'Absent', value: s.daysAbsent },
    { label: 'On leave', value: s.daysOnLeave },
    { label: 'Holiday', value: s.daysHoliday },
    { label: 'Worked', value: duration(s.totalWorkedMinutes) },
    { label: 'Overtime', value: duration(s.totalOvertimeMinutes) },
    { label: 'Late time', value: duration(s.totalLateMinutes) },
  ]

  return (
    <Card
      title="Attendance summary"
      actions={
        <div className="flex gap-2">
          <Select aria-label="Month" value={month} onChange={(e) => setMonth(Number(e.target.value))} className="!mt-0 w-36"
            options={Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: new Date(2000, i, 1).toLocaleString([], { month: 'long' }) }))} />
          <Select aria-label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))} className="!mt-0 w-24"
            options={Array.from({ length: 5 }, (_, i) => ({ value: now.getFullYear() - i, label: String(now.getFullYear() - i) }))} />
        </div>
      }
    >
      {summary.isLoading ? (
        <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
      ) : !s ? (
        <EmptyState icon={Clock} title="No data" description="No attendance summary for this month." />
      ) : (
        <>
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-5">
            {chips.map((c) => (
              <div key={c.label} className="rounded-md bg-slate-50 p-2.5 text-center dark:bg-slate-800/60">
                <p className="text-[11px] uppercase text-slate-400">{c.label}</p>
                <p className="mt-0.5 text-sm font-bold text-slate-900 dark:text-white">{c.value}</p>
              </div>
            ))}
          </div>
          {s.records?.length > 0 && (
            <div className="mt-4 overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                    <th className="px-3 py-2">Date</th>
                    <th className="px-3 py-2">In</th>
                    <th className="px-3 py-2">Out</th>
                    <th className="px-3 py-2">Worked</th>
                    <th className="px-3 py-2">Late</th>
                    <th className="px-3 py-2">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {s.records.map((r) => (
                    <tr key={r.id}>
                      <td className="px-3 py-2">{formatDate(r.workDate)}</td>
                      <td className="px-3 py-2">{formatTime(r.clockInUtc)}</td>
                      <td className="px-3 py-2">{formatTime(r.clockOutUtc)}</td>
                      <td className="px-3 py-2">{duration(r.workedMinutes)}</td>
                      <td className="px-3 py-2">{r.lateMinutes ? `${r.lateMinutes}m` : '—'}</td>
                      <td className="px-3 py-2"><Badge tone={statusTone(r.status)}>{humanize(r.status)}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </Card>
  )
}

// ------------------------------- Offboard / Delete -------------------------------

function OffboardButton({ employee }) {
  const toast = useToast()
  const offboard = useOffboardEmployee()
  const [open, setOpen] = useState(false)
  const [status, setStatus] = useState('Terminated')
  const [date, setDate] = useState(todayInput())
  const [reason, setReason] = useState('')

  async function submit(ev) {
    ev.preventDefault()
    try {
      await offboard.mutateAsync({ id: employee.id, status, terminationDate: date, reason: reason.trim() || undefined })
      toast.success(`${employee.fullName} offboarded (${status})`)
      setOpen(false)
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  return (
    <>
      <Button variant="secondary" onClick={() => setOpen(true)}><UserMinus className="h-4 w-4" aria-hidden="true" /> Offboard</Button>
      <Modal open={open} onClose={() => setOpen(false)} title={`Offboard ${employee.fullName}`} size="sm"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button variant="danger" onClick={submit} loading={offboard.isPending}>Offboard</Button></>}>
        <form onSubmit={submit} className="space-y-4">
          <Select label="Reason type" value={status} onChange={(e) => setStatus(e.target.value)}
            options={[{ value: 'Terminated', label: 'Terminated' }, { value: 'Resigned', label: 'Resigned' }, { value: 'Retired', label: 'Retired' }]} />
          <Input label="Termination date" type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
          <Textarea label="Notes (optional)" value={reason} onChange={(e) => setReason(e.target.value)} />
        </form>
      </Modal>
    </>
  )
}

function DeleteButton({ employee }) {
  const toast = useToast()
  const navigate = useNavigate()
  const remove = useDeleteEmployee()
  const [open, setOpen] = useState(false)

  async function confirm() {
    try {
      await remove.mutateAsync(employee.id)
      toast.success(`${employee.fullName} deleted`)
      navigate('/employees')
    } catch (e) { toast.error(await apiErrorMessage(e)); setOpen(false) }
  }

  return (
    <>
      <Button variant="ghost" aria-label={`Delete ${employee.fullName}`} onClick={() => setOpen(true)}>
        <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
      </Button>
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        onConfirm={confirm}
        danger
        loading={remove.isPending}
        title="Delete employee?"
        message={`${employee.fullName} will be soft-deleted and removed from the active directory.`}
        confirmLabel="Delete"
      />
    </>
  )
}
