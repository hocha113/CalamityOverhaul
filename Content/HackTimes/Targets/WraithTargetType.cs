using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith;
using InnoVault.Actors;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>灵异 Actor 目标工厂</summary>
    internal class WraithTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Wraith;

        public override int HoverPriority => 80;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            List<GlitchWraithActor> list = ActorLoader.GetActiveActors<GlitchWraithActor>();
            if (list == null || list.Count == 0) return null;

            GlitchWraithActor best = null;
            float bestDistSq = float.MaxValue;
            const float expandMargin = 16f;

            foreach (GlitchWraithActor w in list) {
                //可见度过低的灵异体无法被扫描锁定，避免玩家隔空选中隐形目标
                if (w.Visibility < 0.3f) continue;

                float left = w.Position.X - expandMargin;
                float top = w.Position.Y - expandMargin;
                float right = w.Position.X + w.Width + expandMargin;
                float bottom = w.Position.Y + w.Height + expandMargin;
                if (mouseWorld.X < left || mouseWorld.X > right) continue;
                if (mouseWorld.Y < top || mouseWorld.Y > bottom) continue;

                float dx = mouseWorld.X - w.Center.X;
                float dy = mouseWorld.Y - w.Center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = w;
                }
            }

            return best;
        }
    }
}
