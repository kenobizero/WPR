ps_3_0
; FXAA (NVIDIA "FXAA 3 console"-style, branch-free), one texture, no constants to set.
; The texel size arrives in the texcoord's zw (the quad is built per texture), so the effect
; needs no parameters.
def c0, 0.299, 0.587, 0.114, 0.0
def c1, 0.0078125, 0.03125, 8.0, -8.0
def c2, -0.16666667, 0.16666667, -0.5, 0.5
def c3, 0.25, 0.0, 0.0, 0.0
dcl_texcoord0 v0
dcl_2d s0

; r2.xy = one texel in uv units. The vertex shader passes the quad's second texcoord pair
; through in v0.zw (WprFxaaVertex): derivatives (dsx/dsy) would need an extension on GLSL ES 1.00.
mov r2.xy, v0.zw

; corner coordinates: NW r3, NE r4, SW r5, SE r6
add r3.xy, v0, -r2
add r6.xy, v0, r2
add r4.x, v0.x, r2.x
add r4.y, v0.y, -r2.y
add r5.x, v0.x, -r2.x
add r5.y, v0.y, r2.y

texld r7, r3, s0
texld r8, r4, s0
texld r9, r5, s0
texld r10, r6, s0
texld r11, v0, s0

; luma: r12 = (NW, NE, SW, SE), r13.x = M
dp3 r12.x, r7, c0
dp3 r12.y, r8, c0
dp3 r12.z, r9, c0
dp3 r12.w, r10, c0
dp3 r13.x, r11, c0

; lumaMin r14.x, lumaMax r14.y
min r14.x, r12.x, r12.y
min r14.y, r12.z, r12.w
min r14.x, r14.x, r14.y
min r14.x, r14.x, r13.x
max r14.y, r12.x, r12.y
max r14.z, r12.z, r12.w
max r14.y, r14.y, r14.z
max r14.y, r14.y, r13.x

; edge direction
add r15.x, r12.x, r12.y
add r15.y, r12.z, r12.w
add r15.z, r12.x, r12.z
add r15.w, r12.y, r12.w
add r0.x, r15.y, -r15.x
add r0.y, r15.z, -r15.w

; dirReduce = max(sum * 1/32, 1/128); dir = clamp(dir / (min(|dir|) + dirReduce), -8, 8) * texel
add r1.x, r15.x, r15.y
mul r1.x, r1.x, c1.y
max r1.x, r1.x, c1.x
min r1.y, r0_abs.x, r0_abs.y
add r1.y, r1.y, r1.x
rcp r1.y, r1.y
mul r0.xy, r0, r1.y
min r0.xy, r0, c1.z
max r0.xy, r0, c1.w
mul r0.xy, r0, r2

; rgbA = 0.5 * (tap(-1/6) + tap(+1/6)) along dir
mad r3.xy, r0, c2.x, v0
mad r4.xy, r0, c2.y, v0
texld r7, r3, s0
texld r8, r4, s0
add r9, r7, r8
mul r9, r9, c2.w

; rgbB = 0.5 * rgbA + 0.25 * (tap(-1/2) + tap(+1/2))
mad r5.xy, r0, c2.z, v0
mad r6.xy, r0, c2.w, v0
texld r10, r5, s0
texld r11, r6, s0
add r10, r10, r11
mul r10, r10, c3.x
mad r10, r9, c2.w, r10

; fall back to rgbA when rgbB leaves the local luma range
dp3 r1.z, r10, c0
add r2.x, r1.z, -r14.x
add r2.y, r14.y, -r1.z
min r2.x, r2.x, r2.y
cmp r0, r2.x, r10, r9
mov oC0, r0
