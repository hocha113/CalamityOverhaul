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
    /// 单件沿用原版，本族只在其上叠数值与各柱专属形态的命中机制。<br/>
    /// 原版旗标清点：四柱机制 → 原版 UpdateArmorSets 照常执行；无删除项
    /// </summary>
    internal abstract class GsPillarArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        public sealed override bool KeepsVanillaSetBonus => true;
    }

    /// <summary>
    /// 日耀套 · 日冕撞击（近战）：护盾与冲刺照旧；每层日耀护盾额外提供防御 +8 与伤害减免 4%，
    /// 冲刺撞中敌人时引发武器面板 2 倍伤害的日耀爆炸（族内唯一保留爆炸模板的物理撞击）
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
    /// 星旋套 · 星旋电链（远程）：隐身照旧；隐身中弹药不消耗，远程命中有 40% 概率向最近的另一名敌人放出星旋电链；
    /// 未隐身时移速 +15%、远程暴击 +10%
    /// </summary>
    internal class GsVortexArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.VortexHelmet];
        public override int BodyID => ItemID.VortexBreastplate;
        public override int LegsID => ItemID.VortexLeggings;

        protected override string SetBonusLineFallback =>
            "Vortex stealth works as before; while stealthed, ammo is never consumed and ranged hits have a 40% chance to chain vortex lightning to the nearest other enemy for 70% of the hit; while visible, 15% increased movement speed and 10% ranged critical strike chance";

        private const float ChainRange = 360f;

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
            NPC second = NearestEnemy(target.Center, ChainRange, target.whoAmI);
            if (second == null) {
                return;
            }
            SpawnProc(player, "GodSmithVortexEndow", second.Center, Vector2.Zero,
                ModContent.ProjectileType<GsVortexArmorArcProj>(), ProcDamage(damageDone, 0.7f, 20, 150), 4f,
                target.Center.X, target.Center.Y);
        }
    }

    /// <summary>
    /// 星云套 · 星云绽放（魔法）：增幅照旧；每级伤害增幅额外 +5% 魔法伤害，每级生命增幅额外 +2 生命再生，每级魔力增幅额外 −10% 魔耗；
    /// 魔法命中有 25% 概率在目标处开出一朵星云花，脉冲两次
    /// </summary>
    internal class GsNebulaArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NebulaHelmet];
        public override int BodyID => ItemID.NebulaBreastplate;
        public override int LegsID => ItemID.NebulaLeggings;

        protected override string SetBonusLineFallback =>
            "Nebula boosters work as before; each damage booster level also grants 5% magic damage, each life level 2 life regeneration and each mana level 10% reduced mana usage; magic hits have a 25% chance to bloom a nebula flower on the target that pulses twice for 40% of the hit";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Magic) += 0.05f * Math.Clamp(player.nebulaLevelDamage, 0, 3);
            player.lifeRegen += 2 * Math.Clamp(player.nebulaLevelLife, 0, 3);
            player.manaCost -= 0.10f * Math.Clamp(player.nebulaLevelMana, 0, 3);
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Magic)
                || target.life <= 0 || Main.rand.Next(100) >= 25 || !state.TryUseCooldown(this, 30)) {
                return;
            }
            SpawnProc(player, "GodSmithNebulaEndow", target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsNebulaArmorBloomProj>(), ProcDamage(damageDone, 0.4f, 15, 120), 2f, target.whoAmI);
        }
    }

    /// <summary>
    /// 星尘套 · 星尘彗屑（召唤）：守卫照旧；召唤栏 +3、召唤伤害 +15%；仆从命中有 20% 概率召来星尘彗屑追向目标
    /// </summary>
    internal class GsStardustArmor : GsPillarArmorScheme
    {
        public override int[] HeadIDs => [ItemID.StardustHelmet];
        public override int BodyID => ItemID.StardustBreastplate;
        public override int LegsID => ItemID.StardustLeggings;

        protected override string SetBonusLineFallback =>
            "The Stardust Guardian works as before; +3 minion slots and 15% increased summon damage; minion hits have a 20% chance to call a stardust shard that homes in for 80% of the hit";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.maxMinions += 3;
            player.GetDamage(DamageClass.Summon) += 0.15f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Summon)
                || target.life <= 0 || Main.rand.Next(100) >= 20) {
                return;
            }
            Vector2 from = GsArmorTerrainProbe.SkySpawnAbove(target.Center, Main.rand.NextFloat(-120f, 120f), 260f);
            Vector2 velocity = (target.Center - from).SafeNormalize(Vector2.UnitY) * 14f;
            SpawnProc(player, "GodSmithStardustEndow", from, velocity,
                ModContent.ProjectileType<GsStardustShardProj>(), ProcDamage(damageDone, 0.8f, 20, 200), 3f, target.whoAmI);
        }
    }

    /// <summary>星旋电链：出生在第二名敌人身上的一帧判定体，用星旋尘在原目标（ai[0], ai[1]）与自身之间铺一道闪电</summary>
    internal class GsVortexArmorArcProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 4;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] != 0f || Main.dedServ) {
                return;
            }
            Projectile.localAI[0] = 1f;
            Vector2 from = new(Projectile.ai[0], Projectile.ai[1]);
            SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.4f, MaxInstances = 3 }, Projectile.Center);
            int segments = Math.Max(3, (int)(from.Distance(Projectile.Center) / 28f));
            Vector2 prev = from;
            Vector2 normal = (Projectile.Center - from).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = 1; i <= segments; i++) {
                Vector2 next = Vector2.Lerp(from, Projectile.Center, i / (float)segments);
                if (i < segments) {
                    next += normal * Main.rand.NextFloat(-14f, 14f);
                }
                int count = Math.Max(2, (int)(prev.Distance(next) / 6f));
                for (int k = 0; k <= count; k++) {
                    Dust dust = Dust.NewDustPerfect(Vector2.Lerp(prev, next, k / (float)count), DustID.Vortex, Vector2.Zero, 0, default, 1f);
                    dust.noGravity = true;
                }
                prev = next;
            }
            for (int i = 0; i < 10; i++) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Vortex,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 0, default, 1.2f);
                spark.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>星云花：借星云烈焰贴图（4 帧）驻在目标身上（ai[0]）随其移动，40 帧内绽放两次脉冲，随后散作紫光</summary>
    internal class GsNebulaArmorBloomProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NebulaBlaze2;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.NebulaBlaze2]);
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs && Main.npc[index].active) {
                Projectile.Center = Main.npc[index].Center;
            }
            Projectile.velocity = Vector2.Zero;
            //两次脉冲：缩放随时间呼吸，判定靠 localNPCHitCooldown 自然分成两拍
            Projectile.scale = 1.2f + 0.5f * MathF.Abs(MathF.Sin(Life * MathHelper.Pi / 20f));
            Projectile.rotation += 0.05f;
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
            Lighting.AddLight(Projectile.Center, 0.6f, 0.2f, 0.7f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch, 0f, -1f, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.35f, MaxInstances = 3 }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>星尘彗屑：借星尘细胞仆从弹贴图，自目标上空俯冲并追向锁定目标（ai[0]）；轨迹撒超亮火把粒子</summary>
    internal class GsStardustShardProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StardustCellMinionShot;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            //原版细胞弹是竖排四帧，不声明帧数会整条竖图一起画
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.StardustCellMinionShot]);
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
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
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
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
