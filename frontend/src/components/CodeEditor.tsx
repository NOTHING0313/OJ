import Editor from "@monaco-editor/react";

type CodeEditorProps = {
  value: string;
  language: number | string;
  onChange: (value: string) => void;
  height?: string;
  readOnly?: boolean;
};

export function CodeEditor({ value, language, onChange, height = "540px", readOnly = false }: CodeEditorProps) {
  return (
    <div className="code-editor-shell">
      <Editor
        height={height}
        language={toMonacoLanguage(language)}
        theme="vs-dark"
        value={value}
        onChange={(nextValue) => onChange(nextValue ?? "")}
        options={{
          automaticLayout: true,
          fontFamily: 'Consolas, "Cascadia Code", monospace',
          fontSize: 15,
          minimap: { enabled: false },
          mouseWheelZoom: true,
          readOnly,
          renderWhitespace: "selection",
          scrollBeyondLastLine: false,
          tabSize: 4,
          wordWrap: "on"
        }}
      />
    </div>
  );
}

function toMonacoLanguage(language: number | string) {
  if (language === 1 || language === "cpp17" || language === "cpp") {
    return "cpp";
  }

  if (language === 2 || language === "c11" || language === "c") {
    return "c";
  }

  if (language === 3 || language === "csharp") {
    return "csharp";
  }

  return "plaintext";
}
