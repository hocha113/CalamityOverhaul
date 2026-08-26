using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 雷区殉爆弹：任一引爆后邻雷位置生成，15 帧引信起爆，链式传播封顶 4 层。<br/>
    /// ai[0]=链深（1 起）ai[1]=爆炸半径。完全独立于原版陷阱 AI 与冷却；
    /// 起爆帧由 owner 端继续传播（每座陷阱 90 帧至多参与一次，节流在 SentryGrid）
    /// </summary>
    internal class GsSentryChainBlastProj : ModProjectile
    {
        /// <summary>引信帧数</summary>
        private const int FuseFrames = 15;

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private static readonly Color BlastBright = new(255, 224, 150);
        private static readonly Color BlastMain = new(255, 132, 44);
        private static readonly Color BlastDeep = new(122, 44, 16);

        private ref float Depth => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FuseFrames + 16;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>引信期无判定，起爆后 5 帧判定窗</summary>
        public override bool? CanDamage() => Age >= FuseFrames && Age <= FuseFrames + 5 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsSentryBurstProj.DistRectPoint(targetHitbox, Projectile.Center) <= Radius;

        public override void AI() {
            Age++;
            //引信期滴答：临爆加快（各端本地演出）
            if (Age < FuseFrames) {
                if (!VaultUtils.isServer) {
                    int beep = Age > FuseFrames - 6 ? 3 : 6;
                    if (Age % beep == 0f) {
                        PRTLoader.NewParticle<PRT_Spark>(
                            Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                            new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)),
                            BlastBright, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, 10);
                    }
                    Lighting.AddLight(Projectile.Center, BlastMain.ToVector3() * (0.15f + 0.1f * (Age / FuseFrames)));
                }
                return;
            }
            if (Age != FuseFrames) {
                return;
            }
            //起爆帧：演出（各端）+ 链式传播（owner）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.75f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero,
                    Color.White, Radius / 70f)?.Configure(26, BlastMain);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f),
                        Main.rand.NextBool() ? BlastBright : BlastMain,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, new Vector2(0f, -0.8f),
                    BlastDeep, 0.9f)?.Configure(30, 0.5f);
            }
            if (Projectile.IsOwnedByLocalPlayer() && Depth < 4f) {
                SentryGrid.PropagateChain(Projectile.Center, Projectile.owner,
                    (int)Depth + 1, Projectile.damage);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            if (Age < FuseFrames) {
                //引信红闪：脉冲频率随倒计时加快，identity 去同相
                float urgency = Age / FuseFrames;
                float pulse = 0.5f + 0.5f * MathF.Sin(Age * (0.5f + urgency * 0.9f) + Projectile.identity * 0.7f);
                Color warn = Color.Lerp(BlastMain, Color.Red, urgency) * (0.35f + 0.35f * pulse);
                warn.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, warn, 0f, glow.Size() * 0.5f,
                    0.5f + 0.25f * pulse, SpriteEffects.None, 0);
                return false;
            }
            //爆后冲击环
            float t = MathHelper.Clamp((Age - FuseFrames) / 14f, 0f, 1f);
            if (t < 1f) {
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                    Radius * (0.35f + 0.75f * t), 11f, BlastBright, BlastMain, BlastDeep,
                    (1f - t) * 0.9f, squish: 0.75f, timeSeed: Projectile.identity * 0.61f);
            }
            return false;
        }
    }
}
