using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>教程子世界，无敌/满血RAM/InfiniteHack/补SHPC</summary>
    internal class CybCoursePlayer : ModPlayer
    {
        //SHPC只补主背包0~49
        private const int MainInventoryEnd = 50;
        //兜底扫描间隔(帧)
        private const int EnsureSHPCInterval = 30;
        private int ensureSHPCTick;

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!CybCourseWorld.Active) return;
            modifiers.FinalDamage *= 0f;
            modifiers.DisableSound();
            modifiers.DisableDust();
        }

        public override void PreUpdate() {
            if (!CybCourseWorld.Active) return;

            if (++ensureSHPCTick >= EnsureSHPCInterval) {
                ensureSHPCTick = 0;
                EnsureSHPC();
            }
        }

        public override void PostUpdateEquips() {
            if (!CybCourseWorld.Active) return;
            CalamityOverhaul.Content.HackTimes.HackTime.InfiniteHack = true;
            Player.statLife = Player.statLifeMax2;
            RamSystem.Refill();
        }

        //勿写钱币/弹药槽，ForceEquip交换会丢主武器；满背包放弃
        private void EnsureSHPC() {
            for (int i = 0; i < Player.inventory.Length; i++) {
                if (Player.inventory[i].type == SHPCOverride.ID) {
                    return;
                }
            }
            for (int i = 0; i < MainInventoryEnd; i++) {
                if (Player.inventory[i].IsAir) {
                    Player.inventory[i].SetDefaults(SHPCOverride.ID);
                    return;
                }
            }
        }

        public override void OnEnterWorld() {
            //子世界加载期无效，回主世界再发凭证
            if (CybCourseWorld.Active) return;
            if (CybCourse.TryConsumeGrantMewtwo()) {
                if (!Player.HasItem(ModContent.ItemType<Mewtwo>())) {
                    Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), ModContent.ItemType<Mewtwo>(), 1);
                }
            }
            CybCourseWorldGuard.RestoreOnReturn();
        }
    }
}
