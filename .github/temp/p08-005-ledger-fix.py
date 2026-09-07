from pathlib import Path
p = Path('docs/TASK_LEDGER.md')
text = p.read_text(encoding='utf-8')
old = 'P08 remains `IN_PROGRESS`; P08-003, P08-004, P08-005, P08-007, and P08-008 remain PENDING, and the two existing owner-last release blockers remain unchanged.'
new = 'P08 remains `IN_PROGRESS`; P08-003, P08-004, P08-007, and P08-008 remain PENDING, and the two existing owner-last release blockers remain unchanged.'
count = text.count(old)
if count != 1:
    raise SystemExit(f'guard failed: expected one stale P08-006 pending-list phrase, got {count}')
p.write_text(text.replace(old, new, 1), encoding='utf-8', newline='\n')
