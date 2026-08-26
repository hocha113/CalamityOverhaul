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
    /// 毒爆孢囊：黄蜂毒液共振的结算体。目标上方垂落 24 帧后爆裂（伤害窗 4 帧，半径 70），
    /// 之后转为毒雾残留：不再伤害，owner 端每 30 帧给圈内敌人补挂原版中毒。
    /// 相位完全由固定时间线驱动（timeLeft 各端同初值本地递减），零额外同步
    /// </summary>
    internal class GsVenomPodProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color VenomGreen = new(148, 216, 68);
        private static readonly Color VenomDeep = new(72, 118, 34);
        private static readonly Color VenomPale = new(206, 244, 150);

        private const int FallFrames = 24;
        private const int BurstFrames = 4;
        private const int MistFrames = 90;
        private const int TotalFrames = FallFrames + BurstFrames + MistFrames;
        private const float BurstRadius = 70f;
        private const float MistRadius = 80f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InFall => Elapsed < FallFrames;

        private bool InBurst => Elapsed >= FallFrames && Elapsed < FallFrames + BurstFrames;

        private float Seed => Projectile.identity * 0.6421f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (InFall) {
                //垂落相：缓降到位
                Projectile.velocity = new Vector2(0f,
                    3.4f * (1f - Elapsed / (float)FallFrames) + 0.6f);
                if (!VaultUtils.isServer && Elapsed % 3 == 0) {
                    PRTLoader.NewParticle<PRT_ToxicBubble>(
                        Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                        -Projectile.velocity * 0.15f, VenomGreen,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 16));
                }
                return;
            }
            Projectile.velocity = Vector2.Zero;

            //爆裂帧：一次性演出
            if (Elapsed == FallFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = 0.4f },
                    Projectile.Center);
                for (int i = 0; i < 9; i++) {
                    PRTLoader.NewParticle<PRT_ToxicBubble>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                        Main.rand.NextBool() ? VenomGreen : VenomPale,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                }
            }

            //毒雾相：owner 端每 30 帧给圈内敌人补挂中毒（AddBuff 骑原版 buff 同步）
            if (!InBurst && Projectile.IsOwnedByLocalPlayer() && Elapsed % 30 == 0) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.CanBeChasedBy() && npc.Center.Distance(Projectile.Center) <= MistRadius) {
                        npc.AddBuff(BuffID.Poisoned, 150);
                    }
                }
            }
            if (!VaultUtils.isServer && !InBurst && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_ToxicMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(MistRadius * 0.7f, MistRadius * 0.5f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.1f, 0.4f)),
                    VenomDeep, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
                Lighting.AddLight(Projectile.Center, VenomGreen.ToVector3() * 0.1f);
            }
        }

        /// <summary>伤害窗只在爆裂 4 帧</summary>
        public override bool? CanDamage() => InBurst ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= BurstRadius;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 240);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            if (InFall) {
                //孢囊本体：饱胀的毒囊，落速拉伸
                float bulge = 1f + 0.12f * (float)Math.Sin(Elapsed * 0.55f + Seed);
                Main.EntitySpriteDraw(soft, pos, null, VenomDeep * 0.85f, 0f,
                    soft.Size() / 2f, new Vector2(0.2f * bulge, 0.26f / bulge),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(soft, pos, null, VenomGreen * 0.6f, 0f,
                    soft.Size() / 2f, new Vector2(0.13f * bulge, 0.18f / bulge),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, (VenomPale with { A = 0 }) * 0.5f,
                    0f, glow.Size() / 2f, 0.24f, SpriteEffects.None, 0);
                return false;
            }
            //爆裂闪 + 毒雾残留幕
            int sinceBurst = Elapsed - FallFrames;
            float flash = MathHelper.Clamp(1f - sinceBurst / 8f, 0f, 1f);
            if (flash > 0f) {
                Main.EntitySpriteDraw(glow, pos, null, (VenomPale with { A = 0 }) * (0.8f * flash),
                    0f, glow.Size() / 2f, BurstRadius / (glow.Width * 0.5f) * 1.3f,
                    SpriteEffects.None, 0);
            }
            float mistFade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f)
                * MathHelper.Clamp(sinceBurst / 10f, 0f, 1f);
            float drift = 0.06f * (float)Math.Sin(Elapsed * 0.07f + Seed);
            Main.EntitySpriteDraw(soft, pos, null, VenomDeep * (0.5f * mistFade),
                drift, soft.Size() / 2f,
                new Vector2(MistRadius / (soft.Width * 0.5f), MistRadius * 0.7f / (soft.Width * 0.5f)),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
