using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 祭火炮弹（纯视觉载体）：ai[0]=飞行帧数。发射端定帧弹道解算（重力与本类严格同源），
    /// 恰在同帧生成的落点标记引爆帧抵达；伤害全部由落点标记承载，本体永不判定（CanDamage=false）。
    /// 高抛过顶语义故穿地形；寿命尽即自灭
    /// </summary>
    internal class PmkMortarShellProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingWood;

        /// <summary>弹道重力（每帧），与发射端解算严格同源</summary>
        internal const float ShellGravity = 0.26f;
        /// <summary>飞行帧数=落点预告时长（小 Boss 契约 ≥40）</summary>
        internal const int FlightFrames = 52;

        private static readonly Color ShellWarm = new Color(255, 150, 60);

        private int Flight => Math.Max((int)Projectile.ai[0], 10);
        private int Elapsed => Flight - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FlightFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯视觉载体，永不判定</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //寿命由已同步的 ai[0] 各端确定性展开
                Projectile.timeLeft = Flight;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //定帧弹道：每帧先加速后位移，与发射端解算严格同构
            Projectile.velocity.Y += ShellGravity;
            Projectile.rotation += 0.22f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (!Main.dedServ) {
                if (Main.rand.NextBool(3)) {
                    Dust flame = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 1.1f);
                    flame.noGravity = true;
                }
                if (Main.rand.NextBool(6)) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        -Projectile.velocity * 0.08f, 150, default, 0.9f);
                    smoke.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, ShellWarm.ToVector3() * 0.4f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //抵达帧小爆点（真正的轰燃由落点标记同帧接管）
            for (int i = 0; i < 4; i++) {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f), 90, default, Main.rand.NextFloat(1f, 1.5f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frameCount = Math.Max(1, Main.projFrames[ProjectileID.FlamingWood]);
            Rectangle frame = tex.Frame(1, frameCount, 0, Elapsed / 4 % frameCount);
            Vector2 origin = frame.Size() / 2f;

            //同材质拖尾：旧位重画（横轴粗细 ≥ 弹体 0.6）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = Color.Lerp(lightColor, ShellWarm, 0.5f) * (0.45f * t);
                Main.EntitySpriteDraw(tex, oldDrawPos, frame, trail, Projectile.rotation - i * 0.22f,
                    origin, 0.62f + 0.3f * t, SpriteEffects.None, 0);
            }

            //本体（原版哀木燃木贴图，实体层）+ 辉光敷料
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color body = Color.Lerp(lightColor, ShellWarm, 0.35f);
            Main.EntitySpriteDraw(tex, drawPos, frame, body, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, (ShellWarm with { A = 0 }) * 0.45f, 0f,
                glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
