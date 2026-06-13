using InnoVault.Actors;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>炮台目标工厂</summary>
    internal class TurretTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Turret;

        public override int HoverPriority => 40;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            List<Actor> list = ActorLoader.GetActiveActors<Actor>();
            if (list == null || list.Count == 0) return null;

            IHackableTurret best = null;
            float bestDistSq = float.MaxValue;
            const float expandMargin = 24f;

            foreach (Actor actor in list) {
                if (actor is not IHackableTurret turret) continue;
                if (!turret.IsValid) continue;

                float left = actor.Position.X - expandMargin;
                float top = actor.Position.Y - expandMargin;
                float right = actor.Position.X + actor.Width + expandMargin;
                float bottom = actor.Position.Y + actor.Height + expandMargin;
                if (mouseWorld.X < left || mouseWorld.X > right) continue;
                if (mouseWorld.Y < top || mouseWorld.Y > bottom) continue;

                float dx = mouseWorld.X - turret.WorldCenter.X;
                float dy = mouseWorld.Y - turret.WorldCenter.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = turret;
                }
            }

            return best;
        }
    }
}
