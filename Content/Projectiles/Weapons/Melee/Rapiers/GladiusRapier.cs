using CalamityOverhaul.Common;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class GladiusRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override string GlowPath => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Gladius].Value;
        public override Texture2D GlowValue => TextureAssets.Item[ItemID.Gladius].Value;
        int fireIndex;
        public override void SetRapiers() {
            PremanentToSkialthRot = 22;
            overHitModeing = 13;
            StabbingSpread = 0.3f;
            StabAmplitudeMin = 60;
            StabAmplitudeMax = 100;
            ShurikenOut = CWRSound.ShurikenOut with { Pitch = -0.32f };
            CWRUtils.SafeLoadItem(ItemID.Gladius);
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                return;
            }
            if (++fireIndex > 2) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity * 13
                , ModContent.ProjectileType<GladiusBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
                fireIndex = 0;
            }
        }
    }
}
