# Client Decisions

**Last updated:** September 28, 2026 · **Contract reference:** SLA section 6.3 · **Requirements:** A-3, A-4, A-6, A-7

When we started building, a handful of things in the client's requirements weren't spelled out: units, which ranges block a save, how some numbers get calculated. Instead of waiting on email replies, we answered each question from Vulcan's own document (*Standard DOT Concrete Mix Design Rev 1*, which we'll just call **Rev 1**) or from the industry standards Rev 1 points to.

This file is where those answers live. If you're building something and aren't sure how it should behave, check here first. If it's not here, ask Amelia or Tyler. Please don't contact the client directly.

The exact numbers for every rule are in [`schema.md`](schema.md), section 4. This file is the "why" behind them.

**Two words you'll see a lot:**

- **Reject:** the value can't be saved. The operator gets an error that says what the allowed range is.
- **Warn:** the value saves, but it's flagged in amber so someone looks at it.

---

## 1. Mix percentages are by volume, not weight

Rev 1 gives a percentage for each ingredient but never says whether it means weight or volume. We're treating them as **volume**: how much space each ingredient takes up in the finished concrete.

Rev 1's ranges (cement 10–15%, sand 25–30%, gravel 40–55%, water 15–20%) are almost word for word the volume proportions NRMCA publishes: "Typically, a mixture is by volume about 10 to 15 percent cement, 60 to 75 percent aggregates and 15 to 20 percent water."

**"But plants mix by weight."** They do. Georgia DOT requires approved plants to weigh cement and aggregates on scales. That's how the plant *measures* the ingredients. The recipe is still *described* by how much space each one fills. It's like a baker who weighs flour on a scale while the recipe says "2 cups." Both describe the same flour.

**The check that settles it.** Rev 1 also says the ideal water-to-cement ratio is 0.40 to 0.60. If the percentages were by weight, that ratio would just be water ÷ cement:

| If Rev 1 means... | Lowest possible ratio | Highest possible ratio | Ever lands in 0.40–0.60? |
|---|---|---|---|
| Weight | 15 ÷ 15 = 1.00 | 20 ÷ 10 = 2.00 | No, never |
| Volume | 15 ÷ (15 × 3.15) = 0.32 | 20 ÷ (10 × 3.15) = 0.63 | Yes |

Read by weight, Rev 1 contradicts itself. Read by volume, it works. Here's a typical cubic yard both ways (volume uses specific gravities of 3.15 for cement, 2.65 for sand and 2.70 for stone):

| Ingredient | Pounds | By weight | By volume | Rev 1 says |
|---|---|---|---|---|
| Cement | 564 | 14% | 11% | 10–15% |
| Water | 280 | 7% | 17% | 15–20% |
| Sand | 1,300 | 33% | 29% | 25–30% |
| Stone | 1,800 | 46% | 40% | 40–55% |

(The last ~4% by volume is air.)

If real Vulcan data ever shows up in pounds, we'll convert it to volume when we import it. That's a Milestone 2 problem.

*Sources:* Rev 1, "Core Ingredients" and "Hydration" · [NRMCA, About Concrete](https://www.nrmca.org/about-nrmca/about-concrete/) · [Georgia DOT Lab SOP 10, Quality Assurance for Concrete Plants](https://www.dot.ga.gov/partnersmart/business/source/sop/sop10.pdf)
*Affects:* `Composition` table, batch entry form

## 1a. How we calculate water-to-cement ratio

Rev 1 defines the ratio by **mass** ("the mass of water to the mass of cement"), but we store volume. To convert, we multiply each ingredient's volume by its **specific gravity**, which is how much heavier it is than the same amount of water:

```
w/c = water% ÷ (cement% × 3.15  +  SCM% × SCM specific gravity)
```

We include SCM (fly ash, slag or silica fume) in the bottom half because Rev 1 says SCMs are "used to replace a portion of cement." Strictly speaking, that makes it a water-to-*cementitious* ratio (w/cm), which is the industry term when SCMs are counted. With no SCM, the two are the same number. The app **warns** outside 0.40–0.60.

Example: 12% cement, 18% water, no SCM → 18 ÷ (12 × 3.15) = **0.48**. No warning.

| Material | Specific gravity | Where it comes from |
|---|---|---|
| Portland cement | 3.15 | "generally around 3.15" (Pavement Interactive) |
| Water | 1.00 | By definition |
| Slag | 2.90 | PCA gives 2.85–2.95; we use the middle |
| Fly ash | 2.35 | PCA gives 1.9–2.8; we use the middle |
| Silica fume | 2.35 | PCA gives 2.2–2.5; we use the middle |

These live in the `SpecificGravity` table, so if Vulcan gives us their actual numbers, we change the data, not the code.

*Sources:* Rev 1, "Hydration" · [Pavement Interactive, Portland Cement Specific Gravity](https://pavementinteractive.org/reference-desk/testing/cement-tests/portland-cement-specific-gravity/) · [PCA, *Design and Control of Concrete Mixtures*, Ch. 3](https://www.ce.memphis.edu/1101/notes/concrete/PCA_manual_14/Chap03.pdf)
*Affects:* `SpecificGravity` table, `Composition.ScmType`, the w/c calculation

## 2. Ingredients can add up to 100% or less

Rev 1's ranges don't have to add up to exactly 100%, and in real concrete they usually don't. The rest is air (NRMCA: "another 5 to 8 percent"). So the app **rejects** a total over 100% and shows whatever's left as "Air / other." We don't store that number. It's calculated each time.

*Source:* [NRMCA, About Concrete](https://www.nrmca.org/about-nrmca/about-concrete/)
*Affects:* composition validation, the running total on the batch form

## 3. Break strength: 3,000–10,000 PSI, but failing breaks still save

Rev 1 gives the same range for the 3-day, 7-day and 28-day breaks and for the expected final break. We apply it two ways:

- **Expected final break** (the design target): **reject** outside 3,000–10,000 PSI. A target outside that range is a typo.
- **Actual break results:** **warn** below 3,000 PSI, **reject** above 10,000 PSI or at 0 or below.

Why actual breaks below 3,000 aren't rejected: Rev 1 asks for an "early warning system" for mixes that "could produce a failing break test." A failed break is exactly the data that system needs. If the app refused to save a 2,400 PSI break, the failure would never be recorded and the prediction model would never learn from it. Low early breaks are also normal: a 3-day break is often well under the 28-day strength.

*Source:* Rev 1: "3-day break in PSI (3000 to 10000 range)," repeated for each age; "What the software needs to accomplish"
*Affects:* `BreakResult.StrengthPsi`, `Batch.ExpectedFinalBreakPsi`

## 4. pH is the mix water's pH

Rev 1 says "PH reading 1 to 9 (7.0 to 7.5 is normal)." That range can't describe the concrete itself, which runs around pH 12–13 while it cures. It does fit the mix water: Rev 1 calls for potable water "free from harmful amounts of oils, acids, alkalis," and near-neutral water is right in 7.0–7.5.

So pH is recorded **once per batch, for the mix water**, not on every curing reading. Two rules: **reject** outside 1–9, **warn** outside 7.0–7.5.

*Source:* Rev 1, "PH reading" and "Potable Water"
*Affects:* `Batch.MixWaterPh`

## 5. Temperatures in °F, with a curing-range warning

Rev 1 asks for internal and ambient temperature but doesn't give units or a range. Everything else in Rev 1 is in US units (pounds, inches, PSI), so we use **°F**.

For ambient temperature, the app **warns** outside 60–80°F. That's the initial-curing range ASTM C31 sets for test cylinders, so a reading outside it could explain a weak break.

*Source:* ["Who Is Watching Out for the Cylinders?" *Concrete International*, via NRMCA](https://www.nrmca.org/wp-content/uploads/2020/06/CI4008cylindercuring.pdf) (explains the ASTM C31 curing requirements)
*Affects:* `CuringReading.InternalTempF`, `CuringReading.AmbientTempF`

## 6. Batch numbers: flexible for now

We don't know what Vulcan's batch numbers look like, and there's no industry-standard format. Georgia DOT requires each delivery to come with a ticket (project number, truck number, batch time, proportions and so on) but doesn't say how a ticket or batch number should be formatted.

So for now the app accepts **letters, numbers and dashes, up to 32 characters**. Each number must be unique, and capitalization doesn't matter (`A-100` and `a-100` are the same batch). Once we see a real Vulcan batch number, we'll tighten this.

*Source:* [Georgia DOT Lab SOP 10](https://www.dot.ga.gov/partnersmart/business/source/sop/sop10.pdf), batch ticket requirements
*Affects:* `Batch.BatchNumber`, batch search

## 7. All four cement oxides are required

Rev 1 marks alumina, iron oxide, magnesium oxide and sulfur trioxide each as "needs this concentration in test software," so all four are **required**.

There are two levels of limits:

- **Warn** when a value is outside the normal range for Portland cement, from the composition table in Rev 1: alumina 3.0–7.5%, iron oxide 0.5–6.0%, magnesium oxide 0.1–4.0%, sulfur trioxide 1.3–3.0%.
- **Reject** when a value is over the maximum the national cement spec allows for any cement type we accept: magnesium oxide above 6.0%, sulfur trioxide above 4.5%.

Rev 1 says the cement must conform to "AASHTO/ASTM standards." That means AASHTO M 85 or ASTM C150, which set essentially the same limits. Georgia DOT requires AASHTO M 85 for all cement on its projects.

**Why 4.5% for sulfur trioxide.** ASTM C150's limit depends on the cement type and its tricalcium aluminate (C₃A) content:

| Type | C₃A 8% or less | C₃A over 8% |
|---|---|---|
| I | 3.0% | 3.5% |
| II | 3.0% | n/a |
| III | 3.5% | 4.5% |

We don't collect C₃A, and decision 8 allows Type III, so 4.5% is the highest value a valid mill certificate can show. A lower hard limit would block real Type III cement. Anything above 3.0% still gets the amber warning.

One thing to keep straight: these are percentages **of the cement**, not of the whole batch. They never count toward the 100% total in decision 2.

*Sources:* Rev 1, oxide sections and "Portland Cement Ingredient Composition" · [ASTM C150, Standard Specification for Portland Cement](https://store.astm.org/standards/c150), Table 1 · [Georgia DOT Section 830, Portland Cement](https://www.dot.ga.gov/PartnerSmart/Business/Source/specs/ss830.pdf) · [NPCA, "A Closer Look: Cement Types I Through V"](https://precast.org/blog/a-closer-look-cement-types-i-through-v/) (6% magnesium oxide max)
*Affects:* `CementOxideAnalysis` table

## 8. Cement type is recorded per batch

Rev 1 says the mix uses "Portland Cement (Type I, II, or III)," so every batch records which one. Georgia DOT allows Types I and II for general work and Types I and III for high-early-strength concrete, so all three are realistic.

*Sources:* Rev 1 · [Georgia DOT Section 830](https://www.dot.ga.gov/PartnerSmart/Business/Source/specs/ss830.pdf)
*Affects:* `Batch.CementType` (A-4)

## 9. Units and field names

- Resistivity is in **ohms per foot**, as Rev 1 lists it.
- CO₂ is in **ppm**. Rev 1 asks for "Carbon Dioxide content" but doesn't give a unit. ppm is the standard unit for CO₂ sensors, so we chose it.
- **"Sand" and "fine aggregate" are one field** (`FineAggregatePct`). Rev 1's entry list names them separately, but its ingredient section defines them as the same thing: "Fine Aggregate / Sand (25–30%)" and "Fine Aggregates: Clean washed concrete sand."

*Source:* Rev 1
*Affects:* `CuringReading`, `Composition`, entry forms

## 10. Foreign object detection is on hold

Rev 1: "Foreign object detection we can leave off at this time but if there is time near the end, we can discuss and maybe integrate that feature." It's not part of Milestone 1.

---

## Our own defaults (not client rules)

These fill gaps Rev 1 doesn't cover. They're our calls, and we'll change them if real data proves them wrong. The numeric ones live in the `ValidationLimit` table, so changing them is a data change, not a code change.

- **Windows 11, 64-bit.** We build on .NET 10, which supports Windows 10 only in its Enterprise, LTSC and IoT editions ([supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)). If Vulcan's workstations are still on Windows 10, we need to know early. Affects the installer and build target (`win-x64`).
- **Temperature typo guards.** Ambient rejects outside 0–150°F and internal outside 0–200°F. These don't reflect how concrete behaves. They just catch someone typing 720 instead of 72.
- **Times.** Stored in UTC, shown in the computer's local time, so daylight saving never scrambles the order of readings. Vulcan sees Eastern time on screen.
- **Two-tier validation.** Rev 1 separates "allowed" from "normal" for pH, so we use the same reject/warn pattern everywhere it makes sense.
- **Technician name.** Entered once per session instead of on every batch. Every record still stores who created it (FR-22).

---

## If something changes

If Vulcan tells us something different from what's here:

1. Amelia or Tyler updates this file in a pull request and quotes or links what the client said.
2. If a number changed, `schema.md` gets updated in the same pull request.
3. We post it in the team channel.
4. It goes in the next Friday status report, with Professor Murphy copied.
