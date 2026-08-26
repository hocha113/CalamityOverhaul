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
    /// 杰克南瓜灯发射器重铸：区域压制。滚地南瓜原版保留；右键「齐声狞笑」全部原地
    /// 起爆，每个爆点放出 3 只追踪焰蝠（各 25% 伤，场上封顶 12 只）；本武器一切爆点
    /// 都留下 2.5 秒烛火场（踩踏持续伤害）。MarkData2 = 遥控狞笑旗
    /// </summary>
    internal class GsJackOLanternLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.JackOLanternLauncher;

        protected override string GsDescFallback =>
            "Reforged: right click makes every rolling pumpkin grin and burst, each releasing 3 homing flame wisps; every blast leaves a candle field that scorches whoever stands in it";

        /// <summary>烛火橙</summary>
        internal static readonly Color CandleOrange = new(255, 150, 40);

        /// <summary>场上焰蝠封顶</summary>
        private const int BatCap = 12;

        private LocalizedText tipGrin;

        public override void GsSetStaticDefaults()
            => tipGrin = this.GetLocalization("TipGrin", () => "Grin in unison!");

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            int n = DetonateMarked(player,
                filter: (p, r) => p.type == ProjectileID.JackOLantern && p.timeLeft > 3,
                before: (p, r) => r.MarkData2 = 1f);
            if (n <= 0) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.55f, Pitch = 0.35f }, player.Center);
            LocalTip(player, tipGrin, CandleOrange);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.3f, CandleOrange);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //滚动相：南瓜自带火光，补一层低频余烬
            if (VaultUtils.isServer || proj.type != ProjectileID.JackOLantern || proj.timeLeft <= 3) {
                return;
            }
            if (proj.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_DefEmber>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.4f, 1f)),
                    CandleOrange, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(Main.rand.Next(14, 24));
            }
            Lighting.AddLight(proj.Center, CandleOrange.ToVector3() * 0.24f);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.JackOLantern) {
                return;
            }
            ExplosionAftermath(proj.Center, CandleOrange, 0.95f);
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //区域压制：一切爆点都点起烛火场
            Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center - new Vector2(0f, 8f),
                Vector2.Zero, ModContent.ProjectileType<GsCandleFieldProj>(),
                Math.Max(1, (int)(proj.damage * 0.25f)), 0f, proj.owner);
            //齐声狞笑专属：3 只追踪焰蝠，全场封顶
            if (router.MarkData2 == 1f) {
                Player player = Main.player[proj.owner];
                int alive = player.ownedProjectileCounts[ModContent.ProjectileType<GsJackBatProj>()];
                int batDamage = Math.Max(1, (int)(proj.damage * 0.25f));
                for (int i = 0; i < 3 && alive < BatCap; i++, alive++) {
                    Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f))
                        * Main.rand.NextFloat(5f, 8f);
                    Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, vel,
                        ModContent.ProjectileType<GsJackBatProj>(), batDamage, 0.5f, proj.owner);
                }
            }
        }
    }

    /// <summary>
    /// 狞笑焰蝠：爆点里飞出的小灯灵，散开半拍后咬向最近的敌人。
    /// 复用燃烧杰克贴图缩小滚转，火舌尾迹，亡处火花回落
    /// </summary>
    internal class GsJackBatProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingJack;

        /// <summary>散开段帧数</summary>
        private const int ScatterFrames = 8;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 0.5f;
        }

        public override void AI() {
            Life++;
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.06f, 0.2f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
            }
            Projectile.rotation += 0.28f * Projectile.direction;
            if (!VaultUtils.isServer && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_HellFire>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    GsJackOLanternLauncher.CandleOrange, Main.rand.NextFloat(0.3f, 0.5f));
            }
            Lighting.AddLight(Projectile.Center, GsJackOLanternLauncher.CandleOrange.ToVector3() * 0.28f);
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 520f;
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

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    GsJackOLanternLauncher.CandleOrange, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>
    /// 烛火场：爆点余烬燃成的一片低矮火毯，静止 2.5 秒，踩进来的敌人被持续灼烧。
    /// 判定用本地免疫（每目标约每半秒一跳），火舌与烛光是它的身体
    /// </summary>
    internal class GsCandleFieldProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (VaultUtils.isServer) {
                return;
            }
            //火毯身体：底部升起的火舌与偶发余烬
            if (Projectile.timeLeft % 3 == 0) {
                Vector2 basePos = Projectile.Bottom + new Vector2(
                    Main.rand.NextFloat(-Projectile.width * 0.45f, Projectile.width * 0.45f), -4f);
                PRTLoader.NewParticle<PRT_HellFire>(basePos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    GsJackOLanternLauncher.CandleOrange, Main.rand.NextFloat(0.34f, 0.55f));
            }
            if (Projectile.timeLeft % 9 == 0) {
                PRTLoader.NewParticle<PRT_DefEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.4f, 12f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    GsJackOLanternLauncher.CandleOrange, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(Main.rand.Next(16, 28));
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, GsJackOLanternLauncher.CandleOrange.ToVector3() * (0.5f * fade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);
    }
}
