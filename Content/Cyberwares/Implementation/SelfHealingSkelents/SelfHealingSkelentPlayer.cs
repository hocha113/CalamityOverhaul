using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents
{
    /// <summary>
    /// 自愈骨骼 ModPlayer，脱战计时与 lifeRegen 修改
    /// <br/>须在 UpdateLifeRegen 写 lifeRegen，PostUpdateEquips 会被 UpdatePlayer_Buffs 重置
    /// </summary>
    internal class SelfHealingSkelentPlayer : ModPlayer
    {
        /// <summary>上次受击距今帧数</summary>
        public int FramesSinceLastHurt { get; private set; }

        /// <summary>纳米修复状态，脱战足够久为 true</summary>
        public bool IsRegenerating { get; private set; }

        public override void OnEnterWorld() {
            //开局当脱战
            FramesSinceLastHurt = SelfHealingSkelent.OutOfCombatThreshold;
            IsRegenerating = false;
        }

        public override void OnHurt(Player.HurtInfo info) {
            //装备与否都重置，动态装卸可对上
            FramesSinceLastHurt = 0;
            IsRegenerating = false;
        }

        public override void PostUpdate() {
            //上限 headroom，防溢出习惯
            if (FramesSinceLastHurt < int.MaxValue / 2) {
                FramesSinceLastHurt++;
            }
        }

        public override void UpdateLifeRegen() {
            if (SelfHealingSkelent.GetEquipped(Player) == null) {
                IsRegenerating = false;
                return;
            }

            Player.lifeRegen += SelfHealingSkelent.LifeRegenBonus;

            //脱战纳米修复，lifeRegenTime≥60 抵受击负延迟
            if (FramesSinceLastHurt >= SelfHealingSkelent.OutOfCombatThreshold) {
                IsRegenerating = true;
                Player.lifeRegen += SelfHealingSkelent.OutOfCombatRegenBonus;
                if (Player.lifeRegenTime < 60) {
                    Player.lifeRegenTime = 60;
                }
                if (Main.GameUpdateCount % 30 == 0 && Player.whoAmI == Main.myPlayer) {
                    SpawnRegenParticles();
                }
            }
            else {
                IsRegenerating = false;
            }
        }

        public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
            health = StatModifier.Default;
            mana = StatModifier.Default;
            if (SelfHealingSkelent.GetEquipped(Player) == null) {
                return;
            }
            health.Base = SelfHealingSkelent.MaxLifeBonus;
        }

        /// <summary>纳米修复粒子，浅绿光点上飘</summary>
        private void SpawnRegenParticles() {
            for (int i = 0; i < 2; i++) {
                Vector2 offset = new(Main.rand.NextFloat(-Player.width * 0.5f, Player.width * 0.5f),
                    Main.rand.NextFloat(-Player.height * 0.4f, Player.height * 0.4f));
                Vector2 vel = new(0f, -1.2f);
                Dust dust = Dust.NewDustPerfect(Player.Center + offset, DustID.HealingPlus, vel, 100, default, 1.05f);
                dust.noGravity = true;
            }
        }
    }
}
