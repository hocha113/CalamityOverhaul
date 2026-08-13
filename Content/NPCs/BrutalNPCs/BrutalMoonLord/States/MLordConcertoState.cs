using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 幻影协奏：基线火力网兼连接拍。双手错拍星球扇射，头交叉波弹，
    /// 中段留一个可读的换位喘息，核心裸露后追加螺旋波列
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Concerto, typeof(MLordContext))]
    internal class MLordConcertoState : MLordStateBase
    {
        public override string StateName => "Concerto";
        public override MLordStateIndex StateIndex => MLordStateIndex.Concerto;

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

        /// <summary>服务端节拍表；200 帧后进入连接拍（无新弹幕，编队复位喘息）</summary>
        private void RunServerBeats(MLordContext context) {
            int fanA = Frames(context, 24);
            int fanB = Frames(context, 70);
            int boltStart = Frames(context, 124);

            //错拍星球扇：左手先手，右手后半拍
            if (Timer == fanA) {
                SpawnOrbFan(context, preferLeft: true);
            }
            if (Timer == fanB) {
                SpawnOrbFan(context, preferLeft: false);
            }

            //头部三连交叉波弹
            if (Timer == boltStart || Timer == boltStart + Frames(context, 22) || Timer == boltStart + Frames(context, 44)) {
                SpawnHeadBoltCross(context);
            }

            //裸露阶段：核心自体螺旋波列填充节拍
            if (context.CoreExposed && (Timer == Frames(context, 56) || Timer == Frames(context, 160) || Timer == Frames(context, 236))) {
                SpawnCoreSpiral(context);
            }
        }

        /// <summary>从指定侧手（缺侧退避另一手→头→核心）放出持握星球扇</summary>
        private void SpawnOrbFan(MLordContext context, bool preferLeft) {
            NPC origin = PickPart(context, preferLeft);
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

        /// <summary>优先取指定侧手，缺位退避：另一手→头→核心；全无返回 null 由核心代射</summary>
        private static NPC PickPart(MLordContext context, bool preferLeft) {
            MLordPartsStatus parts = context.Parts;
            int first = preferLeft ? parts.LeftHand : parts.RightHand;
            bool firstAlive = preferLeft ? parts.LeftHandAlive : parts.RightHandAlive;
            int second = preferLeft ? parts.RightHand : parts.LeftHand;
            bool secondAlive = preferLeft ? parts.RightHandAlive : parts.LeftHandAlive;

            if (firstAlive && first >= 0) {
                return Main.npc[first];
            }
            if (secondAlive && second >= 0) {
                return Main.npc[second];
            }
            if (parts.HeadAlive && parts.Head >= 0) {
                return Main.npc[parts.Head];
            }
            return context.Npc;
        }
    }
}
