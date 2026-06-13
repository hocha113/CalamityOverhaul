using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>NPC 目标工厂，悬停优先级最高</summary>
    internal class NpcTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Npc;

        public override int HoverPriority => 100;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;
            const float expandMargin = 16f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!HackTime.IsHackableTarget(npc)) continue;

                float left = npc.position.X - expandMargin;
                float top = npc.position.Y - expandMargin;
                float right = npc.position.X + npc.width + expandMargin;
                float bottom = npc.position.Y + npc.height + expandMargin;

                if (mouseWorld.X < left || mouseWorld.X > right) continue;
                if (mouseWorld.Y < top || mouseWorld.Y > bottom) continue;

                float dx = mouseWorld.X - npc.Center.X;
                float dy = mouseWorld.Y - npc.Center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            return bestIndex < 0 ? null : new NpcScannable(bestIndex);
        }
    }
}
