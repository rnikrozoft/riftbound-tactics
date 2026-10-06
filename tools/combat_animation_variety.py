"""Cycle genuine attack sheets while preserving the existing ability cadence."""
from copy import deepcopy
from pathlib import Path
import struct


def apply_animation_variety(slug, rules, root):
    folder = root / 'assets' / 'characters' / slug
    available = []
    for number in range(1, 4):
        name = f'attack{number:02}'
        path = folder / f'{slug}_{name}.png'
        if slug == 'swordsman' and number == 3:
            path = folder / 'swordsman_attack3.png'
        if path.exists():
            width, height = struct.unpack('>II', path.read_bytes()[16:24])
            available.append((name, (width // 100) * (height // 100)))
        elif slug == 'knight':
            available.append((name, 9 if number == 3 else 6))
    if len(available) < 2:
        return
    actions = rules['actions']
    for action in actions:
        for name, frames in available:
            if action['animation'] == name:
                set_animation(action, name, frames)
                break
    attack_modes = {'melee', 'ranged'}
    used = {a['animation'] for a in actions}
    missing = [v for v in available if v[0] not in used]
    if not missing:
        return
    if len(actions) == 1 and actions[0]['mode'] in attack_modes:
        # Repeat the same ability with each genuine sheet, including finishers.
        variants = [deepcopy(actions[0]) for _ in available]
        for action, (name, frames) in zip(variants, available):
            set_animation(action, name, frames)
        rules['actions'] = variants
    elif len(actions) == 2 and any(a['mode'] in attack_modes for a in actions):
        # Two complete skill cycles keep status/heal/summon frequency unchanged.
        variants = deepcopy(actions)
        for action in variants:
            if action['mode'] in attack_modes and missing:
                name, frames = missing.pop(0)
                set_animation(action, name, frames)
        rules['actions'] = actions + variants
    elif len(actions) == 3:
        seen = set()
        for action in actions:
            name = action['animation']
            if name in seen and action['mode'] in attack_modes and missing:
                alternate, frames = missing.pop(0)
                set_animation(action, alternate, frames)
            seen.add(name)


def set_animation(action, name, frames):
    action['animation'] = name
    action['frames'] = frames
    hits = action['hits']
    action['hit_frames'] = [min(frames - 1, max(1, round((i + 1) * frames / (hits + 1)))) for i in range(hits)]
