import wave,array,random,math
from pathlib import Path
root=Path(__file__).resolve().parents[1]/'artifacts/test-audio'
root.mkdir(parents=True,exist_ok=True)
def save(name,samples):
    with wave.open(str(root/name),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(16000);w.writeframes(array.array('h',samples).tobytes())
save('digital-silence.wav',[0]*80000)
random.seed(27)
save('synthetic-fan-noise.wav',[int(150*math.sin(2*math.pi*120*i/16000)+random.uniform(-50,50)) for i in range(80000)])
with wave.open(str(root/'jfk.wav'),'rb') as w: samples=array.array('h',w.readframes(w.getnframes()))
save('jfk-quiet.wav',[int(x*.1) for x in samples])
(root/'cleanup-email.txt').write_text('Um hi Sara, um we can meet on November 4 at 3:30. Please send the 37 invoices to Naith@example.com. Actually make that 38 invoices. Thanks, Naith.',encoding='utf-8')
print('Created clearly labeled silence, synthetic noise, attenuated public speech and text-cleanup fixtures.')
