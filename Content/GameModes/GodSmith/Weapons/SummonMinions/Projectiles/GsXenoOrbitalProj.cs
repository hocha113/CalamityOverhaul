using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 轨道歼灭：UFO 舰群校准完毕后从高轨落下的相干光矛。
    /// 三相 = 锁定 16 帧（咬住目标跟随移动，无伤害）/ 光矛 10 帧（伤害窗）/
    /// 电离余辉 18 帧（无伤害）。ai[0] = 目标索引，ai[1] = 目标类型校验
    /// </summary>
    internal class GsXenoOrbitalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.UFOLaser;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int LockFrames = 16;
        private const int BeamFrames = 10;
        private const int IonFrames = 18;
        private const int TotalFrames = LockFrames + BeamFrames + IonFrames;
        /// <summary>光柱上端相对锚点的高度</summary>
        private const float BeamTop = 230f;
        /// <summary>光柱下探深度（没入目标脚下）</summary>
        private const float BeamBottom = 58f;
        private const float BeamWidth = 46f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Locking => Elapsed < LockFrames;

        private bool Firing => Elapsed >= LockFrames && Elapsed < LockFrames + BeamFrames;

        private bool Ionizing => Elapsed >= LockFrames + BeamFrames;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //锁定期咬住目标移动；目标失踪即中止校准（不放空矛）
            if (Locking) {
                NPC target = BoundTarget;
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //锁定起始：校准滴答
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.6f },
                    Projectile.Center);
            }
            //光矛落下：轨道炮鸣
            if (Elapsed == LockFrames) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.85f, Pitch = -0.45f },
                    Projectile.Center);
            }
        }

        /// <summary>只有光矛相结算伤害</summary>
        public override bool? CanDamage() => Firing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle column = new((int)(Projectile.Center.X - BeamWidth / 2f),
                (int)(Projectile.Center.Y - BeamTop), (int)BeamWidth,
                (int)(BeamTop + BeamBottom));
            return column.Intersects(targetHitbox);
        }

        /// <summary>线状几何：原版激光贴图沿判定柱一次拉伸（锁定期细线渐宽、余辉期渐隐，lightColor 着色）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float widen = Locking ? MathHelper.Lerp(0.15f, 0.4f, Elapsed / (float)LockFrames) : 1f;
            float fade = Ionizing
                ? MathHelper.Clamp(Projectile.timeLeft / (float)IonFrames, 0f, 1f) : 1f;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float columnH = BeamTop + BeamBottom;
            Vector2 mid = Projectile.Center + new Vector2(0f, (BeamBottom - BeamTop) * 0.5f)
                - Main.screenPosition;
            Main.EntitySpriteDraw(tex, mid, null, lightColor * fade, 0f, tex.Size() / 2f,
                new Vector2(BeamWidth * widen / tex.Width, columnH / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
