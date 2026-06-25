using CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>奸奇变数微粒：缓慢上浮并随生命轮转色相的十字魔火符点</summary>
    public class TzeentchRunePRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public float Size = Main.rand.NextFloat(2.0f, 4.5f);
        public float RiseSpeed = Main.rand.NextFloat(0.15f, 0.55f);
        public float Drift = Main.rand.NextFloat(-0.35f, 0.35f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(120f, 220f);
        public float Seed = Main.rand.NextFloat(10f);
        public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        public float RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f);
        public float HuePhase = Main.rand.NextFloat();

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Pos.Y -= RiseSpeed;
            Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * Drift;
            Rotation += RotationSpeed;
            return Life >= MaxLife || Pos.Y < panelPos.Y + 12f;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * Math.PI);
            float pulse = (float)Math.Sin((Life + Seed * 20f) * 0.08f) * 0.5f + 0.5f;
            float scale = Size * (0.7f + pulse * 0.45f);

            Color hue = TzeentchPalette.Cycle(HuePhase + Life * 0.004f);
            Color glow = hue * (alpha * 0.5f * fade);
            Color glint = Color.Lerp(hue, Color.White, 0.5f) * (alpha * 0.7f * fade);

            //外层柔光
            sb.Draw(px, Pos, null, glow, 0f, new Vector2(0.5f), new Vector2(scale * 2.6f), SpriteEffects.None, 0f);
            //十字魔火符:两道垂直拉伸的细芒
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), glint, Rotation, new Vector2(0.5f), new Vector2(scale * 2.4f, scale * 0.4f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), glint * 0.85f, Rotation + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(scale * 2.4f, scale * 0.4f), SpriteEffects.None, 0f);
            //中心亮点
            sb.Draw(px, Pos, null, Color.White * (alpha * 0.5f * fade * pulse), 0f, new Vector2(0.5f), new Vector2(scale * 0.4f), SpriteEffects.None, 0f);
        }
    }
}
