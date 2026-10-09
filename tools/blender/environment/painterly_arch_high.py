"""RS_HeroArch_P, stage 1: the painterly hero arch's high poly (ART_DIRECTION_PAINTERLY s2 "chunky braids").

Same generator, spine, footprint and crown as heroarch_v2_high (the Span placement and the WaterfallMouth stay
valid), but 8 thick strands instead of 14 (three ropes of 3/3/2, wider ropes), one secondary root instead of
three, and no weathering: no chips, cracks or grain, only the broad lumps. Rounded, pillowy strands with clean
seams between them; the paint (painterly_rock.py) carries the rest.
Output: work/rootstone/RS_HeroArch_P_high.blend (object RS_HeroArch_P_high). Then painterly_rock.py RS_HeroArch.
SUPERSEDED 2026-10-09 by painterly_arch_strands.py (the shipped RS_HeroArch_P); kept for reference.
Usage: blender ... -P painterly_arch_high.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
import heroarch_v2_high as H  # noqa: E402

H.NAME = 'RS_HeroArch_P'
H.SPEC.update(
    ropes=[dict(n=3, splay=(1.25, 0.7)), dict(n=3, splay=(0.55, 1.2)), dict(n=2, splay=(0.9, 0.95))],
    rope_off=0.66, rope_r=0.6, strand_turns=0.75, secondary=1, voxel=0.15, seed=112)

_displace = E.displace


def broad_only(ob, texture, strength, *a, **kw):
    """Keep the broad lumps (at 60 %), skip every fine weathering displacement."""
    if texture.name.startswith('lumps'):
        _displace(ob, texture, strength * 0.6, *a, **kw)


E.displace = broad_only
H.rockify = lambda *a, **kw: None
H.cracks = lambda *a, **kw: None

if __name__ == '__main__':
    H.build(quick=False)
