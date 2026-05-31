using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs
{
    internal class NeutronMagchStar : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "MagicStar2";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.MaxUpdates = 4;
            Projectile.penetrate = 13 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
            Projectile.tileCollide = false;
            Projectile.alpha = 0;
            Projectile.timeLeft = 300;
            Projectile.ArmorPenetration = 80;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            Lighting.AddLight(Projectile.Center, Color.Blue.ToVector3());
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + VaultUtils.RandVr(8), Projectile.velocity.UnitVector() * Main.rand.Next(6, 16), Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 7);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vr = VaultUtils.RandVr(6);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, vr.X, vr.Y);
                Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FireworkFountain_Blue, vr.X, vr.Y)].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                float oldRotation = Projectile.oldRot[i];
                SpriteEffects effects = Projectile.oldSpriteDirection[i] == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Color color = Color.Lerp(Color.BlueViolet, Color.White, fade * 0.5f) * fade * 0.8f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, color, oldRotation, origin, Projectile.scale, effects);
            }
            return false;
        }
    }
}
