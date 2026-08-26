using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 星星炮重铸（借调发射器族）：右键切换弹道。[直射]（原版）/ [星落]（星弹先升空，
    /// 自光标上空坠落，90px 散布的星雨压制；命中溅出星尘小爆，40px 内 30% 溅射）。
    /// 坠星贴图与音效保留。MarkData = 弹道模式，MarkData2 = 星落落点 X
    /// </summary>
    internal class GsStarCannon : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.StarCannon;

        protected override string GsDescFallback =>
            "Reforged: right click swaps trajectory. Direct fire, or Starfall: stars loft skyward and rain down around your cursor, splashing stardust on hit";

        /// <summary>星辉金</summary>
        internal static readonly Color StarGold = new(255, 226, 130);

        private LocalizedText modeDirect;
        private LocalizedText modeStarfall;

        public override void GsSetStaticDefaults() {
            modeDirect = this.GetLocalization("ModeDirect", () => "Trajectory: direct");
            modeStarfall = this.GetLocalization("ModeStarfall", () => "Trajectory: starfall");
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            mp.fuzeMode = (mp.fuzeMode + 1) % 2;
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = 0.3f * mp.fuzeMode }, player.Center);
            LocalTip(player, mp.fuzeMode == 1 ? modeStarfall : modeDirect, StarGold);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 0.8f, StarGold);
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.StarCannonStar) {
                return;
            }
            GsLaunchersPlayer mp = Main.player[proj.owner].GetModPlayer<GsLaunchersPlayer>();
            router.MarkData = mp.fuzeMode;
            if (mp.fuzeMode == 1) {
                router.MarkData2 = Main.MouseWorld.X;
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.StarCannonStar || (int)router.MarkData != 1) {
                return true;
            }
            //星落弹道：升空更快、俯冲更利
            return GsOrbitalHelper.RunOrbital(proj, router, 22f, 30f, 90f);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type != ProjectileID.StarCannonStar) {
                return;
            }
            int mode = (int)router.MarkData;
            if (mode == 1 && proj.timeLeft % 3 == 0) {
                //星落相：星尘长尾
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    StarGold, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Color.White, Main.rand.Next(12, 20), 0.1f);
                Lighting.AddLight(proj.Center, StarGold.ToVector3() * 0.22f);
            }
            else if (mode == 0 && proj.timeLeft % 6 == 0) {
                //直射相：低频增色，不夺原版星辉的戏
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.04f, StarGold, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(false, Main.rand.Next(8, 12));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //星落档命中：星尘溅射小爆
            if (proj.type != ProjectileID.StarCannonStar || (int)router.MarkData != 1) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsStarBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.30f)), 0.5f, proj.owner);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type != ProjectileID.StarCannonStar) {
                return;
            }
            //星亡余痕：回落的星屑
            int count = (int)router.MarkData == 1 ? 3 : 2;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    StarGold, Main.rand.NextFloat(0.26f, 0.42f))
                    ?.Configure(Color.White, Main.rand.Next(16, 26), 0.08f);
            }
        }
    }

    /// <summary>
    /// 星尘溅射：坠星命中处绽开的一瞬星屑，40px 内的溅射判定。
    /// 星炮与超级星星炮共用，伤害由生成者折算
    /// </summary>
    internal class GsStarBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 4;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] != 0f || VaultUtils.isServer) {
                return;
            }
            Projectile.localAI[0] = 1f;
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 5 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                GsStarCannon.StarGold, 0.14f)?.Configure(0.05f, 0.5f, 14);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    GsStarCannon.StarGold, Main.rand.NextFloat(0.26f, 0.44f))
                    ?.Configure(Color.White, Main.rand.Next(12, 22), 0.12f);
            }
        }
    }
}
