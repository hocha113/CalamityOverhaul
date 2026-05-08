using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 教程世界的玩家兜底层
    /// 进入超梦教程子世界时为玩家提供：无敌、刷血、满 RAM、InfiniteHack、自动补 SHPC
    /// 离开子世界时这些状态会随 ResetEffects/PreUpdate 自然停摆
    /// </summary>
    internal class CybCoursePlayer : ModPlayer
    {
        //背包尾部用于强行塞 SHPC 的预留槽
        private const int SHPCFallbackSlotStart = 50;
        private const int SHPCFallbackSlotEnd = 58;
        //刷新背包间隔，避免每帧扫描
        private const int EnsureSHPCInterval = 30;
        private int ensureSHPCTick;

        /// <summary>
        /// 教程世界激活时彻底免疫所有伤害
        /// </summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!CybCourseWorld.Active) return;
            modifiers.FinalDamage *= 0f;
            modifiers.DisableSound();
            modifiers.DisableDust();
        }

        public override void PreUpdate() {
            if (!CybCourseWorld.Active) return;

            //每隔半秒做一次 SHPC 兜底，平摊性能开销
            if (++ensureSHPCTick >= EnsureSHPCInterval) {
                ensureSHPCTick = 0;
                EnsureSHPC();
            }
        }

        public override void PostUpdateEquips() {
            if (!CybCourseWorld.Active) return;
            //血量与 RAM 双保险，确保任何异常都不会让玩家陷入劣势
            Player.statLife = Player.statLifeMax2;
            RamSystem.Refill();
        }

        /// <summary>
        /// 检查热键栏 + 背包是否已存在 SHPC，否则补一把
        /// 优先放入第 0 号热键槽，被占用则塞背包尾部（50~57）
        /// </summary>
        private void EnsureSHPC() {
            for (int i = 0; i < Player.inventory.Length; i++) {
                if (Player.inventory[i].type == SHPCOverride.ID) {
                    return;
                }
            }
            if (Player.inventory[0].IsAir) {
                Player.inventory[0].SetDefaults(SHPCOverride.ID);
                return;
            }
            for (int i = SHPCFallbackSlotStart; i < SHPCFallbackSlotEnd; i++) {
                if (Player.inventory[i].IsAir) {
                    Player.inventory[i].SetDefaults(SHPCOverride.ID);
                    return;
                }
            }
        }

        public override void OnEnterWorld() {
            //从超梦子世界回到主世界时发放超梦接入凭证，子世界加载期无效所以延迟到此处
            if (CybCourseWorld.Active) return;
            if (CybCourse.TryConsumeGrantMewtwo()) {
                if (!Player.HasItem(ModContent.ItemType<Mewtwo>())) {
                    Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), ModContent.ItemType<Mewtwo>(), 1);
                }
            }
            //无论是否需要发放超梦凭证，都尝试恢复快照数据（如无快照则无操作）
            CybCourseWorldGuard.RestoreOnReturn();
        }
    }
}
