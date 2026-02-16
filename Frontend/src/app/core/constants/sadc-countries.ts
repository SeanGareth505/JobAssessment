export const SADC_COUNTRIES: { code: string; name: string; currency: string }[] = [
  { code: 'AO', name: 'Angola', currency: 'AOA' },
  { code: 'BW', name: 'Botswana', currency: 'BWP' },
  { code: 'KM', name: 'Comoros', currency: 'KMF' },
  { code: 'CD', name: 'DRC', currency: 'CDF' },
  { code: 'SZ', name: 'Eswatini', currency: 'SZL' },
  { code: 'LS', name: 'Lesotho', currency: 'LSL' },
  { code: 'MG', name: 'Madagascar', currency: 'MGA' },
  { code: 'MW', name: 'Malawi', currency: 'MWK' },
  { code: 'MU', name: 'Mauritius', currency: 'MUR' },
  { code: 'MZ', name: 'Mozambique', currency: 'MZN' },
  { code: 'NA', name: 'Namibia', currency: 'NAD' },
  { code: 'SC', name: 'Seychelles', currency: 'SCR' },
  { code: 'ZA', name: 'South Africa', currency: 'ZAR' },
  { code: 'TZ', name: 'Tanzania', currency: 'TZS' },
  { code: 'ZM', name: 'Zambia', currency: 'ZMW' },
  { code: 'ZW', name: 'Zimbabwe', currency: 'ZWL' },
];

export function getAllowedCurrenciesForCountry(countryCode: string): string[] {
  const c = SADC_COUNTRIES.find((x) => x.code === countryCode);
  if (!c) return [];
  if (c.code === 'ZW') return ['ZWL', 'USD'];
  return [c.currency];
}

export const ORDER_STATUS_LABELS: Record<number, string> = {
  0: 'Pending',
  1: 'Paid',
  2: 'Fulfilled',
  3: 'Cancelled',
};
