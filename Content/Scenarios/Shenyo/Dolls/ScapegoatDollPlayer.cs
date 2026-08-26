using CalamityOverhaul.Content.Narrative;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Dolls
{
    /// <summary>
    /// 替死娃娃的持有逻辑:背包生效的挡死结算(镜像 EmblemOfDread 的 PreKill 先例),
    /// 以及娃娃偶尔蹦出来遛弯的节律计时
    /// </summary>
    internal class ScapegoatDollPlayer : ModPlayer
    {
        /// <summary>挡死后的无敌帧</summary>
        private const int BlockImmuneFrames = 180;

        //蹦出遛弯的随机间隔与条件不满足时的重试间隔(帧)
        private const int WalkerIntervalMin = 60 * 90;
        private const int WalkerIntervalMax = 60 * 240;
        private const int WalkerRetryDelay = 60 * 5;

        private int walkerTimer;

        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            int dollType = ModContent.ItemType<ScapegoatDoll>();
            if (!Player.HasItem(dollType)) {
                return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
            }

            //背包扣除只在拥有者本机结算,其余端只取消死亡,等原版背包差量同步
            if (Player.whoAmI == Main.myPlayer) {
                ConsumeOneDoll(dollType);
            }

            Player.GivePlayerImmuneState(BlockImmuneFrames);
            Player.Heal(Player.statLifeMax2);
            if (!Main.dedServ) {
                ShatterFX();
            }

            playSound = false;
            genDust = false;
            return false;
        }

        private void ConsumeOneDoll(int dollType) {
            for (int i = 0; i < Player.inventory.Length; i++) {
                Item item = Player.inventory[i];
                if (item.type != dollType || item.stack <= 0) {
                    continue;
                }
                item.stack--;
                if (item.stack <= 0) {
                    item.TurnToAir();
                }
                return;
            }
        }

        private void ShatterFX() {
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.4f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.35f, Volume = 0.8f }, Player.Center);
            for (int i = 0; i < 26; i++) {
                float rot = MathHelper.TwoPi / 26f * i;
                Vector2 vel = rot.ToRotationVector2() * Main.rand.NextFloat(1.6f, 4.2f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.Shadowflame, vel, 100,
                    default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Smoke,
                    0f, -1.2f, 120, default, Main.rand.NextFloat(0.8f, 1.3f));
            }
            CombatText.NewText(Player.getRect(), new Color(186, 145, 255), ScapegoatDoll.ShatterText.Value, dramatic: true);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (!Player.HasItem(ModContent.ItemType<ScapegoatDoll>())) {
                return;
            }

            //首次持有(或计时器尚未初始化)时先摇一个间隔
            if (walkerTimer <= 0) {
                walkerTimer = Main.rand.Next(WalkerIntervalMin, WalkerIntervalMax);
                return;
            }

            walkerTimer--;
            if (walkerTimer > 0) {
                return;
            }

            if (!CanPopOut()) {
                walkerTimer = WalkerRetryDelay;
                return;
            }

            SpawnWalker();
            walkerTimer = Main.rand.Next(WalkerIntervalMin, WalkerIntervalMax);
        }

        private bool CanPopOut() {
            if (Player.dead || Player.mount.Active || Player.velocity.Y != 0f) {
                return false;
            }
            if (CWRWorld.HasBoss || NarrativeTriggerGate.IsBusy) {
                return false;
            }
            return Player.ownedProjectileCounts[ModContent.ProjectileType<ScapegoatDollWalker>()] <= 0;
        }

        private void SpawnWalker() {
            int dir = Main.rand.NextBool() ? 1 : -1;
            Vector2 vel = new(dir * Main.rand.NextFloat(1.4f, 2.2f), -Main.rand.NextFloat(4.6f, 5.8f));
            Projectile.NewProjectile(Player.GetSource_Misc("ScapegoatDollWalker"), Player.Center, vel,
                ModContent.ProjectileType<ScapegoatDollWalker>(), 0, 0f, Player.whoAmI);
        }
    }
}
