"""Turns the GDD into a Word file (used by .github/workflows/gdd-pages.yml), so collaborators can download it from the
GDD web page.

Pandoc does the conversion. Its default Word styles are plain blue headings and tables without borders, so this script
first patches pandoc's own reference.docx with the forest colours of the web page: green headings, each chapter on a
new page, and tables with borders and a shaded header row. Nothing binary is kept in the repository.

Usage: python build_docx.py <gdd.md> <out.docx> <updated text>
"""
import pathlib
import re
import subprocess
import sys
import tempfile
import zipfile

GREEN = "2E7D32"
DARK_GREEN = "1B5E20"
HEADER_FILL = "E8F5E9"
BORDER = "A5D6A7"


def style(styles, style_id, patch):
    """Applies patch() to the <w:style> element with this id."""
    pattern = re.compile(r'<w:style [^>]*w:styleId="%s".*?</w:style>' % style_id, re.S)
    match = pattern.search(styles)
    if not match:
        raise SystemExit(f"Style {style_id} not found in pandoc's reference.docx")
    return styles[:match.start()] + patch(match.group(0)) + styles[match.end():]


def recolour(colour):
    return lambda xml: re.sub(r'<w:color [^>]*/>', f'<w:color w:val="{colour}" />', xml)


def chapter_on_new_page(xml):
    return recolour(DARK_GREEN)(xml.replace("<w:keepNext />", "<w:pageBreakBefore />\n      <w:keepNext />", 1))


def bordered_table(xml):
    side = f'w:val="single" w:sz="4" w:space="0" w:color="{BORDER}"'
    borders = "<w:tblBorders>" + "".join(
        f"<w:{edge} {side} />" for edge in ("top", "left", "bottom", "right", "insideH", "insideV")) + "</w:tblBorders>"
    xml = xml.replace('<w:tblInd w:w="0" w:type="dxa" />', '<w:tblInd w:w="0" w:type="dxa" />' + borders, 1)
    header = (f'<w:shd w:val="clear" w:color="auto" w:fill="{HEADER_FILL}" />'
              f'<w:tcBorders><w:bottom w:val="single" w:sz="8" w:space="0" w:color="{GREEN}" /></w:tcBorders>')
    xml = re.sub(r"<w:tcBorders>\s*<w:bottom w:val=\"single\"/>\s*</w:tcBorders>", header, xml)
    return xml.replace('<w:tblStylePr w:type="firstRow">', '<w:tblStylePr w:type="firstRow"><w:rPr><w:b /></w:rPr>', 1)


def make_reference(path):
    default = subprocess.run(["pandoc", "--print-default-data-file", "reference.docx"],
                             check=True, capture_output=True).stdout
    source = path.with_suffix(".default.docx")
    source.write_bytes(default)
    with zipfile.ZipFile(source) as src, zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            data = src.read(item.filename)
            if item.filename == "word/styles.xml":
                styles = data.decode("utf-8")
                styles = style(styles, "Heading1", chapter_on_new_page)
                for heading in ("Heading2", "Heading3", "Heading4", "Title", "TOCHeading"):
                    styles = style(styles, heading, recolour(GREEN))
                styles = style(styles, "Table", bordered_table)
                data = styles.encode("utf-8")
            dst.writestr(item, data)


def main():
    gdd, out, updated = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
    with tempfile.TemporaryDirectory() as tmp:
        reference = pathlib.Path(tmp) / "reference.docx"
        make_reference(reference)
        metadata = pathlib.Path(tmp) / "metadata.yaml"
        metadata.write_text(
            "title: Guardianes del Bosque\n"
            "subtitle: Documento de diseño del juego (GDD)\n"
            f"date: 'Última actualización: {updated}'\n"
            "lang: es-CO\n"
            "toc-title: Contenido\n", encoding="utf-8")
        subprocess.run([
            "pandoc", str(gdd), "--from", "gfm", "--to", "docx",
            "--resource-path", str(gdd.parent),  # the images are linked as GDDImages/...
            "--metadata-file", str(metadata), "--reference-doc", str(reference),
            "--toc", "--toc-depth", "2", "--output", str(out)], check=True)
    print(f"Word file written to {out} ({out.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
