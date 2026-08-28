using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 掀地投掷：连续掀起身前冻土，碎块升空悬滞后按落点标记砸下。
    /// 落点全程可读，考验持续走位；二阶段多一轮
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.RubbleToss, typeof(DeerclopsStateContext))]
    internal class DeerclopsRubbleTossState : DeerclopsStateBase
    {
        public override string StateName => "RubbleToss";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.RubbleToss;

        private const int ScoopLength = 58;
        private const int TossOffset = 32;
        private const int ChunksPerScoop = 7;

        private int ScoopCount(DeerclopsStateContext ctx) => ctx.IsPhase2 ? 3 : 2;
        private int StateEnd(DeerclopsStateContext ctx) => ScoopCount(ctx) * ScoopLength + 36;

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            int scoopCount = ScoopCount(context);
            int scoopingTime = scoopCount * ScoopLength;

            npc.damage = 0;
            FaceTarget(context);

            if (Timer <= scoopingTime) {
                context.HaltMovement = true;
                context.AnimMode = DeerAnimMode.Scoop;
                int local = (Timer - 1) % ScoopLength;
                context.AnimTimer = local;

                //原版音效节拍：低吼在掀地前，掀地声与碎块齐飞
                if (local == 12 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.85f, Pitch = -0.1f }, npc.Center);
                }
                if (local == TossOffset) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 1f }, npc.Center);
                    }
                    DeerclopsMotion.CameraPunch(npc.Bottom, 5.5f, 18, "DeerRubbleScoop", -Vector2.UnitY);
                    SpawnScoopCluster(context, (Timer - 1) / ScoopLength);
                }
            }

            if (Timer >= StateEnd(context)) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        /// <summary>服务端掀起一轮碎块：从身前地表拔起，落点按预测+确定性散布</summary>
        private void SpawnScoopCluster(DeerclopsStateContext context, int clusterIndex) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            Player player = context.Target;
            if (player == null) {
                return;
            }

            int dir = DirToTarget(context);
            Point feet = npc.Bottom.ToTileCoordinates();
            int damage = context.IsAsuraMode ? 20 : 16;

            for (int k = 0; k < ChunksPerScoop; k++) {
                //掀起点：身前地表
                int tileX = feet.X + dir * (2 + k * 2 + clusterIndex);
                Vector2 groundPos = DeerclopsMotion.FindGroundBelow(new Vector2(tileX * 16f + 8f, npc.Bottom.Y - 32f));
                Vector2 spawnPos = groundPos - new Vector2(0f, 10f);

                //落点：预测位+横向散布(带位铺开)
                float spread = ((k + clusterIndex * 3) % ChunksPerScoop - ChunksPerScoop / 2) * 72f;
                Vector2 predicted = player.Center + player.velocity * (24f + k * 5f) + new Vector2(spread, 0f);
                Vector2 landing = DeerclopsMotion.FindGroundBelow(predicted - new Vector2(0f, 60f));

                int frameStyle = 6 + (k + clusterIndex) % 6;
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<DeerRubbleProj>(), damage, 0f, Main.myPlayer,
                    landing.X, frameStyle, landing.Y);
            }
        }
    }
}
