using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 空投礼盒（迫击炮弹视觉载体）：ai[0]=引信帧。全程无判定（威胁只在被标记的弹着环），
    /// 定时长抛物线由发射端解算，与 <see cref="Gravity"/> 严格对齐，引信归零即自毁。
    /// 穿墙飞行（攻城语义：翻越工事，落点由标记环诚实宣告）
    /// </summary>
    internal class FrmPresentShellProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Present;

        /// <summary>每帧重力（发射端弹道解算与此对齐）</summary>
        internal const float Gravity = 0.22f;

        private int FuseFrames => Math.Max((int)Projectile.ai[0], 10);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = false;//纯视觉载体，永不判定
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = FuseFrames;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 6 }, Projectile.Center);
                }
            }

            //抛物线：不设落速钳制，保证与解算弹道严格一致（落点承诺）
            Projectile.velocity.Y += Gravity;
            Projectile.rotation += Projectile.velocity.X * 0.03f + 0.09f;

            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    -Projectile.velocity * 0.1f, 140, default, 0.8f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //抵达即消隐（轰爆表现归弹着环所有）
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 1f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Present);
            Texture2D tex = TextureAssets.Item[ItemID.Present].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质拖尾（旧位重画，横轴≥体宽一半）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, null, lightColor * (0.32f * t),
                    Projectile.rotation - i * 0.1f, orig, 0.62f * t + 0.25f, SpriteEffects.None, 0);
            }

            //礼盒本体（原版物品贴图，真 alpha 实体层）+ 弱辉光敷料
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float twinkle = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);
            Main.EntitySpriteDraw(glow, pos, null, new Color(255, 216, 130, 0) * (0.3f * twinkle), 0f,
                glow.Size() / 2f, 0.36f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, lightColor, Projectile.rotation, orig, 0.95f, SpriteEffects.None, 0);
            return false;
        }
    }
}
