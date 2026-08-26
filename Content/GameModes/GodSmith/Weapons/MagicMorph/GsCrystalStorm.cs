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
    /// 水晶风暴重铸：小领域二形态。<br/>
    /// A 形态保留高速连射；B 形态（右键蓄 60t，领域开销高）「晶暴领域」：
    /// 光标处开 3s 折射旋涡，自机碎晶穿域获得 +30% 弹速与穿透 +1（弹道折射感）
    /// </summary>
    internal class GsCrystalStorm : GsMorphScheme
    {
        public override int TargetItemID => ItemID.CrystalStorm;

        protected override string GsDescFallback =>
            "Reforged: hold right click to charge a refraction vortex at the cursor.\nYour crystal shards passing through it refract, gaining speed and one extra pierce";

        protected override int ChargeTicksB => 60;
        //晶暴领域是一次性大开销：原版单发蓝耗很低，此处折算约 35 蓝
        protected override float ChargeManaMult => 7f;
        protected override Color ChargeColor => new(232, 122, 200);
        protected override float BaseDamageMult => 1.05f;

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.9f, Pitch = -0.3f }, player.Center);
            Vector2 anchor = Main.MouseWorld;
            if (player.Center.Distance(anchor) > 600f) {
                anchor = player.Center + GsAimUnit(player) * 600f;
            }
            if (!GsDomainProj.TryMigrate<GsCrystalDomainProj>(player, anchor)) {
                SpawnMorph(player, item, anchor, Vector2.Zero,
                    ModContent.ProjectileType<GsCrystalDomainProj>(), 1, 0f, KindB);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.CrystalStorm) {
                return;
            }
            //穿域折射：owner 端每 4t 采样一次防性能热点；增益一次性，随 netUpdate 过线
            if (proj.owner == Main.myPlayer && router.MarkData2 == 0f && proj.timeLeft % 4 == 0) {
                int domainType = ModContent.ProjectileType<GsCrystalDomainProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile domain = Main.projectile[i];
                    if (!domain.active || domain.type != domainType || domain.owner != proj.owner) {
                        continue;
                    }
                    if (proj.Center.DistanceSQ(domain.Center) < GsCrystalDomainProj.Radius * (float)GsCrystalDomainProj.Radius) {
                        router.MarkData2 = 1f;
                        proj.velocity *= 1.3f;
                        if (proj.penetrate > 0) {
                            proj.penetrate++;
                        }
                        proj.netUpdate = true;
                        break;
                    }
                }
            }
            //折射后的碎晶带晶闪尾（各端按 MarkData2 渲染）
            if (!VaultUtils.isServer && router.MarkData2 >= 1f && proj.timeLeft % 3 == 0) {
                Color c = new(255, 190, 235);
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, -proj.velocity * 0.08f, c, 0.2f)
                    ?.Configure(c, 9, 0.2f, 0.8f);
            }
        }
    }
}
