using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 四柱套公共层：原版套装机制（日耀护盾与冲刺、星旋隐身、星云增幅、星尘守卫）全部放行，
    /// 单件沿用原版，本族只在其上叠数值与命中机制
    /// </summary>
    internal abstract class GsPillarArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        public sealed override bool KeepsVanillaSetBonus => true;

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();
    }

    /// <summary>
    /// 日耀套：护盾与冲刺照旧；每层日耀护盾额外提供防御 +8 与伤害减免 4%，
    /// 冲刺撞中敌人时额外引发武器面板 2 倍伤害的日耀爆炸
    /// </summary>
    internal class GsSolarArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SolarFlareHelmet];
        public override int BodyID => ItemID.SolarFlareBreastplate;
        public override int LegsID => ItemID.SolarFlareLeggings;

        protected override string SetBonusLineFallback =>
            "Solar shields and the solar dash work as before; each shield also grants 8 defense and 4% damage reduction, and dashing into an enemy triggers a solar explosion for 2x your weapon's damage";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            int shields = Math.Clamp(player.solarShields, 0, 3);
            if (shields <= 0) {
                return;
            }
            player.statDefense += 8 * shields;
            player.endurance += 0.04f * shields;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (player.whoAmI != Main.myPlayer || !player.solarDashing) {
                return;
            }
            int damage = WeaponPanelDamage(player) * 2;
            DashCollide(player, 24f, 30, npc => {
                SpawnBlast(player, npc.Center, damage, 150f, "GodSmithSolarEndow", GsArmorBlastProj.Style.Solar);
            });
        }
    }

    /// <summary>
    /// 星旋套：隐身照旧；隐身中弹药不消耗，远程命中有 40% 概率引发武器面板 1.5 倍伤害的星旋爆裂；
    /// 未隐身时移速 +15%、远程暴击 +10%
    /// </summary>
    internal class GsVortexArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.VortexHelmet];
        public override int BodyID => ItemID.VortexBreastplate;
        public override int LegsID => ItemID.VortexLeggings;

        protected override string SetBonusLineFallback =>
            "Vortex stealth works as before; while stealthed, ammo is never consumed and ranged hits have a 40% chance to trigger a vortex burst for 1.5x your weapon's damage; while visible, 15% increased movement speed and 10% ranged critical strike chance";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            if (player.vortexStealthActive) {
                return;
            }
            player.moveSpeed += 0.15f;
            player.GetCritChance(DamageClass.Ranged) += 10f;
        }

        public override bool? EndowCanConsumeAmmo(Player player, Item weapon, Item ammo) => player.vortexStealthActive ? false : null;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !player.vortexStealthActive
                || !hit.DamageType.CountsAsClass(DamageClass.Ranged) || Main.rand.Next(100) >= 40) {
                return;
            }
            SpawnBlast(player, target.Center, (int)(WeaponPanelDamage(player, hit) * 1.5f), 130f,
                "GodSmithVortexEndow", GsArmorBlastProj.Style.Vortex);
        }
    }

    /// <summary>
    /// 星云套：增幅照旧；每级伤害增幅额外 +5% 魔法伤害，每级生命增幅额外 +2 生命再生，每级魔力增幅额外 -10% 魔耗；
    /// 魔法命中有 25% 概率引发武器面板 1.2 倍伤害的星云脉冲
    /// </summary>
    internal class GsNebulaArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NebulaHelmet];
        public override int BodyID => ItemID.NebulaBreastplate;
        public override int LegsID => ItemID.NebulaLeggings;

        protected override string SetBonusLineFallback =>
            "Nebula boosters work as before; each damage booster level also grants 5% magic damage, each life level 2 life regeneration and each mana level 10% reduced mana usage; magic hits have a 25% chance to trigger a nebula pulse for 1.2x your weapon's damage";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Magic) += 0.05f * Math.Clamp(player.nebulaLevelDamage, 0, 3);
            player.lifeRegen += 2 * Math.Clamp(player.nebulaLevelLife, 0, 3);
            player.manaCost -= 0.10f * Math.Clamp(player.nebulaLevelMana, 0, 3);
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Magic) || Main.rand.Next(100) >= 25) {
                return;
            }
            SpawnBlast(player, target.Center, (int)(WeaponPanelDamage(player, hit) * 1.2f), 120f,
                "GodSmithNebulaEndow", GsArmorBlastProj.Style.Nebula);
        }
    }

    /// <summary>
    /// 星尘套：守卫照旧；召唤栏 +3、召唤伤害 +15%；仆从命中有 20% 概率召来星尘彗屑追向目标，造成 150 点伤害
    /// </summary>
    internal class GsStardustArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.StardustHelmet];
        public override int BodyID => ItemID.StardustBreastplate;
        public override int LegsID => ItemID.StardustLeggings;

        protected override string SetBonusLineFallback =>
            "The Stardust Guardian works as before; +3 minion slots and 15% increased summon damage; minion hits have a 20% chance to call a stardust shard that homes in for 150 damage";

        /// <summary>彗屑伤害</summary>
        private const int ShardDamage = 150;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.maxMinions += 3;
            player.GetDamage(DamageClass.Summon) += 0.15f;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsStardustShardProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Summon) || Main.rand.Next(100) >= 20) {
                return;
            }
            Vector2 from = target.Center + new Vector2(Main.rand.NextFloat(-120f, 120f), -260f);
            Vector2 velocity = (target.Center - from).SafeNormalize(Vector2.UnitY) * 14f;
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithStardustEndow"), from, velocity,
                ModContent.ProjectileType<GsStardustShardProj>(), ShardDamage, 3f, player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>星尘彗屑：借星尘细胞仆从弹贴图，自目标上空俯冲并追向锁定目标（ai[0]）；轨迹撒超亮火把粒子</summary>
    internal class GsStardustShardProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StardustCellMinionShot;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs) {
                NPC target = Main.npc[index];
                if (target.active && !target.friendly) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.8f);
            if (!Main.dedServ) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.UltraBrightTorch,
                    0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.UltraBrightTorch,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}
