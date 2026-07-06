#!/usr/bin/env python3
"""Builds LFZ-Project-Documentation.docx from the project markdown docs.

Pipeline: markdown -> styled HTML -> .docx via macOS textutil.
Mermaid diagrams are kept as monospaced source blocks.
"""
import pathlib
import subprocess

import markdown

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCES = [
    ("LFZ Plot Management — Project Overview", ROOT / "README.md"),
    ("Architecture", ROOT / "docs/architecture.md"),
    ("Database Schema", ROOT / "docs/database-schema.md"),
    ("DWG Extraction Report", ROOT / "docs/dwg-extraction-report.md"),
]

STYLE = """
body { font-family: 'Helvetica Neue', Arial, sans-serif; font-size: 11pt; color: #1f2937; }
h1 { font-size: 20pt; color: #0f2c4c; border-bottom: 2px solid #0f2c4c; padding-bottom: 4px; }
h2 { font-size: 15pt; color: #0f2c4c; margin-top: 18px; }
h3 { font-size: 12.5pt; color: #16406e; }
table { border-collapse: collapse; margin: 8px 0; }
th, td { border: 1px solid #9ca3af; padding: 4px 8px; font-size: 10pt; }
th { background: #e5e7eb; }
code { font-family: Menlo, monospace; font-size: 9.5pt; }
pre { background: #f3f4f6; border: 1px solid #d1d5db; padding: 8px; font-size: 9pt; }
.titlepage { text-align: center; margin-top: 200px; }
.titlepage h1 { border: none; font-size: 26pt; }
.titlepage p { color: #6b7280; }
.pagebreak { page-break-before: always; }
"""

sections = []
for title, path in SOURCES:
    body = markdown.markdown(
        path.read_text(),
        extensions=["tables", "fenced_code"],
    )
    sections.append(f'<div class="pagebreak"></div><h1>{title}</h1>{body}')

html = f"""<!DOCTYPE html>
<html><head><meta charset="utf-8"><style>{STYLE}</style></head><body>
<div class="titlepage">
  <h1>LFZ Plot Management</h1>
  <p>Solution documentation — architecture, database schema and DWG extraction report</p>
  <p>.NET 8 · Clean Architecture · SQL Server (spatial) · Blazor Server</p>
  <p>7 July 2026</p>
</div>
{''.join(sections)}
</body></html>"""

html_path = ROOT / "LFZ-Project-Documentation.html"
html_path.write_text(html)

docx_path = ROOT / "LFZ-Project-Documentation.docx"
subprocess.run(
    ["textutil", "-convert", "docx", str(html_path), "-output", str(docx_path)],
    check=True,
)
html_path.unlink()
print(f"Wrote {docx_path} ({docx_path.stat().st_size / 1024:.0f} KB)")
