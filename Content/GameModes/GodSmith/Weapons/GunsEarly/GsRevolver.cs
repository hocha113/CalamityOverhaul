using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 左轮手枪「轮盘速转」：赌徒装填。<br/>
    /// 6 膛转轮（逐膛装填可打断）；末发固定 +40%；右键长按 24t 轮盘速转：
    /// 立即随机装回 1~6 发（本地掷骰），恰好 6 发则下一发「正午」+100% 伤、击退 ×2、白闪。
    /// 空匣正常 Reload 40t，装填中右键仍可搏完美窗。<br/>
    /// 账目：周期 150t 打 6 发对原版 6.8 发（0.88），末发均值 1.067，伤害行 ×1.15 → 约 108%；
    /// 轮盘收益随机（期望装回 3.5 发省时），正午是低概率爆点（待游戏内标定）
    /// </summary>
    internal class GsRevolver : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Revolver;

        protected override string GsDescFallback =>
            "Reforged: a six-round cylinder; the final chamber always hits +40% harder.\n" +
            "Hold right-click to spin the wheel: instantly reload a random 1 to 6 rounds.\n" +
            "Roll exactly six and the next shot is High Noon: double damage, double knockback";

        public override int MagSize => 6;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Cylinder;
        protected override float GetRecoil(bool lastRound) => lastRound ? 1.5f : 1f;

        /// <summary>轮盘长按所需帧</summary>
        private const int RouletteHoldTicks = 24;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.15f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (lastRound) {
                damage = (int)(damage * 1.4f);  //末发固定 +40%
            }
            if (mp.noonArmed) {
                damage *= 2;                    //正午：+100%、击退 ×2
                knockback *= 2f;
            }
        }

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ConsumeNoon(mp, position, velocity, false);
            return null;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 1f;   //末发弹打标：飞行拖金屑
            ConsumeNoon(mp, position, velocity, true);
            return null;
        }

        /// <summary>正午一发的出膛演出与标记（1 帧白闪 + 高亮曳光）</summary>
        private void ConsumeNoon(GsGunsEarlyPlayer mp, Vector2 position, Vector2 velocity, bool lastRound) {
            if (!mp.noonArmed) {
                return;
            }
            mp.noonArmed = false;
            pendingMark = 2f;
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.9f, Pitch = 0.4f }, position);
            PRTLoader.NewParticle<PRT_Light>(position + aim * 8f, Vector2.Zero, Color.White, 0.5f)?.Configure(8, 0.9f);
            PRTLoader.NewParticle<PRT_StarPulseRing>(position, Vector2.Zero, Color.White, 0f)?.Configure(0.05f, 0.35f, 8);
        }

        //==================== 右键：轮盘（替代战术装填） ====================

        protected override void OnRightClick(Item item, Player player, GsGunsEarlyPlayer mp) {
            //装填中仍可搏完美窗；未装填时右键按下不做事，轮盘由长按驱动
            if (mp.reloadDuration > 0) {
                base.OnRightClick(item, player, mp);
            }
        }

        protected override void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (mp.reloadDuration > 0 || !player.controlUseTile) {
                mp.rouletteHold = 0;
                return;
            }
            mp.rouletteHold++;
            if (!VaultUtils.isServer && mp.rouletteHold % 8 == 0 && mp.rouletteHold < RouletteHoldTicks) {
                //转轮爬音
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.2f + mp.rouletteHold * 0.02f }, player.Center);
            }
            if (mp.rouletteHold < RouletteHoldTicks) {
                return;
            }
            mp.rouletteHold = 0;
            //赌徒装填：本地掷骰，随机装回 1~6 发（结果只影响本地弹匣，无同步面）
            int roll = Main.rand.Next(1, 7);
            mp.magLeft = roll;
            mp.noonArmed = roll == 6;
            if (VaultUtils.isServer) {
                return;
            }
            if (mp.noonArmed) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.9f, Pitch = 0.5f }, player.Center);
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.2f }, player.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.1f }, player.Center);
                CombatText.NewText(player.getRect(), Color.White, GsGunsEarlyPlayer.HighNoonText.Value);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, Color.White, 0f)
                    ?.Configure(0.05f, 0.5f, 14);
            }
            else {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.75f, Pitch = 0.1f }, player.Center);
                CombatText.NewText(player.getRect(), GameModeTheme.GodSmithAccent, roll.ToString());
            }
        }

        //==================== 逐膛装填音画 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //甩轮 + 抛壳
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.65f, Pitch = -0.3f }, player.Center);
            int shells = MagSize - mp.magLeft;
            for (int i = 0; i < shells; i++) {
                PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 6f, -2f),
                    new Vector2(-player.direction * Main.rand.NextFloat(0.6f, 1.6f), -Main.rand.NextFloat(1.6f, 3f)),
                    new Color(190, 150, 70), Main.rand.NextFloat(0.45f, 0.6f))
                    ?.Configure(new Color(255, 224, 150), Main.rand.Next(22, 32), 0.6f);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //逐膛咔嗒，音阶上行
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.7f, Pitch = -0.25f + 0.09f * roundIndex }, player.Center);
            }
        }

        //==================== 弹幕表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            bool noon = router.MarkData >= 2f;
            if (noon) {
                Lighting.AddLight(proj.Center, 0.4f, 0.38f, 0.3f);
            }
            int interval = noon ? 2 : 4;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.04f, Color.White, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(noon ? Color.White : GameModeTheme.GodSmithEmber, Main.rand.Next(8, 14), 0.1f, 0.6f);
            }
        }
    }
}
