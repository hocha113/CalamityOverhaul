using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>节段自爆链：巡游中沿途蜕落自爆壳，把行迹布成引信走廊，随后顺序殉爆</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.HuskMines, typeof(EowStateContext))]
    internal class EowHuskMinesState : EowStateBase
    {
        public override string StateName => "HuskMines";
        public override EowStateIndex StateIndex => EowStateIndex.HuskMines;

        #region 节奏常量
        private const int LayTime = 176;
        private const int RetreatTime = 60;
        /// <summary>首壳引信基准(布完+撤离后开始殉爆)</summary>
        private const int FuseBase = 210;
        /// <summary>殉爆链间隔</summary>
        private const int FuseStep = 5;
        #endregion

        private int ShedInterval(EowStateContext ctx) => ctx.IsPhase2 ? 11 : 14;

        private int shedCount;

        public EowHuskMinesState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            shedCount = 0;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.55f, 0.8f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Tick();

            //布雷巡游：大半径S形穿场
            if (Timer <= LayTime) {
                float t = Timer * 0.021f;
                Vector2 sweepTarget = player.Center + new Vector2(
                    (float)Math.Cos(t) * 830f,
                    (float)Math.Sin(t * 1.6f) * 420f - 120f);
                SetMovement(context, sweepTarget, context.IsPhase2 ? 24f : 20f, 1.35f);
                context.SlitherStrength = 0.85f;

                //蜕壳游标可视化：整段布雷期常亮波前(逐帧声明)
                int segCount = Math.Max(context.Segments.Count, 1);
                int cursorOrdinal = ((shedCount + 1) * 4 + 5) % Math.Max(segCount - 4, 1);
                context.PulseKind = 1;
                context.PulsePhase = cursorOrdinal / (float)segCount;

                //蜕壳节拍：被点到的体节蜕出自爆壳
                if (Timer % ShedInterval(context) == 0 && context.Segments.Count > 8) {
                    shedCount++;
                    int ordinal = (shedCount * 4 + 5) % Math.Max(context.Segments.Count - 4, 1);
                    NPC seg = context.Segments[ordinal];
                    if (seg.Alives()) {
                        //蜕壳表现各端本地
                        EowMotionFX.SpawnMoltHusk(seg);
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 5 }, seg.Center);
                        //壳体(服务端)
                        if (!VaultUtils.isClient) {
                            int fuse = FuseBase - Timer + shedCount * FuseStep;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center,
                                seg.velocity * 0.2f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                                ModContent.ProjectileType<EowHuskMine>(),
                                (int)(EowSpitBarrageState.SpitDamage(npc) * 1.3f), 0f, Main.myPlayer,
                                fuse, shedCount * 0.37f);
                        }
                    }
                }
                return null;
            }

            //撤离段：拉开距离旁观引信点燃
            if (Timer <= LayTime + RetreatTime) {
                int side = Math.Sign(npc.Center.X - player.Center.X);
                if (side == 0) {
                    side = 1;
                }
                SetMovement(context, player.Center + new Vector2(side * 900f, -420f), 26f, 1.1f);
                context.SlitherStrength = 0.6f;
                return null;
            }

            //殉爆链在状态时刻 FuseBase+k*FuseStep 逐响；等最后一响再收
            int lastBlastTime = FuseBase + shedCount * FuseStep + 40;
            if (Timer > Math.Max(lastBlastTime, LayTime + RetreatTime + 30)) {
                return new EowWeaveState();
            }

            //旁观期低速绕行
            SetMovement(context, player.Center + new Vector2(0f, -520f), 15f, 1f);
            context.SlitherStrength = 0.8f;
            return null;
        }
    }
}
