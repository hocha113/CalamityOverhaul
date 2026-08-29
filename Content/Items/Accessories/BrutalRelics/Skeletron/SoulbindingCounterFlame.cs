using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 咒焰反掷：魂魄格挡吞掉敌方弹幕后化出的幽青焰球，扑向附近最近的敌人。<br/>
    /// ai[0]=目标NPC下标 ai[1]=目标类型（槽位复用校验），目标身份随生成参数进生成包。
    /// 由格挡判定端（owner 客户端）生成，友方弹幕走原版同步；
    /// 弹体全部用既有鬼火 PRT 与冷焰语汇拼装，不新增贴图
    /// </summary>
    internal class SoulbindingCounterFlame : ModProjectile, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>反掷基伤（挂 Generic 加成，生成时 ApplyTo）</summary>
        public const int BaseDamage = 60;
        /// <summary>索敌半径（px），与格挡点为圆心</summary>
        public const float SeekRange = 600f;
        /// <summary>巡航速度上限 px/t</summary>
        private const float MaxSpeed = 17f;
        /// <summary>每帧最大转向（弧度）</summary>
        private const float SteerRate = 0.14f;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float TargetType => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Generic;
        }

        private bool TryGetTarget(out NPC target) {
            target = null;
            int idx = (int)TargetIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[idx];
            //下标会被复用：类型 + 可追猎双重校验
            if (!npc.active || npc.type != (int)TargetType || !npc.CanBeChasedBy()) {
                return false;
            }
            target = npc;
            return true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                }
            }

            //受限转向 + 匀加速追踪；目标失效后按惯性直飞到寿终
            if (TryGetTarget(out NPC target)) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 cur = Projectile.velocity.SafeNormalize(want);
                float turn = MathHelper.Clamp(MathF.Sign(cur.X * want.Y - cur.Y * want.X)
                    * MathF.Acos(MathHelper.Clamp(Vector2.Dot(cur, want), -1f, 1f)), -SteerRate, SteerRate);
                float speed = MathHelper.Clamp(Projectile.velocity.Length() + 0.9f, 5f, MaxSpeed);
                Projectile.velocity = cur.RotatedBy(turn) * speed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //飞行余烬：速度反向剥落的鬼火拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool() ? SkeletronRenderHelper.GhostCyan : SkeletronRenderHelper.GhostDeep,
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(14, 24));
            }
            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * 0.35f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            SoulbindingArmRender.AddPop(Projectile.Center, 0.8f);
        }

        public override void OnKill(int timeLeft) {
            //余韵：残焰比弹体活得久
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(2.4f, 2.4f) - new Vector2(0f, 0.8f),
                    Main.rand.NextBool() ? SkeletronRenderHelper.GhostCyan : SkeletronRenderHelper.GhostDeep,
                    Main.rand.NextFloat(0.9f, 1.5f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>焰球本体：外鞘 + 骨白内芯双层冷焰，焰轴顺速度方向（运动即拉丝）</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            float rot = Projectile.velocity.ToRotation();
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0.3f, 1f);
            float seed = Projectile.identity * 0.137f % 1f;
            //外鞘：拉长的幽青焰体，尾根略后置
            SkeletronFlameRender.Push(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 8f,
                rot, new Vector2(15f, 30f + 16f * speedT), 0.55f, seed, 0.35f, 0.9f);
            //内芯：骨白高热小焰
            SkeletronFlameRender.Push(Projectile.Center, rot,
                new Vector2(7f, 15f + 6f * speedT), 0.95f, seed + 0.41f, 0.05f, 0.85f);
        }
    }
}
