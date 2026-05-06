/**
 * Deterministic company name anonymizer.
 * Same company always gets the same placeholder within a session.
 */
const ADJECTIVES = [
  'Global', 'Dynamic', 'Innovative', 'Strategic', 'Advanced',
  'Premier', 'Unified', 'Integrated', 'Digital', 'Apex',
  'Nexus', 'Vertex', 'Quantum', 'Stellar', 'Horizon',
  'Pinnacle', 'Catalyst', 'Synergy', 'Vanguard', 'Meridian',
]

const NOUNS = [
  'Solutions', 'Systems', 'Technologies', 'Enterprises', 'Group',
  'Partners', 'Ventures', 'Industries', 'Services', 'Corp',
  'Labs', 'Works', 'Dynamics', 'Networks', 'Holdings',
  'Consulting', 'Analytics', 'Platforms', 'Capital', 'Associates',
]

const cache = new Map()

function hashStr(str) {
  let h = 0
  for (let i = 0; i < str.length; i++) {
    h = (Math.imul(31, h) + str.charCodeAt(i)) | 0
  }
  return Math.abs(h)
}

export function anonymize(company) {
  if (!company) return company
  if (cache.has(company)) return cache.get(company)
  const h    = hashStr(company)
  const adj  = ADJECTIVES[h % ADJECTIVES.length]
  const noun = NOUNS[Math.floor(h / ADJECTIVES.length) % NOUNS.length]
  const result = `${adj} ${noun}`
  cache.set(company, result)
  return result
}
