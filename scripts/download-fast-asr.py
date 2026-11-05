"""Fetch pinned public benchmark models; check both length and SHA-256."""
import hashlib
import json
import os
from pathlib import Path
import urllib.request

project = Path(__file__).resolve().parent.parent
catalog = json.loads((project / 'docs/fast-asr-candidates.json').read_text())
root = Path(os.environ['LOCALAPPDATA']) / 'N3moStudio/Murmur/models'
for name, candidate in zip(('parakeet-v3', 'moonshine-base-en'), catalog['candidates']):
    folder = root / name
    folder.mkdir(parents=True, exist_ok=True)
    for item in candidate['files']:
        dest = folder / item['file']
        def valid(path):
            if not path.exists() or path.stat().st_size != item['bytes']:
                return False
            with path.open('rb') as source:
                return hashlib.file_digest(source, 'sha256').hexdigest() == item['sha256']
        if valid(dest):
            print(f'Verified existing {name}/{dest.name}', flush=True)
            continue
        partial = dest.with_suffix(dest.suffix + '.part')
        request = urllib.request.Request(item['url'], headers={'User-Agent': 'Murmur-model-verification/0.1'})
        with urllib.request.urlopen(request, timeout=90) as response, partial.open('wb') as output:
            while block := response.read(1024 * 1024):
                output.write(block)
        if not valid(partial):
            raise ValueError(f'Pinned model verification failed: {name}/{dest.name}')
        partial.replace(dest)
        print(f'Downloaded and verified {name}/{dest.name}', flush=True)
