using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 蛇发凝视通道（美杜莎头颅全接管）。朝光标 70° 扇区（半径 360px）持续凝视：
    /// 扇内敌每 0.25s 受创并叠石纹，满 4 层触发 0.5s 小石化（Boss 豁免硬控）；
    /// 白热带扇区收窄到 40°、伤害经白热乘区 ×1.4（聚焦凝视，窄扇要求瞄准）。<br/>
    /// 扇区判定与扇区绘制读同一组几何量（瞄准角 = 同步 velocity、
    /// 扇宽 = 热段 ai[1] 驱动的本地平滑量、半径 × 展开进度），判定绘制同源
    /// </summary>
    internal class GsGazeBeamProj : GsConduitHeldProj
    {
        internal const float GazeRadius = 360f;
        private const float WideHalfArc = 35f * (MathHelper.Pi / 180f);
        private const float FocusHalfArc = 20f * (MathHelper.Pi / 180f);
        private const int GrowTicks = 8;

        protected override int BoundItemID => ItemID.MedusaHead;
        protected override float ManaPerSecond => 6f;
        protected override float HeatPerTick => 1.0f;
        protected override int HitCooldown => 15;
        protected override float TickDamageCoef => 0.55f;
        protected override bool UseChannelFlag => true;

        /// <summary>蛇首直立悬在扇心（原版由宿主 535 自绘，0/π 直立姿态）</summary>
        protected override GsConduitBodyPose BodyPose => GsConduitBodyPose.MuzzleUpright;

        /// <summary>扇半宽的本地平滑量（从同步热段确定性推进，判定端只有 owner）</summary>
        private float halfArcCur = WideHalfArc;

        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[1] / GrowTicks, 0f, 1f);

        /// <summary>当前有效扇半宽（含塌缩收窄）</summary>
        private float EffHalfArc(float collapse01) => halfArcCur * (1f - 0.9f * collapse01);

        private float EffRadius => GazeRadius * VaultUtils.EaseOutCubic(GrowProgress);

        private float lastCollapse01;

        protected override void ChannelAI(float collapse01) {
            lastCollapse01 = collapse01;
            float target = HeatStageSync >= 1 ? FocusHalfArc : WideHalfArc;
            halfArcCur = MathHelper.Lerp(halfArcCur, target, 0.14f);

            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.6f, Pitch = 0.45f, MaxInstances = 2 }, Projectile.Center);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (Projectile.localAI[1] % 55 == 0 && collapse01 <= 0f) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
            }

            //扇内石尘与端弧石屑（纯装饰，端间随机发散可接受）
            float half = EffHalfArc(collapse01);
            float aimAngle = AimUnit.ToRotation();
            Lighting.AddLight(Projectile.Center + AimUnit * 60f, GsConduitVFX.StoneMain.ToVector3() * 0.4f);
            if (Main.GameUpdateCount % 2 == 0 && collapse01 <= 0f) {
                float ang = aimAngle + Main.rand.NextFloat(-half, half);
                float dist = Main.rand.NextFloat(0.35f, 0.98f) * EffRadius;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * dist,
                    ang.ToRotationVector2() * 0.6f, GsConduitVFX.StoneMain, Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        protected override bool? DamageGate() => GrowProgress >= 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 origin = Projectile.Center;
            float reach = EffRadius;
            if (!GsConduitVFX.CircleVsRect(origin, reach, targetHitbox)) {
                return false;
            }
            //贴脸兜底：扇心极近处不吃角度差
            if (GsConduitVFX.CircleVsRect(origin, 42f, targetHitbox)) {
                return true;
            }
            float half = EffHalfArc(lastCollapse01);
            float aimAngle = AimUnit.ToRotation();
            Span<Vector2> points = [
                targetHitbox.Center.ToVector2(),
                targetHitbox.TopLeft(),
                new Vector2(targetHitbox.Right, targetHitbox.Top),
                new Vector2(targetHitbox.Left, targetHitbox.Bottom),
                new Vector2(targetHitbox.Right, targetHitbox.Bottom),
            ];
            foreach (Vector2 p in points) {
                float diff = MathHelper.WrapAngle((p - origin).ToRotation() - aimAngle);
                if (MathF.Abs(diff) <= half) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //石纹叠层：满 4 层触发小石化（只在攻击方端执行，buff 走原生同步）
            GsConduitVFX.ApplyPetrify(target, 1);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(2f, 1.5f) - new Vector2(0f, 1f),
                        GsConduitVFX.StoneMain, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //先画蛇首本体，凝视扇线与瞳孔星芒压在其上（瞳孔即蛇首的发光双目）
            DrawWeaponBody();
            float reach = EffRadius;
            float half = EffHalfArc(lastCollapse01);
            if (reach < 20f || half < 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = Projectile.Center;
            float aimAngle = AimUnit.ToRotation();
            bool whiteHot = HeatStageSync >= 1;
            float alpha = (0.55f + (whiteHot ? 0.25f : 0f)) * (1f - lastCollapse01 * 0.6f);

            //扇形线束：边界最亮、中央白芯、内里暗线，与判定同一组角度/半径
            int rays = Math.Max(5, (int)(half * 2f / 0.11f) | 1);
            for (int i = 0; i < rays; i++) {
                float t = i / (float)(rays - 1);
                float ang = aimAngle + MathHelper.Lerp(-half, half, t);
                bool edge = i == 0 || i == rays - 1;
                bool center = i == rays / 2;
                float w = edge ? 9f : center ? 11f : 5f;
                float a = (edge ? 0.85f : center ? 1f : 0.32f) * alpha;
                //identity 定相闪烁，绘制路径零随机
                a *= 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity * 0.7f + i * 1.9f);
                GsConduitVFX.DrawBeam(sb, origin, ang, reach, w,
                    GsConduitVFX.StoneMain, center ? Color.White : GsConduitVFX.StoneBright, a);
            }

            //端弧石点：沿半径末端等距列点，收口扇形的物理边界
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int arcDots = 8;
            for (int i = 0; i < arcDots; i++) {
                float ang = aimAngle + MathHelper.Lerp(-half, half, i / (float)(arcDots - 1));
                Vector2 pos = origin + ang.ToRotationVector2() * reach - Main.screenPosition;
                sb.Draw(glow, pos, null, GsConduitVFX.StoneBright with { A = 0 } * (0.5f * alpha),
                    0f, glow.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
            }

            //凝视之瞳：枪口星芒缓旋
            Texture2D star = CWRAsset.StarTexture.Value;
            Color pupil = (whiteHot ? Color.White : GsConduitVFX.StoneBright) with { A = 0 };
            sb.Draw(star, origin - Main.screenPosition, null, pupil * (0.8f * alpha),
                Main.GlobalTimeWrappedHourly * 1.6f, star.Size() / 2f, 0.14f, SpriteEffects.None, 0f);
            sb.Draw(glow, origin - Main.screenPosition, null,
                GsConduitVFX.StoneMain with { A = 0 } * (0.7f * alpha), 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
