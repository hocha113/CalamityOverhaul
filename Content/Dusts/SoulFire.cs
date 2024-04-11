using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Dusts
{
    internal class SoulFire : ModDust
    {
        public override string Texture => CWRConstant.Dust + "SoulFire";

        public override void OnSpawn(Dust dust) {
            dust.scale = Main.rand.NextFloat(0.8f, 1.2f);
            dust.velocity = new Vector2(0, -Main.rand.NextFloat(1, 1.2f));
            dust.alpha = Main.rand.Next(185, 255);
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.015f;
            dust.alpha -= 1;
            if (dust.scale <= 0) {
                dust.active = false;
            }
            return false;
        }

        public override bool PreDraw(Dust dust) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                texture,
                dust.position - Main.screenPosition,
                CWRUtils.GetRec(texture, (int)(Main.GameUpdateCount / 10 % 4), 4),
                Color.White * (dust.alpha / 255f),
                dust.rotation,
                CWRUtils.GetOrig(texture, 4),
                dust.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
