using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
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
    /// 电球发射器重铸：电网压制。场上同时存在两颗以上自己的电球时自动两两拉起
    /// 电弧链（最多 3 条，弧伤为球伤一半，敌人越弧即遭电击）；右键「过载」：
    /// 所有电球半径 +40%、剩余时间压缩为 2 秒快放电。电球本体行为原版保留。<br/>
    /// MarkData2 = 过载旗（owner 写 + netUpdate，各端按旗同源 Resize）
    /// </summary>
    internal class GsElectrosphereLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.ElectrosphereLauncher;

        protected override string GsDescFallback =>
            "Reforged: two or more of your spheres link up with tesla arcs (up to 3, half sphere damage); right click overloads them all: +40% radius, discharged in 2 seconds";

        /// <summary>特斯拉青</summary>
        internal static readonly Color TeslaCyan = new(120, 220, 255);

        /// <summary>弧链上限</summary>
        private const int ArcCap = 3;

        /// <summary>连弧最大跨距（像素）</summary>
        private const float ArcRange = 480f;

        private LocalizedText tipOverload;

        /// <summary>每球本地包：过载 Resize 只执行一次</summary>
        private class SphereState
        {
            public bool overloadApplied;
        }

        public override void GsSetStaticDefaults()
            => tipOverload = this.GetLocalization("TipOverload", () => "Overload!");

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            int n = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != ProjectileID.Electrosphere
                    || !proj.TryGetGlobalProjectile(out GodSmithProjRouter router)
                    || router.MarkScheme != this || router.MarkData2 == 1f) {
                    continue;
                }
                router.MarkData2 = 1f;
                proj.timeLeft = Math.Min(proj.timeLeft, 120);
                proj.netUpdate = true;
                n++;
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_SkyBolt>(proj.Center, Vector2.Zero, TeslaCyan,
                        Main.rand.NextFloat(0.5f, 0.7f));
                }
            }
            if (n <= 0) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.8f, Pitch = 0.2f }, player.Center);
            LocalTip(player, tipOverload, TeslaCyan);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.0f, TeslaCyan);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Electrosphere) {
                return;
            }
            //过载：各端按同步旗执行一次 Resize（判定与绘制同源换算）
            if (router.MarkData2 == 1f) {
                SphereState st = router.GetOrCreateState<SphereState>();
                if (!st.overloadApplied) {
                    st.overloadApplied = true;
                    proj.Resize((int)(proj.width * 1.4f), (int)(proj.height * 1.4f));
                }
                if (!VaultUtils.isServer && proj.timeLeft % 5 == 0) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(
                        proj.Center + Main.rand.NextVector2Circular(proj.width * 0.4f, proj.height * 0.4f),
                        Main.rand.NextVector2Circular(1.5f, 1.5f), TeslaCyan,
                        Main.rand.NextFloat(0.3f, 0.5f));
                }
            }

            //电弧链管理：owner 端低频扫描配对，弧本体是弹幕、生成包自然广播
            if (!proj.IsOwnedByLocalPlayer() || proj.timeLeft % 15 != 0) {
                return;
            }
            int arcType = ModContent.ProjectileType<GsElectroArcProj>();
            int arcCount = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == arcType && p.owner == proj.owner) {
                    arcCount++;
                }
            }
            if (arcCount >= ArcCap) {
                return;
            }
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type != ProjectileID.Electrosphere || other.owner != proj.owner
                    || other.identity <= proj.identity
                    || !other.TryGetGlobalProjectile(out GodSmithProjRouter r) || r.MarkScheme != this) {
                    continue;
                }
                if (proj.Center.Distance(other.Center) > ArcRange || ArcExists(proj, other, arcType)) {
                    continue;
                }
                Projectile.NewProjectile(proj.GetSource_FromThis(),
                    Vector2.Lerp(proj.Center, other.Center, 0.5f), Vector2.Zero, arcType,
                    Math.Max(1, proj.damage / 2), 0f, proj.owner, proj.identity, other.identity);
                if (++arcCount >= ArcCap) {
                    return;
                }
            }
        }

        /// <summary>这对球之间是否已有弧（identity 无序对匹配）</summary>
        private static bool ArcExists(Projectile a, Projectile b, int arcType) {
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != arcType || p.owner != a.owner) {
                    continue;
                }
                int i0 = (int)p.ai[0];
                int i1 = (int)p.ai[1];
                if ((i0 == a.identity && i1 == b.identity) || (i0 == b.identity && i1 == a.identity)) {
                    return true;
                }
            }
            return false;
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //球熄灭：电火花散场（球不是爆炸物，导弹撞击的爆点也走这层）
            if (VaultUtils.isServer
                || proj.type is not (ProjectileID.Electrosphere or ProjectileID.ElectrosphereMissile)) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(proj.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    TeslaCyan, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }
    }

    /// <summary>
    /// 特斯拉电弧：两颗电球之间的持续放电链。端点以弹幕 identity 记账（跨端一致），
    /// 任一端点熄灭或超距即断链；线段采样判定，本地免疫约每三分之一秒电击一次。
    /// ThunderTrail 绘制，每 3 帧重掷弧形
    /// </summary>
    internal class GsElectroArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ArcPointCount = 7;

        private ThunderTrail arcTrail;
        private readonly Vector2[] arcPoints = new Vector2[ArcPointCount];
        private float arcAlpha;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按 identity 找回端点球（弹幕槽位跨端不一致，identity 才是通用身份）</summary>
        private Projectile FindSphere(int identity) {
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == ProjectileID.Electrosphere && p.owner == Projectile.owner
                    && p.identity == identity) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            Projectile a = FindSphere((int)Projectile.ai[0]);
            Projectile b = FindSphere((int)Projectile.ai[1]);
            if (a == null || b == null || a.Center.Distance(b.Center) > 560f) {
                Projectile.Kill();
                return;
            }
            //两端都在：弧常驻续命
            if (Projectile.timeLeft < 10) {
                Projectile.timeLeft = 10;
            }
            Projectile.Center = Vector2.Lerp(a.Center, b.Center, 0.5f);

            //首帧噼啪声
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                        Volume = 0.45f,
                        Pitch = 0.1f,
                        MaxInstances = 5
                    }, Projectile.Center);
                }
            }

            arcAlpha = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            for (int i = 0; i < ArcPointCount; i++) {
                arcPoints[i] = Vector2.Lerp(a.Center, b.Center, i / (float)(ArcPointCount - 1));
            }
            if (VaultUtils.isServer) {
                return;
            }

            float dist = a.Center.Distance(b.Center);
            arcTrail ??= new ThunderTrail(CWRAsset.ThunderTrail, WidthFunc, ColorFunc, AlphaFunc) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 3
            };
            arcTrail.BasePositions = arcPoints;
            if (Projectile.timeLeft % 3 == 0 || Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                arcTrail.SetRange((0, MathHelper.Clamp(dist * 0.07f, 6f, 24f)));
                arcTrail.SetExpandWidth(4);
                arcTrail.RandomThunder();
            }
            if (Projectile.timeLeft % 6 == 0) {
                Vector2 at = Vector2.Lerp(a.Center, b.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(at, Main.rand.NextVector2Circular(1f, 1f),
                    GsElectrosphereLauncher.TeslaCyan, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(false, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(Projectile.Center, GsElectrosphereLauncher.TeslaCyan.ToVector3() * 0.3f);
        }

        private float WidthFunc(float factor) => (float)Math.Sin(factor * MathHelper.Pi) * 9f;
        private Color ColorFunc(float factor) => GsElectrosphereLauncher.TeslaCyan;
        private float AlphaFunc(float factor) => arcAlpha;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //沿采样段逐段线判定：越弧即中
            for (int i = 0; i < ArcPointCount - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    arcPoints[i], arcPoints[i + 1])) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (arcTrail == null) {
                return false;
            }
            arcTrail.DrawThunder(Main.instance.GraphicsDevice);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Projectile a = FindSphere((int)Projectile.ai[0]);
            Projectile b = FindSphere((int)Projectile.ai[1]);
            Color c = (GsElectrosphereLauncher.TeslaCyan with { A = 0 }) * (arcAlpha * 0.7f);
            if (a != null) {
                Main.EntitySpriteDraw(glow, a.Center - Main.screenPosition, null, c, 0f,
                    glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            }
            if (b != null) {
                Main.EntitySpriteDraw(glow, b.Center - Main.screenPosition, null, c, 0f,
                    glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
