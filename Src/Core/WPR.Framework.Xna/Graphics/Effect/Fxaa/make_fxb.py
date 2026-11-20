#!/usr/bin/env python3
"""Builds FxaaEffect.fxb from the YUV-to-RGBA effect (a compiled D3D9 effect whose vertex shader
just passes position and texcoord through) by swapping its pixel shader for the assembled FXAA one.

The pixel shader blob sits in the "large object" table behind a 24-byte header whose last dword is
its length; the vertex shader's header and blob follow it, so only that length and the blob change.
The constant table (CTAB) of the original blob is kept so the effect's sampler parameters still match.
"""
import struct, sys
src, ps_bin, out = sys.argv[1:4]
b = open(src, 'rb').read()
PS_LEN_AT, PS_AT, PS_LEN = 0x14c, 0x150, 0x208
assert struct.unpack_from('<I', b, PS_LEN_AT)[0] == PS_LEN
assert b[PS_AT:PS_AT + 4] == bytes.fromhex('0003ffff')                      # ps_3_0 version token
old = b[PS_AT:PS_AT + PS_LEN]
assert old[4:8] == bytes.fromhex('feff3700') and old[8:12] == b'CTAB'        # comment token, 0x37 dwords
ctab = old[4:4 + 4 * (1 + 0x37)]
mine = open(ps_bin, 'rb').read()
assert mine[:4] == bytes.fromhex('0003ffff') and mine[-4:] == bytes.fromhex('ffff0000')

# MojoShader's assembler writes every pixel-shader input declaration with usage 0 (it assumes ps_1/ps_2,
# which carry none). ps_3_0 inputs are linked to the vertex shader's outputs BY usage, so the texcoord0
# input would link against POSITION0 and the program would fail to link (black screen). Patch the
# usage token of each input-register dcl to TEXCOORD (5), index 0.
words = list(struct.unpack('<%dI' % (len(mine) // 4), mine))
i, patched = 1, 0
while i < len(words) - 1:
    t = words[i]
    if t == 0x0000ffff:
        break
    if (t & 0xffff) == 0xfffe:                                   # comment: length in bits 16..30
        i += 1 + ((t >> 16) & 0x7fff)
        continue
    n = (t >> 24) & 0x0f
    if (t & 0xffff) == 0x1f and n == 2:
        dst = words[i + 2]
        regtype = ((dst >> 28) & 7) | ((dst >> 8) & 0x18)
        if regtype == 1 and words[i + 1] == 0x80000000:         # REG_TYPE_INPUT
            words[i + 1] = 0x80000000 | 5
            patched += 1
    i += 1 + n
assert patched == 1, patched
mine = struct.pack('<%dI' % len(words), *words)
blob = mine[:4] + ctab + mine[4:]
assert len(blob) % 4 == 0
new = b[:PS_LEN_AT] + struct.pack('<I', len(blob)) + blob + b[PS_AT + PS_LEN:]
# The YUV vertex shader copies only the texcoord's xy to the output (float2 tex). FXAA carries the texel
# size in zw, so widen the dcl_texcoord output mask and the mov's write mask from .xy to .xyzw.
def words(*w): return struct.pack('<%dI' % len(w), *w)
for old, repl in ((words(0x0200001f, 0x80000005, 0xe0030000), words(0x0200001f, 0x80000005, 0xe00f0000)),
                  (words(0x02000001, 0xe0030000, 0x90e40000), words(0x02000001, 0xe00f0000, 0x90e40000))):
    assert new.count(old) == 1, old.hex()
    new = new.replace(old, repl)
open(out, 'wb').write(new)
print(f"wrote {out}: {len(new)} bytes (pixel shader {len(blob)} bytes, was {PS_LEN})")
