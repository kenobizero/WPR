namespace WPR.Xna.Rhi
{
	/// <summary>
	/// Implemented by a graphics backend that enlarges render targets behind the game's back
	/// (supersampling). Lets the framework ask how much bigger the texture behind a render target of
	/// a given size really is, so a pass that samples it one texel at a time (FXAA) steps by the
	/// real texel, not the game's.
	/// </summary>
	public interface ITextureScaleSource
	{
		/// <summary>The factor a render target created at this size is enlarged by; 1 when it is not.</summary>
		float TextureScale(int width, int height);
	}
}
