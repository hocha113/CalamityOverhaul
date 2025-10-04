using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class BloodThorn
    {
        public static int ID = 3;
    }

    internal class BloodThornProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;//使用空白占位符，因为这里要手动获取纹理

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SharpTears);
            //原版的血刺纹理，总共六帧，通常用于随机血刺的不同形态
            Texture2D value = TextureAssets.Projectile[ProjectileID.SharpTears].Value;
            return false;
        }
    }
}
