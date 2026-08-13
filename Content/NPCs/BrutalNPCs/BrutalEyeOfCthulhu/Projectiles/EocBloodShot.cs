using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles
{
    /// <summary>
    /// 血珠弹：ai[0]=0 直射（微减速再增压），=1 重力喷泉弹<br/>
    /// 液体材质：速度拉伸+沿途滴洒+落点溅裂
    /// </summary>
    internal class EocBloodShot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool IsFountain => Projectile.ai[0] == 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 480;
            Projectile.alpha = 255;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
                }
            }

            if (IsFountain) {
                //重力弹：喷泉抛物线
                Projectile.velocity.Y += 0.34f;
                if (Projectile.velocity.Y > 18f) {
                    Projectile.velocity.Y = 18f;
                }
            }
            else {
                //直射弹：先泄力后增压，飞行期有演变
                if (Projectile.timeLeft > 440) {
                    Projectile.velocity *= 0.985f;
                }
                else if (Projectile.timeLeft > 300) {
                    Projectile.velocity *= 1.021f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //沿途滴洒
            if (!VaultUtils.isServer && Main.rand.NextBool(5) && EocMotion.OnScreen(Projectile.Center, 200f)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, -Projectile.velocity * 0.12f
                    + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    EocMotion.Arterial * 0.85f, Main.rand.NextFloat(0.5f, 0.9f))?
                    .Configure(Main.rand.Next(14, 24), 0.3f, 0.98f);
            }

            Lighting.AddLight(Projectile.Center, EocMotion.Arterial.ToVector3() * 0.4f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, IsFountain ? 150 : 90);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !EocMotion.OnScreen(Projectile.Center, 300f)) {
                return;
            }
            //溅裂
            Vector2 splashDir = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = splashDir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.3f))?.Configure(Main.rand.Next(18, 30), 0.34f, 0.98f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.4f, Pitch = -0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //旧位残迹（液体拖丝）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                spriteBatchDrawGlob(oldDrawPos, t * 0.35f, 0.55f * t);
            }

            spriteBatchDrawGlob(pos, 1f, 1f);
            return false;

            void spriteBatchDrawGlob(Vector2 drawPos, float alpha, float scaleMul) {
                //快成线、慢成珠
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1f);
                Vector2 scale = new Vector2(0.44f * (1f - stretch * 0.4f), 0.6f * (1f + stretch * 1.6f))
                    * Projectile.scale * scaleMul;
                Color dark = EocMotion.VenousDark * (0.9f * alpha);
                Color core = Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, 0.5f) * alpha;
                Main.EntitySpriteDraw(tex, drawPos, null, dark, Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
    }
}
