using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 肉后六套矿石甲的公共层：三顶头盔的原版单件属性照旧（职业分化保留），只把套装奖励换成本族定义；
    /// 头盔同样可镶嵌低一档头盔（钴/钯金→秘银/山铜→精金/钛金），整套穿好后继承其套装奖励
    /// </summary>
    internal abstract class GsHardmodeOreArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;
    }

    /// <summary>钴套：所有伤害 +15 固定、所有武器攻速 +15%、魔耗 -20%，攻击时 30% 概率回复 30 魔力</summary>
    internal class GsCobaltArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CobaltHelmet, ItemID.CobaltHat, ItemID.CobaltMask];
        public override int BodyID => ItemID.CobaltBreastplate;
        public override int LegsID => ItemID.CobaltLeggings;
        protected override string SetBonusLineFallback =>
            "All damage increased by 15, 15% increased attack speed for all weapons, 20% reduced mana usage; attacks have a 30% chance to restore 30 mana";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Generic).Flat += 15f;
            player.GetAttackSpeed(DamageClass.Generic) += 0.15f;
            player.manaCost -= 0.20f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= 30 || player.statMana >= player.statManaMax2) {
                return;
            }
            int restored = Math.Min(30, player.statManaMax2 - player.statMana);
            player.statMana += restored;
            player.ManaEffect(restored);
        }
    }

    /// <summary>钯金套：防御 +10%、召唤栏 +2，代价伤害 -20 固定；攻击时将 10% 伤害转为治疗（上限 25，每 5 秒一次）</summary>
    internal class GsPalladiumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PalladiumHelmet, ItemID.PalladiumHeadgear, ItemID.PalladiumMask];
        public override int BodyID => ItemID.PalladiumBreastplate;
        public override int LegsID => ItemID.PalladiumLeggings;
        protected override string SetBonusLineFallback =>
            "10% more defense and +2 minion slots, but all damage reduced by 20; attacks heal you for 10% of the damage dealt (up to 25, once every 5 seconds)";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.statDefense *= 1.10f;
            player.maxMinions += 2;
            player.GetDamage(DamageClass.Generic).Flat -= 20f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            HealOnHit(player, state, damageDone, 0.10f, 25, 300);
        }
    }

    /// <summary>秘银套：攻击附带灵液、诅咒焰与着火，暴击 +10%，伤害 +10%</summary>
    internal class GsMythrilArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MythrilHelmet, ItemID.MythrilHood, ItemID.MythrilHat];
        public override int BodyID => ItemID.MythrilChainmail;
        public override int LegsID => ItemID.MythrilGreaves;
        protected override string SetBonusLineFallback =>
            "Attacks inflict Ichor, Cursed Inferno and On Fire!; 10% increased critical strike chance and 10% increased damage";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetCritChance(DamageClass.Generic) += 10f;
            player.GetDamage(DamageClass.Generic) += 0.10f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            target.AddBuff(BuffID.Ichor, 300);
            target.AddBuff(BuffID.CursedInferno, 300);
            target.AddBuff(BuffID.OnFire, 300);
        }
    }

    /// <summary>山铜套：召唤栏 +1；攻击时 70% 概率迸出 2 到 3 片追踪樱花，各 100 伤害并可穿透两次</summary>
    internal class GsOrichalcumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.OrichalcumHelmet, ItemID.OrichalcumHeadgear, ItemID.OrichalcumMask];
        public override int BodyID => ItemID.OrichalcumBreastplate;
        public override int LegsID => ItemID.OrichalcumLeggings;
        protected override string SetBonusLineFallback =>
            "+1 minion slot; attacks have a 70% chance to release 2 to 3 homing petals that deal 100 damage and pierce twice";

        /// <summary>樱花伤害</summary>
        private const int PetalDamage = 100;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.maxMinions += 1;

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsOrichalcumPetalProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= 70) {
                return;
            }
            int count = Main.rand.Next(2, 4);
            for (int i = 0; i < count; i++) {
                //自目标身侧迸出，先散开再追踪
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 9f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithOrichalcumEndow"),
                    target.Center + velocity * 3f, velocity,
                    ModContent.ProjectileType<GsOrichalcumPetalProj>(), PetalDamage, 2f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 钛金套：击中敌人获得钛金屏障（原版机制），钛金碎片环绕守护；碎片越多暴击最多 +15%、伤害最多 +10%，
    /// 受伤后屏障与碎片立即消散
    /// </summary>
    internal class GsTitaniumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TitaniumHelmet, ItemID.TitaniumHeadgear, ItemID.TitaniumMask];
        public override int BodyID => ItemID.TitaniumBreastplate;
        public override int LegsID => ItemID.TitaniumLeggings;
        protected override string SetBonusLineFallback =>
            "Hitting an enemy raises the Titanium Barrier and summons titanium shards around you; more shards grant up to 15% critical strike chance and 10% damage, but the barrier shatters the moment you take damage";

        /// <summary>原版屏障的碎片上限</summary>
        private const int MaxShards = 7;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.onHitTitaniumStorm = true;
            int shards = Math.Min(MaxShards, player.ownedProjectileCounts[ProjectileID.TitaniumStormShard]);
            if (shards <= 0) {
                return;
            }
            float t = shards / (float)MaxShards;
            player.GetCritChance(DamageClass.Generic) += 15f * t;
            player.GetDamage(DamageClass.Generic) += 0.10f * t;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            player.ClearBuff(BuffID.TitaniumStorm);
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == ProjectileID.TitaniumStormShard) {
                    proj.Kill();
                }
            }
        }
    }

    /// <summary>精金套：召唤栏 +3、移速 +15%，代价魔耗 +15%</summary>
    internal class GsAdamantiteArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.AdamantiteHelmet, ItemID.AdamantiteHeadgear, ItemID.AdamantiteMask];
        public override int BodyID => ItemID.AdamantiteBreastplate;
        public override int LegsID => ItemID.AdamantiteLeggings;
        protected override string SetBonusLineFallback => "+3 minion slots and 15% increased movement speed, but 15% increased mana usage";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.maxMinions += 3;
            player.moveSpeed += 0.15f;
            player.manaCost += 0.15f;
        }
    }

    /// <summary>
    /// 山铜樱花：借原版花瓣贴图，出手短暂散开后咬向最近敌人，可穿透两次并对同一目标反复命中；
    /// 消散只用原版粉色花瓣粒子
    /// </summary>
    internal class GsOrichalcumPetalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>散开段帧数，之后开始追踪</summary>
        private const int ScatterFrames = 8;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.06f, 0.18f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            Projectile.rotation += 0.25f;
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy,
                    0f, 0f, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 600f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
