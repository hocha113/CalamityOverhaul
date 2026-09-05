using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 死灵套（含远古死灵头盔）：三件各伤害 +5%，头盔另给最大魔力 +100 与魔法暴击 +5%，
    /// 胸甲另给暴击 +10% 与魔法伤害 +5%，护胫另给穿甲 +10 与魔耗 -20%；
    /// 套装奖励为造成伤害时 60% 概率掷出 1 到 2 根骸骨（10 点伤害，受重力，可穿透两次）
    /// </summary>
    internal class GsNecroArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NecroHelmet, ItemID.AncientNecroHelmet];
        public override int BodyID => ItemID.NecroBreastplate;
        public override int LegsID => ItemID.NecroGreaves;

        protected override string HeadLineFallback => "5% increased damage, 100 more maximum mana and 5% increased magic critical strike chance";
        protected override string BodyLineFallback => "5% increased damage, 10% increased critical strike chance and 5% increased magic damage";
        protected override string LegsLineFallback => "5% increased damage, 10 armor penetration and 20% reduced mana usage";
        protected override string SetBonusLineFallback =>
            "Dealing damage has a 60% chance to hurl 1 to 2 bones that deal 10 damage, fall with gravity and pierce twice";

        /// <summary>骸骨伤害</summary>
        private const int BoneDamage = 10;

        public override void UpdateHead(Player player, Item item) {
            player.GetDamage(DamageClass.Generic) += 0.05f;
            player.statManaMax2 += 100;
            player.GetCritChance(DamageClass.Magic) += 5f;
        }

        public override void UpdateBody(Player player, Item item) {
            player.GetDamage(DamageClass.Generic) += 0.05f;
            player.GetCritChance(DamageClass.Generic) += 10f;
            player.GetDamage(DamageClass.Magic) += 0.05f;
        }

        public override void UpdateLegs(Player player, Item item) {
            player.GetDamage(DamageClass.Generic) += 0.05f;
            player.GetArmorPenetration(DamageClass.Generic) += 10f;
            player.manaCost -= 0.20f;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsNecroBoneProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= 60) {
                return;
            }
            int count = Main.rand.Next(1, 3);
            Vector2 toTarget = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            //抛物线：朝目标方向抛出并略微抬手，飞行途中受重力落回
            float dist = player.Center.Distance(target.Center);
            float speed = MathHelper.Clamp(dist / 28f, 8f, 14f);
            for (int i = 0; i < count; i++) {
                Vector2 velocity = toTarget * speed + new Vector2(0f, -3f - dist * 0.01f);
                velocity = velocity.RotatedByRandom(0.12f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithNecroEndow"), player.Center, velocity,
                    ModContent.ProjectileType<GsNecroBoneProj>(), BoneDamage, 2f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 死灵骸骨：借骨手套骸骨贴图，受重力抛飞、随速自转，可穿透两次（最多命中三次），触地即碎；碎裂只用原版骨屑粒子
    /// </summary>
    internal class GsNecroBoneProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneGloveProj;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 16f);
            Projectile.rotation += Projectile.velocity.X * 0.08f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, MaxInstances = 3 }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Bone,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f));
                dust.noGravity = false;
            }
        }
    }
}
