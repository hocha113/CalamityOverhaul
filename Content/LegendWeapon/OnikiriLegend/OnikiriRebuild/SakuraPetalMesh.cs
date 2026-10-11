using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraPetalMesh
{
    internal readonly Vector3[] Positions, Normals, FrontColors, BackColors;
    internal readonly float[] Fold;
    internal readonly int[] Indices;
    private static readonly Vector2[] Control =
    [
        new(.06f, .77f), new(-.19f, .54f), new(-.56f, -.05f), new(-.34f, -.48f),
        new(-.34f, -.48f), new(-.30f, -.59f), new(-.26f, -.62f), new(-.20f, -.63f),
        new(-.20f, -.63f), new(-.19f, -.60f), new(-.18f, -.51f), new(-.14f, -.50f),
        new(-.14f, -.50f), new(-.12f, -.57f), new(-.10f, -.68f), new(-.045f, -.73f),
        new(-.045f, -.73f), new(.20f, -.81f), new(.48f, -.48f), new(.49f, -.24f),
        new(.49f, -.24f), new(.58f, .11f), new(.32f, .49f), new(.06f, .77f)
    ];

    internal SakuraPetalMesh(int sectors, int rings)
    {
        int count = 1 + sectors * rings;
        Positions = new Vector3[count]; Normals = new Vector3[count];
        FrontColors = new Vector3[count]; BackColors = new Vector3[count]; Fold = new float[count];
        Indices = new int[sectors * (2 * rings - 1) * 3];
        Vector2 center = new(0, -.08f);
        SetPoint(0, center);
        for (int ring = 1; ring <= rings; ring++)
            for (int sector = 0; sector < sectors; sector++)
                SetPoint(1 + (ring - 1) * sectors + sector,
                    Vector2.Lerp(center, Outline(sector * 6f / sectors), ring / (float)rings));
        int n = 0;
        void Triangle(int a, int b, int c) { Indices[n++] = a; Indices[n++] = b; Indices[n++] = c; }
        for (int s = 0; s < sectors; s++) Triangle(0, 1 + s, 1 + (s + 1) % sectors);
        for (int ring = 1; ring < rings; ring++)
            for (int s = 0; s < sectors; s++)
            {
                int next = (s + 1) % sectors;
                int a = 1 + (ring - 1) * sectors + s, b = 1 + (ring - 1) * sectors + next;
                int c = 1 + ring * sectors + s, d = 1 + ring * sectors + next;
                Triangle(a, c, d); Triangle(a, d, b);
            }
    }

    private static Vector2 Outline(float at)
    {
        int i = (int)at * 4;
        float t = at - (int)at, q = 1 - t;
        return q * q * q * Control[i] + 3 * q * q * t * Control[i + 1]
            + 3 * q * t * t * Control[i + 2] + t * t * t * Control[i + 3];
    }

    private void SetPoint(int i, Vector2 p)
    {
        float x = p.X, y = p.Y, ridge = MathF.Exp(-32 * x * x);
        float z = .52f * x * x + .16f * y * y - .18f * x * y + .09f * y + .085f * ridge * (y + .8f);
        float dx = 1.04f * x - .18f * y - 5.44f * x * ridge * (y + .8f);
        float dy = .32f * y - .18f * x + .09f + .085f * ridge;
        Positions[i] = new Vector3(x, y, z - .06f);
        Normals[i] = Vector3.Normalize(new Vector3(dx, dy, -1));
        float along = MathHelper.Clamp((y + .7f) / 1.47f, 0, 1);
        FrontColors[i] = Color.Lerp(new Color(255, 217, 225), new Color(255, 158, 187), along * .78f).ToVector3();
        BackColors[i] = Color.Lerp(new Color(247, 162, 191), new Color(222, 104, 150), along * .78f).ToVector3();
        Fold[i] = MathF.Exp(-95 * x * x) * (.025f + .035f * along);
    }
}
