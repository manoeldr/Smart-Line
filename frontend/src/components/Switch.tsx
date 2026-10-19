// Toggle switch reutilizável — substitui checkboxes simples por um visual mais moderno.
interface Props {
  checked: boolean
  onChange?: (checked: boolean) => void
  disabled?: boolean
}

export default function Switch({ checked, onChange, disabled }: Props) {
  return (
    <label className={`relative inline-flex items-center flex-shrink-0 ${disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}>
      <input
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={e => onChange?.(e.target.checked)}
        className="sr-only peer"
      />
      <div className="w-9 h-5 bg-superficie-3 rounded-full peer-checked:bg-accent-600 transition-colors" />
      {/* Ligado, a bolinha usa a cor do texto sobre o destaque (no tema escuro o trilho é claro) */}
      <div className="absolute left-0.5 top-0.5 bg-white w-4 h-4 rounded-full shadow transition-transform peer-checked:translate-x-4 peer-checked:bg-accent-fg" />
    </label>
  )
}