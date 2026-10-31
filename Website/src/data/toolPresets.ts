/** Values verified against StyleCatalog.cs / KeycapMetrics.cs on 2026-10-29. */
export const keyPresets = {
  classic: { family: 'lowprofile', primary: '#FFFFFF', secondary: '#1A1A1A', ink: '#000000', border: '#1A1A1A', group: '#FFFFFF99' },
  graphite: { family: 'lowprofile', primary: '#2B2D31', secondary: '#111214', ink: '#F2F3F5', border: '#0B0C0E', group: '#00000066' },
  silver: { family: 'laptop', primary: '#E9EBEF', secondary: '#C3C8D0', ink: '#1F2328', border: '#9AA1AB', group: '#FFFFFF80' },
  midnight: { family: 'laptop', primary: '#2A2E37', secondary: '#1B1E24', ink: '#E8EAED', border: '#0E1013', group: '#00000059' },
  cream: { family: 'pbt', primary: '#F4EBDD', secondary: '#CDBFA8', ink: '#3B3227', border: '#6B5E4C', group: '#FFFFFF73' },
  ocean: { family: 'pbt', primary: '#DCEBF5', secondary: '#3E6E8E', ink: '#12324A', border: '#1D3B52', group: '#FFFFFF73' },
  minimal: { family: 'minimal', primary: 'transparent', secondary: 'transparent', ink: '#FFFFFF', border: 'transparent', group: '#0000008C' },
  ink: { family: 'minimal', primary: 'transparent', secondary: 'transparent', ink: '#111111', border: 'transparent', group: '#FFFFFFB3' },
  lavender: { family: '3d', primary: '#E8E9FB', secondary: '#A9ACDB', ink: '#1A1B3A', border: '#1E2147', group: 'transparent' },
  mint: { family: '3d', primary: '#C7EBD4', secondary: '#5FAE7E', ink: '#12301F', border: '#173826', group: 'transparent' },
  rose: { family: '3d', primary: '#F6D3E6', secondary: '#D9629B', ink: '#3A1028', border: '#3A1028', group: 'transparent' },
} as const;
export type KeyPreset = keyof typeof keyPresets;
export const familyPresets: KeyPreset[] = ['classic', 'silver', 'cream', 'minimal', 'lavender'];
export const profilePresets: KeyPreset[] = ['cream', 'graphite', 'mint'];
export const mousePresets = {
  outline: { line: '#FFFFFF', accent: '#22C55E', plate: '#00000099', body: 'transparent', top: 'transparent' },
  solid: { line: '#1C1C1C', accent: '#EF4444', plate: '#FFFFFFCC', body: '#1C1C1C', top: '#1C1C1C' },
  retro: { line: '#5E6B80', accent: '#7DD3FC', plate: '#FFFFFF59', body: '#B8C4D6', top: '#C8D2E0' },
} as const;
export type MousePreset = keyof typeof mousePresets;
