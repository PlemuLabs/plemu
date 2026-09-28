# DMCA / Takedown Policy

Plemu takes intellectual property seriously. This document describes how to submit a
takedown notice, what happens after submission, and how to file a counter-notice.

---

## Designated Agent

**TODO:** Register a designated DMCA agent with the U.S. Copyright Office at
https://www.copyright.gov/dmca-directory/ before launch.

Interim contact while registration is pending:

> Designated Agent: TBD-verify  
> Organization: Plemu  
> Email: dmca@plemu.xyz (TBD-verify — confirm address before publishing)  
> Mailing address: TBD-verify

---

## Notice Requirements

A valid takedown notice under 17 U.S.C. § 512(c)(3) must include:

1. Identification of the copyrighted work you claim is infringed.
2. Identification of the infringing material — include the registry entry id (e.g.
   `gb/starforge-demo`) and any CDN URLs.
3. Your contact information (name, address, phone, email).
4. A statement that you have a good-faith belief the use is not authorised.
5. A statement that the information in the notice is accurate and, under penalty of perjury, that
   you are the copyright owner or authorised to act on their behalf.
6. Your physical or electronic signature.

Submit notices to the designated agent above. You may also open an Issue using the structured
[Takedown form](.github/ISSUE_TEMPLATE/takedown.yml) for faster routing.

---

## Response Target: <24 Hours

Upon receipt of a valid notice, we target action within **24 hours**:

1. The on-call curator triggers the **emergency-revoke workflow**, which sets `revoked: true` and
   `revokedReason: "dmca"` in the next signed manifest. The manifest is republished immediately.
2. CDN delivery of the artifact is suspended.
3. The registry entry is flagged with a `DMCA-HOLD` label in GitHub; the PR / entry is not deleted
   pending counter-notice window.
4. The claimant is notified by email that action has been taken.

The 24-hour target is best-effort; complex cases (e.g. counter-notice already on file) may take
longer. We will acknowledge receipt within 4 hours during business days.

---

## Counter-Notice Window (10–14 Business Days)

If a creator believes their work was wrongly removed, they may file a counter-notice under
17 U.S.C. § 512(g)(3). A valid counter-notice must include:

1. Identification of the removed material and its location before removal.
2. A statement under penalty of perjury that the material was removed by mistake or misidentification.
3. The creator's name, address, phone number, and consent to jurisdiction of the federal district
   court for their location (or, if outside the U.S., consent to the jurisdiction of any judicial
   district in which we may be found).
4. Physical or electronic signature.

Upon receipt of a valid counter-notice, we will:

- Notify the original claimant that we received a counter-notice.
- Wait **10–14 business days**.
- If no court action is filed by the claimant during that period, restore the content.

---

## Reinstatement

Reinstatement is performed by re-running the emergency-revoke workflow in reverse (setting
`revoked: false`). The registry entry is preserved throughout the hold period precisely to allow
reinstatement.

---

## Transparency Log (`removals.ndjson`)

Every takedown action (removal and reinstatement) is recorded in `removals.ndjson` at the root of
this repository. The file is newline-delimited JSON; each line is one event:

```json
{"eventType":"removed","entryId":"gb/starforge-demo","reason":"dmca","timestamp":"2026-08-01T10:00:00Z","noticeRef":"DMCA-2026-001"}
{"eventType":"reinstated","entryId":"gb/starforge-demo","reason":"counter-notice-accepted","timestamp":"2026-08-15T10:00:00Z","noticeRef":"DMCA-2026-001"}
```

Fields: `eventType` (`removed` | `reinstated`), `entryId`, `reason`, `timestamp` (ISO 8601 UTC),
`noticeRef` (internal reference number). The file starts empty; do not add comments (NDJSON does
not support comments).
