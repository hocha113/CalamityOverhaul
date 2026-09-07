using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 蘑菇套 · 孢子箭塔（远程，三顶头盔）。单件与原版潜行机制照旧（放行原版套装奖励）。<br/>
    /// 原版旗标清点：潜行（shroomiteStealth）→ 原版 UpdateArmorSets 照常执行；无删除项。另叠远程暴击 +15%、弹药 30% 不消耗。<br/>
    /// 签名：深度潜行时远程命中 30% 概率（冷却半秒）在目标脚下长出一株孢子蘑菇，1.5 秒内向目标喷三发追踪孢子
    /// </summary>
    internal class GsShroomiteArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShroomiteHeadgear, ItemID.ShroomiteMask, ItemID.ShroomiteHelmet];
        public override int BodyID => ItemID.ShroomiteBreastplate;
        public override int LegsID => ItemID.ShroomiteLeggings;
        public override bool OverridesPieceStats => false;
        public override bool KeepsVanillaSetBonus => true;

        protected override string SetBonusLineFallback =>
            "Standing still turns you stealthy as before; 15% increased ranged critical strike chance and a 30% chance not to consume ammo; while deep in stealth, ranged hits have a 30% chance to sprout a spore turret under the target that fires three homing spores";

        /// <summary>潜行阈值：原版 stealth 从 1 降到 0，越低越隐蔽</summary>
        private const float DeepStealth = 0.3f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetCritChance(DamageClass.Ranged) += 15f;
        }

        public override bool? EndowCanConsumeAmmo(Player player, Item weapon, Item ammo)
            => weapon.DamageType.CountsAsClass(DamageClass.Ranged) && Main.rand.Next(100) < 30 ? false : null;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || player.stealth > DeepStealth || target.life <= 0 || target.type == NPCID.TargetDummy
                || !hit.DamageType.CountsAsClass(DamageClass.Ranged) || Main.rand.Next(100) >= 30 || !state.TryUseCooldown(this, 30)) {
                return;
            }
            //箭塔长在目标脚下的实地上；找不到地面就长在脚底
            GsArmorTerrainProbe.TryFindGroundBelow(target.Bottom, 10, out float groundY);
            Vector2 root = new(target.Center.X + Main.rand.NextFloat(-24f, 24f), Math.Max(groundY, target.Bottom.Y) - 12f);
            SpawnProc(player, "GodSmithShroomiteEndow", root, Vector2.Zero,
                ModContent.ProjectileType<GsShroomiteArmorTurretProj>(), ProcDamage(damageDone, 0.3f, 10, 60), 1f, target.whoAmI);
        }
    }

    /// <summary>
    /// 孢子蘑菇塔：借松露孢子贴图放大作菌盖，出土生长 20 帧后每 25 帧朝锁定目标（ai[0]）喷一发孢子，共三发，随后凋散；
    /// 只在 owner 端出弹
    /// </summary>
    internal class GsShroomiteArmorTurretProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TruffleSpore;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        private const int GrowFrames = 20;
        private const int FireInterval = 25;
        private const int Shots = 3;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GrowFrames + FireInterval * Shots + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.8f);
            if (Life <= GrowFrames) {
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GlowingMushroom, 0f, -1f, 100, default, 0.9f);
                    dust.noGravity = true;
                }
                return;
            }
            int sinceGrown = (int)Life - GrowFrames;
            if (sinceGrown % FireInterval != 1 || sinceGrown / FireInterval >= Shots) {
                return;
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            }
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            Vector2 aim = target != null && target.active && !target.friendly
                ? (target.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY)
                : -Vector2.UnitY;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, aim.RotatedByRandom(0.15f) * 9f,
                ModContent.ProjectileType<GsShroomiteArmorSporeProj>(), Projectile.damage, 1f, Projectile.owner, TargetIndex);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GlowingMushroom,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, 0f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //菌盖：生长期从地里冒出并放大
            float grow = MathHelper.Clamp(Life / GrowFrames, 0f, 1f);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Bottom - Main.screenPosition, null, lightColor, 0f, origin,
                1.6f * grow, SpriteEffects.None);
            return false;
        }
    }

    /// <summary>孢子弹：借松露孢子贴图，飞向锁定目标（ai[0]）并追踪，命中即散</summary>
    internal class GsShroomiteArmorSporeProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TruffleSpore;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs) {
                NPC target = Main.npc[index];
                if (target.active && !target.friendly) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.1f);
                }
            }
            Projectile.rotation += 0.15f;
            Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.6f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GlowingMushroom, 0f, 0f, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GlowingMushroom,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
