using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>交叉火力：躯干宝石与分离飞头半拍错开互射短促射线，射线在玩家位置交叉；墙面射线口补第三声部</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Crossfire, typeof(GolemStateContext))]
    internal class GolemCrossfireState : GolemStateBase
    {
        public override string StateName => "Crossfire";
        public override GolemStateIndex StateIndex => GolemStateIndex.Crossfire;

        private int beatTimer;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            beatTimer = 0;

            //布设墙面射线口（第三声部，服务端）
            if (!VaultUtils.isClient) {
                int rayDamage = GolemDirector.ScaleDamage(GolemDirector.EyeRayDamage, context.DeathMode);
                GolemTrapUnit.PlantOnSide(context.Npc, context.Target, -1, GolemTrapUnit.TrapKind.RayPort,
                    GolemDirector.TrapTelegraph + 40, rayDamage);
                GolemTrapUnit.PlantOnSide(context.Npc, context.Target, 1, GolemTrapUnit.TrapKind.RayPort,
                    GolemDirector.TrapTelegraph + 130, rayDamage);
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            GroundBrake(npc);
            //站桩状态强制恢复地形碰撞
            npc.noTileCollide = false;
            context.VeinGlow = Math.Max(context.VeinGlow, 0.55f);
            context.SetChargeState(1, 0.4f + 0.3f * MathF.Sin(Timer * 0.1f));

            //交叉节拍：躯干整拍，飞头半拍
            int beat = Tempo(context, 44);
            if (!VaultUtils.isClient) {
                beatTimer++;
                int half = beat / 2;
                if (beatTimer >= beat) {
                    beatTimer = 0;
                    FireCrossRay(context, fromFreeHead: false);
                }
                else if (beatTimer == half) {
                    FireCrossRay(context, fromFreeHead: true);
                }
            }

            Timer++;
            int duration = Tempo(context, 320);
            if (Timer >= duration && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>从躯干宝石或飞头发射短促可读射线（服务端）</summary>
        private void FireCrossRay(GolemStateContext context, bool fromFreeHead) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Vector2 muzzle;

            if (fromFreeHead) {
                GolemLimbStatus limbs = context.Limbs;
                if (!limbs.FreeHeadAlive) {
                    return;
                }
                muzzle = Main.npc[limbs.FreeHeadIndex].Center + new Vector2(0f, 8f);
            }
            else {
                muzzle = npc.Center + new Vector2(0f, -6f);
            }

            //射线锚向玩家预读位置，预警期不追踪——留出穿缝空间
            Vector2 aim = target.Center + target.velocity * 10f;
            float rot = (aim - muzzle).ToRotation();
            int damage = ScaleDamage(context, GolemDirector.EyeRayDamage);
            GolemEyeRay.Fire(npc, muzzle, rot, GolemDirector.RayTelegraph, damage,
                followNpcIndex: fromFreeHead ? context.Limbs.FreeHeadIndex : npc.whoAmI);
        }
    }
}
