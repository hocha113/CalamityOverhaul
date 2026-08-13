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

            //平台悬浮在虚空上，跌出甲板即回收到出生点
            //只回收本端自己的玩家：远端副本交给对方客户端自己处理再同步过来
            if (Player.whoAmI == Main.myPlayer
                && Player.Center.Y > (CybCourseGen.FloorY + 26) * 16f) {
                Player.Center = new Vector2(
                    CybCourseGen.SpawnTileX * 16f + 8f,
                    CybCourseGen.SpawnTileY * 16f - Player.height * 0.5f);
                Player.velocity = Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item6, Player.Center);
            }

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
                    Player.GiveItem(Player.GetSource_GiftOrReward(), ModContent.ItemType<Mewtwo>(), 1);
                }
            }
            CybCourseWorldGuard.RestoreOnReturn();
        }
    }
}
