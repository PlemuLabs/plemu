import Link from "next/link";
export default function NotFound() {
  return (
    <div className="page-container empty-state not-found">
      <span className="eyebrow">404 / SIGNAL LOST</span>
      <h1>This world is off the map.</h1>
      <p>Head back to the arcade and discover something new.</p>
      <Link href="/" className="button primary">
        Back to Explore →
      </Link>
    </div>
  );
}
