using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents
{
    /// <summary>
    /// 自愈骨骼的玩家组件
    /// <br/>承担两件事：
    /// <list type="bullet">
    ///   <item>追踪"上次受击距今多少帧"，决定是否进入纳米修复状态</item>
    ///   <item>在 <see cref="ModPlayer.UpdateLifeRegen"/> 阶段直接修改 <c>player.lifeRegen</c>，与原版护甲、Buff 共享同一通道</item>
    /// </list>
    /// 修改 lifeRegen 必须在 <see cref="ModPlayer.UpdateLifeRegen"/> 中完成；放在 PostUpdateEquips 时机过早，
    /// 会被原版 <c>UpdatePlayer_Buffs</c> 之后的 lifeRegen 计算重置
    /// </summary>
    internal class SelfHealingSkelentPlayer : ModPlayer
    {
        /// <summary>
        /// 上次受到伤害到现在过了多少帧，用于判断"出战"状态
        /// </summary>
        public int FramesSinceLastHurt { get; private set; }

        /// <summary>
        /// 是否处于纳米修复状态（脱战足够久），公开供视觉/HUD 接入
        /// </summary>
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

            //脱战足够久后启用纳米修复：追加回复并强制 lifeRegenTime 为正，
            //防止刚被打过的负 lifeRegenTime 把回复速率拉低到 0
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

        /// <summary>
        /// 纳米修复粒子：在玩家身上沿垂直方向飘升的浅绿光点，区别于普通的回血视觉
        /// </summary>
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
