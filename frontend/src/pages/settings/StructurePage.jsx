import { useState } from 'react'
import { Plus, Pencil, Trash2, Network } from 'lucide-react'
import { useStructure, useSaveStructure, useDeleteStructure, useEmployees } from '../../api/queries'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Modal from '../../components/ui/Modal'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'

const CONFIG = {
  departments: { title: 'Departments', subtitle: 'Team structure used across the directory, reports and approvals.', withManager: true },
  branches: { title: 'Branches', subtitle: 'Physical locations where employees work.', withManager: false },
  positions: { title: 'Positions', subtitle: 'Job titles and levels.', withManager: false, withLevel: true },
  'employment-types': { title: 'Employment types', subtitle: 'Full-time, part-time, contract and similar categories.', withManager: false },
}

export default function StructurePage({ resource }) {
  const config = CONFIG[resource] || { title: resource }
  const { hasPerm } = useAuth()
  const canManage = hasPerm('structure.manage')
  const toast = useToast()

  const list = useStructure(resource)
  const save = useSaveStructure(resource)
  const remove = useDeleteStructure(resource)
  const employees = useEmployees({ page: 1, pageSize: 300 }, { enabled: canManage && config.withManager })

  const [editor, setEditor] = useState(null) // null | {} | item
  const [deleteFor, setDeleteFor] = useState(null)

  const columns = [
    { key: 'name', header: 'Name', render: (r) => <span className="font-medium text-slate-900 dark:text-slate-100">{r.name}</span> },
    { key: 'code', header: 'Code', render: (r) => r.code || '—' },
    ...(config.withLevel ? [{ key: 'level', header: 'Level', className: 'text-right', render: (r) => r.level ?? '—' }] : []),
    ...(config.withManager ? [{ key: 'managerEmployeeId', header: 'Manager', render: (r) => (employees.data?.items || []).find((e) => e.id === r.managerEmployeeId)?.fullName || '—' }] : []),
    { key: 'description', header: 'Description', render: (r) => <span className="max-w-64 truncate">{r.description || '—'}</span> },
    ...(list.data?.some((x) => x.employeeCount != null)
      ? [{ key: 'employeeCount', header: 'Employees', className: 'text-right' }]
      : []),
    ...(canManage
      ? [{
          key: 'actions', header: '', className: 'text-right',
          render: (r) => (
            <span className="flex justify-end gap-1">
              <Button variant="ghost" size="icon" aria-label={`Edit ${r.name}`} onClick={() => setEditor(r)}>
                <Pencil className="h-4 w-4" aria-hidden="true" />
              </Button>
              <Button variant="ghost" size="icon" aria-label={`Delete ${r.name}`} onClick={() => setDeleteFor(r)}>
                <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
              </Button>
            </span>
          ),
        }]
      : []),
  ]

  async function onConfirmDelete() {
    try {
      await remove.mutateAsync(deleteFor.id)
      toast.success(`${deleteFor.name} deleted`)
      setDeleteFor(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
      setDeleteFor(null)
    }
  }

  return (
    <div>
      <PageHeader
        title={config.title}
        subtitle={config.subtitle}
        actions={canManage && (
          <Button onClick={() => setEditor({})}><Plus className="h-4 w-4" aria-hidden="true" /> Add {config.title.replace(/s$/, '').toLowerCase()}</Button>
        )}
      />
      <Card bodyClassName="!p-0">
        <DataTable
          columns={columns}
          rows={list.data || []}
          loading={list.isLoading}
          empty={
            <EmptyState
              icon={Network}
              title={`No ${config.title.toLowerCase()} yet`}
              description={canManage ? 'Create the first one to organize your organization.' : 'Ask an administrator to set these up.'}
              action={canManage && <Button onClick={() => setEditor({})}>Add</Button>}
            />
          }
        />
      </Card>

      {editor !== null && (
        <EditorModal
          open
          onClose={() => setEditor(null)}
          resource={resource}
          item={editor.id ? editor : null}
          withManager={config.withManager}
          withLevel={config.withLevel}
          managerOptions={(employees.data?.items || []).map((e) => ({ value: e.id, label: `${e.fullName} (${e.employeeNumber})` }))}
          onSave={async (values) => {
            try {
              await save.mutateAsync(values)
              toast.success(editor.id ? 'Saved' : 'Created')
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
        title={`Delete ${deleteFor?.name || ''}?`}
        message="Employees assigned to it may need to be reassigned. This cannot be undone."
        confirmLabel="Delete"
      />
    </div>
  )
}

function EditorModal({ open, onClose, item, withManager, withLevel, managerOptions, onSave, saving }) {
  const [values, setValues] = useState({
    name: item?.name || '',
    code: item?.code || '',
    description: item?.description || '',
    managerEmployeeId: item?.managerEmployeeId || '',
    level: item?.level ?? '',
  })

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  function submit(e) {
    e.preventDefault()
    if (!values.name.trim()) return
    onSave({
      ...(item?.id ? { id: item.id } : {}),
      name: values.name.trim(),
      code: values.code.trim() || undefined,
      description: values.description.trim() || undefined,
      ...(withManager ? { managerEmployeeId: values.managerEmployeeId || undefined } : {}),
      ...(withLevel ? { level: values.level === '' ? undefined : Number(values.level) } : {}),
    })
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={item ? `Edit ${item.name}` : 'Add new'}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Save</Button></>}
    >
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Name" value={values.name} onChange={(e) => set('name', e.target.value)} required autoFocus className="sm:col-span-2" />
        <Input label="Code (optional)" value={values.code} onChange={(e) => set('code', e.target.value)} />
        {withLevel && <Input label="Level (optional)" type="number" value={values.level} onChange={(e) => set('level', e.target.value)} />}
        {withManager && (
          <Select label="Manager (optional)" placeholder="—" value={values.managerEmployeeId} onChange={(e) => set('managerEmployeeId', e.target.value)} options={managerOptions} className="sm:col-span-2" />
        )}
        <Textarea label="Description (optional)" value={values.description} onChange={(e) => set('description', e.target.value)} className="sm:col-span-2" />
      </form>
    </Modal>
  )
}
