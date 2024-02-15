using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.CWRUtils;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class GunCasing : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "GunCasing";

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.damage = 10;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.scale = 1.2f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void AI() {
            Time++;
            Projectile.rotation += MathHelper.ToRadians(Projectile.velocity.X * 13);
            Projectile.velocity += new Vector2(0, 0.1f);

            if (Time % 13 == 0)
                Dust.NewDust(Projectile.Center, 3, 3, DustID.Smoke, Projectile.velocity.X, Projectile.velocity.Y);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = GetT2DValue(Texture);

            Main.EntitySpriteDraw(
                mainValue,
                WDEpos(Projectile.Center),
                null,
                Color.White,
                Projectile.rotation,
                GetOrig(mainValue),
                Projectile.scale,
                SpriteEffects.None,
                0
                );

            return false;
        }
    }
}
