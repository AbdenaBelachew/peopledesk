import { useState } from 'react'
import { Plus, Pencil, Megaphone } from 'lucide-react'
import { useAnnouncements, useSaveAnnouncement } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDateTime, dateInput } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import Modal from '../../components/ui/Modal'
import EmptyState from '../../components/ui/EmptyState'
import Spinner from '../../components/ui/Spinner'

export default function Announcements() {
  const [includeExpired, setIncludeExpired] = useState(true)
  const announcements = useAnnouncements(includeExpired)
  const save = useSaveAnnouncement()
  const toast = useToast()
  const [editor, setEditor] = useState(null)

  return (
    <div>
      <PageHeader
        title="Announcements"
        subtitle="Company-wide notices shown on dashboards."
        actions={<Button onClick={() => setEditor({})}><Plus className="h-4 w-4" aria-hidden="true" /> New announcement</Button>}
      />

      <Checkbox
        label="Show expired announcements"
        checked={includeExpired}
        onChange={(e) => setIncludeExpired(e.target.checked)}
        className="mb-3"
      />

      {announcements.isLoading ? (
        <div className="py-12 text-center"><Spinner className="mx-auto" /></div>
      ) : !announcements.data?.length ? (
        <Card>
          <EmptyState icon={Megaphone} title="No announcements" description="Publish news, policy changes and events to everyone." />
        </Card>
      ) : (
        <div className="space-y-3">
          {announcements.data.map((a) => {
            const expired = a.expiresAtUtc && new Date(a.expiresAtUtc) < new Date()
            return (
              <Card key={a.id}>
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-semibold text-slate-900 dark:text-slate-100">{a.title}</p>
                    <p className="mt-1 whitespace-pre-line text-sm text-slate-600 dark:text-slate-300">{a.body}</p>
                    <p className="mt-2 text-xs text-slate-400">
                      Published {formatDateTime(a.publishedAtUtc)}
                      {a.expiresAtUtc ? ` · expires ${formatDateTime(a.expiresAtUtc)}` : ''}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    {expired && <Badge tone="red">Expired</Badge>}
                    <Button variant="ghost" size="icon" aria-label={`Edit ${a.title}`} onClick={() => setEditor(a)}>
                      <Pencil className="h-4 w-4" aria-hidden="true" />
                    </Button>
                  </div>
                </div>
              </Card>
            )
          })}
        </div>
      )}

      {editor !== null && (
        <AnnouncementModal
          open
          onClose={() => setEditor(null)}
          announcement={editor.id ? editor : null}
          onSave={async (values) => {
            try {
              await save.mutateAsync(values)
              toast.success(editor.id ? 'Announcement updated' : 'Announcement published')
              setEditor(null)
            } catch (e) {
              toast.error(await apiErrorMessage(e))
            }
          }}
          saving={save.isPending}
        />
      )}
    </div>
  )
}

function AnnouncementModal({ open, onClose, announcement, onSave, saving }) {
  const [values, setValues] = useState({
    title: announcement?.title || '',
    body: announcement?.body || '',
    expiresAt: announcement?.expiresAtUtc ? dateInput(announcement.expiresAtUtc) : '',
  })

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  function submit(e) {
    e.preventDefault()
    if (!values.title.trim() || !values.body.trim()) return
    onSave({
      ...(announcement?.id ? { id: announcement.id } : {}),
      title: values.title.trim(),
      body: values.body.trim(),
      expiresAtUtc: values.expiresAt ? new Date(`${values.expiresAt}T23:59:59`).toISOString() : undefined,
    })
  }

  return (
    <Modal open={open} onClose={onClose} title={announcement ? 'Edit announcement' : 'New announcement'}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={saving}>Publish</Button></>}>
      <form onSubmit={submit} className="space-y-4">
        <Input label="Title" value={values.title} onChange={(e) => set('title', e.target.value)} required autoFocus />
        <Textarea label="Body" rows={5} value={values.body} onChange={(e) => set('body', e.target.value)} required />
        <Input label="Expires on (optional)" type="date" value={values.expiresAt} onChange={(e) => set('expiresAt', e.target.value)} />
      </form>
    </Modal>
  )
}
