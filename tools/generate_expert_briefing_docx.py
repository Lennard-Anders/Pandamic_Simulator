from __future__ import annotations

import argparse
import datetime as dt
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET


PAGEBREAK_MARKER = "{{PAGEBREAK}}"


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def parse_table_row(line: str) -> list[str]:
    stripped = line.strip()
    if stripped.startswith("|"):
        stripped = stripped[1:]
    if stripped.endswith("|"):
        stripped = stripped[:-1]
    return [cell.strip() for cell in stripped.split("|")]


def is_table_separator(cells: list[str]) -> bool:
    if not cells:
        return False

    for cell in cells:
        candidate = cell.replace(":", "").replace("-", "").strip()
        if candidate:
            return False

    return True


def is_heading(line: str) -> bool:
    return bool(re.match(r"^(#{1,3})\s+\S+", line))


def is_list_item(line: str) -> bool:
    return bool(re.match(r"^\s*[-*]\s+\S+", line))


def is_table_line(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("|") and "|" in stripped[1:]


def parse_markdown(markdown_text: str) -> list[dict]:
    lines = markdown_text.splitlines()
    blocks: list[dict] = []
    index = 0

    while index < len(lines):
        line = lines[index].rstrip()
        stripped = line.strip()

        if not stripped:
            index += 1
            continue

        if stripped == PAGEBREAK_MARKER:
            blocks.append({"type": "pagebreak"})
            index += 1
            continue

        heading_match = re.match(r"^(#{1,3})\s+(.*)$", stripped)
        if heading_match:
            blocks.append(
                {
                    "type": "heading",
                    "level": len(heading_match.group(1)),
                    "text": heading_match.group(2).strip(),
                }
            )
            index += 1
            continue

        if is_table_line(line):
            table_lines: list[str] = []
            while index < len(lines) and is_table_line(lines[index]):
                table_lines.append(lines[index])
                index += 1

            rows = [parse_table_row(table_line) for table_line in table_lines]
            if len(rows) >= 2 and is_table_separator(rows[1]):
                rows.pop(1)

            blocks.append({"type": "table", "rows": rows})
            continue

        if is_list_item(line):
            items: list[str] = []
            while index < len(lines) and is_list_item(lines[index]):
                items.append(re.sub(r"^\s*[-*]\s+", "", lines[index].strip()))
                index += 1

            blocks.append({"type": "list", "items": items})
            continue

        paragraph_lines: list[str] = []
        while index < len(lines):
            candidate = lines[index].rstrip()
            stripped_candidate = candidate.strip()

            if not stripped_candidate:
                break
            if stripped_candidate == PAGEBREAK_MARKER or is_heading(stripped_candidate) or is_list_item(candidate) or is_table_line(candidate):
                break

            paragraph_lines.append(stripped_candidate)
            index += 1

        if paragraph_lines:
            blocks.append({"type": "paragraph", "text": " ".join(paragraph_lines)})
            continue

        index += 1

    return blocks


def paragraph_xml(
    text: str,
    *,
    style: str = "Normal",
    bold: bool = False,
    centered: bool = False,
    spacing_before: int | None = None,
    spacing_after: int | None = None,
    indent_left: int | None = None,
    indent_hanging: int | None = None,
) -> str:
    ppr_parts = [f'<w:pStyle w:val="{style}"/>']
    if centered:
        ppr_parts.append('<w:jc w:val="center"/>')
    if spacing_before is not None or spacing_after is not None:
        before = spacing_before if spacing_before is not None else 0
        after = spacing_after if spacing_after is not None else 0
        ppr_parts.append(f'<w:spacing w:before="{before}" w:after="{after}"/>')
    if indent_left is not None or indent_hanging is not None:
        attrs = []
        if indent_left is not None:
            attrs.append(f'w:left="{indent_left}"')
        if indent_hanging is not None:
            attrs.append(f'w:hanging="{indent_hanging}"')
        ppr_parts.append(f'<w:ind {" ".join(attrs)}/>')

    rpr = "<w:rPr>"
    if bold:
        rpr += "<w:b/>"
    rpr += "</w:rPr>"

    return (
        "<w:p>"
        f"<w:pPr>{''.join(ppr_parts)}</w:pPr>"
        f"<w:r>{rpr}<w:t xml:space=\"preserve\">{xml_escape(text)}</w:t></w:r>"
        "</w:p>"
    )


def page_break_xml() -> str:
    return "<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"


def table_xml(rows: list[list[str]]) -> str:
    if not rows:
        return ""

    max_cols = max(len(row) for row in rows)
    col_width = int(9000 / max_cols) if max_cols else 9000
    grid_cols = "".join(f'<w:gridCol w:w="{col_width}"/>' for _ in range(max_cols))

    parts = [
        "<w:tbl>",
        "<w:tblPr>",
        '<w:tblW w:w="0" w:type="auto"/>',
        '<w:tblLayout w:type="fixed"/>',
        "<w:tblBorders>"
        '<w:top w:val="single" w:sz="8" w:space="0" w:color="808080"/>'
        '<w:left w:val="single" w:sz="8" w:space="0" w:color="808080"/>'
        '<w:bottom w:val="single" w:sz="8" w:space="0" w:color="808080"/>'
        '<w:right w:val="single" w:sz="8" w:space="0" w:color="808080"/>'
        '<w:insideH w:val="single" w:sz="6" w:space="0" w:color="B0B0B0"/>'
        '<w:insideV w:val="single" w:sz="6" w:space="0" w:color="B0B0B0"/>'
        "</w:tblBorders>",
        '<w:tblCellMar><w:top w:w="100" w:type="dxa"/><w:left w:w="100" w:type="dxa"/><w:bottom w:w="100" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tblCellMar>',
        "</w:tblPr>",
        f"<w:tblGrid>{grid_cols}</w:tblGrid>",
    ]

    for row_index, row in enumerate(rows):
        padded_row = row + [""] * (max_cols - len(row))
        parts.append("<w:tr>")
        for cell in padded_row:
            parts.append("<w:tc>")
            tc_pr = [f'<w:tcPr><w:tcW w:w="{col_width}" w:type="dxa"/>']
            if row_index == 0:
                tc_pr.append('<w:shd w:val="clear" w:color="auto" w:fill="E9ECEF"/>')
            tc_pr.append("</w:tcPr>")
            parts.append("".join(tc_pr))
            parts.append(paragraph_xml(cell, bold=row_index == 0, spacing_before=0, spacing_after=0))
            parts.append("</w:tc>")
        parts.append("</w:tr>")

    parts.append("</w:tbl>")
    return "".join(parts)


def build_document_xml(blocks: list[dict]) -> str:
    body_parts: list[str] = []
    first_heading_rendered = False

    for block in blocks:
        block_type = block["type"]

        if block_type == "pagebreak":
            body_parts.append(page_break_xml())
            continue

        if block_type == "heading":
            level = block["level"]
            text = block["text"]
            if not first_heading_rendered and level == 1:
                body_parts.append(paragraph_xml(text, style="Title", bold=True, centered=True, spacing_after=240))
                first_heading_rendered = True
            else:
                style = {1: "Heading1", 2: "Heading2", 3: "Heading3"}.get(level, "Heading3")
                spacing_before = 200 if level == 1 else 120
                body_parts.append(paragraph_xml(text, style=style, spacing_before=spacing_before, spacing_after=80))
            continue

        if block_type == "paragraph":
            body_parts.append(paragraph_xml(block["text"], spacing_after=120))
            continue

        if block_type == "list":
            for item in block["items"]:
                body_parts.append(
                    paragraph_xml(
                        f"\u2022 {item}",
                        spacing_after=40,
                        indent_left=540,
                        indent_hanging=240,
                    )
                )
            continue

        if block_type == "table":
            body_parts.append(table_xml(block["rows"]))
            body_parts.append(paragraph_xml("", spacing_after=100))
            continue

    sect_pr = (
        "<w:sectPr>"
        '<w:pgSz w:w="11906" w:h="16838"/>'
        '<w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="708" w:footer="708" w:gutter="0"/>'
        "</w:sectPr>"
    )

    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas" '
        'xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" '
        'xmlns:o="urn:schemas-microsoft-com:office:office" '
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" '
        'xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math" '
        'xmlns:v="urn:schemas-microsoft-com:vml" '
        'xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing" '
        'xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" '
        'xmlns:w10="urn:schemas-microsoft-com:office:word" '
        'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
        'xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml" '
        'xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup" '
        'xmlns:wpi="http://schemas.microsoft.com/office/word/2010/wordprocessingInk" '
        'xmlns:wne="http://schemas.microsoft.com/office/word/2006/wordml" '
        'xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape" '
        'mc:Ignorable="w14 wp14">'
        f"<w:body>{''.join(body_parts)}{sect_pr}</w:body>"
        "</w:document>"
    )


def build_styles_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault>
      <w:rPr>
        <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="Calibri" w:cs="Calibri"/>
        <w:sz w:val="22"/>
        <w:szCs w:val="22"/>
        <w:lang w:val="de-DE"/>
      </w:rPr>
    </w:rPrDefault>
    <w:pPrDefault>
      <w:pPr>
        <w:spacing w:after="120" w:line="276" w:lineRule="auto"/>
      </w:pPr>
    </w:pPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/>
    <w:qFormat/>
    <w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Title">
    <w:name w:val="Title"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:jc w:val="center"/><w:spacing w:after="200"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="34"/><w:color w:val="1F1F1F"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:spacing w:before="280" w:after="80"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="30"/><w:color w:val="1F1F1F"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="heading 2"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:spacing w:before="180" w:after="60"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="26"/><w:color w:val="2D2D2D"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading3">
    <w:name w:val="heading 3"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:spacing w:before="120" w:after="40"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="24"/><w:color w:val="404040"/></w:rPr>
  </w:style>
</w:styles>
"""


def build_content_types_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
"""


def build_root_rels_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"""


def build_document_rels_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
"""


def build_core_xml(title: str) -> str:
    now = dt.datetime.now(dt.UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    return f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
 xmlns:dc="http://purl.org/dc/elements/1.1/"
 xmlns:dcterms="http://purl.org/dc/terms/"
 xmlns:dcmitype="http://purl.org/dc/dcmitype/"
 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>{xml_escape(title)}</dc:title>
  <dc:creator>OpenAI Codex</dc:creator>
  <cp:lastModifiedBy>OpenAI Codex</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">{now}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">{now}</dcterms:modified>
</cp:coreProperties>
"""


def build_app_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
 xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Pandamic_Simulator export script</Application>
</Properties>
"""


def validate_xml_parts(parts: dict[str, str]) -> None:
    for name, xml_text in parts.items():
        try:
            ET.fromstring(xml_text)
        except ET.ParseError as ex:
            raise RuntimeError(f"XML validation failed for {name}: {ex}") from ex


def generate_docx(source_path: Path, output_path: Path) -> None:
    markdown_text = source_path.read_text(encoding="utf-8")
    blocks = parse_markdown(markdown_text)

    title = "Pandemic Simulator - Expert Briefing"
    for block in blocks:
        if block["type"] == "heading" and block["level"] == 1:
            title = block["text"]
            break

    parts = {
        "[Content_Types].xml": build_content_types_xml(),
        "_rels/.rels": build_root_rels_xml(),
        "docProps/core.xml": build_core_xml(title),
        "docProps/app.xml": build_app_xml(),
        "word/document.xml": build_document_xml(blocks),
        "word/styles.xml": build_styles_xml(),
        "word/_rels/document.xml.rels": build_document_rels_xml(),
    }

    validate_xml_parts(parts)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for part_name, xml_text in parts.items():
            archive.writestr(part_name, xml_text.encode("utf-8"))


def main(argv: list[str]) -> int:
    default_source = Path("docs") / "Pandemic_Simulator_Expert_Briefing_Source.md"
    default_output = Path("Pandemic_Simulator_Expert_Briefing.docx")

    parser = argparse.ArgumentParser(description="Generate the expert briefing .docx without external dependencies.")
    parser.add_argument("--source", type=Path, default=default_source)
    parser.add_argument("--output", type=Path, default=default_output)
    args = parser.parse_args(argv)

    if not args.source.exists():
        print(f"Source file not found: {args.source}", file=sys.stderr)
        return 1

    generate_docx(args.source, args.output)
    print(f"Generated {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
