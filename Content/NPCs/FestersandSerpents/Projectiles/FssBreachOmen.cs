using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 破土预告实体：腐沙隆起 + 渗金脓液 + 隆隆声。生成位置即锁定位置（预告即承诺，
    /// 突袭不再改向）。本体无伤害。
    /// ai[0]=预告总帧数；ai[1]=0 纯预告（破土时机由蛇自己控制）；ai[2]=头 whoAmI。
    /// </summary>
    internal class FssBreachOmen : FssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private int TotalFrames => (int)Projectile.ai[0];
        private float Progress => TotalFrames > 0 ? 1f - Projectile.timeLeft / (float)TotalFrames : 1f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 22;
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

            //渗沙密度随进度上量；掺金脓渗液（变异身份）
            if (!Main.dedServ) {
                int count = 1 + (int)(p * 3f);
                for (int i = 0; i < count; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f) * (0.4f + p), -2f),
                        DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(1f, 2.6f + 3.5f * p)),
                        100, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = false;
                }
                if (p > 0.4f && Main.rand.NextBool(5)) {
                    Dust gold = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                        DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.5f, 3.5f)),
                        40, default, Main.rand.NextFloat(0.8f, 1.1f));
                    gold.noGravity = false;
                }
                //临破前的碎屑弹跳
                if (p > 0.55f && Main.rand.NextBool(4)) {
                    Dust stone = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f),
                        DustID.CorruptGibs, new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(2f, 4f)),
                        80, default, Main.rand.NextFloat(0.7f, 1f));
                    stone.noGravity = false;
                }
            }

            //隆隆声与就近微震，节奏随进度加密
            int rumbleGap = p > 0.6f ? 10 : 16;
            if (Projectile.timeLeft % rumbleGap == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.45f + 0.35f * p, Pitch = -0.55f + 0.3f * p, MaxInstances = 3 },
                    Projectile.Center);
                FssVfx.Shake(Projectile.Center, 1.2f + 2.4f * p, 900f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //腐沙隆起：原版沙球贴图堆出鼓包，随进度顶升（漫反射材质，乘本地光照 + 污沙染色）
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float p = Progress;
            float rise = p * p * 17f;

            Span<float> slotX = stackalloc float[] { -20f, -10f, 0f, 10f, 20f };
            Span<float> slotH = stackalloc float[] { 0.35f, 0.7f, 1f, 0.7f, 0.35f };
            for (int i = 0; i < slotX.Length; i++) {
                Vector2 pos = Projectile.Center + new Vector2(slotX[i], 2f - rise * slotH[i]);
                Color tint = lightColor.MultiplyRGB(FssVfx.TaintedSand);
                float scale = 0.95f + 0.55f * slotH[i] * p;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint,
                    i * 0.7f + p * 2f, origin, scale, SpriteEffects.None, 0);
            }
            //鼓包缝隙渗金微光（加色薄层，灵液在土下透光的读数）
            if (p > 0.3f) {
                float seep = (p - 0.3f) / 0.7f;
                Main.EntitySpriteDraw(tex, Projectile.Center + new Vector2(0f, -rise) - Main.screenPosition, null,
                    FssVfx.IchorGold with { A = 0 } * (0.35f * seep), p * 2f, origin,
                    1.3f + 0.4f * seep, SpriteEffects.None, 0);
                Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * 0.25f * seep);
            }
            return false;
        }
    }
}
