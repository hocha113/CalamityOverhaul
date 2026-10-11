using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

// Float colors preserve radiance in the dedicated bloom source, without byte clamping.
[StructLayout(LayoutKind.Sequential)]
internal struct SakuraHdrVertex : IVertexType
{
    public Vector3 Position;
    public Vector4 Color;
    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.Color, 0));
    VertexDeclaration IVertexType.VertexDeclaration => Declaration;

    public SakuraHdrVertex(Vector3 position, Vector4 color) { Position = position; Color = color; }
}
