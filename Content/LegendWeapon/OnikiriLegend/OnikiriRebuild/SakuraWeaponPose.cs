using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal readonly struct SakuraWeaponPose(
    Vector2 center, Vector2 rotationOrigin, float fullRotation, int facingDirection, int gravityDirection)
{
    private static readonly Vector2[] WeaponUvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    public Vector2 RotateWorldPoint(Vector2 point) =>
        rotationOrigin + Vector2.Transform(point - rotationOrigin, Matrix.CreateRotationZ(fullRotation));

    public Vector2 TransformLocalPoint(Vector2 point) => RotateWorldPoint(center +
        new Vector2(point.X * facingDirection, point.Y * gravityDirection));

    public Matrix CreateSlashTransform(float scale, float tilt) =>
        Matrix.CreateScale(scale * facingDirection, scale * gravityDirection, 1f) *
        Matrix.CreateRotationZ(tilt * facingDirection * gravityDirection + fullRotation);

    public void FillWeaponVertices(Vector2[] corners, float scale, Matrix worldToScreen, Color color,
        VertexPositionColorTexture[] vertices)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 world = TransformLocalPoint(corners[i] * scale);
            vertices[i] = new VertexPositionColorTexture(
                Vector3.Transform(new Vector3(world, 0), worldToScreen), color, WeaponUvs[i]);
        }
    }
}
