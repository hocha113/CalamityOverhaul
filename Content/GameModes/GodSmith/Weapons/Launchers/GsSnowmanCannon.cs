using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
    /// 雪人加农炮重铸：经典不毁强化。右键切换运载模式：[追踪群]（原版追踪，命中给
    /// 目标盖雪花印，印在时本枪对其 +8%）/ [暴雪轨道]（火箭升空后自光标上空俯冲，
    /// 落点带散布，区域压制）。特种火箭效果 100% 保真：轨道只改运载轨迹，爆炸仍是
    /// 原弹（液体火箭轨道即液毯空投，与原版液体火箭同规则，防刷液由原版逻辑管）。<br/>
    /// MarkData = 运载模式，MarkData2 = 轨道落点 X
    /// </summary>
    internal class GsSnowmanCannon : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.SnowmanCannon;

        protected override string GsDescFallback =>
            "Reforged: right click swaps carriers. Homing swarm brands victims with a snowflake (+8% from this weapon); Blizzard orbit lofts rockets skyward to dive from above your cursor";

        /// <summary>雪人火箭主弹全家（子雷 ClusterSnowmanFragments 不在内，不接管）</summary>
        internal static readonly HashSet<int> SnowRocketTypes = [
            ProjectileID.RocketSnowmanI, ProjectileID.RocketSnowmanII,
            ProjectileID.RocketSnowmanIII, ProjectileID.RocketSnowmanIV,
            ProjectileID.ClusterSnowmanRocketI, ProjectileID.ClusterSnowmanRocketII,
            ProjectileID.WetSnowmanRocket, ProjectileID.LavaSnowmanRocket,
            ProjectileID.HoneySnowmanRocket,
            ProjectileID.MiniNukeSnowmanRocketI, ProjectileID.MiniNukeSnowmanRocketII,
            ProjectileID.DrySnowmanRocket,
        ];

        /// <summary>暴雪冰蓝</summary>
        internal static readonly Color BlizzardBlue = new(150, 208, 255);

        private LocalizedText modeHoming;
        private LocalizedText modeOrbital;

        public override void GsSetStaticDefaults() {
            modeHoming = this.GetLocalization("ModeHoming", () => "Carrier: homing swarm");
            modeOrbital = this.GetLocalization("ModeOrbital", () => "Carrier: blizzard orbit");
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            mp.fuzeMode = (mp.fuzeMode + 1) % 2;
            SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.6f, Pitch = 0.3f * mp.fuzeMode }, player.Center);
            LocalTip(player, mp.fuzeMode == 1 ? modeOrbital : modeHoming, BlizzardBlue);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.3f, BlizzardBlue);
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (!SnowRocketTypes.Contains(proj.type)) {
                return;
            }
            GsLaunchersPlayer mp = Main.player[proj.owner].GetModPlayer<GsLaunchersPlayer>();
            router.MarkData = mp.fuzeMode;
            if (mp.fuzeMode == 1) {
                router.MarkData2 = Main.MouseWorld.X;
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (!SnowRocketTypes.Contains(proj.type) || (int)router.MarkData != 1 || proj.timeLeft <= 3) {
                return true;
            }
            //暴雪轨道：升空、横移、俯冲；爆窗与碰撞起爆全归原版
            return GsOrbitalHelper.RunOrbital(proj, router, 18f, 26f, 120f);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || !SnowRocketTypes.Contains(proj.type)
                || (int)router.MarkData != 1 || proj.timeLeft <= 3) {
                return;
            }
            //轨道接管期的暴雪尾迹（原版烟被接管压掉，由这层顶上）
            if (proj.timeLeft % 2 == 0) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BlizzardBlue, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(12, 20));
            }
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(proj.Center - proj.velocity * 0.6f,
                    -proj.velocity * 0.03f, Color.Lerp(BlizzardBlue, Color.White, 0.5f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22), 0.32f);
            }
            Lighting.AddLight(proj.Center, BlizzardBlue.ToVector3() * 0.2f);
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //雪花印在身：本枪一切打标弹（含承签子雷）对其 +8%
            int markType = ModContent.ProjectileType<GsSnowMarkProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == markType && p.owner == proj.owner && (int)p.ai[0] == target.whoAmI) {
                    modifiers.FinalDamage *= 1.08f;
                    return;
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //追踪档命中盖雪花印；已有印则续时
            if (!SnowRocketTypes.Contains(proj.type) || (int)router.MarkData != 0
                || !target.active || target.life <= 0) {
                return;
            }
            int markType = ModContent.ProjectileType<GsSnowMarkProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == markType && p.owner == proj.owner && (int)p.ai[0] == target.whoAmI) {
                    p.timeLeft = 240;
                    p.netUpdate = true;
                    return;
                }
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                markType, 0, 0f, proj.owner, target.whoAmI);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (SnowRocketTypes.Contains(proj.type)) {
                float scale = proj.type is ProjectileID.MiniNukeSnowmanRocketI
                    or ProjectileID.MiniNukeSnowmanRocketII ? 1.5f : 1f;
                ExplosionAftermath(proj.Center, BlizzardBlue, scale);
            }
        }
    }

    /// <summary>
    /// 雪花印：盖在目标头顶的冰晶烙印（无伤状态载体，天然同步）。
    /// 六芒星辉呼吸，宿主死亡或 4 秒后融化
    /// </summary>
    internal class GsSnowMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC Host => Main.npc[(int)Projectile.ai[0]];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            NPC host = Host;
            if (!host.active || host.life <= 0) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = host.Top - new Vector2(0f, 18f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 12 == 0) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 4f),
                    new Vector2(0f, -0.3f), GsSnowmanCannon.BlizzardBlue,
                    Main.rand.NextFloat(0.2f, 0.3f))?.Configure(Main.rand.Next(12, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star == null) {
                return false;
            }
            //六芒雪花：两层星辉旋相错开，加色批安全色
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity * 0.9f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            Color glow = GsSnowmanCannon.BlizzardBlue with { A = 0 };
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = star.Size() * 0.5f;
            Main.EntitySpriteDraw(star, pos, null, glow * (0.7f * pulse * fade),
                Main.GlobalTimeWrappedHourly * 0.8f, origin, 0.11f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null, glow * (0.5f * pulse * fade),
                -Main.GlobalTimeWrappedHourly * 0.6f + MathHelper.PiOver4, origin, 0.08f, SpriteEffects.None, 0);
            return false;
        }
    }
}
