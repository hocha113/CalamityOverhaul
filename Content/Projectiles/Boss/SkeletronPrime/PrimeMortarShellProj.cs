using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 迫击炮重型榴弹：真实抛物线飞向预告落点，到点（或中途撞地）引爆为
    /// 恰好覆盖预警环的火球区域伤害——蓄力预警的兑现是大爆炸，而非普攻火箭。
    /// <br/>ai[0] = 落点 X
    /// <br/>ai[1] = 落点 Y
    /// <br/>ai[2] = 飞行帧数（生成侧据此反解初速，弹道学上必中落点）
    /// </summary>
    internal class PrimeMortarShellProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;

        /// <summary>重力 px/帧²（与初速反解公式共享）</summary>
        internal static float Gravity => 0.3f;
        /// <summary>标准飞行帧数（固定值使预警环时长可在发射前预解）</summary>
        internal static int FlightFrames => 55;
        /// <summary>爆炸判定直径（= 预警环直径，承诺即兑现，不多不少）</summary>
        internal static int BlastDiameter => 620;
        internal static int BlastFrames => 12;

        private ref float Timer => ref Projectile.localAI[0];
        private bool Detonated {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>
        /// 反解初速：恰好在 flightFrames 帧后落在 impact。
        /// 离散积分（先位移后加重力）：pos(T) = pos0 + v0·T + g·T(T-1)/2
        /// </summary>
        internal static Vector2 SolveLaunchVelocity(Vector2 spawn, Vector2 impact, int flightFrames) {
            float dropTerm = Gravity * flightFrames * (flightFrames - 1) / 2f;
            return (impact - spawn - new Vector2(0f, dropTerm)) / flightFrames;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;//出膛宽限后开启
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FlightFrames + BlastFrames + 60;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Detonated) {
                Timer++;
                if (Timer >= BlastFrames) {
                    Projectile.Kill();
                }
                return;
            }

            Timer++;
            Projectile.tileCollide = Timer > 10;
            Projectile.velocity.Y += Gravity;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            //重弹观感：缓慢放大 + 浓尾焰
            Projectile.scale = MathHelper.Clamp(Projectile.scale + 0.008f, 1f, 1.4f);

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.4f, 0.1f));

            if (!VaultUtils.isServer) {
                if (PRTLoader.NumberUsablePRT() > 10) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, -Projectile.velocity * 0.3f,
                        Color.DarkRed, 1.7f)?.Configure(false, 8);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.2f,
                        Color.LightGoldenrodYellow, 1.1f)?.Configure(false, 12);
                }
                Dust smoke = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity, DustID.Smoke,
                    -Projectile.velocity * 0.15f, 120, Color.Gray, 1.4f);
                smoke.fadeIn = 0.6f;
            }

            int flight = (int)Projectile.ai[2] > 0 ? (int)Projectile.ai[2] : FlightFrames;
            if (Timer >= flight) {
                Detonate();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!Detonated) {
                Detonate();
            }
            return false;
        }

        /// <summary>引爆：判定箱扩张到预警环大小，火球 + 冲击环 + 震屏一次性兑现</summary>
        private void Detonate() {
            Detonated = true;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.Resize(BlastDiameter, BlastDiameter);
            Projectile.netUpdate = true;

            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.1f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.2f }, Projectile.Center);
            PrimeScreenEffects.PushShockRing(Projectile.Center, 0.8f, 380f);
            PrimeDeathPerformancePlayer.RequestShake(7f, 10);

            //火球闪光 + 环状火尘 + 放射火花 + 翻腾烟柱 + 残骸
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, Color.OrangeRed, 3f)?.Configure(16);
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 12f, 24, 24,
                    DustID.Torch, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.8f, 3f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f) + Vector2.UnitY * -2f;
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 10f, 20, 20,
                    DustID.Smoke, vel.X, vel.Y, 130, Color.DarkRed, 1.6f);
                dust.fadeIn = 1f;
            }
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi / 16f * i;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, ang.ToRotationVector2() * Main.rand.NextFloat(6f, 11f),
                    Color.Gold, Main.rand.NextFloat(1.3f, 1.9f))?.Configure(false, 18);
            }
            for (int i = 0; i < 3; i++) {
                Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center,
                    Main.rand.NextVector2Circular(4f, 4f), Main.rand.Next(61, 64), Main.rand.NextFloat(0.8f, 1.2f));
            }
        }

        //爆炸判定按圆形裁决，恰好兑现环形预警
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Detonated) {
                return null;
            }
            float hitRadius = BlastDiameter / 2f + System.Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return Vector2.DistanceSquared(Projectile.Center, targetHitbox.Center.ToVector2()) <= hitRadius * hitRadius;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Detonated) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                new Color(255, 120, 30, 0) * 0.8f, 0f, glow.Size() / 2f, 0.5f * Projectile.scale, SpriteEffects.None, 0);

            Texture2D tex = TextureAssets.Projectile[ProjectileID.RocketSkeleton].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
