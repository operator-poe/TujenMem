#!/usr/bin/env node

/**
 * CLI tool to query PoE trade API for T16 corrupted map prices.
 * Designed to be called by an agent — never exits on transient errors.
 *
 * Usage:
 *   POESESSID=abc123 node map-price-cli.mjs [options]
 *
 * Options (all optional):
 *   --rarity <n>          Min item rarity (map_iir)
 *   --quantity <n>         Min item quantity (map_iiq)
 *   --pack-size <n>        Min pack size (pseudo stat)
 *   --maps <n>             Min "more map drops" (pseudo stat)
 *   --scarabs <n>          Min "more scarab drops" (pseudo stat)
 *   --currency <n>         Min "more currency drops" (pseudo stat)
 *   --min-price <amt><c|d> Min price, e.g. 3d (3 divine) or 300c (300 chaos)
 */

const API_BASE = 'https://www.pathofexile.com'
const POESESSID = 'edf9696b85fb2f5b60701dd25c30f9fe'
const LEAGUE = 'Mirage'
const MAX_RETRIES = 5
const RESULTS_LIMIT = 10

if (!POESESSID) {
  console.error('Error: POESESSID environment variable is required.')
  process.exit(1)
}

const HEADERS = {
  'Accept': 'application/json',
  'Content-Type': 'application/json',
  'User-Agent': 'awakened-poe-trade-cli/1.0',
  'Cookie': `POESESSID=${POESESSID}`
}

// --- Arg parsing ---

function parseArgs () {
  const args = process.argv.slice(2)
  const opts = {
    rarity: null,
    quantity: null,
    packSize: null,
    maps: null,
    scarabs: null,
    currency: null,
    minPrice: null,
    priceCurrency: null
  }

  for (let i = 0; i < args.length; i++) {
    switch (args[i]) {
      case '--rarity': opts.rarity = parseInt(args[++i]); break
      case '--quantity': opts.quantity = parseInt(args[++i]); break
      case '--pack-size': opts.packSize = parseInt(args[++i]); break
      case '--maps': opts.maps = parseInt(args[++i]); break
      case '--scarabs': opts.scarabs = parseInt(args[++i]); break
      case '--currency': opts.currency = parseInt(args[++i]); break
      case '--min-price': {
        const raw = args[++i]
        const suffix = raw.slice(-1).toLowerCase()
        const amount = parseFloat(raw.slice(0, -1))
        if (suffix === 'd') {
          opts.priceCurrency = 'divine'
        } else if (suffix === 'c') {
          opts.priceCurrency = 'chaos'
        } else {
          console.error(`Invalid --min-price suffix: "${suffix}". Use "c" (chaos) or "d" (divine), e.g. 3d or 300c`)
          process.exit(1)
        }
        if (isNaN(amount)) {
          console.error(`Invalid --min-price amount: "${raw}"`)
          process.exit(1)
        }
        opts.minPrice = amount
        break
      }
      default:
        console.error(`Unknown option: ${args[i]}`)
        process.exit(1)
    }
  }

  return opts
}

// --- Rate limit tracking ---

let rateLimitState = { remaining: null, resetAt: 0 }

function logRateLimits (headers) {
  const rules = headers.get('x-rate-limit-rules')
  if (!rules) return

  for (const rule of rules.split(',')) {
    const limit = headers.get(`x-rate-limit-${rule}`)
    const state = headers.get(`x-rate-limit-${rule}-state`)
    if (limit && state) {
      console.error(`  Rate limit [${rule}]: ${state} / ${limit}`)
    }
  }
}

async function rateLimitedFetch (url, init) {
  const now = Date.now()
  if (rateLimitState.remaining === 0 && now < rateLimitState.resetAt) {
    const waitMs = rateLimitState.resetAt - now + 100
    console.error(`  Waiting ${(waitMs / 1000).toFixed(1)}s for rate limit...`)
    await new Promise(r => setTimeout(r, waitMs))
  }

  const res = await fetch(url, init)

  logRateLimits(res.headers)

  if (res.status === 429) {
    const retryAfter = res.headers.get('retry-after')
    const waitSec = retryAfter ? parseInt(retryAfter) : 60
    console.error(`  Rate limited! Waiting ${waitSec}s...`)
    await new Promise(r => setTimeout(r, waitSec * 1000))
    return rateLimitedFetch(url, init)
  }

  return res
}

// --- API helpers ---

async function safeJson (res) {
  const text = await res.text()
  try {
    return JSON.parse(text)
  } catch {
    const { writeFileSync } = await import('node:fs')
    writeFileSync('error.html', text)
    console.error('Non-JSON response body written to error.html')
    throw new Error(`Non-JSON response (HTTP ${res.status}), see error.html`)
  }
}

function buildQueryBody (opts) {
  const statFilters = [{ id: 'implicit.stat_2696470877' }]
  if (opts.maps != null) statFilters.push({ id: 'pseudo.pseudo_map_more_map_drops', value: { min: opts.maps } })
  if (opts.scarabs != null) statFilters.push({ id: 'pseudo.pseudo_map_more_scarab_drops', value: { min: opts.scarabs } })
  if (opts.currency != null) statFilters.push({ id: 'pseudo.pseudo_map_more_currency_drops', value: { min: opts.currency } })
  if (opts.packSize != null) statFilters.push({ id: 'pseudo.pseudo_map_quality_pack_size', value: { min: opts.packSize } })

  const mapFilterEntries = { map_tier: { min: 16 }, map_series: { option: 'current' } }
  if (opts.rarity != null) mapFilterEntries.map_iir = { min: opts.rarity }
  if (opts.quantity != null) mapFilterEntries.map_iiq = { min: opts.quantity }

  const tradeFilterEntries = { indexed: { option: '3hours' } }
  if (opts.minPrice != null) tradeFilterEntries.price = { option: opts.priceCurrency, min: opts.minPrice }

  return {
    query: {
      status: { option: 'securable' },
      stats: [{ type: 'and', filters: statFilters }],
      filters: {
        map_filters: { disabled: false, filters: mapFilterEntries },
        misc_filters: { filters: { corrupted: { option: 'true' } } },
        trade_filters: { filters: tradeFilterEntries }
      }
    },
    sort: { price: 'asc' }
  }
}

async function tradeSearch (opts) {
  const body = buildQueryBody(opts)

  console.error(`\nSearching trade API (league: ${LEAGUE})...`)
  console.error(`Query: ${JSON.stringify(body, null, 2)}\n`)

  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    try {
      const res = await rateLimitedFetch(`${API_BASE}/api/trade/search/${encodeURIComponent(LEAGUE)}`, {
        method: 'POST',
        headers: HEADERS,
        body: JSON.stringify(body)
      })

      const data = await safeJson(res)
      if (!data.error) return data

      const waitSec = Math.min(5 * 2 ** (attempt - 1), 120)
      console.error(`Trade API error (attempt ${attempt}/${MAX_RETRIES}): ${data.error.message} (code: ${data.error.code}). Retrying in ${waitSec}s...`)
      await new Promise(r => setTimeout(r, waitSec * 1000))
    } catch (err) {
      const waitSec = Math.min(5 * 2 ** (attempt - 1), 120)
      console.error(`Trade search error (attempt ${attempt}/${MAX_RETRIES}): ${err.message}. Retrying in ${waitSec}s...`)
      await new Promise(r => setTimeout(r, waitSec * 1000))
    }
  }

  console.error('Trade search failed after all retries.')
  return { id: null, result: [], total: 0 }
}

async function fetchResults (queryId, resultIds) {
  if (resultIds.length === 0) return []

  console.error(`Fetching ${resultIds.length} results...`)

  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    try {
      const res = await rateLimitedFetch(
        `${API_BASE}/api/trade/fetch/${resultIds.join(',')}?query=${queryId}`,
        { headers: HEADERS }
      )

      const data = await safeJson(res)
      if (!data.error) return data.result.filter(r => r != null)

      const waitSec = Math.min(5 * 2 ** (attempt - 1), 120)
      console.error(`Fetch error (attempt ${attempt}/${MAX_RETRIES}): ${data.error.message}. Retrying in ${waitSec}s...`)
      await new Promise(r => setTimeout(r, waitSec * 1000))
    } catch (err) {
      const waitSec = Math.min(5 * 2 ** (attempt - 1), 120)
      console.error(`Fetch error (attempt ${attempt}/${MAX_RETRIES}): ${err.message}. Retrying in ${waitSec}s...`)
      await new Promise(r => setTimeout(r, waitSec * 1000))
    }
  }

  console.error('Result fetch failed after all retries.')
  return []
}

// --- Display ---

function displayTradeResults (results) {
  if (results.length === 0) {
    console.log('No results found.')
    return
  }

  console.log(`\n${'Price'.padEnd(20)} ${'Seller'.padEnd(25)} ${'IGN'.padEnd(25)} Listed`)
  console.log('-'.repeat(85))

  for (const r of results) {
    const price = r.listing.price
      ? `${r.listing.price.amount} ${r.listing.price.currency}`
      : 'no price'
    const account = r.listing.account.name
    const ign = r.listing.account.lastCharacterName
    const indexed = r.listing.indexed

    console.log(
      `${price.padEnd(20)} ${account.padEnd(25)} ${ign.padEnd(25)} ${indexed}`
    )
  }
}

// --- Main ---

async function main () {
  const opts = parseArgs()

  const search = await tradeSearch(opts)
  if (!search.id) return

  console.error(`Found ${search.total} total results (fetching up to ${RESULTS_LIMIT})`)

  const idsToFetch = search.result.slice(0, RESULTS_LIMIT)
  const allResults = []
  for (let i = 0; i < idsToFetch.length; i += 10) {
    const batch = idsToFetch.slice(i, i + 10)
    const results = await fetchResults(search.id, batch)
    allResults.push(...results)
  }

  displayTradeResults(allResults)

  console.log(`\nhttps://www.pathofexile.com/trade/search/${encodeURIComponent(LEAGUE)}/${search.id}`)
}

main().catch(err => {
  console.error('Fatal error:', err.message)
  process.exit(1)
})
