import { useEffect, useState } from 'react'
import { Plus, Save } from 'lucide-react'
import { useWorkSchedules, useSaveWorkSchedule } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { WEEKDAYS } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Checkbox from '../../components/ui/Checkbox'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'

// The API accepts numeric dayOfWeek (0 = Sunday … 6 = Saturday) but echoes
// enum names ("Monday"). Normalize both directions so the grid stays stable.
const DAY_NAMES = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday']

function parseDayOfWeek(value) {
  if (typeof value === 'number' && value >= 0 && value <= 6) return value
  const byName = DAY_NAMES.indexOf(String(value || '').toLowerCase())
  if (byName >= 0) return byName
  const n = Number.parseInt(value, 10)
  return Number.isFinite(n) && n >= 0 && n <= 6 ? n : 0
}

function normalizeSchedule(schedule) {
  const days = (schedule.days || []).map((d) => ({ ...d, dayOfWeek: parseDayOfWeek(d.dayOfWeek) }))
  days.sort((a, b) => a.dayOfWeek - b.dayOfWeek)
  // Fill any missing day so the 7-row grid always renders.
  for (let i = 0; i < 7; i++) {
    if (!days.some((d) => d.dayOfWeek === i)) {
      days.splice(i, 0, { dayOfWeek: i, isWorkingDay: i >= 1 && i <= 5, startTime: '09:00', endTime: '17:30', breakMinutes: 60 })
    }
  }
  return { ...schedule, days }
}

function blankDays() {
  return WEEKDAYS.map((name, dayOfWeek) => ({
    dayOfWeek,
    isWorkingDay: dayOfWeek >= 1 && dayOfWeek <= 5,
    startTime: '09:00',
    endTime: '17:30',
    breakMinutes: 60,
  }))
}

export default function WorkSchedules() {
  const schedules = useWorkSchedules()
  const save = useSaveWorkSchedule()
  const toast = useToast()
  const [drafts, setDrafts] = useState([])

  useEffect(() => {
    if (schedules.data) setDrafts(schedules.data.map(normalizeSchedule))
  }, [schedules.data])

  function updateDraft(index, updater) {
    setDrafts((d) => d.map((s, i) => (i === index ? updater(s) : s)))
  }

  async function persist(draft) {
    try {
      await save.mutateAsync({
        ...(draft.id ? { id: draft.id } : {}),
        name: draft.name,
        isDefault: draft.isDefault,
        days: draft.days,
      })
      toast.success(`Schedule “${draft.name}” saved`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <div>
      <PageHeader
        title="Work schedules"
        subtitle="Standard weekly hours used for lateness, overtime and payroll."
        actions={(
          <Button
            onClick={() =>
              setDrafts((d) => [...d, { name: 'New schedule', isDefault: false, days: blankDays() }])
            }
          >
            <Plus className="h-4 w-4" aria-hidden="true" /> Add schedule
          </Button>
        )}
      />

      {schedules.isLoading ? (
        <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
      ) : drafts.length === 0 ? (
        <EmptyState title="No schedules" description="Create a standard schedule for your organization." />
      ) : (
        <div className="space-y-4">
          {drafts.map((draft, idx) => (
            <Card
              key={draft.id || `new-${idx}`}
              title={(
                <input
                  value={draft.name}
                  onChange={(e) => updateDraft(idx, (s) => ({ ...s, name: e.target.value }))}
                  aria-label="Schedule name"
                  className="w-56 rounded border border-transparent bg-transparent px-1 py-0.5 text-sm font-semibold text-slate-900 hover:border-slate-300 focus:border-blue-500 focus:outline-none dark:text-slate-100 dark:hover:border-slate-600"
                />
              )}
              actions={(
                <div className="flex items-center gap-3">
                  <Checkbox
                    label="Default"
                    checked={draft.isDefault}
                    onChange={(e) => updateDraft(idx, (s) => ({ ...s, isDefault: e.target.checked }))}
                  />
                  <Button size="sm" loading={save.isPending} onClick={() => persist(draft)}>
                    <Save className="h-4 w-4" aria-hidden="true" /> Save
                  </Button>
                </div>
              )}
            >
              <div className="overflow-x-auto">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                      <th className="px-3 py-2">Day</th>
                      <th className="px-3 py-2">Working</th>
                      <th className="px-3 py-2">Start</th>
                      <th className="px-3 py-2">End</th>
                      <th className="px-3 py-2">Break (min)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {draft.days.map((day, di) => (
                      <tr key={day.dayOfWeek}>
                        <td className="px-3 py-2 font-medium text-slate-900 dark:text-slate-100">{WEEKDAYS[day.dayOfWeek]}</td>
                        <td className="px-3 py-2">
                          <Checkbox
                            aria-label={`${WEEKDAYS[day.dayOfWeek]} working day`}
                            checked={day.isWorkingDay}
                            onChange={(e) =>
                              updateDraft(idx, (s) => ({
                                ...s,
                                days: s.days.map((x, i) => (i === di ? { ...x, isWorkingDay: e.target.checked } : x)),
                              }))
                            }
                          />
                        </td>
                        <td className="px-3 py-2">
                          <input
                            type="time"
                            aria-label={`${WEEKDAYS[day.dayOfWeek]} start time`}
                            disabled={!day.isWorkingDay}
                            value={day.startTime}
                            onChange={(e) =>
                              updateDraft(idx, (s) => ({
                                ...s,
                                days: s.days.map((x, i) => (i === di ? { ...x, startTime: e.target.value } : x)),
                              }))
                            }
                            className="rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm disabled:opacity-40 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                          />
                        </td>
                        <td className="px-3 py-2">
                          <input
                            type="time"
                            aria-label={`${WEEKDAYS[day.dayOfWeek]} end time`}
                            disabled={!day.isWorkingDay}
                            value={day.endTime}
                            onChange={(e) =>
                              updateDraft(idx, (s) => ({
                                ...s,
                                days: s.days.map((x, i) => (i === di ? { ...x, endTime: e.target.value } : x)),
                              }))
                            }
                            className="rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm disabled:opacity-40 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                          />
                        </td>
                        <td className="px-3 py-2">
                          <input
                            type="number"
                            min="0"
                            aria-label={`${WEEKDAYS[day.dayOfWeek]} break minutes`}
                            disabled={!day.isWorkingDay}
                            value={day.breakMinutes}
                            onChange={(e) =>
                              updateDraft(idx, (s) => ({
                                ...s,
                                days: s.days.map((x, i) => (i === di ? { ...x, breakMinutes: Number(e.target.value) } : x)),
                              }))
                            }
                            className="w-20 rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm disabled:opacity-40 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
