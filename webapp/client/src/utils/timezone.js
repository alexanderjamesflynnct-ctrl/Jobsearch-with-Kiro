/**
 * Country/region → IANA timezone mapping.
 * For US jobs we also map by state.
 */
const COUNTRY_TZ = {
  'USA':          'America/New_York',   // default, overridden by state below
  'Canada':       'America/Toronto',
  'UK':           'Europe/London',
  'Ireland':      'Europe/Dublin',
  'Australia':    'Australia/Sydney',
  'New Zealand':  'Pacific/Auckland',
  'Singapore':    'Asia/Singapore',
  'Hong Kong':    'Asia/Hong_Kong',
  'India':        'Asia/Kolkata',
  'South Africa': 'Africa/Johannesburg',
  'Nigeria':      'Africa/Lagos',
  'Philippines':  'Asia/Manila',
  'Pakistan':     'Asia/Karachi',
  'Germany':      'Europe/Berlin',
  'France':       'Europe/Paris',
  'Netherlands':  'Europe/Amsterdam',
}

// US state → IANA timezone
const US_STATE_TZ = {
  // Eastern
  CT:'America/New_York', DE:'America/New_York', FL:'America/New_York',
  GA:'America/New_York', IN:'America/New_York', KY:'America/New_York',
  MA:'America/New_York', MD:'America/New_York', ME:'America/New_York',
  MI:'America/New_York', NC:'America/New_York', NH:'America/New_York',
  NJ:'America/New_York', NY:'America/New_York', OH:'America/New_York',
  PA:'America/New_York', RI:'America/New_York', SC:'America/New_York',
  TN:'America/New_York', VA:'America/New_York', VT:'America/New_York',
  WV:'America/New_York', DC:'America/New_York',
  // Central
  AL:'America/Chicago', AR:'America/Chicago', IA:'America/Chicago',
  IL:'America/Chicago', KS:'America/Chicago', LA:'America/Chicago',
  MN:'America/Chicago', MO:'America/Chicago', MS:'America/Chicago',
  ND:'America/Chicago', NE:'America/Chicago', OK:'America/Chicago',
  SD:'America/Chicago', TX:'America/Chicago', WI:'America/Chicago',
  // Mountain
  AZ:'America/Phoenix', CO:'America/Denver', ID:'America/Denver',
  MT:'America/Denver',  NM:'America/Denver', UT:'America/Denver',
  WY:'America/Denver',
  // Pacific
  CA:'America/Los_Angeles', NV:'America/Los_Angeles', OR:'America/Los_Angeles',
  WA:'America/Los_Angeles',
  // Other
  AK:'America/Anchorage', HI:'Pacific/Honolulu',
}

const EST_TZ = 'America/New_York'

/**
 * Get the IANA timezone for a job based on country + state.
 */
export function getJobTimezone(country, state) {
  if (country === 'USA' && state && US_STATE_TZ[state.toUpperCase()]) {
    return US_STATE_TZ[state.toUpperCase()]
  }
  return COUNTRY_TZ[country] ?? null
}

/**
 * Get a human-readable EST offset label for a job location.
 * e.g. "EST", "EST-3h", "EST+5h"
 */
export function getTimezoneLabel(country, state) {
  const tz = getJobTimezone(country, state)
  if (!tz) return null
  try {
    const now = new Date()
    const diff = getOffsetHours(tz, now) - getOffsetHours(EST_TZ, now)
    if (diff === 0) return 'EST'
    return diff > 0 ? `EST+${diff}h` : `EST${diff}h`
  } catch {
    return null
  }
}

function getOffsetHours(tz, date) {
  // Use Intl to get the UTC offset in minutes for a given timezone
  const utcDate   = new Date(date.toLocaleString('en-US', { timeZone: 'UTC' }))
  const tzDate    = new Date(date.toLocaleString('en-US', { timeZone: tz }))
  return Math.round((tzDate - utcDate) / 3600000)
}

/**
 * Format a UTC ISO string to EST display.
 */
export function toEST(isoString) {
  if (!isoString) return '—'
  try {
    const d = new Date(isoString)
    return d.toLocaleString('en-US', {
      timeZone: EST_TZ,
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
      timeZoneName: 'short',
    })
  } catch {
    return isoString.slice(0, 16).replace('T', ' ')
  }
}

/**
 * Format a UTC ISO string to the local time of a given country.
 */
export function toLocalTime(isoString, country, state) {
  if (!isoString) return null
  const tz = getJobTimezone(country, state)
  if (!tz) return null
  try {
    const d = new Date(isoString)
    return d.toLocaleString('en-US', {
      timeZone: tz,
      month: 'short', day: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
      timeZoneName: 'short',
    })
  } catch {
    return null
  }
}

export function getUTCOffset(country, state) {
  const tz = getJobTimezone(country, state)
  if (!tz) return null
  try {
    const now = new Date()
    return new Intl.DateTimeFormat('en-US', { timeZone: tz, timeZoneName: 'shortOffset' })
      .formatToParts(now).find(p => p.type === 'timeZoneName')?.value ?? null
  } catch {
    return null
  }
}
