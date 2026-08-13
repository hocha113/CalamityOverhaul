using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 星陨彗星：引力弯折的坠落轨迹 + 星尘拖尾。
    /// ai[0]=横向弯折加速度，ai[1]=1 落地生星火，ai[2]=引爆深度 Y（世界坐标）
    /// </summary>
    internal class MLordCometProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            //重力 + 横向弯折（天体弧线轨迹，绝不匀速直线）
            Projectile.velocity.Y += 0.11f;
            Projectile.velocity.X += Projectile.ai[0];
            if (Projectile.velocity.Length() > 23f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 23f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //出生 24 帧后允许撞地
            if (Timer > 24f) {
                Projectile.tileCollide = true;
            }
            //到达引爆深度
            if (Projectile.ai[2] > 0f && Projectile.Center.Y >= Projectile.ai[2]) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.8f);

            if (VaultUtils.isServer) {
                return;
            }
            //星尘剥落 ∝ 速度
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * Main.rand.NextFloat(0.05f, 0.16f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(14, 26));
            }
        }

        public override void OnKill(int timeLeft) {
            //落点爆裂
            if (!VaultUtils.isServer) {
                MLordScreenFX.StarBurst(Projectile.Center, 1.05f, 16);
                MLordScreenFX.Punch(Projectile.Center, 4.5f, 9, Projectile.velocity);
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.75f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
            }
            //星火余留（服务端裁定）
            if (!VaultUtils.isClient && Projectile.ai[1] == 1f) {
                Vector2 ground = MLordScreenFX.FindGroundBelow(Projectile.Center);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), ground - new Vector2(0f, 18f), Vector2.Zero,
                    ModContent.ProjectileType<MLordStarfireProj>(), Projectile.damage * 2 / 3, 0f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }

            //拖尾光带：逐段连成收锥彗尾（暗紫外鞘 + 青热芯），弯折轨迹上段间无断口
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            if (soft != null) {
                Vector2 prev = Projectile.Center;
                for (int i = 1; i < Projectile.oldPos.Length; i++) {
                    //trail 缓存未填满前是零向量，画出去会拉一条通向世界原点的巨型光带
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    Vector2 cur = Projectile.oldPos[i] + Projectile.Size / 2f;
                    Vector2 seg = prev - cur;
                    float segLen = seg.Length();
                    if (segLen > 0.5f) {
                        float fade = 1f - i / (float)Projectile.oldPos.Length;
                        Vector2 mid = (prev + cur) * 0.5f - Main.screenPosition;
                        float rot = seg.ToRotation();
                        Vector2 stretchScale = new(segLen * 1.7f / soft.Width, 1f);
                        Main.EntitySpriteDraw(soft, mid, null, MLordDirector.DeepViolet with { A = 0 } * (0.5f * fade),
                            rot, soft.Size() / 2f, stretchScale * new Vector2(1f, (46f * fade + 8f) / soft.Height),
                            SpriteEffects.None, 0);
                        if (i < 7) {
                            Main.EntitySpriteDraw(soft, mid, null, MLordDirector.Phantasmal with { A = 0 } * (0.62f * fade),
                                rot, soft.Size() / 2f, stretchScale * new Vector2(1f, (24f * fade + 5f) / soft.Height),
                                SpriteEffects.None, 0);
                        }
                    }
                    prev = cur;
                }
            }

            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.2f, 0.8f);
            Vector2 bodyScale = new(0.4f * (1f + stretch), 0.4f * (1f - stretch * 0.35f));
            float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.whoAmI);

            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * flicker,
                Projectile.rotation, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * flicker),
                Projectile.rotation * 0.4f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            return false;
        }
    }
}
