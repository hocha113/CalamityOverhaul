using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 幻影协奏（四臂分声部）：基线火力网兼连接拍。上对错拍星球扇射（布阵声部），
    /// 下对同拍低位剪切波（自两肋交叉掠过玩家脚下，执行声部），头交叉波弹，
    /// 中段留一个可读的换位喘息，核心裸露后追加螺旋波列
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Concerto, typeof(MLordContext))]
    internal class MLordConcertoState : MLordStateBase
    {
        public override string StateName => "Concerto";
        public override MLordStateIndex StateIndex => MLordStateIndex.Concerto;

        /// <summary>公平阀（契约3）：此帧起进入连接拍，节拍表整体停射，
        /// 状态尾段（约 1/3 时长）保证无新弹幕的喘息窗</summary>
        internal const int BreatherStart = 200;

        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = Frames(context, 300);
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心慢速横漂：编队时钟驱动的正弦游走
            float drift = (float)Math.Sin(context.FormationClock * 0.017f) * 260f;
            HoverTo(npc, target.Center + new Vector2(drift, 0f) + MLordDirector.CoreHoverOffset, 7.5f);
            UpdateLean(context);

            if (!VaultUtils.isClient) {
                RunServerBeats(context);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>服务端节拍表；<see cref="BreatherStart"/> 帧起进入连接拍（无新弹幕，编队复位喘息）</summary>
        private void RunServerBeats(MLordContext context) {
            //喘息窗硬闸：连接拍内任何分支都射不出弹幕
            if (Timer >= Frames(context, BreatherStart)) {
                return;
            }
            int fanA = Frames(context, 24);
            int fanB = Frames(context, 70);
            int shearA = Frames(context, 46);
            int shearB = Frames(context, 148);
            int boltStart = Frames(context, 124);

            //错拍星球扇（上对声部）：上左先手，上右后半拍
            if (Timer == fanA) {
                SpawnOrbFan(context, preferSlot: 0);
            }
            if (Timer == fanB) {
                SpawnOrbFan(context, preferSlot: 1);
            }

            //低位剪切波（下对声部）：两肋同拍交叉掠向玩家脚下
            if (Timer == shearA || Timer == shearB) {
                SpawnLowShear(context);
            }

            //头部三连交叉波弹
            if (Timer == boltStart || Timer == boltStart + Frames(context, 22) || Timer == boltStart + Frames(context, 44)) {
                SpawnHeadBoltCross(context);
            }

            //裸露阶段：核心自体螺旋波列填充节拍（末拍收在喘息窗之前）
            if (context.CoreExposed && (Timer == Frames(context, 56) || Timer == Frames(context, 130) || Timer == Frames(context, 188))) {
                SpawnCoreSpiral(context);
            }
        }

        /// <summary>从偏好手槽（缺位就近换手→头→核心）放出持握星球扇</summary>
        private void SpawnOrbFan(MLordContext context, int preferSlot) {
            NPC origin = PickPart(context, preferSlot);
            if (origin == null) {
                return;
            }
            Vector2 aim = (context.Target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
            int count = context.CoreExposed ? 6 : 5;
            int damage = ScaleDamage(context, MLordDirector.OrbDamage);
            int launchDelay = Frames(context, 52);
            for (int i = 0; i < count; i++) {
                float spread = MathHelper.Lerp(-0.52f, 0.52f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector2 offset = aim.RotatedBy(spread) * 74f;
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + offset, Vector2.Zero,
                    ModContent.ProjectileType<MLordOrbProj>(), damage, 0f, Main.myPlayer,
                    origin.whoAmI, 0f, launchDelay + i * 4);
            }
            origin.netUpdate = true;
        }

        /// <summary>头部交叉波弹：一发直指两发斜掠</summary>
        private void SpawnHeadBoltCross(MLordContext context) {
            NPC origin = context.Parts.Head >= 0 && context.Parts.HeadAlive
                ? Main.npc[context.Parts.Head] : context.Npc;
            Vector2 muzzle = origin.Center + new Vector2(0f, 30f);
            Vector2 aim = (context.Target.Center - muzzle).SafeNormalize(Vector2.UnitY);
            int damage = ScaleDamage(context, MLordDirector.BoltDamage);
            float speed = context.CoreExposed ? 9.5f : 8f;
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = aim.RotatedBy(i * 0.34f) * speed;
                Projectile.NewProjectile(origin.GetSource_FromAI(), muzzle, vel,
                    ProjectileID.PhantasmalBolt, damage, 0f, Main.myPlayer);
            }
        }

        /// <summary>核心螺旋波列：按编队时钟转相位的环射</summary>
        private void SpawnCoreSpiral(MLordContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(context, MLordDirector.BoltDamage);
            float baseAngle = context.FormationClock * 0.09f;
            for (int i = 0; i < 8; i++) {
                float angle = baseAngle + MathHelper.TwoPi / 8f * i;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                    angle.ToRotationVector2() * 5.6f, ProjectileID.PhantasmalBolt, damage, 0f, Main.myPlayer);
            }
        }

        /// <summary>下对同拍剪切波：两只下手各向玩家脚下点位掠射短扇，弹道在低位交叉封走位</summary>
        private void SpawnLowShear(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.EyeDamage);
            Vector2 aimPoint = context.Target.Center + new Vector2(0f, 90f);
            for (int slot = 2; slot < MLordPartsStatus.HandSlots; slot++) {
                if (!parts.HandAlive(slot) || parts.HandIndex(slot) < 0) {
                    continue;
                }
                NPC hand = Main.npc[parts.HandIndex(slot)];
                Vector2 aim = (aimPoint - hand.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center + aim * 46f,
                        aim.RotatedBy(i * 0.16f) * 6.4f, ProjectileID.PhantasmalEye, damage, 0f, Main.myPlayer);
                }
            }
        }

        /// <summary>优先取指定手槽，缺位就近换手→头→核心（核心代射兜底）</summary>
        private static NPC PickPart(MLordContext context, int preferSlot) {
            MLordPartsStatus parts = context.Parts;
            int hand = parts.FirstAliveHand(preferSlot);
            if (hand >= 0) {
                return Main.npc[hand];
            }
            if (parts.HeadAlive && parts.Head >= 0) {
                return Main.npc[parts.Head];
            }
            return context.Npc;
        }
    }
}
