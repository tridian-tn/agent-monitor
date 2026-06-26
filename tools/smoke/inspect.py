import json, sys, os, glob, time

projects = r'C:\Users\tryst\.claude\projects'
for f in glob.glob(r'C:\Users\tryst\.claude\sessions\*.json'):
    rec = json.load(open(f))
    sid = rec['sessionId']
    cwd = rec.get('cwd', '')
    slug = cwd.replace(':', '-').replace('\\', '-').replace('/', '-')
    title = os.path.basename(cwd.rstrip('\\/')) or sid[:8]
    path = os.path.join(projects, slug, sid + '.jsonl')
    if not os.path.exists(path):
        print(f"{title:22} NO TRANSCRIPT ({path})")
        continue
    age = time.time() - os.path.getmtime(path)
    last_assistant_stop = None
    last_type = None
    with open(path, encoding='utf-8', errors='replace') as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                o = json.loads(line)
            except Exception:
                continue
            last_type = o.get('type')
            if o.get('type') == 'assistant':
                m = o.get('message') or {}
                last_assistant_stop = m.get('stop_reason')
    print(f"{title:22} age={age:7.1f}s  last_type={last_type:11} last_assistant_stop={last_assistant_stop}")
