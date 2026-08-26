using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 流星法杖重铸：标准二形态。<br/>
    /// A 形态流星落点残留 1.2s 熔坑（踩踏受灼）；
    /// B 形态（右键蓄 50t）「陨星雨」：光标横向 240px 均布撒 5 连流星
    /// </summary>
    internal class GsMeteorStaff : GsMorphScheme
    {
        public override int TargetItemID => ItemID.MeteorStaff;

        protected override string GsDescFallback =>
            "Reforged: meteors leave a scorching crater where they land.\nHold right click to charge; release to call a five-meteor barrage across the cursor";

        protected override int ChargeTicksB => 50;
        protected override float ChargeManaMult => 2.0f;
        protected override Color ChargeColor => new(255, 150, 70);
        protected override float BaseDamageMult => 1.08f;

        private static bool IsMeteor(int type)
            => type == ProjectileID.Meteor1 || type == ProjectileID.Meteor2 || type == ProjectileID.Meteor3;

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.9f, Pitch = -0.2f }, player.Center);
            Vector2 cursor = Main.MouseWorld;
            int dmg = (int)(player.GetWeaponDamage(item) * 0.6f);
            for (int i = 0; i < 5; i++) {
                //横向 240px 均布，出生位沿用原版天降口径（目标上方屏外）
                float offX = MathHelper.Lerp(-120f, 120f, i / 4f);
                Vector2 target = cursor + new Vector2(offX, 0f);
                Vector2 spawn = target + new Vector2(Main.rand.NextFloat(-40f, 40f), -560f - Main.rand.NextFloat(80f));
                Vector2 vel = (target - spawn).SafeNormalize(Vector2.UnitY) * 14f;
                int type = ProjectileID.Meteor1 + Main.rand.Next(3);
                SpawnMorph(player, item, spawn, vel, type, dmg, item.knockBack, KindB, i);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (!IsMeteor(proj.type) || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //落点熔坑（真弹幕残留物，可并存；伤害 ×0.2 折算）
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsScorchDomainProj>(),
                (int)MathHelper.Max(1f, proj.damage * 0.2f), 0f, proj.owner);
        }
    }
}
