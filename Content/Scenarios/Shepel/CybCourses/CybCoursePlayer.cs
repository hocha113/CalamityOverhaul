using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 教程子世界 ModPlayer：无敌、满血/RAM、InfiniteHack、自动补 SHPC
    /// </summary>
    internal class CybCoursePlayer : ModPlayer
    {
        //SHPC 兜底只允许落在主背包(0~49，含热键栏)，绝不碰钱币槽(50~53)/弹药槽(54~57)
        private const int MainInventoryEnd = 50;
        //刷新背包间隔，避免每帧扫描
        private const int EnsureSHPCInterval = 30;
        private int ensureSHPCTick;

        /// <summary>
        /// 教程子世界内免疫伤害
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
            //血量与 RAM 兜底
            Player.statLife = Player.statLifeMax2;
            RamSystem.Refill();
        }

        /// <summary>
        /// 背包无 SHPC 则补一把：只补到主背包(0~49)第一个空格(循环天然优先热键栏首格)。
        /// 绝不写入钱币/弹药槽、绝不覆盖任何已有物品——否则会被 ForceEquipSHPC 的交换把主武器挤进非法槽位而丢失。
        /// 主背包全满时直接放弃(与原版满背包拾取一致)，待出现空位再补。
        /// </summary>
        private void EnsureSHPC() {
            //已持有(任意可达槽位，含钱币/弹药)就不再补，避免产生重复
            for (int i = 0; i < Player.inventory.Length; i++) {
                if (Player.inventory[i].type == SHPCOverride.ID) {
                    return;
                }
            }
            //只往主背包空格补发，绝不动已有物品
            for (int i = 0; i < MainInventoryEnd; i++) {
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
