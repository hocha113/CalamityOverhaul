using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 电球发射器重铸：电网压制。场上同时存在两颗以上自己的电球时自动两两拉起
    /// 电弧链（最多 3 条，弧伤为球伤一半，敌人越弧即遭电击）。电球本体行为原版保留
    /// </summary>
    internal class GsElectrosphereLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.ElectrosphereLauncher;

        protected override string GsDescFallback =>
            "Reforged: two or more of your spheres link up with tesla arcs (up to 3, half sphere damage)";
        /// <summary>弧链上限</summary>
        private const int ArcCap = 3;

        /// <summary>连弧最大跨距（像素）</summary>
        private const float ArcRange = 480f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.0f);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Electrosphere) {
                return;
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
    }

    /// <summary>
    /// 特斯拉电弧：两颗电球之间的持续放电链。端点以弹幕 identity 记账（跨端一致），
    /// 任一端点熄灭或超距即断链；线段采样判定，本地免疫约每三分之一秒电击一次。
    /// 借原版磁球电矢贴图在两端点间拉伸一笔
    /// </summary>
    internal class GsElectroArcProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagnetSphereBolt;

        private const int ArcPointCount = 7;

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
        }

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

        /// <summary>线状一笔：电矢贴图（原版朝上）从 a 端拉伸到 b 端，随 arcAlpha 淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Vector2 a = arcPoints[0];
            Vector2 b = arcPoints[ArcPointCount - 1];
            Vector2 delta = b - a;
            float length = delta.Length();
            if (length < 4f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 scale = new(1f, length / tex.Height);
            Main.EntitySpriteDraw(tex, a - Main.screenPosition, null, lightColor * arcAlpha,
                delta.ToRotation() - MathHelper.PiOver2, new Vector2(tex.Width / 2f, 0f), scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
