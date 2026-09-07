using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 熔岩套 · 熔火喷发（近战）。单件沿用原版（头 +7% 近暴、胸 +7% 近伤、腿 +7% 近攻速）。<br/>
    /// 原版旗标清点：+10% 近战伤害 / 免疫着火 → 原样补回；无删除项。<br/>
    /// 签名：近战命中点燃 3 秒；每 6 次近战命中在目标脚下喷出一柱熔火（三颗火球向上迸射再落回），喷口探地
    /// </summary>
    internal class GsMoltenArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MoltenHelmet];
        public override int BodyID => ItemID.MoltenBreastplate;
        public override int LegsID => ItemID.MoltenGreaves;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% increased melee damage and immunity to On Fire!; melee hits set enemies on fire, and every 6th melee hit erupts a column of molten fire beneath the target";

        /// <summary>喷发所需近战命中数</summary>
        private const int HitsPerEruption = 6;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Melee) += 0.10f;
            player.buffImmune[BuffID.OnFire] = true;
            player.buffImmune[BuffID.OnFire3] = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }
            target.AddBuff(BuffID.OnFire, 180);
            if (target.type == NPCID.TargetDummy || !TallyUp(player, HitsPerEruption) || player.whoAmI != Main.myPlayer) {
                return;
            }
            //喷口落在目标脚下的实地上；找不到地面就直接在脚底喷
            GsArmorTerrainProbe.TryFindGroundBelow(target.Bottom, 8, out float groundY);
            Vector2 mouth = new(target.Center.X, Math.Max(groundY, target.Bottom.Y) - 6f);
            int damage = ProcDamage(damageDone, 0.4f, 5, 25);
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = new Vector2(-2.2f + i * 2.2f, -9f - Main.rand.NextFloat(1.5f)).RotatedByRandom(0.08f);
                SpawnProc(player, "GodSmithMoltenEndow", mouth, velocity,
                    ModContent.ProjectileType<GsMoltenArmorFireballProj>(), damage, 3f);
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, mouth);
            for (int i = 0; i < 12; i++) {
                Dust lava = Dust.NewDustPerfect(mouth + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f), DustID.Lava,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)), 0, default, 1.3f);
                lava.noGravity = false;
            }
        }
    }

    /// <summary>
    /// 熔火球：借原版火球贴图，自喷口向上迸出后受重力落回，命中点燃；出生 6 帧内免地形碰撞以脱离地面
    /// </summary>
    internal class GsMoltenArmorFireballProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > 6f) {
                Projectile.tileCollide = true;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 14f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 0.1f);
            if (Main.dedServ) {
                return;
            }
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.5f);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 100, default, 1.4f);
                dust.noGravity = true;
            }
        }
    }
}
