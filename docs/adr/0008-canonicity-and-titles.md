# Canonicity is a third axis, cascaded from a new Title concept

Added **Canonicity** (Official/Unofficial/Parody) as a Connection property,
distinct from ADR-0007's Confidence. Confidence is about whether a human has
confirmed what a Connection's Interaction Tier actually is; Canonicity is
about whether the underlying event counts as real within the fiction at
all — a Connection can be Verified and still Unofficial (the Blondie 50th
anniversary strip really happened, on the record, but Charles Schulz never
treated Peanuts crossovers as canon) or Parody (Mad Magazine really did
lampoon a given character, but "both appeared in the same issue" implies no
relationship whatsoever between them).

Parody specifically needed a new **Title** concept (a publication like "Mad
Magazine," of which Issues are installments) so a whole anthology/satire
publication can be flagged once, cascading Parody Canonicity to every
Connection derived from its Issues — without requiring a human to review
each one individually the way Confidence normally requires.

Behavior: Parody-Canonicity Connections are stored (they can be the only
honest answer for some pairs — this is literally why the Snoopy → Snoop
Dogg query, the project's inspiration, might only connect through Mad
Magazine) but excluded from the *default* Batman Number path. They surface
only through an explicit opt-in — a single **Minimum Canonicity** control
(Official / Unofficial / Parody, see `docs/UI.md`), defaulting to
Unofficial. That default resolves the question `docs/POST_MVP.md` left
open: Unofficial Connections (e.g. the Blondie strip) *are* included by
default, since they're a real, deliberately-drawn interaction; only Parody
is opt-in — otherwise Mad Magazine, which has lampooned nearly everyone,
would become a universal free bridge the same way Shared Identity was
deliberately designed not to be.

The crawl should also skip using Parody-flagged Titles as a basis for
*expanding* the search frontier (chasing friends-of-friends leads generated
from an anthology appearance) even though it still records a direct
same-issue Connection between two characters actually being compared —
those leads are structurally meaningless and would waste request budget
under Comic Vine's rate limit for no real signal.
