// Recharts renders SVG with inline styles, so `dark:` Tailwind variants can't
// reach it. These consume the CSS variables from index.css, which flip with the
// `.dark` class — giving charts seamless light/dark styling.
export const chartTooltipProps = {
  contentStyle: {
    background: 'var(--pd-tt-bg)',
    border: '1px solid var(--pd-tt-border)',
    borderRadius: 12,
    color: 'var(--pd-tt-text)',
    boxShadow: 'var(--pd-tt-shadow)',
    fontSize: 12,
    padding: '8px 12px',
  },
  labelStyle: { color: 'var(--pd-tt-text)', fontWeight: 600, marginBottom: 4 },
  itemStyle: { color: 'var(--pd-tt-text)' },
  cursor: { fill: 'rgba(148, 163, 184, 0.12)' },
}

export const chartTick = { fontSize: 11, fill: 'var(--pd-tt-text)', opacity: 0.75 }
