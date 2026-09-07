using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 丛林套 · 孢子回魔（魔法，含远古钴三件可混搭）。单件沿用原版（帽 +40 魔力 +6% 魔暴、
    /// 衣 +20 魔力 +6% 魔伤、裤 +20 魔力 +6% 魔暴）。<br/>
    /// 原版旗标清点：套装 −16% 魔耗 → 原样补回；无删除项。<br/>
    /// 签名：魔法命中 25% 概率在目标处留下孢子团（2 秒，每半秒结算，命中中毒），孢子团每次伤害敌人为你回复 2 魔力
    /// </summary>
    internal class GsJungleArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.JungleHat, ItemID.AncientCobaltHelmet];
        public override int BodyID => ItemID.JungleShirt;
        public override int LegsID => ItemID.JunglePants;
        public override int[] BodyIDs => [ItemID.JungleShirt, ItemID.AncientCobaltBreastplate];
        public override int[] LegsIDs => [ItemID.JunglePants, ItemID.AncientCobaltLeggings];
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "16% reduced mana usage; magic hits have a 25% chance to leave a spore cloud on the target that poisons enemies, and every spore hit restores 2 mana";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.manaCost -= 0.16f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Magic)
                || Main.rand.Next(100) >= 25 || !state.TryUseCooldown(this, 30)) {
                return;
            }
            SpawnProc(player, "GodSmithJungleEndow", target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsJungleArmorSporeProj>(), ProcDamage(damageDone, 0.2f, 2, 6), 0f);
        }
    }

    /// <summary>
    /// 丛林孢子团：借原版孢子云贴图（5 帧），驻在原地缓缓漂散，两秒内每半秒结算一次并挂中毒；
    /// 每次命中为主人回复 2 魔力（只在 owner 端写魔力）
    /// </summary>
    internal class GsJungleArmorSporeProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeCloud;

        /// <summary>每次命中回复的魔力</summary>
        private const int ManaPerHit = 2;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.SporeCloud]);
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 60;
        }

        public override void AI() {
            //缓缓上漂并随时间散开、变淡
            Projectile.velocity = new Vector2(MathF.Sin(Projectile.timeLeft * 0.08f) * 0.3f, -0.25f);
            Projectile.rotation += 0.02f;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 60f, Projectile.timeLeft / 120f);
            if (++Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
            Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.1f);
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.JungleSpore,
                    0f, -0.5f, 120, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 120);
            if (Projectile.owner == Main.myPlayer) {
                Player owner = Main.player[Projectile.owner];
                if (owner.statMana < owner.statManaMax2) {
                    owner.statMana = Math.Min(owner.statManaMax2, owner.statMana + ManaPerHit);
                }
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.2f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.JungleSpore,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, 0f), 120, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
