import { Link } from "react-router-dom";

export function ForbiddenPage() {
  return (
    <section className="page-section narrow">
      <div className="alert error">无权限访问该页面。</div>
      <Link className="button" to="/challenges">
        返回挑战
      </Link>
    </section>
  );
}
