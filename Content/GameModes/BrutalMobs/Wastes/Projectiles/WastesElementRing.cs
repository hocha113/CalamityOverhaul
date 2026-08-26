using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles
{
    /// <summary>
    /// 元素近身环。ai[0]=宿主NPC ai[1]=风味（0冰/1沙） ai[2]=半径。
    /// 可见环即判定环：环缘绘制与内圈判定读同一个 ai[2] 半径；
    /// 淡入 30 帧为预告期，完成前不施加减益。冰环滚动施加寒颤，沙环施加黑暗；
    /// 宿主死亡或失格后环随之消散（无伤害，纯控制领域）
    /// </summary>
    internal class WastesElementRing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>淡入帧数=预告期（公平契约 ≥30），判定在此之后</summary>
        private const int FadeInFrames = 30;
        private const int FadeOutFrames = 24;
        /// <summary>冰环寒颤时长（滚动施加）</summary>
        private const int BuffFramesIce = 40;
        /// <summary>沙环黑暗时长</summary>
        private const int BuffFramesSand = 30;
        /// <summary>环缘晶体数（绘制用）</summary>
        private const int RingTicks = 12;

        private int HostIndex => (int)Projectile.ai[0];
        private bool IsSand => Projectile.ai[1] == 1f;
        /// <summary>环半径：绘制与判定共用（可见环=判定环）</summary>
        private float Radius => Projectile.ai[2];
        private ref float FadeIn => ref Projectile.localAI[0];
        private ref float FadeOut => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯控制领域，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //宿主校验：类型必须与风味匹配，防槽位复用错挂
            NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[HostIndex] : null;
            int expectedType = IsSand ? NPCID.DesertDjinn : NPCID.IceElemental;
            bool hostValid = host != null && host.active && host.type == expectedType;

            if (hostValid && FadeOut == 0f) {
                Projectile.Center = host.Center;
                Projectile.timeLeft = 90;//宿主在则常驻
                if (FadeIn < FadeInFrames) {
                    FadeIn++;
                }
            }
            else {
                //宿主消失：进入消散
                FadeOut++;
                if (FadeOut >= FadeOutFrames) {
                    Projectile.Kill();
                    return;
                }
            }

            //判定：淡入（预告期）完成且未消散才施加；本机 AddBuff 原生同步
            if (FadeIn >= FadeInFrames && FadeOut == 0f && !Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead
                    && localPlayer.Distance(Projectile.Center) < Radius) {
                    localPlayer.AddBuff(IsSand ? BuffID.Darkness : BuffID.Chilled,
                        IsSand ? BuffFramesSand : BuffFramesIce);
                }
            }

            //环缘微尘（≤1 粒/帧，落在判定半径正上，强化环=判定的读法）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * Radius,
                    IsSand ? DustID.Sand : DustID.Frost, Vector2.Zero, 140, default, 0.9f);
                dust.noGravity = true;
                dust.velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.6f;
            }

            Lighting.AddLight(Projectile.Center,
                (IsSand ? new Vector3(0.3f, 0.24f, 0.1f) : new Vector3(0.12f, 0.22f, 0.34f)) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float visibility = MathHelper.Clamp(FadeIn / FadeInFrames, 0f, 1f)
                * MathHelper.Clamp(1f - FadeOut / FadeOutFrames, 0f, 1f);
            if (visibility <= 0.01f) {
                return false;
            }

            //环缘晶体贴图按风味取原版素材
            int vanillaId = IsSand ? ProjectileID.SandBallFalling : ProjectileID.IceBolt;
            Main.instance.LoadProjectile(vanillaId);
            Texture2D tex = TextureAssets.Projectile[vanillaId].Value;
            Vector2 orig = tex.Size() / 2f;
            float spin = Main.GlobalTimeWrappedHourly * (IsSand ? 0.9f : 0.6f);
            Color bodyTint = IsSand ? new Color(232, 202, 130) : new Color(198, 234, 255);

            //环缘晶体（实体层），半径与判定同源
            for (int i = 0; i < RingTicks; i++) {
                float ang = MathHelper.TwoPi * i / RingTicks + spin;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Radius - Main.screenPosition;
                Color tick = Color.Lerp(lightColor, bodyTint, 0.6f) * (0.85f * visibility);
                Main.EntitySpriteDraw(tex, pos, null, tick, ang + MathHelper.PiOver2, orig,
                    0.55f, SpriteEffects.None, 0);
            }

            //晶体间隙的加色闪点（敷料）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity);
            Color spark = (IsSand ? new Color(255, 226, 150, 0) : new Color(150, 220, 255, 0)) * (0.3f * visibility * pulse);
            for (int i = 0; i < RingTicks; i++) {
                float ang = MathHelper.TwoPi * (i + 0.5f) / RingTicks + spin;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Radius - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null, spark, 0f, glow.Size() / 2f, 0.16f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
