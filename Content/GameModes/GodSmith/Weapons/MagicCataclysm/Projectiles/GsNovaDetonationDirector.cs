using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 星云烈焰灾变「新星引爆」：蓄势 35t 反向收缩标记环；爆发 90t 三层错拍环爆
    /// （80/140/200px，×1.5/×1.2/×0.9，错拍 18t 分帧摊开），t36 起中心引力窟吸引非 Boss 0.6s，
    /// NeutronWarp 引力透镜全程压屏（仅本地视口附近生效）；余韵 90t 引力尘埃缓旋。<br/>
    /// ai[1]=锚定目标 whoAmI（-1 锚定触发点），目标亡则驻留原地
    /// </summary>
    internal class GsNovaDetonationDirector : GsCataclysmDirectorProj, IWarpDrawable
    {
        public override int OmenTicks => 35;
        public override int MainTicks => 90;
        public override int AftermathTicks => 90;

        /// <summary>三层环爆半径</summary>
        private static readonly float[] RingRadius = [80f, 140f, 200f];
        /// <summary>三层伤害倍率</summary>
        private static readonly float[] RingMul = [1.5f, 1.2f, 0.9f];
        /// <summary>层间错拍</summary>
        private const int RingGap = 18;
        /// <summary>每层判定窗</summary>
        private const int RingWindow = 8;
        /// <summary>引力窟起止（爆发相内帧）</summary>
        private const int WellStart = 36;
        private const int WellEnd = 72;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color NovaPink = new(255, 120, 210);
        internal static readonly Color NovaViolet = new(150, 80, 235);
        internal static readonly Color NovaDeep = new(58, 24, 96);

        protected override int HitTickRate => 10;

        private int AnchorNpc => (int)Projectile.ai[1];

        /// <summary>锚定目标存活则跟随，亡则驻留最后位置（各端按同步的 NPC 态一致判断）</summary>
        protected override void UpdateAnchor() {
            int idx = AnchorNpc;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[idx];
            if (npc.active && npc.CanBeChasedBy()) {
                Projectile.Center = npc.Center;
            }
            else {
                Projectile.ai[1] = -1f;
            }
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, NovaPink.ToVector3() * 0.5f * (t / (float)OmenTicks));
            //收缩期向心星尘（约 1/2 帧）
            if (!VaultUtils.isServer && t % 2 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(120f, 220f);
                PRTLoader.NewParticle<PRT_Sparkle>(pos,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f),
                    Color.Lerp(NovaPink, NovaViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(NovaPink, 18);
            }
        }

        protected override void MainUpdate(int t) {
            //三层错拍起爆帧：音效 + 吸积盘冲击粒子
            for (int k = 0; k < 3; k++) {
                if (t != k * RingGap) {
                    continue;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.8f, Pitch = -0.25f + 0.18f * k }, Projectile.Center);
                    for (int i = 0; i < 3; i++) {
                        float angle = MathHelper.TwoPi / 3f * i + k * 0.7f;
                        Vector2 pos = Projectile.Center + angle.ToRotationVector2() * RingRadius[k] * 0.7f;
                        PRTLoader.NewParticle<PRT_AccretionDiskImpact>(pos, angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2.2f,
                            Color.Lerp(NovaPink, NovaViolet, i / 3f), Main.rand.NextFloat(0.55f, 0.8f))
                            ?.Configure(26, 0.05f);
                    }
                }
            }
            if (t == WellStart && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.75f, Pitch = -0.5f }, Projectile.Center);
            }

            //引力窟：非 Boss 拉向中心（权威端改 NPC 速度，自然入同步）
            if (Authoritative && t >= WellStart && t < WellEnd) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy() || npc.knockBackResist <= 0f) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist > 260f || dist < 24f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.85f * npc.knockBackResist;
                    if (npc.velocity.Length() > 12f) {
                        npc.velocity *= 12f / npc.velocity.Length();
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, NovaViolet.ToVector3() * 0.8f);
        }

        protected override void AftermathUpdate(int t) {
            float fade = 1f - t / (float)AftermathTicks;
            Lighting.AddLight(Projectile.Center, NovaDeep.ToVector3() * 1.4f * fade);
            //引力尘埃缓旋（约 1/3 帧）
            if (!VaultUtils.isServer && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                    Color.Lerp(NovaViolet, NovaDeep, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.4f, 0.7f) * fade + 0.2f)
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(90f, 190f), 34);
            }
        }

        /// <summary>只有爆发段有判定：环带扩张窗</summary>
        public override bool? CanDamage() => Phase == 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 1) {
                return false;
            }
            int mainT = Elapsed - OmenTicks;
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            for (int k = 0; k < 3; k++) {
                int age = mainT - k * RingGap;
                if (age < 0 || age >= RingWindow) {
                    continue;
                }
                float r = MathHelper.Lerp(RingRadius[k] * 0.6f, RingRadius[k], age / (float)RingWindow);
                if (Math.Abs(dist - r) < 46f + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f) {
                    return true;
                }
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int layer = Math.Clamp((Elapsed - OmenTicks) / RingGap, 0, 2);
            modifiers.FinalDamage *= RingMul[layer];
        }

        //==================== 引力透镜（屏幕级，只在本地视口附近生效） ====================

        public bool DontUseBlueshiftEffect() => true;

        public bool CanDrawCustom() => false;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        private float WarpEnvelope() {
            if (Phase == 0) {
                return 0f;
            }
            if (Phase == 1) {
                int mainT = Elapsed - OmenTicks;
                if (mainT < WellStart) {
                    return 0.14f + 0.10f * (mainT / (float)WellStart);
                }
                return MathHelper.Lerp(0.24f, 0.4f, MathHelper.Clamp((mainT - WellStart) / 12f, 0f, 1f));
            }
            int aftT = Elapsed - OmenTicks - MainTicks;
            return MathHelper.Lerp(0.3f, 0f, aftT / (float)AftermathTicks);
        }

        public void Warp() {
            float intensity = WarpEnvelope();
            if (intensity <= 0.03f) {
                return;
            }
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            if (Vector2.Distance(Projectile.Center, screenCenter) > 1500f) {
                return;
            }
            NeutronWarpHelper.DrawWarp(Projectile.Center, 560f, 560f, intensity * 0.85f, intensity, 0f, "GravitationalLens", 0.42f);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int e = Elapsed;
            //蓄势：反向收缩标记环
            if (Phase == 0) {
                float prog = VaultUtils.EaseOutQuad(e / (float)OmenTicks);
                float r = MathHelper.Lerp(240f, 16f, prog);
                ShockRingDraw.Draw(sb, Projectile.Center, r, 12f, NovaPink, NovaViolet, NovaDeep,
                    0.35f + 0.55f * prog, tearPx: 10f, innerGlow: 0.2f, timeSeed: Projectile.identity * 0.31f);
            }
            //爆发：三层扩散环余辉
            if (Phase == 1) {
                int mainT = e - OmenTicks;
                for (int k = 0; k < 3; k++) {
                    int age = mainT - k * RingGap;
                    if (age < 0 || age >= 26) {
                        continue;
                    }
                    float prog = VaultUtils.EaseOutCubic(age / 26f);
                    float r = MathHelper.Lerp(RingRadius[k] * 0.55f, RingRadius[k] * 1.18f, prog);
                    ShockRingDraw.Draw(sb, Projectile.Center, r, 22f - 5f * k,
                        Color.Lerp(Color.White, NovaPink, 0.4f), NovaPink, NovaViolet,
                        (1f - prog) * 0.95f, innerGlow: 0.35f, timeSeed: k * 1.7f);
                }
            }
            //中心暗核与吸积辉光（引力窟期与余韵）
            float warpEnv = WarpEnvelope();
            Texture2D glow = GlowTex?.Value;
            if (glow != null && warpEnv > 0.05f) {
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                float spin = Main.GlobalTimeWrappedHourly * 2.1f + Projectile.identity * 0.53f;
                //暗核吸光体（AlphaBlend 深色）
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(10, 4, 24) * (1.6f * warpEnv),
                    spin, glow.Size() * 0.5f, 0.5f * warpEnv + 0.12f, SpriteEffects.None, 0);
                //侧倾吸积盘双层（加色）
                Main.EntitySpriteDraw(glow, drawPos, null, NovaViolet with { A = 0 } * (1.5f * warpEnv),
                    spin * 0.6f, glow.Size() * 0.5f, new Vector2(0.9f, 0.3f) * warpEnv * 1.6f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, NovaPink with { A = 0 } * (1.1f * warpEnv),
                    -spin * 0.8f, glow.Size() * 0.5f, new Vector2(0.6f, 0.2f) * warpEnv * 1.6f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
