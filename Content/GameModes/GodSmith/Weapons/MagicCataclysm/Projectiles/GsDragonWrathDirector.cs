using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 贝特西之怒灾变「龙王孽焰」：锚定光标区。蓄势 40t 龙吼与龙影掠空；
    /// 爆发 140t 龙影俯冲 3 趟，每趟身后拖焰幕帘（分段线判定 ×1.5，叠贝特西诅咒）；
    /// 余韵 120t 地面咒火滩（0.3×，附燃烧）。判定线段与可见龙影路径同源
    /// </summary>
    internal class GsDragonWrathDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 40;
        public override int MainTicks => 138;
        public override int AftermathTicks => 120;

        protected override int HitTickRate => 30;

        protected override float TickDamageMul => Phase == 2 ? 0.3f : 1.5f;

        /// <summary>每趟俯冲时长（3 趟共 138t）</summary>
        private const int DiveTicks = 46;
        /// <summary>焰幕帘半宽</summary>
        private const float FlameHalf = 30f;
        /// <summary>咒火滩半宽/半高</summary>
        private const float BedHalfW = 180f;
        private const float BedHalfH = 18f;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color BetsyOrange = new(255, 150, 60);
        internal static readonly Color BetsyEmber = new(210, 70, 30);

        /// <summary>首趟方向（identity 定相，判定与绘制同源）</summary>
        private float BaseDir => Hash01(3) > 0.5f ? 1f : -1f;

        /// <summary>趟 k 的起点/终点（相对锚点）</summary>
        private void DivePath(int k, out Vector2 start, out Vector2 end) {
            float dir = BaseDir * (k % 2 == 0 ? 1f : -1f);
            start = Projectile.Center + new Vector2(dir * 520f, -360f);
            end = Projectile.Center + new Vector2(-dir * 520f, 140f);
        }

        /// <summary>当前趟龙影位置（趟内缓入缓出）</summary>
        private Vector2 DivePos(int k, float diveT) {
            DivePath(k, out Vector2 start, out Vector2 end);
            float prog = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(diveT / DiveTicks, 0f, 1f));
            return Vector2.Lerp(start, end, prog);
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Volume = 0.9f }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, BetsyEmber.ToVector3() * 0.35f * (t / (float)OmenTicks));
        }

        protected override void MainUpdate(int t) {
            int k = Math.Min(t / DiveTicks, 2);
            float diveT = t - k * DiveTicks;
            if (diveT == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.85f, Pitch = -0.1f + 0.08f * k }, Projectile.Center);
            }
            //龙影沿线喷焰（约 2/帧，守预算）
            if (!VaultUtils.isServer) {
                Vector2 pos = DivePos(k, diveT);
                Lighting.AddLight(pos, BetsyOrange.ToVector3() * 0.7f);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(pos + Main.rand.NextVector2Circular(26f, 26f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.8f)),
                        BetsyOrange, Main.rand.NextFloat(0.5f, 0.85f));
                }
            }
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0) {
                if (Projectile.localAI[2] == 0f) {
                    //咒火滩钉在锚点下方地面
                    Projectile.localAI[2] = 1f;
                    Projectile.localAI[0] = Projectile.Center.X;
                    Projectile.localAI[1] = FindGroundY(Projectile.Center);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                }
            }
            if (Projectile.localAI[2] != 0f) {
                Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1] - BedHalfH);
            }
            if (!VaultUtils.isServer && t % 5 == 0) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-BedHalfW, BedHalfW), 2f);
                PRTLoader.NewParticle<PRT_HellFlame>(pos, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)),
                    Color.Lerp(BetsyOrange, BetsyEmber, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.7f));
            }
            Lighting.AddLight(Projectile.Center, BetsyEmber.ToVector3() * 0.5f * (1f - t / (float)AftermathTicks));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase == 1) {
                //焰幕帘：当前趟起点到龙影现位的线段带
                int t = Elapsed - OmenTicks;
                int k = Math.Min(t / DiveTicks, 2);
                DivePath(k, out Vector2 start, out _);
                Vector2 head = DivePos(k, t - k * DiveTicks);
                float unused = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    start, head, FlameHalf * 2f, ref unused);
            }
            if (Phase == 2 && Projectile.localAI[2] != 0f) {
                Rectangle bed = new((int)(Projectile.Center.X - BedHalfW), (int)(Projectile.Center.Y - BedHalfH),
                    (int)(BedHalfW * 2f), (int)(BedHalfH * 2f + 10f));
                return bed.Intersects(targetHitbox);
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase == 1) {
                target.AddBuff(BuffID.BetsysCurse, 300);
            }
            else if (Phase == 2) {
                target.AddBuff(BuffID.OnFire3, 120);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            Main.instance.LoadNPC(NPCID.DD2Betsy);
            Texture2D betsy = TextureAssets.Npc[NPCID.DD2Betsy].Value;
            int frames = Math.Max(1, Main.npcFrameCount[NPCID.DD2Betsy]);
            int frameH = betsy.Height / frames;
            Rectangle src = new(0, frameH * (Elapsed / 6 % frames), betsy.Width, frameH);
            Vector2 origin = new Vector2(betsy.Width, frameH) * 0.5f;

            if (Phase == 0) {
                //掠空龙影：横穿锚点上空的暗剪影
                float prog = Elapsed / (float)OmenTicks;
                Vector2 pos = Projectile.Center + new Vector2((prog * 2f - 1f) * BaseDir * 640f, -290f);
                bool faceLeft = BaseDir > 0f;
                Main.EntitySpriteDraw(betsy, pos - Main.screenPosition, src, new Color(24, 12, 30) * 0.6f, 0f, origin, 0.7f,
                    faceLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
            else if (Phase == 1) {
                int t = Elapsed - OmenTicks;
                int k = Math.Min(t / DiveTicks, 2);
                DivePath(k, out Vector2 start, out Vector2 end);
                Vector2 head = DivePos(k, t - k * DiveTicks);
                //已扫过路径的焰幕：沿线布脉动焰点（identity 定相）
                if (glow != null) {
                    float len = Vector2.Distance(start, head);
                    int dots = Math.Min(10, (int)(len / 90f) + 1);
                    for (int i = 0; i < dots; i++) {
                        float f = dots <= 1 ? 0f : i / (float)(dots - 1);
                        Vector2 dot = Vector2.Lerp(start, head, f);
                        float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 1.9f + Projectile.identity * 0.53f);
                        Main.EntitySpriteDraw(glow, dot - Main.screenPosition, null,
                            BetsyOrange with { A = 0 } * (0.5f * pulse), 0f, glow.Size() * 0.5f,
                            FlameHalf * 2.6f / glow.Width * pulse, SpriteEffects.None, 0);
                        Main.EntitySpriteDraw(glow, dot - Main.screenPosition, null,
                            BetsyEmber with { A = 0 } * (0.35f * pulse), 0f, glow.Size() * 0.5f,
                            FlameHalf * 4f / glow.Width, SpriteEffects.None, 0);
                    }
                }
                //俯冲龙影：暗剪影 + 熔橙描边
                float rot = (end - start).ToRotation();
                bool flip = (end - start).X > 0f;
                //贴图基准朝左：向右俯冲时水平翻转并校正角度
                float drawRot = flip ? rot + MathHelper.Pi : rot;
                Main.EntitySpriteDraw(betsy, head - Main.screenPosition, src, BetsyOrange with { A = 0 } * 0.3f,
                    drawRot, origin, 0.78f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
                Main.EntitySpriteDraw(betsy, head - Main.screenPosition, src, new Color(26, 12, 30) * 0.8f,
                    drawRot, origin, 0.75f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
            else if (glow != null) {
                //咒火滩：贴地双层焰光
                float fade = MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
                for (int i = -1; i <= 1; i++) {
                    Vector2 pos = Projectile.Center + new Vector2(i * BedHalfW * 0.55f, 0f) - Main.screenPosition;
                    float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + i * 2.1f + Projectile.identity * 0.71f);
                    Main.EntitySpriteDraw(glow, pos, null, BetsyEmber with { A = 0 } * (0.45f * fade * pulse), 0f,
                        glow.Size() * 0.5f, new Vector2(160f, 40f) / glow.Width * pulse, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
