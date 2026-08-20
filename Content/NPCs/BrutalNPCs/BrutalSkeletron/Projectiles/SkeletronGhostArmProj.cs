using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>
    /// 幽灵追加臂：凭空凝聚的灵体手，四种役使方式<br/>
    /// ai[0]=模式 ai[1]=方位角/参数 ai[2]=起手延迟；命中箱=手掌<br/>
    /// 手掌走遮挡层（A&gt;0 实体，契约4），臂条带走预乘图元，掌焰走冷焰批
    /// </summary>
    internal class SkeletronGhostArmProj : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal enum ArmMode : int
        {
            /// <summary>环猎扑抓：环位凝聚→回抽→直线扑穿</summary>
            CircleLunge = 0,
            /// <summary>巷道扫掠：定向匀速划过战场</summary>
            LaneSweep = 1,
            /// <summary>临渊轮斩：绕头持位→穿心横贯</summary>
            MaelstromSlam = 2,
            /// <summary>亡礼环抱：死亡演出中缓缓伸向颅骨（无伤害）</summary>
            DeathCradle = 3,
        }

        internal const float LungeRingRadius = 470f;
        internal const float SlamRingRadius = 560f;
        internal const float CradleRadius = 430f;

        private ArmMode Mode => (ArmMode)(int)Projectile.ai[0];
        private ref float ParamAngle => ref Projectile.ai[1];
        private ref float Delay => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];
        private ref float Traveled => ref Projectile.localAI[1];

        /// <summary>扫掠模式缓存的巷道速度（由生成速度得来，各端一致）</summary>
        private Vector2 cachedSweepVel;
        private bool sweepCached;
        /// <summary>肩根朝向（平滑）</summary>
        private Vector2 shoulderDir;
        private bool launchedSoundDone;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        /// <summary>凝聚帧数</summary>
        private int CondenseFrames => Mode switch {
            ArmMode.CircleLunge => (int)MathF.Min(Delay, 26f),
            ArmMode.LaneSweep => (int)MathF.Min(ParamAngle, 26f),
            ArmMode.MaelstromSlam => (int)MathF.Min(Delay, 30f),
            _ => 34,
        };

        /// <summary>0~1 凝聚进度</summary>
        private float Grow => MathHelper.Clamp(Age / MathF.Max(CondenseFrames, 1f), 0f, 1f);

        /// <summary>0~1 末段消散进度（吃 timeLeft）</summary>
        private float Dissolve => MathHelper.Clamp(1f - Projectile.timeLeft / 16f, 0f, 1f);

        public override void AI() {
            //首帧初始化
            if (Age == 0f) {
                InitOnFirstTick();
            }
            Age++;

            switch (Mode) {
                case ArmMode.CircleLunge:
                    UpdateCircleLunge();
                    break;
                case ArmMode.LaneSweep:
                    UpdateLaneSweep();
                    break;
                case ArmMode.MaelstromSlam:
                    UpdateMaelstromSlam();
                    break;
                default:
                    UpdateDeathCradle();
                    break;
            }

            //起势音效：由速度阶跃在各端本地判定（巷道冻结期速度为真值，需排除）
            bool sweepFrozen = Mode == ArmMode.LaneSweep && Age < ParamAngle;
            if (!launchedSoundDone && !sweepFrozen && Projectile.velocity.Length() > 9f) {
                launchedSoundDone = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.85f, Pitch = -0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.75f }, Projectile.Center);
                }
            }

            //凝聚期灵质吸入
            if (!VaultUtils.isServer && Grow < 1f && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, (Projectile.Center - pos) * 0.12f,
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(14, 0f);
            }
            //高速期灵焰剥落
            if (!VaultUtils.isServer && !sweepFrozen && Projectile.velocity.Length() > 9f && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    -Projectile.velocity * 0.12f,
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.7f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * 0.5f * Grow);
        }

        private void InitOnFirstTick() {
            if (Mode == ArmMode.LaneSweep) {
                //巷道速度来自生成参数，冻结待发
                cachedSweepVel = Projectile.velocity;
                sweepCached = true;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = (int)(ParamAngle + Delay + 24f);
                shoulderDir = -cachedSweepVel.SafeNormalize(Vector2.UnitX);
            }
            else if (Mode == ArmMode.CircleLunge) {
                Projectile.timeLeft = (int)Delay + 62;
                shoulderDir = ParamAngle.ToRotationVector2();
            }
            else if (Mode == ArmMode.MaelstromSlam) {
                Projectile.timeLeft = (int)Delay + 84;
                shoulderDir = ParamAngle.ToRotationVector2();
            }
            else {
                shoulderDir = ParamAngle.ToRotationVector2();
                //亡礼臂寿命与其余模式同路：各端首帧确定性设定（生成包不含 timeLeft，生成后外改不会到达客户端）
                Projectile.timeLeft = States.SkeletronDeathState.DeathEnd - States.SkeletronDeathState.LamentEnd + 20;
            }

            if (Mode == ArmMode.DeathCradle) {
                Projectile.hostile = false;
                Projectile.damage = 0;
            }
        }

        #region 各模式

        private void UpdateCircleLunge() {
            Vector2 outward = ParamAngle.ToRotationVector2();

            if (Age < Delay) {
                Projectile.velocity = Vector2.Zero;
                //末8帧回抽蓄势
                float reel = MathHelper.Clamp((Age - (Delay - 8f)) / 8f, 0f, 1f);
                if (reel > 0f) {
                    Projectile.position += outward * MathF.Pow(reel, 3f) * 3.4f;
                }
                shoulderDir = Vector2.Lerp(shoulderDir, outward, 0.2f).SafeNormalize(outward);
                return;
            }

            //起扑：服务端锁定当前目标并广播
            if ((int)Age == (int)Delay && !VaultUtils.isClient) {
                int target = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                Vector2 aim = target >= 0
                    ? Main.player[target].Center + Main.player[target].velocity * 5f
                    : Projectile.Center - outward * 300f;
                Projectile.velocity = (aim - Projectile.Center).SafeNormalize(-outward)
                    * SkeletronDirector.GhostLungeSpeed(CWRRef.GetDeathMode() || CWRRef.GetBossRushActive());
                Projectile.netUpdate = true;
            }

            //扑穿后减速消散
            if (Age > Delay + 22f) {
                Projectile.velocity *= 0.88f;
            }
            shoulderDir = Vector2.Lerp(shoulderDir, -Projectile.velocity.SafeNormalize(outward), 0.25f).SafeNormalize(outward);
        }

        private void UpdateLaneSweep() {
            if (!sweepCached) {
                //生成包携带真实巷道速度，任意时刻同步/补收都正确
                cachedSweepVel = Projectile.velocity;
                sweepCached = true;
            }
            shoulderDir = -cachedSweepVel.SafeNormalize(Vector2.UnitX);

            if (Age < ParamAngle) {
                //巷道预告：速度保持真值（防联机补同步覆写为0），位置回退抵消位移
                Projectile.position -= Projectile.velocity;
                return;
            }
            if (Age >= ParamAngle + Delay) {
                Projectile.velocity *= 0.85f;
            }
        }

        private void UpdateMaelstromSlam() {
            NPC head = GetActiveHead();
            Vector2 outward = ParamAngle.ToRotationVector2();

            if (head == null) {
                //坛主失效，就地消散
                if (Projectile.timeLeft > 16) {
                    Projectile.timeLeft = 16;
                }
                Projectile.velocity *= 0.9f;
                return;
            }

            if (Age < Delay) {
                //持位绕坛，末8帧外撑蓄势
                Vector2 hold = head.Center + outward * SlamRingRadius;
                float reel = MathHelper.Clamp((Age - (Delay - 8f)) / 8f, 0f, 1f);
                hold += outward * MathF.Pow(reel, 3f) * 46f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, hold, 0.3f);
                Projectile.velocity = Vector2.Zero;
                shoulderDir = outward;
                return;
            }

            //穿心横贯（各端由同一几何确定）
            if ((int)Age == (int)Delay) {
                Projectile.velocity = (head.Center - Projectile.Center).SafeNormalize(-outward) * 30f;
                if (!VaultUtils.isClient) {
                    Projectile.netUpdate = true;
                }
            }
            Traveled += Projectile.velocity.Length();
            if (Traveled > SlamRingRadius * 2.15f) {
                Projectile.velocity *= 0.82f;
            }
            shoulderDir = Vector2.Lerp(shoulderDir, -Projectile.velocity.SafeNormalize(outward), 0.2f).SafeNormalize(outward);
        }

        private void UpdateDeathCradle() {
            NPC head = GetActiveHead();
            if (head == null) {
                if (Projectile.timeLeft > 16) {
                    Projectile.timeLeft = 16;
                }
                return;
            }

            //缓缓伸向颅骨，末段轻颤
            float reach = MathHelper.Clamp((Age - Delay) / 116f, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - reach, 3f);
            float sway = MathF.Sin(Age * 0.05f + ParamAngle * 3f) * 0.05f;
            float radius = MathHelper.Lerp(CradleRadius, 96f, ease);
            Vector2 dir = (ParamAngle + sway).ToRotationVector2();
            Projectile.Center = head.Center + dir * radius;
            if (reach > 0.9f) {
                Projectile.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
            }
            Projectile.velocity = Vector2.Zero;
            shoulderDir = dir;
        }

        private static NPC GetActiveHead() {
            int idx = SkeletronHeadAI.ActiveHeadIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC head = Main.npc[idx];
            return head.active && head.type == NPCID.SkeletronHead ? head : null;
        }

        #endregion

        /// <summary>只有成形且真正处于挥击段的手掌伤人</summary>
        public override bool? CanDamage() {
            if (Mode == ArmMode.DeathCradle) {
                return false;
            }
            //巷道模式冻结期速度保持真值，须按起扫拍另行判定
            if (Mode == ArmMode.LaneSweep && Age < ParamAngle) {
                return false;
            }
            return Grow >= 1f && Projectile.velocity.Length() > 7f ? null : false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(Projectile.Center + Main.rand.NextVector2Circular(22f, 22f),
                    Main.rand.NextVector2Circular(2.8f, 2.8f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        #region 绘制

        /// <summary>臂身条带（肩根→腕口）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            float opacity = 1f;
            float grow = Grow;
            float dissolve = Dissolve;
            if (grow <= 0.03f || dissolve >= 0.97f) {
                return;
            }

            //肩根探出方向，弯曲相位给每条臂独立姿态
            float armLen = Mode == ArmMode.DeathCradle ? 200f : 168f;
            Vector2 hand = Projectile.Center - shoulderDir * 6f;
            Vector2 shoulder = Projectile.Center + shoulderDir * armLen;
            float curvature = MathF.Sin(Projectile.whoAmI * 2.39996f) * 46f
                + MathF.Sin(Age * 0.045f + Projectile.whoAmI) * 14f;

            SkeletronRenderHelper.DrawGhostArmStrip(shoulder, hand, curvature, 46f,
                grow, dissolve, opacity, Projectile.whoAmI * 0.137f % 1f);
        }

        /// <summary>手掌实体与掌焰（遮挡层批：A&gt;0 压死身后弹幕，掌焰入冷焰队列延后画）</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            float grow = Grow;
            float fade = (1f - Dissolve) * grow;
            if (fade <= 0.03f) {
                return;
            }

            //指尖朝行进/内侧
            Vector2 inward = -shoulderDir;
            float rotation = inward.ToRotation() + MathHelper.PiOver2;
            float scale = (Mode == ArmMode.MaelstromSlam ? 1.35f : 1.1f) * (0.7f + 0.3f * grow);

            //掌底灵鞘：宽扁冷焰自腕口向指尖舔出（顶点批）
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 2.3f);
            SkeletronFlameRender.Push(Projectile.Center + shoulderDir * 26f * scale, inward.ToRotation(),
                new Vector2(64f, 84f) * scale * pulse,
                0.35f, Projectile.whoAmI * 0.137f, 0.2f,
                0.55f * fade);

            SkeletronRenderHelper.DrawGhostHandSprite(spriteBatch, Projectile.Center, rotation, scale, fade,
                shoulderDir.X >= 0f ? 1 : -1);
        }

        #endregion
    }
}
