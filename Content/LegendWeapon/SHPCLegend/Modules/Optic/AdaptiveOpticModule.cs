using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 自适应瞄具：预测火控系统。持续解析光标附近目标的运动轨迹，
    /// 在其「预测拦截点」投射全息标线；新发射的光束自动偏转打提前量，
    /// 命中正在移动的目标时火控确认，追加 25% 适应打击
    /// （世纪之花礼物 —— 猎杀移动猎物的丛林之眼）
    /// </summary>
    internal sealed class AdaptiveOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //自适应莱姆绿
        public override Color TintColor => new(170, 255, 120);

        /// <summary>光束基准速度（与 CyberTraceBeamProj.Speed 一致），用于解算拦截点</summary>
        private const float BeamBaseSpeed = 14f;
        private const float LockRange = 1000f;
        private const float MovingThreshold = 2f;

        /// <summary>已完成偏转的光束，OnBeamKill 时清理</summary>
        private readonly HashSet<int> steeredBeams = [];

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.25f;
            ctx.CritAdd += 4;
        }

        /// <summary>
        /// 解算拦截点：用两轮迭代逼近"光束抵达时目标所在位置"
        /// </summary>
        internal static Vector2 PredictIntercept(Vector2 from, NPC target, float beamSpeed) {
            Vector2 predicted = target.Center;
            for (int i = 0; i < 2; i++) {
                float flightTime = Vector2.Distance(from, predicted) / MathF.Max(beamSpeed, 1f);
                predicted = target.Center + target.velocity * flightTime;
            }
            return predicted;
        }

        /// <summary>寻找光标附近的锁定目标</summary>
        internal static NPC FindLockTarget(Player player) {
            NPC target = Main.MouseWorld.FindClosestNPC(400f, false, true);
            target ??= player.Center.FindClosestNPC(LockRange, false, true);
            if (target != null && Vector2.DistanceSquared(target.Center, player.Center) > LockRange * LockRange) {
                return null;
            }
            return target;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if (!steeredBeams.Add(beam.Projectile.whoAmI)) return; //仅在首帧偏转一次

            Player owner = Main.player[beam.Projectile.owner];
            NPC target = FindLockTarget(owner);
            if (target == null) return;

            Vector2 intercept = PredictIntercept(beam.Projectile.Center, target,
                BeamBaseSpeed * MathF.Max(beam.SpeedMul, 0.1f));
            Vector2 desired = intercept - beam.Projectile.Center;
            //偏转角限制在 ±50°，防止光束向后回折
            float diff = MathHelper.WrapAngle(desired.ToRotation() - beam.FlightDirection.ToRotation());
            if (Math.Abs(diff) > MathHelper.ToRadians(50f)) return;
            beam.SetFlightDirection(desired);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            steeredBeams.Remove(beam.Projectile.whoAmI);
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (target.velocity.Length() < MovingThreshold) return;
            //火控确认：对移动中的目标追加适应打击
            int extra = Math.Max((int)(damageDone * 0.25f), 1);
            target.SimpleStrikeNPC(extra, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                        Main.rand.NextVector2CircularEdge(4.5f, 4.5f),
                        new Color(190, 255, 130), Main.rand.NextFloat(0.6f, 1.1f))
                        .Configure(new Color(70, 160, 40), Main.rand.Next(14, 24));
                }
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (player.whoAmI != Main.myPlayer) return;
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            //维持唯一的全息标线弹幕
            int markerType = ModContent.ProjectileType<SHPCAdaptiveReticleProj>();
            if (player.ownedProjectileCounts[markerType] <= 0) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, markerType, 0, 0f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 自适应全息标线：悬浮在锁定目标预测拦截点上的旋转括弧准星。
    /// 纯视觉弹幕，无伤害；目标丢失或玩家收起武器时自动淡出消亡
    /// </summary>
    internal sealed class SHPCAdaptiveReticleProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color ReticleMain = new(170, 255, 120);
        private static readonly Color ReticleAccent = new(255, 130, 200);

        private float fadeAlpha;
        private int lockedNpcId = -1;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;

            NPC target = AdaptiveOpticModule.FindLockTarget(owner);
            if (target == null) {
                lockedNpcId = -1;
                fadeAlpha = MathF.Max(fadeAlpha - 0.08f, 0f);
                return;
            }

            //新锁定时的捕获提示音
            if (lockedNpcId != target.whoAmI && Main.netMode != NetmodeID.Server && fadeAlpha < 0.3f) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.25f, Pitch = 0.8f }, target.Center);
            }
            lockedNpcId = target.whoAmI;
            fadeAlpha = MathF.Min(fadeAlpha + 0.1f, 1f);

            Vector2 intercept = AdaptiveOpticModule.PredictIntercept(owner.Center, target, 14f);
            //标线平滑追踪拦截点，目标急转时带一点惯性滞后
            Projectile.Center = Vector2.Lerp(Projectile.Center, intercept, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null) return;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float spin = (float)Main.timeForVisualEffects * 0.05f;
            float breathe = 26f + 4f * MathF.Sin((float)Main.timeForVisualEffects * 0.12f);

            //四角旋转括弧：每个括弧由两条短亮线构成
            for (int i = 0; i < 4; i++) {
                float ang = spin + MathHelper.PiOver2 * i + MathHelper.PiOver4;
                Vector2 cornerPos = drawPos + ang.ToRotationVector2() * breathe;
                Vector2 inward = (drawPos - cornerPos).SafeNormalize(Vector2.Zero);
                float armAng1 = inward.ToRotation() + MathHelper.PiOver4;
                float armAng2 = inward.ToRotation() - MathHelper.PiOver4;
                spriteBatch.Draw(white, cornerPos, null, ReticleMain * fadeAlpha * 0.9f,
                    armAng1, new Vector2(0.5f, 0.5f), new Vector2(11f, 2f), SpriteEffects.None, 0f);
                spriteBatch.Draw(white, cornerPos, null, ReticleMain * fadeAlpha * 0.9f,
                    armAng2, new Vector2(0.5f, 0.5f), new Vector2(11f, 2f), SpriteEffects.None, 0f);
            }
            //中心预测点：粉色小点 + 柔光
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, ReticleAccent * fadeAlpha * 0.6f, 0f,
                    glow.Size() * 0.5f, 0.32f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(white, drawPos, null, ReticleAccent * fadeAlpha,
                spin * 2f, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), SpriteEffects.None, 0f);
        }
    }
}
