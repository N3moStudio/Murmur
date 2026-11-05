import json,struct,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
failed=[]; report={}
for arch,expected in [('arm64',0xaa64),('x64',0x8664)]:
    records=[]
    for file in (root/'artifacts/publish'/('win-'+arch)).rglob('*'):
        if file.suffix.lower() not in ('.exe','.dll'):continue
        data=file.read_bytes()
        if data[:2]!=b'MZ':continue
        pe=struct.unpack_from('<I',data,0x3c)[0]
        machine=struct.unpack_from('<H',data,pe+4)[0]
        optional=pe+24; magic=struct.unpack_from('<H',data,optional)[0]
        directory=optional+(112 if magic==0x20b else 96)
        managed=struct.unpack_from('<I',data,directory+14*8)[0]!=0
        compatible=machine==expected or (managed and machine==0x14c)
        item={'file':str(file.relative_to(root/'artifacts/publish'/('win-'+arch))),'machine':hex(machine),'managed':managed,'compatible':compatible}
        records.append(item)
        if not compatible:failed.append(item)
    report[arch]=records
(root/'artifacts/architecture-inventory.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'files':{a:len(x) for a,x in report.items()},'wrong_architecture':failed},indent=2))
sys.exit(bool(failed))
