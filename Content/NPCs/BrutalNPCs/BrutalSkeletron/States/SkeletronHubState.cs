using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>悬浮压制连接件：轻颅火点射 + 按阶段循环表派发下一招</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.Hub, typeof(SkeletronStateContext))]
    internal class SkeletronHubState : SkeletronStateBase
    {
        public override string StateName => "Hub";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.Hub;

        /// <summary>缺口（契约3）：贴脸距离内不点射——近身是明确的安全窗，发射条件直接读取</summary>
        private const float MinFireDistancePx = 240f;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            bool p2 = (int)npc.ai[SkeletronAiSlots.HeadPhase] >= SkeletronPhase.Unbound;

            Movement(context, p2);
            LeanByVelocity(npc);

            int hubDuration = SkeletronDirector.HubFrames;
            if (p2) {
                hubDuration = (int)(hubDuration * SkeletronDirector.P2TempoMult);
            }
            if (context.MasterMode) {
                hubDuration -= 10;
            }

            //轻压制：诅咒颅火点射，绝不留空白窗口；近身缺口内停火
            int fireInterval = p2 ? 26 : 34;
            if (!VaultUtils.isClient && Timer % fireInterval == fireInterval - 1
                && npc.Distance(context.Target.Center) > MinFireDistancePx
                && Collision.CanHitLine(npc.Center, 1, 1, context.Target.position, context.Target.width, context.Target.height)) {
                Vector2 vel = DirectionToTarget(context).RotatedByRandom(0.14f) * (p2 ? 7.4f : 6.2f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 6f, vel,
                    ModContent.ProjectileType<SkeletronCursedSkull>(), SkullDamage(context), 0f, Main.myPlayer, 1f, 0f);
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer && Timer % fireInterval == fireInterval - 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.55f, Pitch = -0.4f }, npc.Center);
            }

            Timer++;
            if (Timer >= hubDuration && !VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                return DispatchNext(context, p2);
            }
            return null;
        }

        /// <summary>手工节奏表：压迫→机动→区域→迸发交替</summary>
        private static ISkeletronState DispatchNext(SkeletronStateContext context, bool p2) {
            if (!p2) {
                int step = context.AttackIndexP1 % 4;
                context.AttackIndexP1++;
                return step switch {
                    0 => new SkeletronHandCrushState(),
                    1 => new SkeletronSpinBoneStormState(),
                    2 => new SkeletronGhostArmCircleState(),
                    //合拍槽位的混拍升级：普通钳杀教会几何，投技惩罚松懈
                    _ => SkeletronPalmSnatchState.CanDispatch(context)
                        ? new SkeletronPalmSnatchState()
                        : new SkeletronClapPincerState(),
                };
            }

            //二阶段紧凑五拍：区域压制→机动→迸发→旋骨区域封锁→黑暗猎杀收束（原6拍含颅雨复读+一阶段鬼臂圈返场，删）
            int stepP2 = context.AttackIndexP2 % 5;
            context.AttackIndexP2++;
            return stepP2 switch {
                0 => new SkeletronGhostPandemoniumState(),
                1 => new SkeletronSkullRainTeleportState(),
                2 => new SkeletronSpinBoneStormState(),
                3 => new SkeletronBoneWheelState(),
                _ => new SkeletronCurseDomainState(),
            };
        }

        private void Movement(SkeletronStateContext context, bool p2) {
            float vAccel = p2 ? 0.055f : 0.045f;
            float vMax = p2 ? 5.4f : 4.6f;
            float hAccel = p2 ? 0.14f : 0.1f;
            float hMax = p2 ? 11f : 9f;
            HoverMovement(context, vAccel, vMax, hAccel, hMax, 0.95f, 300);
        }
    }
}
