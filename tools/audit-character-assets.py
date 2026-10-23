#!/usr/bin/env python3
"""Verify active character groups use distinct actual sprite texture sets."""
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[1]
catalog = json.loads((root / 'data/characters.json').read_text())
by_group = defaultdict(set)
by_textures = defaultdict(set)
for card in catalog['characters']:
    if not card['enabled']:
        continue
    scene = root / card['scene_path'].removeprefix('res://')
    textures = re.findall(r'path="(res://[^"\n]+\.png)"', scene.read_text())
    if not textures:
        raise SystemExit('Missing texture references: ' + str(scene))
    signature = tuple(sorted(hashlib.sha256((root / path.removeprefix('res://')).read_bytes()).hexdigest() for path in textures))
    by_group[card['group']].add(card['scene_path'])
    by_textures[signature].add(card['character_id'])
duplicates = [sorted(cards) for cards in by_textures.values() if len(cards) > 1]
if duplicates:
    raise SystemExit('Duplicate active character texture sets: ' + json.dumps(duplicates))
assert len(by_group) == 5 and len(by_group[4]) == 58 and all(len(cards) == (11 if group in (47, 48) else 10) for group, cards in by_group.items() if group != 4) and len(by_textures) == 100
print(f'UNIQUE CHARACTER ASSETS PASS: {len(by_group)} active groups, {len(by_textures)} unique texture sets, no duplicate roles')
