# Author Outreach Template

Use this template when contacting a game creator to request permission to include their work on
Plemu. Adapt the subject and greeting; keep the body's key facts intact.

---

## Email Template

**Subject:** Plemu — permission request for [Game Title]

---

Hi [Author Name / handle],

My name is [Your Name], and I'm a curator for **Plemu** — an open, community-curated
library of homebrew, public-domain, and freely-licensed retro games, distributed via a mobile
Android app.

I came across **[Game Title]** and think it would be a great fit. Before including anything we need
explicit permission from the creator, so I'm reaching out directly.

**What Plemu is:**
- A curated library of legal homebrew content for retro gaming systems and fantasy consoles.
- Free to download and play for users.
- The platform includes a **tip and NFT creator economy**, meaning players can optionally tip
  creators and creators can mint NFTs of their works. This involves potential commercial flows, so
  **if you are not comfortable with commercial uses of your work, please decline** — we respect
  that, and there is no pressure.

**What we need from you:**

If you are happy to have your game included, please reply with a simple statement like:

> I grant Plemu the right to distribute [Game Title] under [the license you choose,
> e.g. CC0, CC-BY 4.0, MIT, or a custom grant].

That reply — your written grant — will be archived in our registry as the legal basis for
inclusion. We'll link your creator wallet / profile page from the game's entry if you'd like.

**What we will do:**
- Archive your grant text in our public registry.
- Credit you as the creator in the app and metadata.
- Remove the game promptly if you ever change your mind (just email us).

If you have questions about the platform, the license options, or the process, feel free to ask.
If you'd prefer we not include your game, just say so — we'll respect that immediately.

Thanks for your time and for making great games,

[Your Name]
Plemu — [hub URL TBD-verify]
Registry contact: [contact email TBD-verify]

---

## After You Receive a Reply

1. Save the full email thread (headers + body) as a `.eml` or `.txt` file.
2. Commit it to `license-evidence/` in the entry directory as `author-grant.eml` (or `.txt`).
3. Set `license.type` to the agreed license in `metadata.json`.
4. Set `license.evidenceUrl` to `null` (local archive) or the public forum/post URL if applicable.
5. Summarise the grant in `license-evidence/evidence.md`.
