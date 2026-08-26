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
    /// 雨云魔棒重铸：小领域「积雨权柄」。<br/>
    /// 原版双云保留；两云齐备时自动在其间架起「雨幕走廊」（伤害线带 + 温和减速）；
    /// 右键瞬发切换温雨（纯伤害）/雷雨（走廊周期落雷）两种形态。
    /// 开云蓝耗为原版 2.5 倍
    /// </summary>
    internal class GsNimbusRod : GsMorphScheme
    {
        public override int TargetItemID => ItemID.NimbusRod;

        protected override string GsDescFallback =>
            "Reforged: with both clouds afield, a rain corridor bridges them, soaking and slowing foes inside.\nRight click toggles warm rain or thunderstorm; the storm calls down bolts along the corridor";

        protected override float BaseDamageMult => 1.10f;

        /// <summary>开云 25 蓝（原版 10 的 2.5 倍）</summary>
        public override void GsModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
            => mult *= 2.5f;

        /// <summary>右键：瞬发切换温雨/雷雨形态（owner 本地偏好，落雷产物全端可见）</summary>
        protected override void OnAltTrigger(Item item, Player player) {
            GsMorphPlayer morph = player.GetModPlayer<GsMorphPlayer>();
            morph.NimbusStorm = !morph.NimbusStorm;
            SoundEngine.PlaySound(morph.NimbusStorm
                ? SoundID.Thunder with { Volume = 0.3f, Pitch = 0.5f }
                : SoundID.Item8 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
            Color tone = morph.NimbusStorm ? new Color(150, 200, 255) : new Color(140, 165, 200);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(20f, 20f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f), tone, 0.16f)?.Configure(14, 0.8f);
            }
        }

        /// <summary>本武器无蓄力形态，右键已改为瞬发切换</summary>
        protected override void FireMorphB(Item item, Player player) { }

        protected override void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.RainCloudMoving && proj.type != ProjectileID.RainCloudRaining) {
                return;
            }
            //新云落位后若 owner 已有两朵云且走廊未在场，则架设走廊（雨滴伤害基准 ×0.35）
            int clouds = 0;
            bool hasCorridor = false;
            int corridorType = ModContent.ProjectileType<GsNimbusCorridorProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != proj.owner) {
                    continue;
                }
                if (p.type == ProjectileID.RainCloudMoving || p.type == ProjectileID.RainCloudRaining) {
                    clouds++;
                }
                else if (p.type == corridorType) {
                    hasCorridor = true;
                }
            }
            if (clouds >= 2 && !hasCorridor) {
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                    corridorType, (int)(proj.damage * 0.35f), 0.5f, proj.owner);
            }
        }
    }
}
