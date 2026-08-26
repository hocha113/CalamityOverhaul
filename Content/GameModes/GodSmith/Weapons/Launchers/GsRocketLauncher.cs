using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 火箭筒重铸：遥控空爆。左键原版直射（直击 ×2 保留），右键把全部在途火箭
    /// 原地起爆（timeLeft=3 进原版爆窗，特种火箭效果 100% 保真），空爆点向光标方向
    /// 补射 3 枚穿甲镖片。技巧射击：过顶空爆等于给目标下一场头顶镖雨
    /// </summary>
    internal class GsRocketLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.RocketLauncher;

        protected override string GsDescFallback =>
            "Reforged: right click to detonate every rocket in flight; each airburst hurls 3 piercing darts toward your cursor";

        /// <summary>火箭筒主弹全家（普通/集束/液体/迷你核/干性），承签子雷不在内</summary>
        internal static readonly HashSet<int> RocketTypes = [
            ProjectileID.RocketI, ProjectileID.RocketII, ProjectileID.RocketIII, ProjectileID.RocketIV,
            ProjectileID.ClusterRocketI, ProjectileID.ClusterRocketII,
            ProjectileID.WetRocket, ProjectileID.LavaRocket, ProjectileID.HoneyRocket,
            ProjectileID.MiniNukeRocketI, ProjectileID.MiniNukeRocketII, ProjectileID.DryRocket,
        ];

        /// <summary>灼橙爆色</summary>
        internal static readonly Color BlastWarm = new(255, 138, 46);

        private LocalizedText tipDetonate;

        public override void GsSetStaticDefaults()
            => tipDetonate = this.GetLocalization("TipDetonate", () => "Airburst!");

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            //遥控空爆：只点名主弹家族，打上遥控旗后压进爆窗
            int n = DetonateMarked(player,
                filter: (p, r) => RocketTypes.Contains(p.type) && p.timeLeft > 3,
                before: (p, r) => r.MarkData2 = 1f);
            if (n <= 0) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
            LocalTip(player, tipDetonate, BlastWarm);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.5f, BlastWarm);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //飞行相：低频火星尾（原版烟雾之上的增色层）
            if (VaultUtils.isServer || !RocketTypes.Contains(proj.type) || proj.timeLeft <= 3) {
                return;
            }
            if (proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.05f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    BlastWarm, Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (RocketTypes.Contains(proj.type)) {
                //爆点余痕：迷你核给更大的相
                float scale = proj.type is ProjectileID.MiniNukeRocketI or ProjectileID.MiniNukeRocketII ? 1.5f : 1f;
                ExplosionAftermath(proj.Center, BlastWarm, scale);
                //遥控空爆专属：向光标补 3 枚穿甲镖片（owner 端生成，随生成包广播）
                if (router.MarkData2 == 1f && proj.IsOwnedByLocalPlayer()) {
                    Vector2 dir = (Main.MouseWorld - proj.Center).SafeNormalize(Vector2.UnitY);
                    int dartDamage = Math.Max(1, (int)(proj.damage * 0.4f));
                    for (int i = -1; i <= 1; i++) {
                        Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center,
                            dir.RotatedBy(i * MathHelper.ToRadians(8f)) * 17f,
                            ModContent.ProjectileType<GsPierceDartProj>(),
                            dartDamage, proj.knockBack * 0.5f, proj.owner);
                    }
                }
                return;
            }
            //镖片消亡：小相火花
            if (proj.type == ModContent.ProjectileType<GsPierceDartProj>() && !VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        BlastWarm, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }
        }
    }

    /// <summary>
    /// 穿甲镖片：空爆迸出的高速细镖，穿透 3 目标。轻微下坠 + 火星尾，
    /// 亡处火花回落。伤害与击退由生成者按火箭伤害折算
    /// </summary>
    internal class GsPierceDartProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThrowingKnife;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.06f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (!VaultUtils.isServer && Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * 0.04f, GsRocketLauncher.BlastWarm,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(6, 12));
            }
            Lighting.AddLight(Projectile.Center, GsRocketLauncher.BlastWarm.ToVector3() * 0.18f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (-Projectile.velocity).RotatedByRandom(0.8) * Main.rand.NextFloat(0.15f, 0.4f),
                    GsRocketLauncher.BlastWarm, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
        }
    }
}
