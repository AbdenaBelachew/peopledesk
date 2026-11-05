import { useState } from 'react'
import PageHeader from '../components/ui/PageHeader'
import Tabs from '../components/ui/Tabs'
import HeadcountTab from './reports/HeadcountTab'
import AttendanceTab from './reports/AttendanceTab'
import LeaveTab from './reports/LeaveTab'
import PayrollExpensesTab from './reports/PayrollExpensesTab'
import TurnoverTab from './reports/TurnoverTab'

const TABS = [
  { key: 'headcount', label: 'Headcount' },
  { key: 'attendance', label: 'Attendance trends' },
  { key: 'leave', label: 'Leave utilization' },
  { key: 'payroll', label: 'Payroll expenses' },
  { key: 'turnover', label: 'Turnover' },
]

export default function Reports() {
  const [tab, setTab] = useState('headcount')
  return (
    <div>
      <PageHeader title="Reports" subtitle="Organization analytics with CSV / Excel exports." />
      <Tabs tabs={TABS} active={tab} onChange={setTab} className="mb-4" />
      {tab === 'headcount' && <HeadcountTab />}
      {tab === 'attendance' && <AttendanceTab />}
      {tab === 'leave' && <LeaveTab />}
      {tab === 'payroll' && <PayrollExpensesTab />}
      {tab === 'turnover' && <TurnoverTab />}
    </div>
  )
}
