import ReactMarkdown from "react-markdown";
import rehypeSanitize from "rehype-sanitize";
import remarkGfm from "remark-gfm";

export function HelpMarkdown({ children }: { children: string }) {
  return (
    <div className="help-markdown">
      <ReactMarkdown
        skipHtml
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={{
          a: ({ href, children: linkChildren }) => {
            const isExternal = Boolean(href && (/^https?:\/\//i.test(href) || href.startsWith("//")));
            return (
              <a
                href={href}
                target={isExternal ? "_blank" : undefined}
                rel={isExternal ? "noopener noreferrer" : undefined}
              >
                {linkChildren}
              </a>
            );
          }
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}
