"""Turns the GDD into a Material for MkDocs site (used by .github/workflows/gdd-pages.yml).

The GDD stays one Markdown file (proyectoJuegoUNAD/Assets/Docs/GuardianesdelBosqueGDD.md). This script splits it into
one page per chapter (each top-level "# N. Title" heading), adds a home page with the cover art and a card per chapter,
copies the images and the style, and writes mkdocs.yml with the navigation. Then `mkdocs build` makes the site.

Usage: python build_site.py <gdd.md> <work dir> <updated text> <commit url>
"""
import pathlib
import re
import shutil
import sys
import unicodedata

HERE = pathlib.Path(__file__).resolve().parent
REPO = "https://github.com/elianismedina/proyectoJuegoUNAD"
GDD_PATH = "proyectoJuegoUNAD/Assets/Docs/GuardianesdelBosqueGDD.md"

# A Material icon per chapter number, for the home page cards.
ICONS = {
    1: "lightbulb-on-outline", 2: "earth", 3: "account-group-outline", 4: "book-open-page-variant-outline",
    5: "flag-checkered", 6: "gamepad-variant-outline", 7: "run", 8: "pine-tree", 9: "palette-outline",
    10: "monitor-dashboard", 11: "human", 12: "target", 13: "scale-balance",
}


# One line under each chapter card on the home page.
SUMMARIES = {
    1: "Qué es el juego, para quién es y su idea central.",
    2: "El problema ambiental y lo que el juego quiere enseñar.",
    3: "Quiénes van a jugar y qué necesitan.",
    4: "El bosque contaminado y el papel del guardián.",
    5: "Recoger 10 residuos y llegar a la meta: reglas y estados.",
    6: "Explorar, saltar, recoger y recuperar cada zona.",
    7: "El guardián, sus acciones, animaciones y tropiezos.",
    8: "Las tres zonas, obstáculos, peligros y residuos.",
    9: "Modelos, sonidos e interfaz que necesita el juego.",
    10: "Contador, barra de recuperación, avisos y menús.",
    11: "Pautas WCAG 2.2 y Diseño Universal para el Aprendizaje.",
    12: "Qué entra en esta versión y qué queda fuera.",
    13: "Origen y licencia de cada recurso usado.",
}


def slugify(text):
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def tidy(lines):
    """Python-Markdown is stricter than GitHub: a table or a list needs a blank line before it, and a table after it."""
    out = []
    for line in lines:
        is_table = line.startswith("|")
        is_list = bool(re.match(r"(- |\* |\d+\. )", line))
        prev = out[-1] if out else ""
        prev_table = prev.startswith("|")
        prev_list = bool(re.match(r"(- |\* |\d+\. )", prev))
        if prev.strip() and ((is_table and not prev_table) or (is_list and not prev_list and not prev_table)
                             or (prev_table and not is_table)):
            out.append("")
        out.append(line)
    return out


def main():
    gdd, work, updated, commit_url = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3], sys.argv[4]
    docs = work / "docs"
    if work.exists():
        shutil.rmtree(work)
    (docs / "stylesheets").mkdir(parents=True)

    text = gdd.read_text(encoding="utf-8").replace("\r\n", "\n")
    chapters = []  # (number, title, file name, lines)
    for line in text.split("\n"):
        m = re.match(r"# (\d+)\.\s*(.+)", line)
        if m:
            number, title = int(m.group(1)), m.group(2).strip()
            chapters.append((number, title, f"{number:02d}-{slugify(title)}.md", [line]))
        elif chapters:
            chapters[-1][3].append(line)
        elif line.strip():
            raise SystemExit("Text before the first chapter heading: " + line[:60])

    for number, title, name, lines in chapters:
        (docs / name).write_text("\n".join(tidy(lines)).rstrip() + "\n", encoding="utf-8")

    images = gdd.parent / "GDDImages"
    shutil.copytree(images, docs / "GDDImages", ignore=shutil.ignore_patterns("*.meta"))
    shutil.copy(HERE / "extra.css", docs / "stylesheets" / "extra.css")

    cards = "\n".join(
        f"-   :material-{ICONS.get(n, 'file-document-outline')}:{{ .lg .middle }} **[{n}. {t}]({f})**\n\n"
        f"    {SUMMARIES.get(n, '')}\n"
        for n, t, f, _ in chapters)
    first = chapters[0][2]
    (docs / "index.md").write_text(f"""---
hide:
  - navigation
  - toc
---

<div class="gdd-hero" markdown>

<div class="gdd-hero-text" markdown>

# Guardianes del Bosque

**Documento de diseño del juego (GDD)**

Minijuego educativo en 3D low-poly para estudiantes de 10 a 14 años: recorre el bosque, recoge los residuos y
devuelve la vida a cada zona.

[Empezar a leer :material-arrow-right:]({first}){{ .md-button .md-button--primary }}
[Historial de cambios]({REPO}/commits/main/{GDD_PATH}){{ .md-button }}

</div>

![Arte de portada de Guardianes del Bosque](GDDImages/portada.jpg){{ .gdd-hero-img }}

</div>

<p class="gdd-updated">Última actualización: {updated} · versión <a href="{commit_url}">{commit_url.rsplit('/', 1)[-1][:7]}</a></p>

## Capítulos

<div class="grid cards" markdown>

{cards}

</div>

## El juego hoy

| Zona contaminada | La misma zona, recuperada |
| --- | --- |
| ![Zona contaminada](GDDImages/zona_contaminada.jpg) | ![Zona recuperada, con flores y un ciervo](GDDImages/zona_recuperada.jpg) |
""", encoding="utf-8")

    nav = "\n".join(f'  - "{n}. {t}": {f}' for n, t, f, _ in chapters)
    config = (HERE / "mkdocs.yml").read_text(encoding="utf-8")
    config = config.replace("{{NAV}}", nav).replace("{{UPDATED}}", updated)
    (work / "mkdocs.yml").write_text(config, encoding="utf-8")
    print(f"{len(chapters)} chapters written to {docs}")


if __name__ == "__main__":
    main()
