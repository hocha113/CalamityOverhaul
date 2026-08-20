using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>
    /// 旋骨罗盘：诅咒颅骨为毂、八向枯骨为辐的旋转骨轮，沿直线碾过战场<br/>
    /// ai[0]=滚进角（出生即锁死，全程不追踪），ai[1]=滚速，ai[2]=辐条初相；轨迹各端确定性推演<br/>
    /// 缺口（契约3）：SpokeGapSlots 个相邻辐条槽永空（90°豁口随轮旋转），碰撞与绘制读同一常量；
    /// 凝形期无伤害且沿滚进线画灵息预警带（telegraph 即实体）
    /// </summary>
    internal class SkeletronBoneWheel : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Skull;

        /// <summary>凝形帧数（无伤害的成形读秒）</summary>
        internal const int WindupFrames = 36;
        /// <summary>辐条槽总数</summary>
        private const int SpokeCount = 8;
        /// <summary>永空的相邻辐条槽数（缺口=90°扇区，穿轮窗口）</summary>
        private const int SpokeGapSlots = 2;
        /// <summary>辐条外缘半径</summary>
        private const float RimRadius = 112f;
        /// <summary>毂心半径（颅骨本体咬合圈）</summary>
        private const float HubRadius = 26f;
        /// <summary>辐条自旋角速度（弧度/帧）</summary>
        private const float SpinRate = 0.045f;

        private ref float RollAngle => ref Projectile.ai[0];
        private ref float RollSpeed => ref Projectile.ai[1];
        private ref float SpokeSeed => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        private bool Forming => Age < WindupFrames;

        /// <summary>当前辐条相位（确定性：出生参数+寿命推演）</summary>
        private float SpokePhase => SpokeSeed + Age * SpinRate;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 3;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupFrames + 190;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;

            if (Forming) {
                //凝形：原地成轮，辐条自散乱快旋收拢为骨轮
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, Age / WindupFrames);
                if ((int)Age == 2 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageSummonSkeleton with { Volume = 0.8f, Pitch = -0.35f }, Projectile.Center);
                }
            }
            else {
                //滚进：直线碾场，角度与速度出生即定，绝不转向
                Projectile.velocity = RollAngle.ToRotationVector2() * RollSpeed;
                Projectile.alpha = 0;
                if ((int)Age == WindupFrames && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.85f, Pitch = -0.5f }, Projectile.Center);
                    SkeletronScreenEffects.PushShockRing(Projectile.Center, 0.5f);
                }
            }

            //毂心颅骨朝滚进方向
            Projectile.rotation = RollAngle + MathHelper.PiOver2;

            //三帧循环
            if (++Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //轮缘幽火剥落
            if (!VaultUtils.isServer && !Forming && Main.rand.NextBool(3)) {
                float a = SpokePhase + Main.rand.Next(SpokeCount - SpokeGapSlots) * MathHelper.TwoPi / SpokeCount;
                Vector2 rim = Projectile.Center + a.ToRotationVector2() * RimRadius;
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(rim,
                    a.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.6f,
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * 0.6f);

            //远离战场自清理
            if (!VaultUtils.isClient && Age > WindupFrames + 40f) {
                Player near = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (Projectile.Center.Distance(near.Center) > 2400f) {
                    Projectile.Kill();
                }
            }
        }

        /// <summary>凝形期不咬人（成形本身就是 telegraph）</summary>
        public override bool? CanDamage() => Forming ? false : (bool?)null;

        /// <summary>碰撞与绘制共用一套辐条几何：豁口扇区真实可穿</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //毂心颅骨
            if (targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(HubRadius * 2f)))) {
                return true;
            }
            float _ = 0f;
            for (int slot = 0; slot < SpokeCount; slot++) {
                if (slot >= SpokeCount - SpokeGapSlots) {
                    continue;   //缺口槽：永不参与碰撞
                }
                Vector2 dir = (SpokePhase + slot * MathHelper.TwoPi / SpokeCount).ToRotationVector2();
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center + dir * HubRadius, Projectile.Center + dir * RimRadius, 16f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(RimRadius * 0.7f, RimRadius * 0.7f),
                    Main.rand.NextVector2Circular(2.4f, 2.4f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.7f))?.Configure(Main.rand.Next(16, 28));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = 1f - Projectile.alpha / 255f;
            float form = MathHelper.Clamp(Age / WindupFrames, 0f, 1f);

            //凝形期：沿滚进线的灵息预警带（读秒随成形增强）
            if (Age < WindupFrames + 10f) {
                float strength = Forming ? form : 1f - (Age - WindupFrames) / 10f;
                SkeletronRenderHelper.DrawDashTelegraph(Main.spriteBatch, Projectile.Center, RollAngle, strength * 0.85f);
            }

            DrawSpokes(form, opacity);
            DrawHubSkull(opacity);
            return false;
        }

        /// <summary>八向骨辐：凝形期自外围收拢，滚动期带同材质角度残影</summary>
        private void DrawSpokes(float form, float opacity) {
            Main.instance.LoadProjectile(ProjectileID.Bone);
            Texture2D bone = TextureAssets.Projectile[ProjectileID.Bone].Value;
            Vector2 orig = bone.Size() / 2f;
            //凝形：辐条从远处收拢+快旋衰减到工作转速
            float gather = MathHelper.Lerp(1.8f, 1f, MathF.Pow(form, 0.6f));
            float phase = SpokePhase + (1f - form) * 2.6f;

            for (int slot = 0; slot < SpokeCount; slot++) {
                if (slot >= SpokeCount - SpokeGapSlots) {
                    continue;   //缺口槽：与碰撞同源，绝不虚画
                }
                float ang = phase + slot * MathHelper.TwoPi / SpokeCount;
                Vector2 dir = ang.ToRotationVector2();
                for (int k = 0; k < 3; k++) {
                    float r = MathHelper.Lerp(HubRadius + 16f, RimRadius - 8f, k / 2f) * gather;
                    Vector2 pos = Projectile.Center + dir * r - Main.screenPosition;
                    float roll = (slot * 3 + k) % 2 == 0 ? 0.4f : -0.32f;
                    //角度残影（同材质 A=0 加色，拖行角 0.11 弧度）
                    Vector2 ghostPos = Projectile.Center + (ang - 0.11f).ToRotationVector2() * r - Main.screenPosition;
                    Main.EntitySpriteDraw(bone, ghostPos, null,
                        SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.45f * opacity),
                        ang - 0.11f + MathHelper.PiOver2 + roll, orig, 0.92f, SpriteEffects.None, 0);
                    //骨辐本体（A>0 实体遮挡）
                    Main.EntitySpriteDraw(bone, pos, null,
                        Color.Lerp(SkeletronRenderHelper.BoneShadow, SkeletronRenderHelper.BonePale, 0.62f) * opacity,
                        ang + MathHelper.PiOver2 + roll, orig, 0.96f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>毂心诅咒颅骨：三层幽灵体（外咒紫/主幽青/白核）</summary>
        private void DrawHubSkull(float opacity) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.GetRectangle(Projectile.frame, Main.projFrames[Type]);
            Vector2 orig = rect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.CurseViolet * (0.8f * opacity),
                Projectile.rotation, orig, Projectile.scale * 1.2f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.GhostCyan * (0.85f * opacity),
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, new Color(230, 255, 250, 0) * (0.6f * opacity),
                Projectile.rotation, orig, Projectile.scale * 0.82f, SpriteEffects.None, 0);
        }
    }
}
