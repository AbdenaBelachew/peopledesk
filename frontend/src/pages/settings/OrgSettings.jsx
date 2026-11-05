import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useOrgSettings, useUpdateOrgSettings } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import Card from '../../components/ui/Card'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Checkbox from '../../components/ui/Checkbox'
import Button from '../../components/ui/Button'
import Spinner from '../../components/ui/Spinner'
import { MONTHS } from '../../lib/format'

const schema = z.object({
  fiscalYearStartMonth: z.coerce.number().int().min(1).max(12),
  lateGraceMinutes: z.coerce.number().int().min(0),
  overtimeEnabled: z.boolean(),
  overtimeMultiplier: z.coerce.number().min(1, 'At least 1.0'),
  requireApprovalForSensitiveProfileChanges: z.boolean(),
  autoApproveLeaveWithoutManager: z.boolean(),
})

export default function OrgSettings() {
  const settings = useOrgSettings()
  const update = useUpdateOrgSettings()
  const toast = useToast()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      fiscalYearStartMonth: 1,
      lateGraceMinutes: 10,
      overtimeEnabled: false,
      overtimeMultiplier: 1.5,
      requireApprovalForSensitiveProfileChanges: true,
      autoApproveLeaveWithoutManager: false,
    },
  })

  useEffect(() => {
    if (settings.data) reset(settings.data)
  }, [settings.data, reset])

  async function onSubmit(values) {
    try {
      await update.mutateAsync(values)
      toast.success('Settings saved')
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  if (settings.isLoading) return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>

  return (
    <Card title="General settings" subtitle="Payroll, attendance and approval defaults.">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        <div className="grid gap-4 sm:grid-cols-2">
          <Select
            label="Fiscal year starts in"
            options={MONTHS.map((m, i) => ({ value: i + 1, label: m }))}
            error={errors.fiscalYearStartMonth?.message}
            {...register('fiscalYearStartMonth')}
          />
          <Input
            label="Late grace period (minutes)"
            type="number"
            min="0"
            hint="Clock-ins within this window are not marked late"
            error={errors.lateGraceMinutes?.message}
            {...register('lateGraceMinutes')}
          />
          <Input
            label="Overtime multiplier"
            type="number"
            step="0.05"
            min="1"
            hint="e.g. 1.5 = time and a half"
            error={errors.overtimeMultiplier?.message}
            {...register('overtimeMultiplier')}
          />
        </div>
        <div className="space-y-3 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Checkbox
            label="Enable overtime tracking"
            description="Time beyond the work schedule is paid at the multiplier above."
            {...register('overtimeEnabled')}
          />
          <Checkbox
            label="Require approval for sensitive profile changes"
            description="Employees' bank/TIN edits need HR sign-off."
            {...register('requireApprovalForSensitiveProfileChanges')}
          />
          <Checkbox
            label="Auto-approve leave when no manager is assigned"
            description="Requests from employees without a manager skip the approval step."
            {...register('autoApproveLeaveWithoutManager')}
          />
        </div>
        <Button type="submit" loading={isSubmitting} disabled={!isDirty}>Save settings</Button>
      </form>
    </Card>
  )
}
