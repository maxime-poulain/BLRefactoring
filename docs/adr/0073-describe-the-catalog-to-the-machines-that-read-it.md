# 0073 — Describe the catalog to the machines that read it

- **Status:** Accepted
- **Date:** 2026-08-11

## Context

ADR 0072 made the catalog's HTML readable: a crawler fetching `/catalog`, a training's page or a
trainer's profile now gets the page's own words. What it gets says nothing *about* itself. No
`description` for a search result to quote, so an engine invents one from whatever markup it likes.
No Open Graph, so a shared link unfurls as a bare address. No canonical, so every
`?sort=newest&page=2` view competes with the page it is a view of. No `robots.txt` and no sitemap,
so a crawler discovers the catalog only by following links and is never told which addresses exist
or which spaces are empty shells without a session. No structured data, so a training is prose
rather than a thing an engine can classify.

Two constraints shape any answer. The `/api` guard admits a caller that sends the application's
marker header or a browser's own `Sec-Fetch-Site` (ADR 0063) — a link unfurler sends neither, so
an `og:image` pointing at the API's portrait routes would answer 403 to exactly its audience. And
the repository's file inventory is closed (ADR 0066): a static `robots.txt` would be a new kind of
unread file, while a dynamic endpoint is code like any other — which suits both files anyway,
since the sitemap line and every sitemap address need an origin only the request knows.

## Decision

**Each prerendered page describes itself; the host serves the crawler's two files and a narrow
door to the portraits. Nothing about the guards changes.**

- **The head is the page's own.** Each catalog page writes `<HeadContent>` through the prerendered
  `HeadOutlet`: a `description` summarized from the page's own prose at a word boundary, Open
  Graph, a canonical, and JSON-LD — a `Course` for a training, a `Person` for a trainer, each the
  honest subset of what the page actually knows. No offers and no price, because the domain has
  neither; the `Course` provider is the platform as an `Organization`, because the vocabulary
  wants one there and the trainer is described on their own page.
- **The canonical names the question, not the view.** It keeps `topic` — a closed set of shelves —
  and drops `sort` and `page`, which are views of the same question, and `term`, because a
  free-text search is not an address to index: canonicalizing every permutation of a search box is
  how an index fills with noise. It is built from the page's whitelisted parameters, never from
  the raw address, so a tracking parameter cannot ride into it.
- **A panel that means "nothing here" says `noindex`.** The not-found and unreachable branches
  leave as HTTP 200 — the router is a client component with no status to set — and the tag is
  what keeps that soft answer out of an index. The full head is earned by having something to
  describe: canonical, Open Graph and JSON-LD render on the loaded branch alone.
- **`robots.txt` and `sitemap.xml` are endpoints on the BFF host, at the root, outside every
  guard.** A crawler sends no application header, and these answers exist for exactly that
  caller. The robots file shields the signed-in spaces — empty shells to a crawler — and names
  the sitemap absolutely. The sitemap lists what the catalog currently offers: the listing, each
  training, each trainer with something on offer — read through the same published, anonymous
  search the browser compiles, paged at the contract's own maximum, so the BFF stays outside the
  catalog's door and mounts no reader of its own (ADR 0059). No `lastmod`, because the rows carry
  no date and inventing one would teach crawlers to trust a lie. An unreachable API answers 503:
  a half-empty sitemap teaches a crawler that the missing half is gone.
- **`og:image` points at a portrait pass-through, not at the API.** Two narrow GET routes under
  `/portraits/` forward to the portrait routes ADR 0063 already published — status, content type
  and bytes — and answer `immutable` for the same reason the API may: the address carries the
  photo's identity. Widening the `/api` guard to admit unfurlers would reopen the argument ADR
  0063 settled; a door beside it does not.
- **The routes that do not prerender get a static title and description in `App.razor`,** gated to
  exactly them: their head is empty until WebAssembly boots, and the catalog's pages — which
  write their own — must not have it doubled.

One caveat, recorded rather than solved: the host does not read forwarded headers, so behind a
TLS-terminating proxy the origin written into the canonical, the sitemap and the portrait
addresses is the scheme and host the proxy hands this process. Deploying behind one means teaching
the host `UseForwardedHeaders` first.

## Consequences

- A search result for a training shows the training's own words; a shared link unfurls with a
  title, a description and a face; every view of a page tells the index which address is the page.
- A crawler is told what exists (`sitemap.xml`), what not to waste a crawl on (`robots.txt`), and
  what a training *is* (JSON-LD), instead of inferring all three.
- The BFF host now has an audience that is neither the browser nor an operator, and a family of
  root-level endpoints for it, beside the health checks. The guards are untouched: the crawler's
  doors are new and narrow, not a relaxation of an existing one.
- The sitemap's freshness is the search index's: a training withdrawn this second still lists
  until a crawler asks again, which is the trade every sitemap makes.
- Mistyped or withdrawn identifiers keep answering 200 with a panel, but carry `noindex` — the
  soft-404 stays a rendering fact rather than becoming an indexing one.

## Verification

- `EveryPrerenderedPage_DescribesItselfToTheCrawler` (PrerenderingRules) — each of the three
  catalog pages carries a `<HeadContent>` with a description and a canonical; proved by mutation
  before it was trusted.
- `Robots_names_the_sitemap_and_shields_the_signed_in_spaces`,
  `The_sitemap_lists_what_the_catalog_offers`, `The_sitemap_says_unavailable_when_the_api_is_down`
  (BffTests) — the crawler's files, observed with a bare GET carrying none of the application's
  headers.
- `A_prerendered_training_page_carries_its_head` and `A_training_nobody_offers_is_marked_noindex`
  (BffTests) — the head reaches the prerendered HTML, and the soft answer carries its tag.
- `A_portrait_is_served_to_a_caller_with_no_headers` (BffTests) — the pass-through forwards the
  bytes and answers `immutable`, to a caller the `/api` guard would refuse.
