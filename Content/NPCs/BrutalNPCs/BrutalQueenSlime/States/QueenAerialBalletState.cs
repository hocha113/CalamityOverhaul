using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>二阶段枢纽：利萨如8字巡航，编队检点，间隔选招</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.AerialBallet, typeof(QueenSlimeStateContext))]
    internal class QueenAerialBalletState : QueenSlimeStateBase
    {
        public override string StateName => "AerialBallet";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.AerialBallet;

        private int Duration(QueenSlimeStateContext ctx) {
            int baseTime = ctx.IsDeathMode ? 84 : 108;
            //大招后节奏提速
            return ctx.UltFired ? (int)(baseTime * 0.75f) : baseTime;
        }

        public QueenAerialBalletState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);
            FaceTarget(npc, player.Center);

            //利萨如8字巡航
            float t = Timer * 0.026f;
            Vector2 anchor = player.Center + new Vector2(
                (float)Math.Sin(t) * 430f,
                -330f + (float)Math.Sin(t * 2f + MathHelper.PiOver2) * 84f);
            QueenMotion.SpringHover(npc, anchor, 0.016f, 0.1f, 21f);
            QueenMotion.FlightLean(npc);
            context.PoseCommand = 5;
            context.WingFlapBoost = MathHelper.Clamp(npc.velocity.Length() / 16f, 0.2f, 0.8f);

            //编队检点(服务端，低频)：补齐凝胶伴舞与翼卫
            if (!VaultUtils.isClient && Timer == 30) {
                int dancers = context.CountMinions(QueenMinionRole.GelDancer);
                for (int i = dancers; i < 2; i++) {
                    QueenMotion.SpawnMinion(npc, NPCID.QueenSlimeMinionPink, QueenMinionRole.GelDancer,
                        i, npc.Center + new Vector2((i == 0 ? -1 : 1) * 90f, 40f), QueenSlimeMinionAI.DancerLife());
                }
                int escorts = context.CountMinions(QueenMinionRole.WingedEscort);
                for (int i = escorts; i < 2; i++) {
                    QueenMotion.SpawnMinion(npc, NPCID.QueenSlimeMinionPurple, QueenMinionRole.WingedEscort,
                        i, npc.Center + new Vector2((i == 0 ? -1 : 1) * 150f, -40f), QueenSlimeMinionAI.EscortLife());
                }
            }

            if (Timer >= Duration(context) && !VaultUtils.isClient) {
                return ChooseNextAttack(context);
            }

            return null;
        }

        /// <summary>二阶段手排出招环(服务端)：压迫招与场控招交替；投技冷却好则插队起舞</summary>
        private IQueenSlimeState ChooseNextAttack(QueenSlimeStateContext context) {
            if (QueenCrystalPrisonWaltzState.CanTrigger(context)) {
                return new QueenCrystalPrisonWaltzState();
            }
            IQueenSlimeState[] cycle = [
                new QueenWingGaleWaltzState(),
                new QueenCrystalDiveStompState(),
                new QueenGelMeteorRainState(),
                new QueenRefractionCageState(),
                new QueenCrystalDiveStompState(),
                new QueenChandelierFallState(),
                new QueenPrismVolleyState(),
                new QueenCrystalDiveStompState(),
            ];
            IQueenSlimeState next = cycle[context.AttackPhaseIndex % cycle.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
