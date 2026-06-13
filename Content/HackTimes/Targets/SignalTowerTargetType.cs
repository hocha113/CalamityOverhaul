using InnoVault.Actors;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>信号塔目标工厂，优先级高于炮台</summary>
    internal class SignalTowerTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.SignalTower;

        public override int HoverPriority => 60;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            List<Actor> list = ActorLoader.GetActiveActors<Actor>();
            if (list == null || list.Count == 0) return null;

            IHackableSignalTower best = null;
            float bestDistSq = float.MaxValue;
            const float expandMargin = 24f;

            foreach (Actor actor in list) {
                if (actor is not IHackableSignalTower tower) continue;
                if (!tower.IsValid) continue;

                float left = actor.Position.X - expandMargin;
                float top = actor.Position.Y - expandMargin;
                float right = actor.Position.X + actor.Width + expandMargin;
                float bottom = actor.Position.Y + actor.Height + expandMargin;
                if (mouseWorld.X < left || mouseWorld.X > right) continue;
                if (mouseWorld.Y < top || mouseWorld.Y > bottom) continue;

                float dx = mouseWorld.X - tower.WorldCenter.X;
                float dy = mouseWorld.Y - tower.WorldCenter.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = tower;
                }
            }

            return best;
        }
    }
}
