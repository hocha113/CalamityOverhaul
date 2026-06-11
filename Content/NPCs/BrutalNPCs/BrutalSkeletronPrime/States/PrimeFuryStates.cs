using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 白昼狂暴：拂晓降临，机体解除全部限制器疯狂追杀。
    /// 夜幕重新降临时收势回到对应阶段的常态。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.DayEnrage, typeof(PrimeStateContext))]
    internal class PrimeDayEnrageState : PrimeStateBase
    {
        public override string StateName => "DayEnrage";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.DayEnrage;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar, context.Npc.Center);
            }
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 1;

            npc.damage = 1000;
            npc.defense = 9999;
            SpinRotation(npc);
            ChasePlayer(context);

            Timer++;
            if (!Main.IsItDay() && !VaultUtils.isClient) {
                npc.damage = npc.defDamage;
                return npc.ai[PrimeAiSlots.HeadPhase] >= PrimePhase.Rage
                    ? new PrimeRageHoverState()
                    : new PrimeCommandHoverState();
            }
            return null;
        }

        private static void ChasePlayer(PrimeStateContext context) {
            NPC npc = context.Npc;
            Vector2 toPlayer = context.Target.Center - npc.Center;
            float distance = toPlayer.Length();
            float speed = System.Math.Clamp(10f + distance / 100f, 8f, 32f);
            npc.velocity = toPlayer.SafeNormalize(Vector2.UnitY) * speed;
        }
    }

    /// <summary>
    /// 金币枪狂怒：胆敢用铂金币羞辱机械之王者，将被不计代价地处刑。
    /// 玩家收起金币枪后才会息怒。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CoinGunFury, typeof(PrimeStateContext))]
    internal class PrimeCoinGunFuryState : PrimeStateBase
    {
        public override string StateName => "CoinGunFury";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CoinGunFury;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundStyle sound = "CalamityMod/Sounds/Custom/ExoMechs/AresEnraged".GetSound();
                SoundEngine.PlaySound(sound with { Pitch = -0.18f }, context.Npc.Center);
                SoundEngine.PlaySound(SoundID.ForceRoar, context.Npc.Center);
            }
        }

        /// <summary>目标玩家是否正在用铂金币挑衅</summary>
        internal static bool IsProvoking(Player player) {
            if (player == null || !player.active || player.dead) {
                return false;
            }
            Item heldItem = player.GetItem();
            return heldItem.type == ItemID.CoinGun
                && player.GetShootState().AmmoTypes == ProjectileID.PlatinumCoin;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 1;

            npc.damage = 999;
            npc.defense = 999;
            npc.ChasingBehavior(context.Target.Center, 33);
            npc.rotation += npc.velocity.X > 0 ? 0.42f : -0.42f;

            Timer++;
            if (!IsProvoking(context.Target) && !VaultUtils.isClient) {
                npc.damage = npc.defDamage;
                return npc.ai[PrimeAiSlots.HeadPhase] >= PrimePhase.Rage
                    ? new PrimeRageHoverState()
                    : new PrimeCommandHoverState();
            }
            return null;
        }
    }
}
