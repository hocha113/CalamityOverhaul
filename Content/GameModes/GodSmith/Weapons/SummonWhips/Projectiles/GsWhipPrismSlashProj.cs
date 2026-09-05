using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 万花筒处决「万华镜」单道折线斩：以目标为心的一道折线（0.6x），
    /// 五道成环。摆角由 identity 种子 + 序号定，跨端一致；
    /// 各道按序号错 2f 开扫，琶音展开。<br/>
    /// ai[0] = 序号 0~4；ai[1] = 目标 npc.whoAmI
    /// </summary>
    internal class GsWhipPrismSlashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        private const int SweepFrames = 10;
        private const int LifeFrames = 34;
        private const float ReachPx = 175f;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>本道错帧起点：序号 x2，五道琶音展开</summary>
        private int StartDelay => (int)Projectile.ai[0] * 2;

        /// <summary>本道局部时间</summary>
        private int LocalTime => Elapsed - StartDelay;

        /// <summary>目标失活后的路径锚点</summary>
        private Vector2 anchor;
        private bool anchorInit;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>折线三点：外围入点、偏轴拐点、对侧出点（局部偏移随目标走）</summary>
        private void GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2) {
            int idx = (int)Projectile.ai[1];
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                anchor = Main.npc[idx].Center;
                anchorInit = true;
            }
            else if (!anchorInit) {
                anchor = Projectile.Center;
                anchorInit = true;
            }
            float theta = Projectile.identity * 0.53f + Projectile.ai[0] * (MathHelper.TwoPi / 5f);
            Vector2 dir = theta.ToRotationVector2();
            Vector2 perp = (theta + MathHelper.PiOver2).ToRotationVector2();
            p0 = anchor + dir * ReachPx;
            p1 = anchor + perp * 42f - dir * 15f;
            p2 = anchor - dir * ReachPx - perp * 18f;
        }

        public override bool? CanDamage() => LocalTime >= 3 && LocalTime < 9 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), p0, p1)
                || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), p1, p2);
        }

        public override void AI() {
            GetPath(out _, out Vector2 p1, out _);
            Projectile.Center = p1;   //本体锚在拐点，判定与绘制都走路径
            int lt = LocalTime;
            if (lt == 0 && !VaultUtils.isServer) {
                //五道琶音：音高随序号爬升
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.55f,
                    Pitch = -0.2f + 0.15f * (int)Projectile.ai[0]
                }, anchor);
            }
        }

        /// <summary>沿折线一段拉伸一笔原版光束贴图（贴图竖向，补四分之一圈对齐线段）</summary>
        private static void DrawSeg(Texture2D tex, Vector2 a, Vector2 b, Color c) {
            Vector2 delta = b - a;
            float len = delta.Length();
            if (len < 2f) {
                return;
            }
            Main.EntitySpriteDraw(tex, a - Main.screenPosition, null, c, delta.ToRotation() + MathHelper.PiOver2,
                new Vector2(tex.Width * 0.5f, tex.Height), new Vector2(1f, len / tex.Height), SpriteEffects.None, 0);
        }

        /// <summary>折线本体：两段各拉伸一笔原版光束贴图（lightColor 着色），扫描相后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            int lt = LocalTime;
            if (lt < 0) {
                return false;
            }
            float fade = lt <= SweepFrames
                ? 1f
                : 1f - (lt - SweepFrames) / (float)(LifeFrames - StartDelay - SweepFrames);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2);
            DrawSeg(tex, p0, p1, lightColor * fade);
            DrawSeg(tex, p1, p2, lightColor * fade);
            return false;
        }
    }
}
