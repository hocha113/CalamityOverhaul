using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>灼地皮肤：焦痕墨焰 / 余烬龙火色温；同投射物共型</summary>
    internal enum OniMeiBurnStyle : byte
    {
        Scorch,
        Ember,
    }

    /// <summary>
    /// 铭刻共型灼地。结构借鉴 ArbiterGroundFire（寿命/规模/贴地/头尾伤害闸），
    /// 皮肤走绯墨焰 PRT；禁止生成 Arbiter 类型
    /// </summary>
    internal class OniMeiGroundBurn : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int HitboxWidth = 40;
        private const int HitboxHeight = 100;
        private const float BaseVisualHeight = 26f;
        private const float BaseVisualWidth = 38f;

        private ref float LifeMax => ref Projectile.ai[0];
        private ref float VisualScale => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private OniMeiBurnStyle style;
        private float swayPhase;
        private float visualHeight;
        private float visualWidth;
        private float visualGroundY;

        public OniMeiBurnStyle Style => style;

        public override void SetDefaults() {
            Projectile.width = HitboxWidth;
            Projectile.height = HitboxHeight;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = 600;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)style);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            style = (OniMeiBurnStyle)reader.ReadByte();
        }

        /// <summary>同风格近距刷新寿命/规模，否则新建</summary>
        public static void TrySpawnOrRefresh(Player player, Vector2 worldPos, int damage, int life
            , float scale, OniMeiBurnStyle burnStyle) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }
            float refreshSq = OniMeiCombat.BurnRefreshRadius * OniMeiCombat.BurnRefreshRadius;
            int type = ModContent.ProjectileType<OniMeiGroundBurn>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != type) {
                    continue;
                }
                if (proj.ModProjectile is not OniMeiGroundBurn burn || burn.style != burnStyle) {
                    continue;
                }
                if (Vector2.DistanceSquared(proj.Center, worldPos) > refreshSq) {
                    continue;
                }
                proj.timeLeft = Math.Max(proj.timeLeft, life);
                proj.ai[0] = Math.Max(proj.ai[0], life);
                proj.ai[1] = Math.Max(proj.ai[1], scale);
                if (proj.damage < damage) {
                    proj.damage = damage;
                }
                proj.netUpdate = true;
                return;
            }
            Projectile spawned = Projectile.NewProjectileDirect(
                player.GetSource_Misc("CWR_OniMeiGroundBurn"), worldPos, Vector2.Zero
                , type, Math.Max(1, damage), 0f, player.whoAmI, ai0: life, ai1: scale);
            if (spawned.ModProjectile is OniMeiGroundBurn created) {
                created.style = burnStyle;
                spawned.netUpdate = true;
            }
        }

        /// <summary>是否存在己方指定风格的活坑（余烬疾走税）</summary>
        public static bool AnyOwnedStyle(Player player, OniMeiBurnStyle burnStyle) {
            if (player == null) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiGroundBurn>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != type) {
                    continue;
                }
                if (proj.ModProjectile is OniMeiGroundBurn burn && burn.style == burnStyle) {
                    return true;
                }
            }
            return false;
        }

        public override bool? CanDamage() {
            if (Timer <= 4 || Timer >= Projectile.timeLeft - 6) {
                return false;
            }
            return null;
        }

        public override void AI() {
            if (Timer == 0) {
                if (LifeMax > 0) {
                    Projectile.timeLeft = (int)LifeMax;
                }
                if (VisualScale <= 0.01f) {
                    VisualScale = 1f;
                }
                Projectile.position.Y -= HitboxHeight / 2f;
                swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                visualGroundY = Projectile.Bottom.Y;
                visualWidth = BaseVisualWidth * VisualScale;
            }

            Timer++;
            swayPhase += 0.18f;

            float lifeRatio = Projectile.timeLeft / Math.Max(LifeMax, 1f);
            float baseHeight = BaseVisualHeight * VisualScale;
            visualWidth = BaseVisualWidth * VisualScale;
            if (Timer < 10) {
                visualHeight = MathHelper.Lerp(0f, baseHeight, Timer / 10f);
            }
            else if (lifeRatio < 0.3f) {
                visualHeight = baseHeight * (lifeRatio / 0.3f);
            }
            else {
                visualHeight = baseHeight * (0.9f + MathF.Sin(swayPhase) * 0.1f);
            }

            if (lifeRatio > 0.15f && !Main.dedServ) {
                SpawnCrimsonFlame();
            }

            float lightFactor = visualHeight / Math.Max(baseHeight, 1f);
            Vector3 tint = style == OniMeiBurnStyle.Ember
                ? new Vector3(1.1f, 0.55f, 0.22f)
                : new Vector3(0.95f, 0.22f, 0.18f);
            Lighting.AddLight(new Vector2(Projectile.Center.X, visualGroundY - visualHeight * 0.5f)
                , tint * lightFactor);
        }

        private void SpawnCrimsonFlame() {
            if (visualHeight < 4f) {
                return;
            }
            float baseY = visualGroundY;
            float spreadX = visualWidth * 0.45f;
            Color smokeDeep = style == OniMeiBurnStyle.Ember
                ? new Color(140, 40, 20)
                : new Color(100, 24, 30);
            Color smokeCore = style == OniMeiBurnStyle.Ember
                ? new Color(22, 11, 10)
                : new Color(24, 12, 16);
            Color spark = style == OniMeiBurnStyle.Ember
                ? new Color(235, 150, 80)
                : new Color(255, 110, 90);

            if (Timer % 3 == 0) {
                Vector2 pos = new(Projectile.Center.X + Main.rand.NextFloat(-spreadX, spreadX), baseY);
                Vector2 vel = new(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1.3f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.05f, 0.09f) * VisualScale)
                    ?.Configure(Main.rand.Next(14, 22), smokeDeep, smokeCore);
            }
            if (Timer % 4 == 0) {
                Vector2 pos = new(Projectile.Center.X + Main.rand.NextFloat(-spreadX * 0.7f, spreadX * 0.7f)
                    , baseY - Main.rand.NextFloat(0f, visualHeight * 0.45f));
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.0f, 2.4f));
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, spark
                    , Main.rand.NextFloat(0.2f, 0.38f) * VisualScale)
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
