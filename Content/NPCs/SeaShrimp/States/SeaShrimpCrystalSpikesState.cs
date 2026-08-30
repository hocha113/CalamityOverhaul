using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 背晶齐射→晶刺阵（P2）：拱背蓄势 → f20 锁定阵心（承诺，不追人）→
    /// 背晶迸射（表现）+ 六柱巨晶刺落位，各柱自带 30f 鬼影预告后拔地。
    /// 声明式缺口：柱间距 260px、判定宽 68px，柱间走廊即逃逸通道。
    /// 玩家悬空/搭台（脚下实地深过最大落差）时柱底悬空生成，攻击不再在地下空转
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.CrystalSpikes, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpCrystalSpikesState : SeaShrimpStateBase
    {
        public override string StateName => "CrystalSpikes";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.CrystalSpikes;

        private const int LockFrame = 20;
        private const int CastFrame = 26;
        private const int Total = 100;
        /// <summary>柱位阵列（相对阵心），间距 260px 即声明缺口（巨柱配宽廊）</summary>
        private static readonly float[] SpikeOffsets = [-650f, -390f, -130f, 130f, 390f, 650f];

        private Vector2 lockCenter;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            //拱背蓄势姿态
            float build = MathHelper.Clamp(t / (float)CastFrame, 0f, 1f);
            ctx.SpineCurl = 0.3f * build;
            ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, build);
            ctx.WaveGain = 0.3f;

            if (t < LockFrame) {
                lockCenter = ctx.Target.Center;
            }

            if (t < CastFrame && !Main.dedServ && t % 2 == 0) {
                //背晶向上聚光
                Vector2 back = ctx.Owner.Skeleton.Nodes[1].Pos - ctx.Owner.Skeleton.HeadDown * 26f;
                PRTLoader.NewParticle<PRT_Spark>(back + Main.rand.NextVector2Circular(30f, 10f),
                    -ctx.Owner.Skeleton.HeadDown * Main.rand.NextFloat(1f, 2.4f),
                    SeaShrimpRenderer.CrystalBlue, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(false, Main.rand.Next(10, 16));
            }

            if (t == CastFrame) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.1f }, npc.Center);
                    ShakeNearby(npc.Center, 3f);
                    //背晶迸射表现：晶片从背部弧线飞散
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_DefCrystalShard>(
                            ctx.Owner.Skeleton.Nodes[1].Pos - ctx.Owner.Skeleton.HeadDown * 20f,
                            new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(5f, 9f)),
                            SeaShrimpRenderer.CrystalBlue, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(-0.35f, 0.35f));
                    }
                }
                if (!VaultUtils.isClient) {
                    //六柱落位：阵心已锁，各柱自带鬼影预告（预告即实体）。
                    //落差阀：脚下实地深过最大落差（玩家悬空/搭台）→ 柱底悬空到玩家下方定距
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.CrystalSpikeDamage);
                    foreach (float offset in SpikeOffsets) {
                        Vector2 probe = new(lockCenter.X + offset, lockCenter.Y - 240f);
                        float groundY = FindGroundY(probe);
                        float spawnY = groundY - lockCenter.Y > SeaShrimpDirector.GroundAttackMaxDrop
                            ? lockCenter.Y + SeaShrimpDirector.AirSpawnBelow
                            : groundY;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(probe.X, spawnY), Vector2.Zero,
                            ModContent.ProjectileType<SeaShrimpCrystalSpike>(), damage, 2f,
                            Main.myPlayer, 30f, SeaShrimpDirector.CrystalSpikeHeight);
                    }
                }
            }

            if (t >= Total) {
                return EndAttack(ctx, 55);
            }
            return null;
        }
    }
}
