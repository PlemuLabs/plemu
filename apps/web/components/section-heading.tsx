import Link from "next/link";
import { Icon } from "./icons";
export function SectionHeading({
  eyebrow,
  title,
  href,
  link = "Explore all",
}: {
  eyebrow: string;
  title: string;
  href?: string;
  link?: string;
}) {
  return (
    <div className="section-heading">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h2>{title}</h2>
      </div>
      {href ? (
        <Link className="text-link" href={href}>
          {link}
          <Icon name="arrow" />
        </Link>
      ) : null}
    </div>
  );
}
