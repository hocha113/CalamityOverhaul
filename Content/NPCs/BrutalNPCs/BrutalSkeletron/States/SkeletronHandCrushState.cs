using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>锁链砸击连段（头侧主状态）：手部读取本状态自发执行，头悬高位点射压走位</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.HandCrush, typeof(SkeletronStateContext))]
    internal class SkeletronHandCrushState : SkeletronStateBase
    {
        public override string StateName => "HandCrush";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.HandCrush;

        internal const int Duration = 196;

        /// <summary>缺口（契约3）：双颅火对称劈开 ±SplitHalfAngle，瞄准轴正中永远是空走廊——
        /// 迎着头站/原地不动即安全，发射循环直接读取该角</summary>
        private const float SplitHalfAngle = 0.34f;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //无手可用直接退回 hub（转阶段由全局转移接管）
            if (!VaultUtils.isClient && !context.AnyHandAlive) {
                return new SkeletronHubState();
            }

            //头压在玩家正上方高位，把垂直空间让给砸击
            HoverMovement(context, 0.05f, 4.4f, 0.11f, 9.5f, 0.95f, 430);
            LeanByVelocity(npc);

            //点射封走位（斜角，与砸击垂直威胁交叉；中央走廊留空）
            if (!VaultUtils.isClient && (Timer == 58 || Timer == 150)
                && Collision.CanHitLine(npc.Center, 1, 1, context.Target.position, context.Target.width, context.Target.height)) {
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = DirectionToTarget(context).RotatedBy(i * SplitHalfAngle) * 6.6f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 6f, vel,
                        ModContent.ProjectileType<SkeletronCursedSkull>(), SkullDamage(context), 0f, Main.myPlayer, 0f, 0f);
                }
                npc.netUpdate = true;
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }
    }

    /// <summary>双掌合拍钳杀（头侧主状态）：头后撤观刑，合拍成功由手侧结算</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.ClapPincer, typeof(SkeletronStateContext))]
    internal class SkeletronClapPincerState : SkeletronStateBase
    {
        public override string StateName => "ClapPincer";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.ClapPincer;

        internal const int Duration = 136;

        /// <summary>缺口（契约3）：补刀颅火直落不追踪（ai[0]=0），左右分落 ±DropSideVelX——
        /// 合拍逃出后垂直正下方是安全带，发射循环直接读取</summary>
        private const float DropSideVelX = 2.4f;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            if (!VaultUtils.isClient && !context.AnyHandAlive) {
                return new SkeletronHubState();
            }

            //头退到侧上死角，凝视（威严=静止）
            Vector2 watchPoint = context.Target.Center + new Vector2(0f, -520f);
            npc.velocity = (watchPoint - npc.Center) * 0.03f;
            SettleRotation(npc, 0.15f);

            //合拍瞬间的补刀：头顶垂落两枚直线颅火（合拍后 82 帧），钳口窄区里追踪弹无处可躲——不追踪
            if (!VaultUtils.isClient && Timer == 92) {
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = new Vector2(i * DropSideVelX, 5.4f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(i * 40f, 30f), vel,
                        ModContent.ProjectileType<SkeletronCursedSkull>(), SkullDamage(context), 0f, Main.myPlayer, 0f, 0f);
                }
                npc.netUpdate = true;
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }
    }
}
