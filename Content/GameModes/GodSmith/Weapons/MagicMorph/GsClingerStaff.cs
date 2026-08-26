using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 爬藤怪法杖重铸：小领域「诅咒藤墙」。<br/>
    /// 左键保留原版垂直咒火墙；右键蓄 30t 在光标处沿瞄准方向斜置部署 260px 线形藤墙 8s，
    /// 蓄到 45t 以上追加两端垂直裙墙。墙体命中叠诅咒焰
    /// </summary>
    internal class GsClingerStaff : GsMorphScheme
    {
        public override int TargetItemID => ItemID.ClingerStaff;

        protected override string GsDescFallback =>
            "Reforged: hold right click briefly to lay a 260px cursed vine wall along your aim, at any angle.\nOvercharge it and both ends sprout vertical skirt walls";

        protected override int ChargeTicksB => 30;
        protected override float ChargeManaMult => 1.7f;
        protected override Color ChargeColor => new(128, 230, 76);
        protected override float BaseDamageMult => 1.12f;

        /// <summary>裙墙所需蓄力帧</summary>
        private const int SkirtTicks = 45;

        protected override void FireMorphB(Item item, Player player) {
            Vector2 anchor = Main.MouseWorld;
            if (player.Center.Distance(anchor) > 560f) {
                anchor = player.Center + GsAimUnit(player) * 560f;
            }
            float axisAngle = GsAimUnit(player).ToRotation();
            bool skirt = lastReleaseTicks >= SkirtTicks;
            int dmg = (int)(player.GetWeaponDamage(item) * 0.55f);
            Projectile.NewProjectile(player.GetSource_ItemUse(item), anchor, Vector2.Zero,
                ModContent.ProjectileType<GsClingerWallProj>(), dmg, 1f, player.whoAmI,
                axisAngle, skirt ? 1f : 0f);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.85f, Pitch = -0.35f }, anchor);
        }
    }
}
