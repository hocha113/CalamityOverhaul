using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 雷枢天柱：地标预警→天雷贯落→残响消散；锚点在地面；
    /// ai[0]=预警帧 ai[1]=柱高px（默认1400）
    /// </summary>
    internal class CultistThunderColumn : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int StrikeWindow = 16;
        private const int FadeTime = 26;
        private const float HitWidth = 76f;

        private int TelegraphTime => Math.Max((int)Projectile.ai[0], 20);
        private float ColumnHeight => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1400f;

        private float Timer => Projectile.localAI[0];
        private bool Struck => Timer > TelegraphTime;

        //柱体 1400px 高而 hitbox 仅 30px：默认 480 余量会在玩家高于落点时整根剔除
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            //首帧缓存伤害并按预警参数定寿命（各端确定性）
            if ((int)Timer == 1) {
                Projectile.localAI[1] = Projectile.damage;
                Projectile.timeLeft = TelegraphTime + StrikeWindow + FadeTime;
            }
            Projectile.damage = Struck && Timer <= TelegraphTime + StrikeWindow ? (int)Projectile.localAI[1] : 0;

            if ((int)Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 6 }, Projectile.Center);
            }

            //预警期地面电花攀升
            if (!Struck && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CultistVolt>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f),
                    -Vector2.UnitY * Main.rand.NextFloat(2f, 6f),
                    CultistPalette.ThunderBright, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(10, 18));
            }

            //贯落帧
            if ((int)Timer == TelegraphTime + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.95f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
                CultistScreenFX.Punch(Projectile.Center, 6f, 14, "CultistThunder", Vector2.UnitY);
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Thunder, 1.5f);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_CultistVolt>(
                        Projectile.Center + new Vector2(0f, -Main.rand.NextFloat(ColumnHeight * 0.8f)),
                        Main.rand.NextVector2Circular(4f, 1.5f),
                        CultistPalette.ThunderBright, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(12, 22));
                }
                //落点地面电痕：沿地横向爬行的残响电弧（活过整个柱体淡出）
                for (int i = 0; i < 4; i++) {
                    float side = i % 2 == 0 ? 1f : -1f;
                    float ang = side > 0 ? Main.rand.NextFloat(-0.35f, 0.1f) : MathHelper.Pi + Main.rand.NextFloat(-0.1f, 0.35f);
                    PRTLoader.NewParticle<PRT_CultistArcTrace>(
                        Projectile.Center + new Vector2(side * Main.rand.NextFloat(6f, 26f), Main.rand.NextFloat(-4f, 4f)),
                        Vector2.Zero, CultistPalette.ThunderMain, Main.rand.NextFloat(0.8f, 1.2f))
                        ?.Configure(ang, Main.rand.NextFloat(40f, 80f), Main.rand.Next(26, 42));
                }
            }

            //回闪帧（真实闪电的 restrike）：主击后7帧一次短促二击
            if ((int)Timer == TelegraphTime + 8 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.55f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
            }

            //消散期电离残响：柱路径上升的离子微粒
            if (Struck && Timer > TelegraphTime + StrikeWindow && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CultistVolt>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -Main.rand.NextFloat(ColumnHeight * 0.6f)),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f),
                    CultistPalette.ThunderMain, Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(10, 18));
            }

            if (Struck) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, 300f), CultistPalette.ThunderMain.ToVector3() * 1.4f);
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.ThunderMain.ToVector3() * 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Struck || Timer > TelegraphTime + StrikeWindow) {
                return false;
            }
            //竖直贯柱碰撞
            Rectangle column = new(
                (int)(Projectile.Center.X - HitWidth / 2f),
                (int)(Projectile.Center.Y - ColumnHeight),
                (int)HitWidth,
                (int)ColumnHeight + 20);
            return column.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 45);
        }

        /// <summary>确定性抖动哈希</summary>
        private static float JitterHash(int seed, int i) {
            float h = (float)Math.Sin(seed * 12.9898f + i * 78.233f) * 43758.5453f;
            return h - (float)Math.Floor(h);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 groundPos = Projectile.Center - Main.screenPosition;
            Texture2D line = CWRAsset.LightShot.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D bolt = CWRAsset.ThunderTrail.Value;
            Texture2D scorch = CWRAsset.TearSpread01.Value;

            //落点焦痕：真alpha贴图，主击后驻留到弹体尾（先画，实体批）
            if (Struck) {
                float scorchLife = 1f - MathHelper.Clamp((Timer - TelegraphTime) / (StrikeWindow + FadeTime + 10f), 0f, 1f);
                sb.Draw(scorch, groundPos + new Vector2(0f, 6f), null, new Color(30, 18, 48) * (0.75f * scorchLife + 0.1f),
                    0f, scorch.Size() / 2f, new Vector2(0.5f, 0.16f), SpriteEffects.None, 0f);
            }

            //贴地环（共享冲击环 shader，squish 透视椭圆；加色合成顺序无关，先于加色段画）
            if (!Struck) {
                //收缩预警环：落点越迫近环越小越亮
                float warnT = Timer / TelegraphTime;
                float warnScale = MathHelper.Lerp(1.15f, 0.22f, warnT * warnT);
                ShockRingDraw.Draw(sb, Projectile.Center, 53f * warnScale, 11f * warnScale + 3f,
                    CultistPalette.ThunderBright, CultistPalette.ThunderBright, CultistPalette.ThunderMain,
                    0.55f * warnT + 0.15f, squish: 0.4f, timeSeed: Projectile.identity * 0.31f);
            }
            else {
                //落点冲击环：主击帧展开一圈
                float impactT = MathHelper.Clamp((Timer - TelegraphTime) / 14f, 0f, 1f);
                if (impactT < 1f) {
                    float impactR = 53f * (0.25f + impactT * 1.5f);
                    ShockRingDraw.Draw(sb, Projectile.Center, impactR, impactR * 0.24f + 3f,
                        CultistPalette.ThunderBright, CultistPalette.ThunderBright, CultistPalette.ThunderMain,
                        0.8f * (1f - impactT), squish: 0.4f, timeSeed: Projectile.identity * 0.31f);
                }
            }

            CultistRenderHelper.BeginAdditive(sb);

            if (!Struck) {
                //预警：竖直细线（亮头贴地被落点光盖住，渐淡端朝天=顶端软断口；收缩环在实体批阶段）
                float t = Timer / TelegraphTime;
                float warn = 0.3f + 0.5f * t + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f);
                sb.Draw(line, groundPos, null, CultistPalette.ThunderMain * (0.4f * warn),
                    -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                    new Vector2(ColumnHeight / line.Width, 0.1f + 0.12f * t), SpriteEffects.None, 0f);
                //地面光点
                sb.Draw(glow, groundPos, null, CultistPalette.ThunderBright * (0.4f * t + 0.15f),
                    0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }
            else {
                float sincePeak = Timer - TelegraphTime;
                float total = StrikeWindow + FadeTime;
                float fade = 1f - MathHelper.Clamp(sincePeak / total, 0f, 1f);
                //双峰包络：主击瞬时峰+7帧后回闪二峰（真实闪电 restrike 的频闪签名）
                float strike1 = (float)Math.Exp(-sincePeak * 0.30f);
                float strike2 = sincePeak >= 7f ? (float)Math.Exp(-(sincePeak - 7f) * 0.36f) * 0.75f : 0f;
                float env = MathHelper.Clamp(strike1 + strike2, 0f, 1f);
                float coreW = MathHelper.Lerp(0.65f, 0.1f, sincePeak / total);

                //辉光衬底柱：LightShot 亮端贴地渐淡向天（顶端天然软出）
                sb.Draw(line, groundPos, null, CultistPalette.ThunderDeep * (0.65f * fade + 0.3f * env),
                    -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                    new Vector2(ColumnHeight / line.Width, coreW * 3.4f), SpriteEffects.None, 0f);
                sb.Draw(line, groundPos, null, CultistPalette.ThunderMain * (0.8f * fade + 0.4f * env),
                    -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                    new Vector2(ColumnHeight / line.Width, coreW * 1.7f), SpriteEffects.None, 0f);

                //分形主干：8段折线，每2帧重抖一次（端点相接=几何连续，无断口）
                int slice = (int)(Timer / 2f) + Projectile.identity * 13;
                const int segCount = 8;
                float widthEnv = 0.30f * fade + 0.34f * env;
                Vector2 prev = groundPos;
                for (int i = 1; i <= segCount; i++) {
                    float yFrac = i / (float)segCount;
                    //顶端回中（雷源锚点），中段抖幅最大
                    float amp = 30f * (float)Math.Sin(yFrac * MathHelper.Pi) * (0.6f + 0.4f * env);
                    float ox = (JitterHash(slice, i) - 0.5f) * 2f * amp;
                    Vector2 node = groundPos + new Vector2(ox, -ColumnHeight * yFrac);
                    Vector2 span = node - prev;
                    sb.Draw(bolt, prev, null, Color.White * (0.85f * env + 0.35f * fade),
                        span.ToRotation(), new Vector2(0f, bolt.Height / 2f),
                        new Vector2(span.Length() / bolt.Width, widthEnv), SpriteEffects.None, 0f);
                    sb.Draw(bolt, prev, null, CultistPalette.ThunderBright * (0.5f * env + 0.3f * fade),
                        span.ToRotation(), new Vector2(0f, bolt.Height / 2f),
                        new Vector2(span.Length() / bolt.Width, widthEnv * 2.1f), SpriteEffects.None, 0f);
                    prev = node;

                    //分支须：主干节点向外劈出的短叉（每节点约1/3概率）
                    if (JitterHash(slice, 50 + i) > 0.62f && i < segCount) {
                        float bAng = -MathHelper.PiOver2 + (JitterHash(slice, 70 + i) - 0.5f) * 2.4f;
                        float bLen = 30f + JitterHash(slice, 90 + i) * 55f;
                        sb.Draw(bolt, node, null, CultistPalette.ThunderBright * (0.5f * env + 0.2f * fade),
                            bAng, new Vector2(0f, bolt.Height / 2f),
                            new Vector2(bLen / bolt.Width, widthEnv * 0.55f), SpriteEffects.None, 0f);
                    }
                }

                //落点：光爆（扩散环已移到实体批阶段走共享冲击环 shader）
                sb.Draw(glow, groundPos, null, CultistPalette.ThunderBright * (env + 0.25f * fade),
                    0f, glow.Size() / 2f, 1.1f * env + 0.35f, SpriteEffects.None, 0f);
            }

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
