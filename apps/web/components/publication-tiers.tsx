import { publicationTiers } from "../lib/creation-paths";
export function PublicationTiers() {
  return (
    <section
      className="publication-tiers"
      aria-labelledby="publication-heading"
    >
      <span className="eyebrow">FROM YOUR HANDHELD TO THE WORLD</span>
      <h2 id="publication-heading">
        Publication tiers<span className="pixel-text">Choose your reach.</span>
      </h2>
      <p>
        Local content, private packs and public publishing are three different
        things. Here’s how they stack.
      </p>
      <ol className="publication-ladder">
        {publicationTiers.map((tier, i) => (
          <li key={tier.title}>
            <div className="tier-track">
              <span>0{i + 1}</span>
              <span aria-hidden="true">{i === 3 ? "✦" : "→"}</span>
            </div>
            <small>{tier.reach}</small>
            <h3>{tier.title}</h3>
            <p>{tier.description}</p>
          </li>
        ))}
      </ol>
      <p className="local-note">
        A path to wider distribution, not automatic approval. Concepts saved
        here stay in this browser; public publishing requires review.
      </p>
    </section>
  );
}
