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
blob = mine[:4] + ctab + mine[4:]
assert len(blob) % 4 == 0
new = b[:PS_LEN_AT] + struct.pack('<I', len(blob)) + blob + b[PS_AT + PS_LEN:]
open(out, 'wb').write(new)
print(f"wrote {out}: {len(new)} bytes (pixel shader {len(blob)} bytes, was {PS_LEN})")
