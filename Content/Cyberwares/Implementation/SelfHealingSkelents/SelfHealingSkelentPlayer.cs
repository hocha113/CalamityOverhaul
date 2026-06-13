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
            //初始为脱战状态，避免开荒第一秒错过加成
            FramesSinceLastHurt = SelfHealingSkelent.OutOfCombatThreshold;
            IsRegenerating = false;
        }

        public override void OnHurt(Player.HurtInfo info) {
            //无论装备与否都重置脱战计时，便于动态装备/卸装时维持正确的状态
            FramesSinceLastHurt = 0;
            IsRegenerating = false;
        }

        public override void PostUpdate() {
            //计时器持续递增；上限留少许 headroom，避免 int 溢出（实际 32 位远超不会溢出，仅卫语句习惯）
            if (FramesSinceLastHurt < int.MaxValue / 2) {
                FramesSinceLastHurt++;
            }
        }

        public override void UpdateLifeRegen() {
            if (SelfHealingSkelent.GetEquipped(Player) == null) {
                IsRegenerating = false;
                return;
            }

            //常驻回复始终生效
            Player.lifeRegen += SelfHealingSkelent.LifeRegenBonus;

            //脱战纳米修复：追加回复并强制 lifeRegenTime≥60，抵消受击负延迟
            if (FramesSinceLastHurt >= SelfHealingSkelent.OutOfCombatThreshold) {
                IsRegenerating = true;
                Player.lifeRegen += SelfHealingSkelent.OutOfCombatRegenBonus;
                if (Player.lifeRegenTime < 60) {
                    Player.lifeRegenTime = 60;
                }
                //每隔半秒撒一些金属修复粒子，给玩家明确的视觉信号
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
