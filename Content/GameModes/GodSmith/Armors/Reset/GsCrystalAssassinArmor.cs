using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 水晶刺客套：单件沿用原版；套装奖励保留冲刺并加移速 +10%，
    /// 冲刺撞中敌人引发武器面板 2 倍伤害的水晶爆裂，每次冲刺后接下来 3 次攻击必定暴击
    /// </summary>
    internal class GsCrystalAssassinArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CrystalNinjaHelmet];
        public override int BodyID => ItemID.CrystalNinjaChestplate;
        public override int LegsID => ItemID.CrystalNinjaLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Grants the ability to dash and 10% increased movement speed; dashing into an enemy bursts crystals for 2x your weapon's damage, and your next 3 attacks after a dash are guaranteed critical strikes";

        /// <summary>自建冲刺持续帧数（原版水晶冲刺约 15 帧）</summary>
        private const int DashDuration = 16;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.dashType = DashID.CrystalAssassin;
            player.moveSpeed += 0.10f;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsArmorDashPlayer dash = player.GetModPlayer<GsArmorDashPlayer>();
            //原版只在冲刺起跳帧把 dashDelay 置为 -1，用它起表，之后按自建计时判定冲刺持续期
            if (player.dashDelay < 0) {
                dash.DashFrames = DashDuration;
                dash.GuaranteedCrits = 3;
            }
            if (dash.DashFrames <= 0) {
                return;
            }
            int damage = WeaponPanelDamage(player) * 2;
            DashCollide(player, 20f, 30, npc => {
                SpawnBlast(player, npc.Center, damage, 100f, "GodSmithCrystalAssassinEndow", GsArmorBlastProj.Style.Crystal);
            });
        }

        public override void ModifyEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            ref NPC.HitModifiers modifiers, Projectile sourceProj) {
            GsArmorDashPlayer dash = player.GetModPlayer<GsArmorDashPlayer>();
            if (dash.GuaranteedCrits <= 0) {
                return;
            }
            dash.GuaranteedCrits--;
            modifiers.SetCrit();
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }
    }
}
