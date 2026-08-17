import { Link } from "react-router-dom";

export function PlaceholderPage({ title, description }: { title: string; description: string }) {
  return (
    <section className="page-section narrow">
      <div className="page-header">
        <div>
          <h1>{title}</h1>
          <p>{description}</p>
        </div>
      </div>
      <Link className="button" to="/challenges">
        返回挑战
      </Link>
    </section>
  );
}
