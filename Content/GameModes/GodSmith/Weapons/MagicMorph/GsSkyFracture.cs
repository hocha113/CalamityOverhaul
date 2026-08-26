using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 天空裂痕重铸：标准二形态。<br/>
    /// A 形态魔剑出手带裂隙闪；
    /// B 形态（右键蓄 45t）「裂空阵」：光标品字开三道裂口，0.5s 后各射一剑汇聚光标
    /// </summary>
    internal class GsSkyFracture : GsMorphScheme
    {
        public override int TargetItemID => ItemID.SkyFracture;

        protected override string GsDescFallback =>
            "Reforged: blades tear a flash of fracture as they launch.\nHold right click to charge; release to open three rifts around the cursor that loose converging blades";

        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.8f;
        protected override Color ChargeColor => new(150, 220, 255);
        protected override float BaseDamageMult => 1.08f;

        /// <summary>品字三裂口相对光标的偏置</summary>
        private static readonly Vector2[] RiftOffsets = [new(0f, -74f), new(-64f, 52f), new(64f, 52f)];

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.8f, Pitch = 0.15f }, player.Center);
            Vector2 focus = Main.MouseWorld;
            if (player.Center.Distance(focus) > 620f) {
                focus = player.Center + GsAimUnit(player) * 620f;
            }
            int dmg = (int)(player.GetWeaponDamage(item) * 1.1f);
            foreach (Vector2 off in RiftOffsets) {
                SpawnMorphRift(player, item, focus + off, focus, dmg);
            }
        }

        /// <summary>生成一道裂口（打标 KindB，汇聚点走 ai 随生成包首发，剑经承签继承档位）</summary>
        private void SpawnMorphRift(Player player, Item item, Vector2 pos, Vector2 focus, int damage)
            => SpawnMorph(player, item, pos, Vector2.Zero,
                ModContent.ProjectileType<GsRiftProj>(), damage, item.knockBack, KindB,
                0f, focus.X, focus.Y);

        protected override void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.SkyFracture || VaultUtils.isServer) {
                return;
            }
            //出手裂隙闪（施法者视角反馈，3 粒）
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(
                    proj.Center + Main.rand.NextVector2Circular(10f, 10f),
                    proj.velocity * 0.08f, new Color(160, 225, 255),
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(10, 16), 0.15f);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.SkyFracture || VaultUtils.isServer) {
                return;
            }
            //B 档剑的汇聚辉尾（各端低频）
            if (KindOf(router) == KindB && proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, new Color(150, 220, 255), 0.22f)
                    ?.Configure(new Color(150, 220, 255), 10, 0.15f, 0.8f);
            }
        }
    }
}
