# The Block

The buyer side of a wholesale vehicle auction, built against the 200-lot dataset in [`data/vehicles.json`](data/vehicles.json).

## How to run

You need two things installed:

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (10.0.100 or later — pinned in [`global.json`](global.json))
- **[Node.js 20+](https://nodejs.org)**

Then, from the repository root:

```bash
npm install
npm run dev
```

Open **http://localhost:5173**.

That starts both processes: the API on port 5124 and the client on 5173, which proxies to it. The database is a SQLite file created and seeded on first run — there is no migration step, no Docker, and nothing else to install. The API takes a few seconds to build the first time; the page waits for it rather than erroring.

To run the tests:

```bash
npm test          # or: dotnet test
```

To start over with a fresh sale, delete `api/theblock.db*` and run again.

## What I built

A buyer can browse the catalogue, filter it the way a dealer actually would, open a lot, read its condition honestly, and bid — with the price updating live as other bidding lands.

The interesting part is not the CRUD. It is that **the dataset is a trap, and most of the work went into reading it correctly.**

### The sale is 99 days dead

Every `auction_start` in the dataset falls between 2026-03-31 and 2026-04-06. The generator that produced them ([`scripts/generate_vehicles.mjs:484`](scripts/generate_vehicles.mjs)) picked dates one to seven days *ahead of the moment it ran*, and then the file was frozen. Rendered literally, all 200 lots ended months ago and the marketplace is a graveyard.

So the app normalises them, which the brief explicitly permits. It does it by pulling *now* backward onto the sale's timeline rather than pushing 200 lots forward onto the real one: **sale time is real time minus a stored offset.** Because the offset is a stored value rather than something recomputed on each read, sale time advances in lockstep with real time — a lot two hours from closing has one hour left an hour later. Deriving the offset from "now" on every read is the obvious version of this and it freezes the world, holding every lot at a constant distance from closing forever.

The offset is computed once when the database is seeded and persisted with it, so restarts and hot reloads do not rewind the sale. It is chosen so the sale opens **118 lots in preview, 26 open for bidding, 56 closed, and one on the block** — and snapped so the first lot hammers about five minutes after you first open the page.

It snaps *forward*, which matters more than it sounds. Lots only ever open between 09:00 and 20:00, so more than half the timeline is an overnight dead zone, and the natural target lands inside one. Snapping to the nearest close steps backward into that gap and leaves nothing happening until morning. Snapping forward lands at 09:00 with 26 lots closing across the next twelve hours.

A startup guard re-anchors a stale database once the forward catalogue drops below a quarter of the lots. The sale only spans 179 hours, so a database left alone for a few days has run past its own end — without the guard, coming back next week means 200 dead lots.

### It is a timed-online sale, and the data says so

There is no `auction_end` in the dataset, so a lot's run had to be invented. It opens at `auction_start` and closes 24 hours later, and that duration is not arbitrary: lots only open between 09:00 and 20:00, so a whole-day run closes them inside that same band. A 12- or 36-hour run hammers lots at 3am.

The sale is timed-online rather than a literal block because the data rules the alternative out:

- **Seven lots share the timestamp `2026-04-06T10:00`.** Seven cars cannot cross one ramp at one instant.
- **Lot numbers run in lanes of fifty, but open times are unrelated to lot order.** Lane A runs A-0001 on Apr 5 and A-0002 on Mar 31. A physical lane runs in lot order; this one does not.

So `auction_start` means "bidding opens", and a lane is a catalogue partition — which is exactly what a lane is in a digital marketplace. The block metaphor is kept for the closing ten minutes, where it still means something: a lot goes **on the block**, and a late bid holds it open rather than letting it be won by whoever clicks last.

### Pre-bidding, which is why 88 lots have bids they cannot have

88 lots carry bids despite opening in the future. That reads as corrupt data until you know that wholesale buyers bid during preview, before a lot runs. Naming the state **Preview** and letting it take bids turns the contradiction into the model, and the seeded bidding reproduces each lot's recorded `bid_count` and `current_bid` exactly as the state it was in before it opened.

### Bidding is a script, revealed by the clock

A lot's price is not a column — it is the highest bid standing at the current sale time. That means a lot's whole bidding history can be written at seed (about 4,200 bids) and simply revealed as the clock reaches it.

This is what makes the sale feel inhabited. The clock cannot do it alone: the dataset's own grid caps closes at roughly one an hour, and the live channel has nothing to carry because there are no other users. Two browser windows passing a single bid between them is not a marketplace. It also makes the reveal reversible — nothing is stored as a running total, so winding the clock back un-happens bids.

### "IF" — the ending this catalogue is mostly made of

The dataset draws `current_bid` from 72–102% of each lot's reserve, so taken at face value only **4 of 200 lots ever clear it**. A results view built on that reads as 11% conversion against roughly 60% in real wholesale, which looks like a bug rather than a sale. Bidding across each lot's run closes that gap: the sale now finishes **68% sold, 18% IF, 14% no sale.**

Reserve-not-met stays a real outcome rather than being engineered away, and it gets its real name. An **IF** is a lot that closed under its reserve: the high bid goes to the seller, who may still accept it. It is an outcome, not a failure, and the page says so in words rather than hiding behind the jargon.

## Notable decisions

**The reserve never leaves the server.** Buyers are told whether bidding has cleared the reserve, never what it is — sellers do not publish that number. This is the clearest thing the backend buys: on a frontend-only build the reserve ships to the browser and sits in the devtools network tab. There is a test that guards the boundary itself, so a field added to a contract later cannot quietly reopen it.

**`current_bid` is null on 112 of 200 lots, not zero.** The brief's own sample vehicle shows a number, which is misleading — over half the catalogue has never been bid on. Those lots are not free, and the type stays nullable all the way to the component that renders it, so the UI has to say "Opening bid $8,000" rather than pricing a vehicle at nothing.

**Bidding runs on the server.** The client proposes an amount; it does not decide whether that amount is a bid. Increments widen with the money ($100 under $10k, $250 under $25k, $500 above), and a refusal comes back with the figure that would have worked so the UI can offer it. Bidding is serialised through the lot, which is the aggregate root for its own bids — two buyers who read the same price cannot both win. Verified under a real race: six simultaneous bids at the same amount produced one winner and five conflicts.

**Browsing is client-side; bidding is not.** All 200 lots arrive in one ~130KB response and are filtered and sorted in the browser. At this size that beats a round trip per facet change, and browsing is the one part of this that genuinely does not need a server. At 200,000 lots it moves behind keyset pagination.

**The filters are the ones a wholesale buyer uses.** Condition grade, title, odometer, damage, province, reserve status, lane. The dataset also has exterior colour, interior colour, engine and fuel type, and none of them are facets — nobody buys wholesale by colour. Province is not a nicety either: trucking a car from BC to Ontario eats the margin. Default sort is closing soonest, with lots you can still bid on ahead of lots that already closed.

**No Docker, no migrations, no ORM ceremony.** A single minimal-API project with a pure domain module — deliberately not Clean Architecture. For 200 lots, one bounded context and four endpoints, MediatR and a five-project split would cost more than they return, and the brief asks for clarity over surface area.

**A dev-only clock scrubber.** A sale spread over a week closes a lot about once an hour, which is true to how wholesale runs and useless to watch. **Run this lot** winds the sale to ninety seconds before a chosen lot closes: it goes on the block, the bidding lands, a late bid extends it, and it resolves. It works because everything reads the sale clock and nothing reads the wall clock behind its back — one offset moves the whole world coherently. Gated on the server, not just hidden in the client.

### Things the dataset gets wrong

Worth naming, since both forced a decision:

- **Lot D-0013 records 17 bids across a $500 spread.** At the $500 increment its price band requires, 17 bids need $8,000 of room. 21 of the 88 bid-carrying lots are impossible to reproduce on a strictly legal ladder, so seeded history compresses to fit. Both dataset facts are worth more than a tidy ladder in a history nobody can bid against; the increment rule governs new bids, where it is enforced.
- **The reserve distribution makes the sale a catastrophe on paper**, as above.

## Assumptions and scope

Included because the brief asked: browsing, search, a real detail view, a bid flow with live state, desktop and mobile.

Deliberately left out:

- **Authentication.** Not required. Buyers are identified by an id the browser invents and keeps — per tab rather than per browser, so two tabs are two buyers and a bidding war can be watched from both sides on one machine.
- **Buy now.** The price is shown; the flow is not built. The brief excludes checkout, and a price you can see and not act on is more honest than a button that lies. The page says so.
- **Seller and dealer tooling, payments, transport booking.** Out of scope by the brief.
- **A photo lightbox.** Every image is a placeholder on someone else's host. There is nothing to zoom into, and it would be craft spent on the one part of the dataset that is admittedly fake. The frames hold their shape and degrade to a labelled space rather than a broken icon.
- **Pagination or virtualisation.** 200 cards render fine.

Known limitations:

- **Bids survive a restart; the sale's identity does not.** Close the tab and you are a new buyer, though your bids remain.
- **Seeded bidding does not extend lots.** It is a reconstruction of what already happened; only live bids trigger anti-snipe. This also makes the demo sharper — your bid is the one that visibly buys time.
- **The notifier polls once a second.** At 200 lots that is nothing. A real sale would push from the write path.

## Stack

- **Frontend:** React 19, TypeScript, Vite. No router, no state library, no UI kit — two views did not need them.
- **Backend:** .NET 10 minimal API, SignalR for the live channel.
- **Database:** SQLite via EF Core, created and seeded on first run.

Targeting .NET 10 rather than 9, which left support in May 2026.

## Testing

**94 tests**, `dotnet test`, all on the server where the logic lives.

The domain module — sale clock, lot lifecycle, bid policy — is pure and framework-free, and was written before EF Core or ASP.NET existed in the project. `git log` shows that rather than me claiming it. The tests worth looking at:

- **`SaleClockTests`** pins the property the whole design rests on: sale time advances in lockstep with real time, and a re-anchor moves lots and their bids together.
- **`SaleAnchorDatasetTests`** asserts the anchor against the real 200 lots, including the 118/26/56 composition this README quotes — so the prose cannot quietly drift from the code.
- **`BidConcurrencyTests`** runs two contexts against a real SQLite engine: two buyers reading the same price, one winner, and the refused bid leaving nothing behind.
- **`BidScriptTests`** proves every lot reproduces its recorded pre-bid state exactly, and that the sale finishes at a conversion rate a wholesaler would recognise.

The client is deliberately thin — formatting, filtering and rendering — so it has no test project. Everything that could be got wrong is on the other side of the wire.

Beyond the suite, the flows were driven in a browser: the bid path end to end, two windows watching one lot, a lot run to its hammer, and the layout at 375px.

## Time spent

<!-- Replace this with your own figure before sending the link. -->
_TODO_

## What I'd do with more time

- **A results view.** The sale finishes 68% sold / 18% IF / 14% no sale and there is nowhere to see that. It is the most interesting screen this data can produce and I ran out of room for it.
- **Proxy bidding.** Real wholesale lets a buyer set a maximum and have the system bid for them. It would also explain the dataset's bid counts more honestly than the compression I settled for.
- **Push from the write path** instead of a one-second sweep, and scope broadcasts to the lots a client is actually watching.
- **Server-side querying** behind the same seam, once the catalogue outgrows one response.
- **A watchlist**, which is the first thing a real dealer would ask for and the first thing I cut.
