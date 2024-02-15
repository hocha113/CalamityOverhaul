using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Common.Interfaces
{
    public abstract class CustomBoss : CustomNPC
    {
        public abstract override void BossHeadRotation(ref float rotation);
        public abstract override void BossHeadSlot(ref int index);
        public abstract override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment);
        public abstract override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position);
        public abstract void OnSpanAction();
        public abstract void OnKillAction();
        public abstract override bool CheckDead();
    }
}
