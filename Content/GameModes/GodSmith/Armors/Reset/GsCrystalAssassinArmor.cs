using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 水晶刺客套 · 水晶迸裂（通用）。单件沿用原版。<br/>
    /// 原版旗标清点：+10% 伤害 / +10% 暴击 / 冲刺（dashType）→ 原样补回；无删除项。<br/>
    /// 签名：冲刺撞中敌人时从它身上迸出 5 片水晶碎片（伤害跟随手持武器职业），每次冲刺后接下来 3 次攻击必定暴击
    /// </summary>
    internal class GsCrystalAssassinArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CrystalNinjaHelmet];
        public override int BodyID => ItemID.CrystalNinjaChestplate;
        public override int LegsID => ItemID.CrystalNinjaLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% increased damage and critical strike chance, and the ability to dash; dashing into an enemy bursts 5 crystal shards out of it, and your next 3 attacks after a dash are guaranteed critical strikes";

        /// <summary>自建冲刺持续帧数（原版水晶冲刺约 15 帧）</summary>
        private const int DashDuration = 16;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Generic) += 0.10f;
            player.GetCritChance(DamageClass.Generic) += 10f;
            player.dashType = DashID.CrystalAssassin;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsArmorDashPlayer dash = player.GetModPlayer<GsArmorDashPlayer>();
            //原版只在冲刺起跳帧把 dashDelay 置为 -1，用它起表，之后按自建计时判定冲刺持续期
            if (player.dashDelay < 0) {
                dash.DashFrames = DashDuration;
                dash.GuaranteedCrits = 3;
            }
            if (dash.DashFrames <= 0) {
                return;
            }
            Item held = player.HeldItem;
            DamageClass heldClass = held != null && !held.IsAir && held.damage > 0 ? held.DamageType : DamageClass.Generic;
            int damage = Math.Max(8, (int)(WeaponPanelDamage(player) * 0.5f));
            DashCollide(player, 20f, 30, npc => {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f }, npc.Center);
                for (int i = 0; i < 5; i++) {
                    Vector2 velocity = (MathHelper.TwoPi * i / 5f + Main.rand.NextFloat(-0.2f, 0.2f)).ToRotationVector2() * Main.rand.NextFloat(7f, 9f);
                    Projectile shard = SpawnProc(player, "GodSmithCrystalAssassinEndow", npc.Center, velocity,
                        ModContent.ProjectileType<GsCrystalArmorShardProj>(), damage, 3f);
                    if (shard != null) {
                        shard.DamageType = heldClass;
                    }
                }
            });
        }

        public override void ModifyEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            ref NPC.HitModifiers modifiers, Projectile sourceProj) {
            GsArmorDashPlayer dash = player.GetModPlayer<GsArmorDashPlayer>();
            if (dash.GuaranteedCrits <= 0) {
                return;
            }
            dash.GuaranteedCrits--;
            modifiers.SetCrit();
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>水晶碎片：借水晶风暴碎片贴图，自撞击点向外迸出后受轻微重力下坠，可穿透一次；出生 4 帧内免地形碰撞</summary>
    internal class GsCrystalArmorShardProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalStorm;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > 4f) {
                Projectile.tileCollide = true;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.15f, 12f);
            Projectile.rotation += 0.3f;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.6f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkCrystalShard,
                    0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkCrystalShard,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
