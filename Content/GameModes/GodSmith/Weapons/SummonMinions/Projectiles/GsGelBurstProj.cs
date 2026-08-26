using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 融胶爆：史莱姆协同震波。同一目标短窗内被两只不同史莱姆命中时由 owner 生成，
    /// 半径 60 的一次性凝胶爆（伤害窗前 6 帧），三相 = 挤压弹出 / 凝胶飞溅 / 余渍缓消
    /// </summary>
    internal class GsGelBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color GelBlue = new(96, 148, 255);
        private static readonly Color GelPale = new(178, 210, 255);
        private static readonly Color GelDeep = new(46, 76, 170);

        private const int BurstLife = 14;
        private const float BurstRadius = 60f;

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.7247f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BurstLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //一次爆每目标只结算一次
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            //出手演出放 AI 首帧：OnSpawn 只在生成端跑，远端看不到
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.6f, Pitch = 0.15f },
                    Projectile.Center);
                //飞溅相：凝胶团抛散（一次性 8 粒）
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_FarmGelGlob>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f)
                            - new Vector2(0f, 1.5f),
                        Main.rand.NextBool() ? GelBlue : GelPale,
                        Main.rand.NextFloat(0.4f, 0.75f))?.Configure(Main.rand.Next(18, 30));
                }
            }
            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, GelBlue.ToVector3() * 0.25f
                    * (1f - Life / BurstLife));
            }
        }

        /// <summary>伤害窗只开前 6 帧（余下是纯余渍演出）</summary>
        public override bool? CanDamage() => Life <= 6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = BurstRadius * MathHelper.Clamp(Life / 4f, 0.4f, 1f);
            return targetHitbox.Distance(Projectile.Center) <= radius;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Slimed, 150);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //余痕相：地渍气泡回升，活得比爆体久
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FarmGelBubble>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 16f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    GelPale, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Projectile.Center.Y + 20f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float t = Life / BurstLife;
            //挤压弹出：先横压后纵弹的弹性包络
            float pop = (float)Math.Sin(MathHelper.Clamp(t * 2.2f, 0f, 1f) * MathHelper.Pi);
            float squash = 1f + 0.35f * (float)Math.Sin(t * 9f + Seed) * (1f - t);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float span = BurstRadius / (soft.Width * 0.5f);

            Main.EntitySpriteDraw(soft, pos, null, GelDeep * (0.6f * pop), 0f,
                soft.Size() / 2f, new Vector2(span * squash, span * 0.72f / squash),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, pos, null, GelBlue * (0.55f * pop), 0f,
                soft.Size() / 2f, new Vector2(span * 0.7f * squash, span * 0.5f / squash),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, (GelPale with { A = 0 }) * (0.5f * pop),
                0f, glow.Size() / 2f, span * 1.15f, SpriteEffects.None, 0);
            return false;
        }
    }
}
