using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WPR.Xna.Rhi;

namespace WPR.Backend.FNA
{
    /// <summary>
    /// Wraps the real graphics backend and renders the game's backbuffer at a larger size
    /// (supersampling). The game still sees its own backbuffer size.
    /// </summary>
    public sealed class ScalingGraphicsBackend : IGraphicsBackend
    {
        private const int MaxScaledDimension = 4096;

        private readonly IGraphicsBackend _inner;
        private float _scale = 1f;
        private bool _backbufferBound = true;

        public ScalingGraphicsBackend(IGraphicsBackend inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        // ---- Scaling helpers ----

        private static RhiPresentationParameters ApplyMsaa(RhiPresentationParameters p)
        {
            int msaa = WPR.Engine.Graphics.GraphicsQuality.Msaa;
            if (msaa > p.multiSampleCount)
            {
                p.multiSampleCount = msaa;
                WPR.Common.Log.Info(WPR.Common.LogCategory.AppList, $"[wpr-gfx] msaa {msaa}x requested");
            }
            return p;
        }


        private RhiPresentationParameters ScaleBackbuffer(RhiPresentationParameters p)
        {
            float s = WPR.Engine.Graphics.RenderScale.Factor;
            int longest = Math.Max(p.backBufferWidth, p.backBufferHeight);
            if (longest > 0 && longest * s > MaxScaledDimension)
            {
                s = (float) MaxScaledDimension / longest;
            }
            if (s < 1f)
            {
                s = 1f;
            }
            _scale = s;
            p = ApplyMsaa(p);
            if (s != 1f)
            {
                p.backBufferWidth = (int) Math.Round(p.backBufferWidth * s);
                p.backBufferHeight = (int) Math.Round(p.backBufferHeight * s);
                WPR.Common.Log.Info(
                    WPR.Common.LogCategory.AppList,
                    $"[wpr-gfx] render scale {s:0.##}x: backbuffer {p.backBufferWidth}x{p.backBufferHeight}"
                );
            }
            return p;
        }

        private static void ScaleRect(ref int x, ref int y, ref int w, ref int h, float s)
        {
            int x2 = (int) Math.Round((x + w) * s);
            int y2 = (int) Math.Round((y + h) * s);
            x = (int) Math.Round(x * s);
            y = (int) Math.Round(y * s);
            w = x2 - x;
            h = y2 - y;
        }

        // ---- Members that change behaviour ----

        public IntPtr CreateDevice(ref RhiPresentationParameters presentationParameters, byte debugMode)
        {
            RhiPresentationParameters scaled = ScaleBackbuffer(presentationParameters);
            _backbufferBound = true;
            return _inner.CreateDevice(ref scaled, debugMode);
        }

        public void ResetBackbuffer(IntPtr device, ref RhiPresentationParameters presentationParameters)
        {
            RhiPresentationParameters scaled = ScaleBackbuffer(presentationParameters);
            _backbufferBound = true;
            _inner.ResetBackbuffer(device, ref scaled);
        }

        public void DestroyDevice(IntPtr device)
        {
            _inner.DestroyDevice(device);
            _scale = 1f;
            _backbufferBound = true;
        }

        public void GetBackbufferSize(IntPtr device, out int w, out int h)
        {
            _inner.GetBackbufferSize(device, out w, out h);
            if (_scale != 1f)
            {
                w = (int) Math.Round(w / _scale);
                h = (int) Math.Round(h / _scale);
            }
        }

        public void SetViewport(IntPtr device, ref RhiViewport viewport)
        {
            if (_scale != 1f && _backbufferBound)
            {
                RhiViewport v = viewport;
                int vx = v.x, vy = v.y, vw = v.w, vh = v.h;
                ScaleRect(ref vx, ref vy, ref vw, ref vh, _scale);
                v.x = vx;
                v.y = vy;
                v.w = vw;
                v.h = vh;
                _inner.SetViewport(device, ref v);
                return;
            }
            _inner.SetViewport(device, ref viewport);
        }

        public void SetScissorRect(IntPtr device, ref Rectangle scissor)
        {
            if (_scale != 1f && _backbufferBound)
            {
                int rx = scissor.X, ry = scissor.Y, rw = scissor.Width, rh = scissor.Height;
                ScaleRect(ref rx, ref ry, ref rw, ref rh, _scale);
                Rectangle r = new Rectangle(rx, ry, rw, rh);
                _inner.SetScissorRect(device, ref r);
                return;
            }
            _inner.SetScissorRect(device, ref scissor);
        }

        public void SetRenderTargets(IntPtr device, IntPtr renderTargets, int numRenderTargets, IntPtr depthStencilBuffer, DepthFormat depthFormat, byte preserveDepthStencilContents)
        {
            _backbufferBound = numRenderTargets <= 0;
            _inner.SetRenderTargets(device, renderTargets, numRenderTargets, depthStencilBuffer, depthFormat, preserveDepthStencilContents);
        }

        public void SwapBuffers(IntPtr device, Rectangle? sourceRectangle, Rectangle? destinationRectangle, IntPtr overrideWindowHandle)
        {
            if (_scale != 1f && sourceRectangle.HasValue)
            {
                Rectangle src = sourceRectangle.Value;
                int sx = src.X, sy = src.Y, sw = src.Width, sh = src.Height;
                ScaleRect(ref sx, ref sy, ref sw, ref sh, _scale);
                sourceRectangle = new Rectangle(sx, sy, sw, sh);
            }
            _inner.SwapBuffers(device, sourceRectangle, destinationRectangle, overrideWindowHandle);
        }

        // ---- Everything below is forwarded unchanged ----

        public uint PrepareWindowAttributes() => _inner.PrepareWindowAttributes();

        public void GetDrawableSize(IntPtr window, out int w, out int h) => _inner.GetDrawableSize(window, out w, out h);

        public void Clear(IntPtr device, ClearOptions options, ref Vector4 color, float depth, int stencil) =>
            _inner.Clear(device, options, ref color, depth, stencil);

        public void DrawIndexedPrimitives(
            IntPtr device, PrimitiveType primitiveType, int baseVertex, int minVertexIndex,
            int numVertices, int startIndex, int primitiveCount, IntPtr indices, IndexElementSize indexElementSize) =>
            _inner.DrawIndexedPrimitives(device, primitiveType, baseVertex, minVertexIndex,
                numVertices, startIndex, primitiveCount, indices, indexElementSize);

        public void DrawInstancedPrimitives(
            IntPtr device, PrimitiveType primitiveType, int baseVertex, int minVertexIndex,
            int numVertices, int startIndex, int primitiveCount, int instanceCount, IntPtr indices, IndexElementSize indexElementSize) =>
            _inner.DrawInstancedPrimitives(device, primitiveType, baseVertex, minVertexIndex,
                numVertices, startIndex, primitiveCount, instanceCount, indices, indexElementSize);

        public void DrawPrimitives(IntPtr device, PrimitiveType primitiveType, int vertexStart, int primitiveCount) =>
            _inner.DrawPrimitives(device, primitiveType, vertexStart, primitiveCount);

        public void GetBlendFactor(IntPtr device, out Color blendFactor) => _inner.GetBlendFactor(device, out blendFactor);

        public void SetBlendFactor(IntPtr device, ref Color blendFactor) => _inner.SetBlendFactor(device, ref blendFactor);

        public int GetMultiSampleMask(IntPtr device) => _inner.GetMultiSampleMask(device);

        public void SetMultiSampleMask(IntPtr device, int mask) => _inner.SetMultiSampleMask(device, mask);

        public int GetReferenceStencil(IntPtr device) => _inner.GetReferenceStencil(device);

        public void SetReferenceStencil(IntPtr device, int reference) => _inner.SetReferenceStencil(device, reference);

        public void SetBlendState(IntPtr device, ref RhiBlendState blendState) => _inner.SetBlendState(device, ref blendState);

        public void SetDepthStencilState(IntPtr device, ref RhiDepthStencilState depthStencilState) =>
            _inner.SetDepthStencilState(device, ref depthStencilState);

        public void ApplyRasterizerState(IntPtr device, ref RhiRasterizerState rasterizerState) =>
            _inner.ApplyRasterizerState(device, ref rasterizerState);

        public void VerifySampler(IntPtr device, int index, IntPtr texture, ref RhiSamplerState sampler)
        {
            int aniso = WPR.Engine.Graphics.GraphicsQuality.Anisotropy;
            // Only smooth (Linear / Anisotropic) samplers are upgraded; Point stays Point so pixel-art HUDs keep their look.
            if (aniso > 1 && (sampler.filter == TextureFilter.Linear || sampler.filter == TextureFilter.Anisotropic))
            {
                RhiSamplerState s = sampler;
                s.filter = TextureFilter.Anisotropic;
                if (s.maxAnisotropy < aniso) s.maxAnisotropy = aniso;
                _inner.VerifySampler(device, index, texture, ref s);
                return;
            }
            _inner.VerifySampler(device, index, texture, ref sampler);
        }

        public void VerifyVertexSampler(IntPtr device, int index, IntPtr texture, ref RhiSamplerState sampler) =>
            _inner.VerifyVertexSampler(device, index, texture, ref sampler);

        public void ApplyVertexBufferBindings(IntPtr device, IntPtr bindings, int numBindings, byte bindingsUpdated, int baseVertex) =>
            _inner.ApplyVertexBufferBindings(device, bindings, numBindings, bindingsUpdated, baseVertex);

        public void ResolveTarget(IntPtr device, ref RhiRenderTargetBinding target) => _inner.ResolveTarget(device, ref target);

        public void ReadBackbuffer(IntPtr device, int x, int y, int w, int h, IntPtr data, int dataLength) =>
            _inner.ReadBackbuffer(device, x, y, w, h, data, dataLength);

        public SurfaceFormat GetBackbufferSurfaceFormat(IntPtr device) => _inner.GetBackbufferSurfaceFormat(device);

        public DepthFormat GetBackbufferDepthFormat(IntPtr device) => _inner.GetBackbufferDepthFormat(device);

        public int GetBackbufferMultiSampleCount(IntPtr device) => _inner.GetBackbufferMultiSampleCount(device);

        public IntPtr CreateTexture2D(IntPtr device, SurfaceFormat format, int width, int height, int levelCount, byte isRenderTarget) =>
            _inner.CreateTexture2D(device, format, width, height, levelCount, isRenderTarget);

        public IntPtr CreateTexture3D(IntPtr device, SurfaceFormat format, int width, int height, int depth, int levelCount) =>
            _inner.CreateTexture3D(device, format, width, height, depth, levelCount);

        public IntPtr CreateTextureCube(IntPtr device, SurfaceFormat format, int size, int levelCount, byte isRenderTarget) =>
            _inner.CreateTextureCube(device, format, size, levelCount, isRenderTarget);

        public void AddDisposeTexture(IntPtr device, IntPtr texture) => _inner.AddDisposeTexture(device, texture);

        public void SetTextureData2D(IntPtr device, IntPtr texture, int x, int y, int w, int h, int level, IntPtr data, int dataLength) =>
            _inner.SetTextureData2D(device, texture, x, y, w, h, level, data, dataLength);

        public void SetTextureData3D(IntPtr device, IntPtr texture, int x, int y, int z, int w, int h, int d, int level, IntPtr data, int dataLength) =>
            _inner.SetTextureData3D(device, texture, x, y, z, w, h, d, level, data, dataLength);

        public void SetTextureDataCube(IntPtr device, IntPtr texture, int x, int y, int w, int h, CubeMapFace cubeMapFace, int level, IntPtr data, int dataLength) =>
            _inner.SetTextureDataCube(device, texture, x, y, w, h, cubeMapFace, level, data, dataLength);

        public void SetTextureDataYUV(IntPtr device, IntPtr y, IntPtr u, IntPtr v, int yWidth, int yHeight, int uvWidth, int uvHeight, IntPtr data, int dataLength) =>
            _inner.SetTextureDataYUV(device, y, u, v, yWidth, yHeight, uvWidth, uvHeight, data, dataLength);

        public void GetTextureData2D(IntPtr device, IntPtr texture, int x, int y, int w, int h, int level, IntPtr data, int dataLength) =>
            _inner.GetTextureData2D(device, texture, x, y, w, h, level, data, dataLength);

        public void GetTextureData3D(IntPtr device, IntPtr texture, int x, int y, int z, int w, int h, int d, int level, IntPtr data, int dataLength) =>
            _inner.GetTextureData3D(device, texture, x, y, z, w, h, d, level, data, dataLength);

        public void GetTextureDataCube(IntPtr device, IntPtr texture, int x, int y, int w, int h, CubeMapFace cubeMapFace, int level, IntPtr data, int dataLength) =>
            _inner.GetTextureDataCube(device, texture, x, y, w, h, cubeMapFace, level, data, dataLength);

        public IntPtr GenColorRenderbuffer(IntPtr device, int width, int height, SurfaceFormat format, int multiSampleCount, IntPtr texture) =>
            _inner.GenColorRenderbuffer(device, width, height, format, multiSampleCount, texture);

        public IntPtr GenDepthStencilRenderbuffer(IntPtr device, int width, int height, DepthFormat format, int multiSampleCount) =>
            _inner.GenDepthStencilRenderbuffer(device, width, height, format, multiSampleCount);

        public void AddDisposeRenderbuffer(IntPtr device, IntPtr renderbuffer) => _inner.AddDisposeRenderbuffer(device, renderbuffer);

        public IntPtr GenVertexBuffer(IntPtr device, byte dynamic, BufferUsage usage, int sizeInBytes) =>
            _inner.GenVertexBuffer(device, dynamic, usage, sizeInBytes);

        public void AddDisposeVertexBuffer(IntPtr device, IntPtr buffer) => _inner.AddDisposeVertexBuffer(device, buffer);

        public void SetVertexBufferData(IntPtr device, IntPtr buffer, int offsetInBytes, IntPtr data, int elementCount, int elementSizeInBytes, int vertexStride, SetDataOptions options) =>
            _inner.SetVertexBufferData(device, buffer, offsetInBytes, data, elementCount, elementSizeInBytes, vertexStride, options);

        public void GetVertexBufferData(IntPtr device, IntPtr buffer, int offsetInBytes, IntPtr data, int elementCount, int elementSizeInBytes, int vertexStride) =>
            _inner.GetVertexBufferData(device, buffer, offsetInBytes, data, elementCount, elementSizeInBytes, vertexStride);

        public IntPtr GenIndexBuffer(IntPtr device, byte dynamic, BufferUsage usage, int sizeInBytes) =>
            _inner.GenIndexBuffer(device, dynamic, usage, sizeInBytes);

        public void AddDisposeIndexBuffer(IntPtr device, IntPtr buffer) => _inner.AddDisposeIndexBuffer(device, buffer);

        public void SetIndexBufferData(IntPtr device, IntPtr buffer, int offsetInBytes, IntPtr data, int dataLength, SetDataOptions options) =>
            _inner.SetIndexBufferData(device, buffer, offsetInBytes, data, dataLength, options);

        public void GetIndexBufferData(IntPtr device, IntPtr buffer, int offsetInBytes, IntPtr data, int dataLength) =>
            _inner.GetIndexBufferData(device, buffer, offsetInBytes, data, dataLength);

        public void CreateEffect(IntPtr device, byte[] effectCode, int length, out IntPtr effect, out IntPtr effectData) =>
            _inner.CreateEffect(device, effectCode, length, out effect, out effectData);

        public void CloneEffect(IntPtr device, IntPtr cloneSource, out IntPtr effect, out IntPtr effectData) =>
            _inner.CloneEffect(device, cloneSource, out effect, out effectData);

        public void AddDisposeEffect(IntPtr device, IntPtr effect) => _inner.AddDisposeEffect(device, effect);

        public void SetEffectTechnique(IntPtr device, IntPtr effect, IntPtr technique) =>
            _inner.SetEffectTechnique(device, effect, technique);

        public void ApplyEffect(IntPtr device, IntPtr effect, uint pass, IntPtr stateChanges) =>
            _inner.ApplyEffect(device, effect, pass, stateChanges);

        public void BeginPassRestore(IntPtr device, IntPtr effect, IntPtr stateChanges) =>
            _inner.BeginPassRestore(device, effect, stateChanges);

        public void EndPassRestore(IntPtr device, IntPtr effect) => _inner.EndPassRestore(device, effect);

        public IntPtr CreateQuery(IntPtr device) => _inner.CreateQuery(device);

        public void AddDisposeQuery(IntPtr device, IntPtr query) => _inner.AddDisposeQuery(device, query);

        public void QueryBegin(IntPtr device, IntPtr query) => _inner.QueryBegin(device, query);

        public void QueryEnd(IntPtr device, IntPtr query) => _inner.QueryEnd(device, query);

        public byte QueryComplete(IntPtr device, IntPtr query) => _inner.QueryComplete(device, query);

        public int QueryPixelCount(IntPtr device, IntPtr query) => _inner.QueryPixelCount(device, query);

        public byte SupportsDXT1(IntPtr device) => _inner.SupportsDXT1(device);

        public byte SupportsS3TC(IntPtr device) => _inner.SupportsS3TC(device);

        public byte SupportsBC7(IntPtr device) => _inner.SupportsBC7(device);

        public byte SupportsHardwareInstancing(IntPtr device) => _inner.SupportsHardwareInstancing(device);

        public byte SupportsNoOverwrite(IntPtr device) => _inner.SupportsNoOverwrite(device);

        public byte SupportsSRGBRenderTargets(IntPtr device) => _inner.SupportsSRGBRenderTargets(device);

        public void GetMaxTextureSlots(IntPtr device, out int textures, out int vertexTextures) =>
            _inner.GetMaxTextureSlots(device, out textures, out vertexTextures);

        public int GetMaxMultiSampleCount(IntPtr device, SurfaceFormat format, int preferredMultiSampleCount) =>
            _inner.GetMaxMultiSampleCount(device, format, preferredMultiSampleCount);

        public void SetStringMarker(IntPtr device, string text) => _inner.SetStringMarker(device, text);

        public IntPtr ReadImageStream(Stream stream, out int width, out int height, out int len, int forceW = -1, int forceH = -1, bool zoom = false) =>
            _inner.ReadImageStream(stream, out width, out height, out len, forceW, forceH, zoom);

        public void FreeImage(IntPtr mem) => _inner.FreeImage(mem);

        public void WritePNGStream(Stream stream, int srcW, int srcH, int dstW, int dstH, IntPtr data) =>
            _inner.WritePNGStream(stream, srcW, srcH, dstW, dstH, data);

        public void WriteJPGStream(Stream stream, int srcW, int srcH, int dstW, int dstH, IntPtr data, int quality) =>
            _inner.WriteJPGStream(stream, srcW, srcH, dstW, dstH, data, quality);

        public GraphicsAdapter[] GetGraphicsAdapters() => _inner.GetGraphicsAdapters();

        public DisplayMode GetCurrentDisplayMode(int adapterIndex) => _inner.GetCurrentDisplayMode(adapterIndex);
    }
}
