"""Export public source without private prompts, build outputs or local Git history."""
import argparse
import io
import json
from pathlib import Path
import re
import subprocess
import zipfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('output', type=Path, help='New or empty destination directory')
args = parser.parse_args()
out = args.output.resolve()
if out == root or root.is_relative_to(out):
    raise SystemExit('Choose a separate empty output directory.')
if out.exists() and any(out.iterdir()):
    raise SystemExit('The destination must be empty. Existing files were not changed.')
excluded = {'docs/REQUEST.md', 'docs/USER-STEERING.md', 'AGENTS.md'}
prefixes = ('research/', 'artifacts/', 'tools/', 'node_modules/', 'docs/evidence/', 'docs/reference-images/')
secret = re.compile(r'(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|(?<![A-Za-z0-9])sk-[A-Za-z0-9_-]{24,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)')
private_path = re.compile(r"C:[/\\]Users[/\\](?!<)[^/\\\s\"']+")
archive = subprocess.check_output(['git', 'archive', '--format=zip', 'HEAD'], cwd=root)
contents = {}
with zipfile.ZipFile(io.BytesIO(archive)) as source:
    for info in source.infolist():
        name = info.filename
        if info.is_dir() or name in excluded or name.startswith(prefixes):
            continue
        parts = Path(name).parts
        if '..' in parts or Path(name).is_absolute():
            raise SystemExit('Invalid source path: ' + name)
        if Path(name).suffix.lower() in {'.pfx', '.p12', '.key', '.db', '.sqlite', '.wav', '.caf'} or Path(name).name.startswith('.env'):
            raise SystemExit('Private or generated file needs review: ' + name)
        data = source.read(info)
        try:
            text = data.decode('utf8')
        except UnicodeError:
            text = ''
        if secret.search(text) or private_path.search(text):
            raise SystemExit('Potential private information needs review in: ' + name)
        contents[name] = data
contents['AGENTS.md'] = b"""# Murmur contributor instructions

Read docs/parity.md and docs/build-release.md before changing product scope. Keep implementation, automated checks and physical-device evidence separate. Never mark missing functionality complete.

Preserve user data, microphone privacy, clipboard ownership and changed editor text. Do not log transcripts or credentials. Microphones and orb timers must stop while idle. Ordinary dictation selects its editor at stop and retains recovery when insertion is unsafe. Never auto-submit messages.

Keep licensed third-party notices. Exclude recordings, private profiles, credentials, build outputs and proprietary reference captures from commits and releases. Treat the Mac implementation as a preview until its documented device checks pass.
"""
out.mkdir(parents=True, exist_ok=True)
for name, data in contents.items():
    path = out / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
print(json.dumps({'output': str(out), 'files': len(contents), 'excluded_private_notes': sorted(excluded), 'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()}))
