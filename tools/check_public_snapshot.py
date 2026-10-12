"""Check the explicit source-only snapshot; no network or application execution."""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'MANIFEST.sha256.json'
ALLOWED_GENERATED_DIRS = {'.git', 'bin', 'obj', '__pycache__', '.vs'}

def main():
    record = json.loads(MANIFEST.read_text(encoding='utf-8'))
    expected = record['files']
    failures = []
    for name, digest in expected.items():
        target = ROOT / name
        if not target.is_file():
            failures.append(f'Missing manifest file: {name}')
        elif hashlib.sha256(target.read_bytes()).hexdigest() != digest:
            failures.append(f'Hash mismatch: {name}')
    actual = []
    for target in ROOT.rglob('*'):
        if not target.is_file():
            continue
        relative = target.relative_to(ROOT)
        if any(part in ALLOWED_GENERATED_DIRS for part in relative.parts):
            continue
        actual.append(relative.as_posix())
    unexpected = sorted(set(actual) - set(expected) - {'MANIFEST.sha256.json'})
    failures.extend(f'Unexpected attachment: {name}' for name in unexpected)
    forbidden = [name for name in actual if Path(name).name.lower() in {'catalog.json', 'catalog.en.json', 'mhrice.json'} or Path(name).suffix.lower() in {'.bin', '.sav', '.pdb', '.exe', '.dll', '.zip', '.7z', '.nsp', '.xci', '.nca'}]
    failures.extend(f'Excluded file type/content: {name}' for name in forbidden)
    if failures:
        for message in failures:
            print(message, file=sys.stderr)
        return 1
    print(f'PASS {len(expected)} manifest hashes; source-only attachment boundary verified')
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
