#!/usr/bin/env python3
"""Builds the GitHub wiki (the player guide) from the repo.

The wiki is generated and never edited on GitHub: .github/workflows/wiki.yml pushes this script's
output to <repo>.wiki.git on every push to main (overwriting it), and CI runs it to catch broken links.

Pages:
- docs/wiki/*.md are the wiki's own pages. The file name is the page name (Room-Rules.md is the page
  "Room Rules"); Home.md is the front page, and _Sidebar.md / _Footer.md show on every page.
- SHARED lists player docs that live elsewhere in docs/ (other docs and old release notes link to
  them there) and the page each one becomes.
- Game-Patches.md gets the optional patch list, generated from the patches' own headers
  (GameMod/src/patches/optional/*.asm, the same catalogue the app shows), at PATCH_LIST_MARKER.

Links are written as ordinary relative links between repo files, so they work when browsing the repo
too. In the wiki a link to a page becomes the page's name, a link to any other file or folder in the
repo becomes its GitHub URL (images: the raw file), and a link to a missing file fails the build.

Usage: python scripts/build-wiki.py [out-dir] [--repo-url URL] [--branch BRANCH]
Without out-dir it only checks. Standard library only (Python 3.9+).
"""

import argparse
import posixpath
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WIKI_DIR = "docs/wiki"

# Player docs kept at their own path in docs/, and their wiki page names.
SHARED = {
    "docs/softlocks.md": "Softlocks-and-Warping",
    "docs/self-hosting.md": "Self-Hosting",
}

PATCHES_DIR = "GameMod/src/patches/optional"
PATCH_LIST_PAGE = "Game-Patches"
PATCH_LIST_MARKER = "<!-- build-wiki: patch list"

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"}

# [text](target) and ![alt](target); the target has no spaces in our docs.
MD_LINK = re.compile(r"(!?)\[((?:[^\[\]]|\[[^\]]*\])*)\]\(([^)\s]+)\)")
# src="..." / href="..." in inline HTML (README-style image blocks).
HTML_LINK = re.compile(r'\b(src|href)="([^"]+)"')
EXTERNAL = re.compile(r"^(?:[a-z][a-z0-9+.-]*:|#|//)", re.IGNORECASE)
FENCE = re.compile(r"^\s*(```|~~~)")


def repo_url_from_props() -> str:
    props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    m = re.search(r"<WwoRepositoryUrl>([^<]+)</WwoRepositoryUrl>", props)
    if not m:
        sys.exit("build-wiki: WwoRepositoryUrl not found in Directory.Build.props")
    return m.group(1).strip().rstrip("/")


def page_sources() -> dict:
    """Repo path -> wiki page name, for every page."""
    pages = {}
    for f in sorted((ROOT / WIKI_DIR).glob("*.md")):
        pages[f"{WIKI_DIR}/{f.name}"] = f.stem
    for path, name in SHARED.items():
        if not (ROOT / path).is_file():
            sys.exit(f"build-wiki: SHARED lists {path}, which doesn't exist")
        pages[path] = name
    names = list(pages.values())
    dupes = {n for n in names if names.count(n) > 1}
    if dupes:
        sys.exit(f"build-wiki: more than one source for page(s) {', '.join(sorted(dupes))}")
    if "Home" not in names:
        sys.exit(f"build-wiki: {WIKI_DIR}/Home.md is missing")
    return pages


class Rewriter:
    def __init__(self, pages: dict, repo_url: str, branch: str):
        self.pages = pages
        self.repo_url = repo_url
        self.branch = branch
        self.errors = []

    def target(self, source: str, line_no: int, target: str, image: bool) -> str:
        if EXTERNAL.match(target):
            return target
        path, _, anchor = target.partition("#")
        resolved = posixpath.normpath(posixpath.join(posixpath.dirname(source), path))
        if resolved.startswith("../") or resolved == "..":
            self.errors.append(f"{source}:{line_no}: {target} points outside the repo")
            return target
        suffix = f"#{anchor}" if anchor else ""
        if resolved in self.pages:
            return self.pages[resolved] + suffix
        on_disk = ROOT / resolved
        if not on_disk.exists():
            self.errors.append(f"{source}:{line_no}: {target} -> {resolved} doesn't exist")
            return target
        if resolved == ".":
            return self.repo_url
        if on_disk.is_dir():
            return f"{self.repo_url}/tree/{self.branch}/{resolved}"
        if image or on_disk.suffix.lower() in IMAGE_EXTENSIONS:
            return f"{self.repo_url}/raw/{self.branch}/{resolved}"
        return f"{self.repo_url}/blob/{self.branch}/{resolved}{suffix}"

    def page(self, source: str, text: str) -> str:
        out = []
        in_fence = False
        for i, line in enumerate(text.splitlines(), 1):
            if FENCE.match(line):
                in_fence = not in_fence
            elif not in_fence:
                line = MD_LINK.sub(
                    lambda m: f"{m.group(1)}[{m.group(2)}]({self.target(source, i, m.group(3), bool(m.group(1)))})",
                    line)
                line = HTML_LINK.sub(
                    lambda m: f'{m.group(1)}="{self.target(source, i, m.group(2), m.group(1) == "src")}"', line)
            out.append(line)
        return "\n".join(out) + "\n"


def parse_patch(path: Path) -> dict:
    """The header tags of one optional patch, parsed like OptionalPatchCatalog (repeated tags join)."""
    tags = {}
    # errors="replace" like File.ReadAllText: a stray non-UTF-8 byte in a comment must not stop the build.
    for raw in path.read_text(encoding="utf-8", errors="replace").replace("\r", "").split("\n"):
        line = raw.strip()
        if not line:
            continue
        if not line.startswith(";"):
            break
        m = re.match(r"^;\s*@([a-z]+)\b\s*(.*)$", line)
        if m:
            tags.setdefault(m.group(1), []).append(m.group(2).strip())
    for required in ("name", "description", "category", "default", "match"):
        if required not in tags:
            sys.exit(f"build-wiki: {path.relative_to(ROOT)} has no @{required}")
    conflicts = [c.strip() for v in tags.get("conflicts", []) for c in v.split(",") if c.strip()]
    return {
        "id": path.stem,
        "name": tags["name"][0],
        "description": " ".join(tags["description"]),
        "category": tags["category"][0],
        "default": tags["default"][0].lower() == "on",
        "match": tags["match"][0].lower() == "yes",
        "multiplayer": " ".join(tags.get("multiplayer", [])),
        "conflicts": conflicts,
    }


def sentence(text: str) -> str:
    text = text.strip()
    return text if not text or text[-1] in ".!?)" else text + "."


def patch_list() -> str:
    patches = [parse_patch(f) for f in sorted((ROOT / PATCHES_DIR).glob("*.asm"))]
    if not patches:
        sys.exit(f"build-wiki: no optional patches in {PATCHES_DIR}")
    by_id = {p["id"]: p for p in patches}
    # The app's order (OptionalPatchCatalog): category, then name, both ordinal.
    patches.sort(key=lambda p: (p["category"].encode(), p["name"].encode()))
    lines = []
    category = None
    for p in patches:
        if p["category"] != category:
            category = p["category"]
            if lines:
                lines.append("")
            lines.append(f"### {category}")
            lines.append("")
        state = "on by default" if p["default"] else "off by default"
        entry = f"- **{p['name']}** ({state}). {sentence(p['description'])}"
        if p["match"]:
            entry += " **All players should match.**"
        if p["multiplayer"]:
            entry += f" *In multiplayer:* {sentence(p['multiplayer'])}"
        if p["conflicts"]:
            names = [f"**{by_id[c]['name']}**" if c in by_id else c for c in p["conflicts"]]
            entry += f" Can't be combined with {' or '.join(names)}."
        lines.append(entry)
    return "\n".join(lines)


def build() -> tuple:
    parser = argparse.ArgumentParser(description="Build the GitHub wiki from docs/.")
    parser.add_argument("out", nargs="?", help="output folder (omit to only check)")
    parser.add_argument("--repo-url", help="https://github.com/<owner>/<repo> (default: Directory.Build.props)")
    parser.add_argument("--branch", default="main", help="branch the repo links point at (default: main)")
    args = parser.parse_args()

    pages = page_sources()
    rewriter = Rewriter(pages, (args.repo_url or repo_url_from_props()).rstrip("/"), args.branch)
    built = {}
    for source, name in pages.items():
        text = (ROOT / source).read_text(encoding="utf-8").replace("\r\n", "\n")
        if name == PATCH_LIST_PAGE:
            marker = [l for l in text.split("\n") if l.startswith(PATCH_LIST_MARKER)]
            if len(marker) != 1:
                sys.exit(f"build-wiki: {source} needs exactly one line starting with {PATCH_LIST_MARKER}")
            text = text.replace(marker[0], patch_list())
        built[name] = rewriter.page(source, text)

    if rewriter.errors:
        print("build-wiki: broken links:", file=sys.stderr)
        for e in rewriter.errors:
            print(f"  {e}", file=sys.stderr)
        sys.exit(1)
    return args.out, built


def main() -> None:
    out, built = build()
    if out is None:
        print(f"build-wiki: {len(built)} pages, links OK")
        return
    out_dir = Path(out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, text in built.items():
        (out_dir / f"{name}.md").write_text(text, encoding="utf-8", newline="\n")
    print(f"build-wiki: wrote {len(built)} pages to {out_dir}")


if __name__ == "__main__":
    main()
