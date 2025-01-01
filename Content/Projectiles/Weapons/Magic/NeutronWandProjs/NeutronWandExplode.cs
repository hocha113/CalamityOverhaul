using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs
{
    internal class NeutronWandExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 3;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.ArmorPenetration = 80;
        }

        public bool CanDrawCustom() => false;

        private void SpanStar(Vector2 offset) {
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < 13; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(Projectile.Center + offset
                        , vr * (0.1f + j * 0.1f), false, 20, 0.8f, Color.CadetBlue);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int j = 0; j < 122; j++) {
                    BasePRT spark2 = new PRT_HeavenfallStar(Projectile.Center + new Vector2(Main.rand.Next(-16, 16), -700)
                        , Projectile.velocity.UnitVector() * Main.rand.Next(66, 166), false, 17, Main.rand.NextFloat(1.2f, 1.3f), Color.BlueViolet);
                    PRTLoader.AddParticle(spark2);
                }
            }
            if (Projectile.ai[2] % 5 == 0) {
                SpanStar(new Vector2(0, Projectile.ai[2] * 80 - 500));
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;

            for (int i = 0; i < 33; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, Projectile.velocity.ToRotation() + MathHelper.PiOver2
                    , drawOrig, new Vector2(0.1f, 21), SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
