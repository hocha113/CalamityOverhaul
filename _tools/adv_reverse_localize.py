#!/usr/bin/env python3
"""Reverse-localize ADV C# default strings from zh-Hans hjson."""
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ADV_DIR = REPO / "Content" / "ADV"
EXTRA_CS = [
    REPO / "OtherMods" / "NoxusBoss" / "GiveBlazingBud.cs",
]
LOC_DIR = REPO / "Localization" / "zh-Hans"

HJSON_FILES = {
    "ADV": LOC_DIR / "Mods.CalamityOverhaul.ADV.hjson",
    "ADV.Shepel": LOC_DIR / "Mods.CalamityOverhaul.ADV.Shepel.hjson",
    "ADV.VoidColony": LOC_DIR / "Mods.CalamityOverhaul.ADV.VoidColony.hjson",
}

DEFAULT_CATEGORY = "ADV"
CLASS_ALIASES = {
    "FishoilQuestEntry": ["FishoilSubmitScenario"],
}

CATEGORY_RE = re.compile(
    r'(?:public\s+)?(?:override\s+|new\s+)?string\s+LocalizationCategory\s*=>\s*"([^"]+)"'
)
CLASS_RE = re.compile(
    r'(?:internal|public|private|protected)\s+(?:partial\s+|abstract\s+|sealed\s+)*class\s+(\w+)'
)
GETLOC_START_RE = re.compile(
    r'(?:\w+\.)?(?:GetLocalization|Localized)\s*\(\s*(?:nameof\s*\(\s*(\w+)\s*\)|"([^"]+)")\s*,\s*(?:\(\s*\)\s*=>\s*)?',
    re.MULTILINE,
)


def load_hjson(path: Path) -> dict:
    result = subprocess.run(
        ["npx", "-y", "hjson", "-j", str(path)],
        capture_output=True,
        text=True,
        encoding="utf-8",
        cwd=str(REPO),
        shell=True,
    )
    if result.returncode != 0:
        print(f"Failed to parse {path}: {result.stderr}", file=sys.stderr)
        sys.exit(1)
    return json.loads(result.stdout)


def flatten(obj, prefix: str, out: dict):
    if isinstance(obj, dict):
        for k, v in obj.items():
            k = k.lstrip('\ufeff')
            key = f"{prefix}.{k}" if prefix else k
            if isinstance(v, dict):
                flatten(v, key, out)
            else:
                out[key] = v
    else:
        out[prefix.lstrip('\ufeff') if prefix else prefix] = obj


def build_hjson_map() -> dict[str, str]:
    merged = {}
    for file_prefix, path in HJSON_FILES.items():
        data = load_hjson(path)
        flat = {}
        flatten(data, file_prefix, flat)
        merged.update(flat)
    return merged


def parse_string_literal(source: str, start: int):
    i = start
    n = len(source)
    if i >= n:
        return None, start, False

    if source.startswith('@"""', i):
        i += 4
        buf = []
        while i < n:
            if source.startswith('"""', i):
                return ''.join(buf), i + 3, True
            buf.append(source[i])
            i += 1
        return None, start, True

    if source[i] == '@' and i + 1 < n and source[i + 1] == '"':
        i += 2
        buf = []
        while i < n:
            c = source[i]
            if c == '"':
                if i + 1 < n and source[i + 1] == '"':
                    buf.append('"')
                    i += 2
                    continue
                return ''.join(buf), i + 1, True
            buf.append(c)
            i += 1
        return None, start, True

    if source[i] == '"':
        i += 1
        buf = []
        while i < n:
            c = source[i]
            if c == '\\':
                if i + 1 >= n:
                    break
                nxt = source[i + 1]
                escapes = {'n': '\n', 'r': '\r', 't': '\t', '\\': '\\', '"': '"', '0': '\0'}
                buf.append(escapes.get(nxt, nxt))
                i += 2
                continue
            if c == '"':
                return ''.join(buf), i + 1, False
            buf.append(c)
            i += 1
        return None, start, False

    return None, start, False


def csharp_string_for_value(value: str, prefer_verbatim: bool) -> str:
    if prefer_verbatim or '\n' in value:
        escaped = value.replace('"', '""')
        return f'@"{escaped}"'
    escaped = (
        value.replace('\\', '\\\\')
        .replace('"', '\\"')
        .replace('\n', '\\n')
        .replace('\r', '\\r')
        .replace('\t', '\\t')
    )
    return f'"{escaped}"'


def resolve_category_before(text: str, pos: int) -> str:
    category = DEFAULT_CATEGORY
    for m in CATEGORY_RE.finditer(text[:pos]):
        category = m.group(1)
    return category


def resolve_class_before(text: str, pos: int) -> str | None:
    classes = []
    for m in CLASS_RE.finditer(text[:pos]):
        classes.append(m.group(1))
    return classes[-1] if classes else None


def lookup_hjson(hjson: dict, category: str, class_name: str | None, prop: str, file_path: Path):
    candidates = []
    class_names = [class_name] if class_name else []
    if class_name:
        class_names.extend(CLASS_ALIASES.get(class_name, []))

    prefixes = {category, 'ADV', 'ADV.Shepel'}
    if 'Shepel' in str(file_path):
        prefixes.add('ADV.Shepel')

    for cls in class_names:
        for prefix in prefixes:
            candidates.append(f"{prefix}.{cls}.{prop}")

    for k in candidates:
        if k in hjson:
            return k, hjson[k]

    if class_names and prop:
        suffixes = [f".{cls}.{prop}" for cls in class_names]
        matches = [k for k in hjson if any(k.endswith(s) for s in suffixes)]
        if matches:
            if 'Shepel' in str(file_path):
                shepel = [k for k in matches if '.Shepel.' in k]
                if shepel:
                    return shepel[0], hjson[shepel[0]]
            adv = [k for k in matches if k.startswith('ADV.')]
            if adv:
                return adv[0], hjson[adv[0]]
            return matches[0], hjson[matches[0]]

    return None, None


def find_getloc_entries(content: str):
    entries = []
    for m in GETLOC_START_RE.finditer(content):
        prop = m.group(1) or m.group(2)
        pos = m.end()
        while pos < len(content) and content[pos] in ' \t\r\n':
            pos += 1
        if content.startswith('() =>', pos):
            pos += 5
            while pos < len(content) and content[pos] in ' \t\r\n':
                pos += 1
        parsed, val_end, is_verbatim = parse_string_literal(content, pos)
        if parsed is None:
            continue
        entries.append({
            'prop': prop,
            'value': parsed,
            'is_verbatim': is_verbatim,
            'value_start': pos,
            'value_end': val_end,
            'category': resolve_category_before(content, m.start()),
            'class_name': resolve_class_before(content, m.start()),
        })
    return entries


def make_key(category: str, class_name: str | None, prop: str) -> str:
    if class_name:
        return f"{category}.{class_name}.{prop}"
    return f"{category}.{prop}"


def process_file(path: Path, hjson: dict):
    content = path.read_text(encoding='utf-8')
    entries = find_getloc_entries(content)
    updates = []
    unmatched_code = []

    for e in entries:
        key, hval = lookup_hjson(hjson, e['category'], e['class_name'], e['prop'], path)
        if hval is None:
            unmatched_code.append(make_key(e['category'], e['class_name'], e['prop']))
            continue
        if hval == e['value']:
            continue
        new_lit = csharp_string_for_value(hval, e['is_verbatim'] or '\n' in hval)
        old_lit = content[e['value_start']:e['value_end']]
        updates.append((e, key, old_lit, new_lit))

    if not updates:
        return content, 0, unmatched_code

    new_content = content
    for e, key, old_lit, new_lit in reversed(updates):
        new_content = new_content[:e['value_start']] + new_lit + new_content[e['value_end']:]

    return new_content, len(updates), unmatched_code


def collect_cs_files():
    files = sorted(ADV_DIR.rglob('*.cs'))
    for p in EXTRA_CS:
        if p.exists():
            files.append(p)
    return files


def main():
    hjson = build_hjson_map()
    print(f"Loaded {len(hjson)} hjson keys")

    all_cs = collect_cs_files()
    total_updates = 0
    modified_files = []
    all_unmatched_code = set()

    for path in all_cs:
        new_content, count, unmatched = process_file(path, hjson)
        all_unmatched_code.update(unmatched)
        if count:
            original = path.read_bytes()
            newline = '\r\n' if b'\r\n' in original else '\n'
            path.write_text(new_content, encoding='utf-8', newline=newline)
            modified_files.append((str(path.relative_to(REPO)), count))
            total_updates += count

    code_keys = set()
    for path in all_cs:
        content = path.read_text(encoding='utf-8')
        for e in find_getloc_entries(content):
            key, _ = lookup_hjson(hjson, e['category'], e['class_name'], e['prop'], path)
            if key:
                code_keys.add(key)
            else:
                code_keys.add(make_key(e['category'], e['class_name'], e['prop']))

    unmatched_hjson = sorted(k for k in hjson if k.startswith('ADV') and k not in code_keys)

    print("\n=== MODIFIED FILES ===")
    for f, c in sorted(modified_files):
        print(f"{f}: {c} strings")
    print(f"\nTotal: {len(modified_files)} files, {total_updates} strings")

    print("\n=== CODE KEYS WITHOUT HJSON (sample) ===")
    for k in sorted(all_unmatched_code)[:30]:
        print(k)
    if len(all_unmatched_code) > 30:
        print(f"... and {len(all_unmatched_code) - 30} more")

    print("\n=== HJSON KEYS WITHOUT CODE (sample) ===")
    for k in unmatched_hjson[:30]:
        print(k)
    if len(unmatched_hjson) > 30:
        print(f"... and {len(unmatched_hjson) - 30} more")

    report = REPO / "_tools" / "adv_reverse_localize_report.txt"
    with report.open('w', encoding='utf-8') as f:
        f.write("MODIFIED FILES\n")
        for fp, c in sorted(modified_files):
            f.write(f"{fp}\t{c}\n")
        f.write(f"\nTOTAL\t{len(modified_files)}\t{total_updates}\n\n")
        f.write("CODE WITHOUT HJSON\n")
        for k in sorted(all_unmatched_code):
            f.write(k + "\n")
        f.write("\nHJSON WITHOUT CODE\n")
        for k in unmatched_hjson:
            f.write(k + "\n")
    print(f"\nReport: {report}")


if __name__ == '__main__':
    main()
