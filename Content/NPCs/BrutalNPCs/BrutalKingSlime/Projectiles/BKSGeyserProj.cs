using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>凝胶喷泉：预兆冒泡→喷发立柱→塌落；ai[0]=高度档0/1 ai[1]=预兆帧；锚定地面点；服务端生成</summary>
    internal class BKSGeyserProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int BurstTime = 34;
        private const int CollapseTime = 20;

        private int WarnTime => (int)Projectile.ai[1] <= 0 ? 30 : (int)Projectile.ai[1];
        private float MaxHeight => Projectile.ai[0] == 1f ? 320f : 210f;
        private int TotalLife => WarnTime + BurstTime + CollapseTime;

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>柱高包络：过冲弹起→保持→塌落</summary>
        private float HeightEnvelope {
            get {
                if (Timer <= WarnTime) {
                    return 0f;
                }
                float t = Timer - WarnTime;
                if (t <= 10f) {
                    //快速拔起带过冲
                    float rise = t / 10f;
                    return MathF.Sin(rise * MathHelper.PiOver2) * (1f + 0.18f * MathF.Sin(rise * MathHelper.Pi));
                }
                if (Timer >= WarnTime + BurstTime) {
                    float c = (Timer - WarnTime - BurstTime) / (float)CollapseTime;
                    return 1f - VaultUtils.EaseInQuad(c);
                }
                //保持期轻微呼吸
                return 1f + 0.05f * MathF.Sin((float)(Main.GlobalTimeWrappedHourly * 18f + Projectile.whoAmI));
            }
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 500;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //预兆：地面冒泡+隆隆
            if (Timer <= WarnTime) {
                float warnT = Timer / (float)WarnTime;
                if (!VaultUtils.isServer) {
                    if (Main.rand.NextFloat() < 0.2f + warnT * 0.5f) {
                        KingSlimeGelFX.BubbleFizz(Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), -4f), 10f, 1);
                    }
                    if (Main.rand.NextBool(3)) {
                        Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(26f, 6f), 52, 8,
                            DustID.TintableDust, 0, 0, 160, KingSlimeGelFX.DustBlue, 1f + warnT);
                        d.noGravity = true;
                        d.velocity = new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f) * (0.5f + warnT));
                    }
                }
                return;
            }

            //喷发帧
            if ((int)Timer == WarnTime + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.25f, Volume = 0.9f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = 0.8f, MaxInstances = 3 }, Projectile.Center);
                KingSlimeGelFX.CameraPunch(Projectile.Center, 4.5f, 12, "BKSGeyser", -Vector2.UnitY);
                KingSlimeGelFX.LandingBurst(Projectile.Center, 12f, 0.9f);
            }

            float env = HeightEnvelope;

            //喷发期顶部持续洒珠
            if (!VaultUtils.isServer && env > 0.4f && Timer < WarnTime + BurstTime) {
                Vector2 crest = Projectile.Center - new Vector2(0f, MaxHeight * env);
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(1f, 4.5f));
                    PRTLoader.NewParticle<PRT_BKSGelBead>(crest + Main.rand.NextVector2Circular(14f, 8f), vel,
                        Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelFoam, Main.rand.NextFloat(0.45f)) * 0.85f,
                        Main.rand.NextFloat(0.7f, 1.3f))?.Configure(Main.rand.Next(22, 38));
                }
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0f, MaxHeight * env * 0.5f),
                KingSlimeGelFX.GelMid.ToVector3() * 0.55f * env);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 150);
            //向上顶飞
            target.velocity.Y = Math.Min(target.velocity.Y, -7.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float env = HeightEnvelope;
            if (env < 0.3f) {
                return false;
            }
            float h = MaxHeight * env;
            Rectangle column = new Rectangle(
                (int)(Projectile.Center.X - 26f), (int)(Projectile.Center.Y - h),
                52, (int)h + 12);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = HeightEnvelope;
            float warnT = MathHelper.Clamp(Timer / (float)WarnTime, 0f, 1f);

            //基座沸腾小池
            Effect pool = EffectLoader.BKSGelPool?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (pool != null && noise != null) {
                float spread = Timer <= WarnTime ? warnT : 1f;
                float drain = Timer >= WarnTime + BurstTime
                    ? MathHelper.Clamp((Timer - WarnTime - BurstTime) / (float)CollapseTime, 0f, 1f) : 0f;
                KingSlimeGelFX.SetPoolParams(pool, spread, drain, 0.85f, boil: 0.4f + warnT * 0.6f,
                    seed: Projectile.whoAmI * 0.173f % 1f);
                KingSlimeGelFX.DrawShaderQuad(pool, noise, Projectile.Center + new Vector2(0f, -14f), new Vector2(120f, 52f), 1f);
            }

            if (env <= 0.02f) {
                return false;
            }

            //凝胶柱：三段堆叠拉伸团，宽度随高呼吸
            Texture2D blob = CWRAsset.Extra_98?.Value;
            if (blob != null) {
                Vector2 basePos = Projectile.Center - Main.screenPosition;
                float h = MaxHeight * env;
                Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.25f) * 0.8f;
                int segs = 3;
                for (int i = 0; i < segs; i++) {
                    float segT = (i + 0.5f) / segs;
                    float wobble = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + i * 1.7f + Projectile.whoAmI);
                    //底粗顶细
                    float segW = (1.15f - segT * 0.45f) * wobble;
                    float segH = h / segs / blob.Height * 2.6f;
                    Vector2 pos = basePos - new Vector2(0f, h * segT);
                    Main.EntitySpriteDraw(blob, pos, null, gel, 0f, blob.Size() * 0.5f,
                        new Vector2(segW, segH), SpriteEffects.None, 0);
                }
                //顶冠亮点
                Vector2 crest = basePos - new Vector2(0f, h);
                Main.EntitySpriteDraw(blob, crest, null, KingSlimeGelFX.GelFoam with { A = 0 } * 0.5f * env, 0f,
                    blob.Size() * 0.5f, new Vector2(0.7f, 0.5f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
