using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 长枪墙：六面墙按固定方向轮转（→ ← ↓ ↓ ↖ ↗），每墙整体偏移 ±350 不贴脸，
    /// 枪距 LaneSpacing 是命名通道，另有一枚滚动跳过的 WallGap 宽洞；瞄准期弱追踪衰减到零
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.LanceWall, typeof(EmpressStateContext))]
    internal class EmpressLanceWallState : EmpressStateBase
    {
        public override string StateName => "EmpressLanceWall";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.LanceWall;

        /// <summary>枪距：每两枪之间就是一条可穿过的通道（公平阀，发射循环直接读它）</summary>
        internal const float LaneSpacing = 300f;
        /// <summary>墙到目标的出生距离</summary>
        private const float WallDistance = 1500f;

        private static readonly Vector2[] WallDirs = [
            Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, Vector2.UnitY,
            new Vector2(-0.7071f, -0.7071f), new Vector2(0.7071f, -0.7071f),
        ];
        private static readonly Vector2[] WallOffsets = [
            new(-350f, 0f), new(350f, 0f), new(0f, -350f), new(0f, -350f), new(0f, 350f), new(0f, 350f),
        ];

        private int WallCount => Context.DayEmpowered ? 6 : 4;
        private int Interval => Context.DayEmpowered ? 40 : 50;
        private int LancesPerWall => Context.DayEmpowered ? (Context.IsSecondPhase ? 14 : 12) : 8;
        private int TotalTime => WallCount * Interval + 60 + 86 + 20;

        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            PlayLocal(SoundID.Item164 with { Volume = 0.8f, Pitch = -0.2f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = MathHelper.Clamp(Timer, 0f, 60f);

            //侧上方悬停 (∓500,-250)，让墙有空间从她身侧展开
            if (target.Alives()) {
                Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -500f : 500f, -250f);
                npc.velocity = npc.velocity * 0.7f + (dest - npc.Center) * 0.042f;
            }
            else {
                npc.velocity *= 0.9f;
            }

            int wallIdx = (Timer - 10) / Interval;
            if ((Timer - 10) % Interval == 0 && wallIdx >= 0 && wallIdx < WallCount && target.Alives()) {
                context.SetChargeState(3, 1f);
                CastWall(context, npc, target, wallIdx);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void CastWall(EmpressStateContext context, NPC npc, Player target, int wallIdx) {
            PlayLocal(SoundID.Item162 with { Volume = 0.6f, Pitch = -0.3f + wallIdx * 0.05f }, npc.Center);
            if (VaultUtils.isClient) {
                return;
            }
            Vector2 dir = WallDirs[wallIdx % WallDirs.Length];
            Vector2 offset = WallOffsets[wallIdx % WallOffsets.Length];
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int n = LancesPerWall;
            //滚动宽洞：每墙跳过一枚，索引随墙号推进，人总能找到一条 600 宽的路
            int gapIndex = (wallIdx * 3 + 1) % n;
            Vector2 center = target.Center + offset;
            for (int i = 0; i < n; i++) {
                if (i == gapIndex) {
                    continue;
                }
                Vector2 pos = center - dir * WallDistance + perp * (LaneSpacing * (i - n / 2f + 0.5f));
                EmpressCast.Lance(npc, pos, dir.ToRotation(), context.LanceDamage, EmpressLanceMode.Wall);
            }
        }
    }
}
