using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 暗影之手：巨鹿垂首引影，手从每个玩家的视界之外成形，红芒预兆后直线掠过。
    /// 波次逐涨(1→2→二阶段3)，逐玩家结算；引影期间本体不设防，贴身输出的奖励窗
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.ShadowClaw, typeof(DeerclopsStateContext))]
    internal class DeerclopsShadowClawState : DeerclopsStateBase
    {
        public override string StateName => "ShadowClaw";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.ShadowClaw;

        private const int Wave1 = 30;
        private const int Wave2 = 85;
        private const int Wave3 = 140;
        private const float SpawnEdgeDist = 1080f;

        private int StateEnd(DeerclopsStateContext ctx) => ctx.IsPhase2 ? 200 : 150;

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            //垂首引影，独眼熄暗，影从视界外来
            context.EyeGlow = 0.06f;
            context.VeilTarget = (context.IsPhase2 ? 0.7f : 0.45f) + 0.15f;
            if (Timer < 30) {
                context.AnimMode = DeerAnimMode.Crouch;
            }

            if (Timer == 4 && !Main.dedServ) {
                //影渗低语
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.85f }, npc.Center);
            }

            //本体渗影(本端)
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame,
                    0f, -1.5f, 150, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }

            if (Timer == Wave1) {
                SpawnWave(context, waveIndex: 0, handsPerPlayer: 1);
            }
            if (Timer == Wave2) {
                SpawnWave(context, waveIndex: 1, handsPerPlayer: 2);
            }
            if (context.IsPhase2 && Timer == Wave3) {
                SpawnWave(context, waveIndex: 2, handsPerPlayer: 3);
            }

            if (Timer >= StateEnd(context)) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        /// <summary>服务端逐玩家放手：从其视界边缘外成形，航线锁定预测位</summary>
        private void SpawnWave(DeerclopsStateContext context, int waveIndex, int handsPerPlayer) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int damage = context.IsAsuraMode ? 17 : 14;
            int telegraph = TelegraphTime(context, context.IsPhase2 ? 30 : 34, 26);

            foreach (Player player in Main.ActivePlayers) {
                if (!player.Alives() || player.Distance(npc.Center) > 3200f) {
                    continue;
                }

                for (int h = 0; h < handsPerPlayer; h++) {
                    Vector2 start;
                    Vector2 aim = player.Center + player.velocity * 18f;

                    if (h == 2) {
                        //第三只手：自上方斜落(二阶段专属)
                        int diagSide = (player.whoAmI + waveIndex) % 2 == 0 ? 1 : -1;
                        start = player.Center + new Vector2(-diagSide * 620f, -940f);
                    }
                    else {
                        //水平掠袭：h=0与h=1从两侧夹击；单手时按波次交替边
                        int side = (player.whoAmI + waveIndex + h) % 2 == 0 ? 1 : -1;
                        float dy = h == 1 ? -90f : MathHelper.Clamp(player.velocity.Y * 20f, -80f, 80f);
                        start = player.Center + new Vector2(-side * SpawnEdgeDist, dy);
                        aim = player.Center + player.velocity * 18f + new Vector2(0f, dy * 0.4f);
                    }

                    float angle = (aim - start).ToRotation();
                    DeerShadowHandProj.SpawnSweepHand(npc, start, angle, telegraph, damage);
                }
            }
        }
    }
}
