import { Link } from "react-router-dom";

export function ForbiddenPage() {
  return (
    <section className="page-section narrow ui-v2-page forbidden-v2-page">
      <div className="alert error">无权限访问该页面。</div>
      <Link className="button" to="/challenges">
        返回挑战
      </Link>
    </section>
  );
}
