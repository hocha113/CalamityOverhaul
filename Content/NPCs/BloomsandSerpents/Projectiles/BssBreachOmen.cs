using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 破土预告实体：沙丘隆起 + 渗沙 + 隆隆声。生成位置即锁定位置（预告即承诺，
    /// 突袭不再改向）。ai[0]=预告总帧数。无伤害，纯预告。
    /// </summary>
    internal class BssBreachOmen : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private int TotalFrames => (int)Projectile.ai[0];
        private float Progress => TotalFrames > 0 ? 1f - Projectile.timeLeft / (float)TotalFrames : 1f;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (TotalFrames > 0) {
                    Projectile.timeLeft = TotalFrames;
                }
            }

            float p = Progress;

            //渗沙密度随进度上量
            if (!Main.dedServ) {
                int count = 1 + (int)(p * 3f);
                for (int i = 0; i < count; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f) * (0.4f + p), -2f),
                        DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(1f, 2.6f + 3.5f * p)),
                        100, default, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = false;
                }
                //临破前的碎石弹跳
                if (p > 0.55f && Main.rand.NextBool(4)) {
                    Dust stone = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f),
                        DustID.Dirt, new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(2f, 4f)),
                        80, default, Main.rand.NextFloat(0.7f, 1f));
                    stone.noGravity = false;
                }
            }

            //隆隆声与就近微震，节奏随进度加密
            int rumbleGap = p > 0.6f ? 10 : 16;
            if (Projectile.timeLeft % rumbleGap == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.45f + 0.35f * p, Pitch = -0.5f + 0.3f * p, MaxInstances = 3 },
                    Projectile.Center);
                BssVfx.Shake(Projectile.Center, 1.2f + 2.4f * p, 900f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //沙丘隆起：原版沙球贴图堆出鼓包，随进度顶升（漫反射材质，乘本地光照）
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float p = Progress;
            float rise = p * p * 15f;

            Span<float> slotX = stackalloc float[] { -18f, -9f, 0f, 9f, 18f };
            Span<float> slotH = stackalloc float[] { 0.35f, 0.7f, 1f, 0.7f, 0.35f };
            for (int i = 0; i < slotX.Length; i++) {
                Vector2 pos = Projectile.Center + new Vector2(slotX[i], 2f - rise * slotH[i]);
                Color tint = lightColor.MultiplyRGB(BssVfx.SandWarm);
                float scale = 0.9f + 0.5f * slotH[i] * p;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint,
                    i * 0.7f + p * 2f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
