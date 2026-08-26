using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenSlime
{
    /// <summary>
    /// 棱镜光束：两颗折射水晶之间的杀阵连线。
    /// ai[0]/ai[1]=两端水晶 identity(跨端身份，出生即定)；
    /// ai[2]=色相相位，收线时 owner +10 写入并 netUpdate(相位取模还原)。
    /// 断线裁决(端点消亡/视线被断)只在 owner 端做；
    /// 成本控制：视线复查按 identity 错帧分摊，碰撞先过线段包围盒粗筛
    /// </summary>
    internal class RefractionWaltzBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override Terraria.Localization.LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "棱镜光束");

        internal const int BeamDamage = 85;
        private const int ExpandTime = 8;
        private const int CollapseTime = 12;
        private const float MaxWidth = 22f;
        /// <summary>视线复查周期(帧)，按 identity 错开</summary>
        private const int LosRecheckPeriod = 12;
        /// <summary>和弦脉冲时长(帧)</summary>
        private const int ChordFrames = 22;

        private float HuePhase => Projectile.ai[2] % 10f;
        /// <summary>owner 写 +10 进入收线，各端同步跟进</summary>
        private bool CollapseFlag => Projectile.ai[2] >= 10f;

        private int timer;
        private int collapseTimer;
        /// <summary>本端自查端点失效(同步没跟上时的本地兜底)</summary>
        private bool localCollapse;
        /// <summary>端点至少成功解析过一次(远端水晶包未到前不画不响不判)</summary>
        private bool resolvedOnce;
        private float beamWidth;
        /// <summary>和弦脉冲进度 1→0</summary>
        private float chordPulse;
        private int cachedA = -1;
        private int cachedB = -1;
        private Vector2 endA;
        private Vector2 endB;
        private Rectangle segmentBounds;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        private bool Collapsing => CollapseFlag || localCollapse;

        /// <summary>按 identity 缓存端点索引，失效重扫(数组槽位不是跨端身份)</summary>
        private Projectile ResolveCrystal(ref int cache, int identity) {
            int crystalType = ModContent.ProjectileType<RefractionWaltzCrystalProj>();
            if (cache >= 0 && cache < Main.maxProjectiles) {
                Projectile p = Main.projectile[cache];
                if (p.active && p.type == crystalType && p.owner == Projectile.owner && p.identity == identity) {
                    return p;
                }
            }
            cache = -1;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == crystalType && p.owner == Projectile.owner && p.identity == identity) {
                    cache = i;
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            timer++;
            Projectile.velocity = Vector2.Zero;

            Projectile crystalA = ResolveCrystal(ref cachedA, (int)Projectile.ai[0]);
            Projectile crystalB = ResolveCrystal(ref cachedB, (int)Projectile.ai[1]);
            bool endpointsValid = crystalA?.ModProjectile is RefractionWaltzCrystalProj ca && ca.LinkReady
                && crystalB?.ModProjectile is RefractionWaltzCrystalProj cb && cb.LinkReady;

            if (endpointsValid) {
                endA = crystalA.Center;
                endB = crystalB.Center;

                //首次解析成功=出生拍：挂弦声+和弦脉冲传遍全网(各端看到新光束时各自触发，纯演出)
                if (!resolvedOnce) {
                    resolvedOnce = true;
                    TriggerChordOnSiblings();
                    if (!VaultUtils.isServer) {
                        Vector2 mid = (endA + endB) * 0.5f;
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.45f, MaxInstances = 5 }, mid);
                        SoundEngine.PlaySound(SoundID.Item26 with {
                            Volume = 0.5f,
                            Pitch = 0.1f + Projectile.identity % 5 * 0.14f,
                            MaxInstances = 4
                        }, mid);
                    }
                }
            }

            //断线裁决：owner 端权威
            if (Projectile.owner == Main.myPlayer) {
                if (!endpointsValid) {
                    StartCollapse();
                }
                else if ((timer + Projectile.identity) % LosRecheckPeriod == 0
                    && !Collision.CanHitLine(endA, 1, 1, endB, 1, 1)) {
                    StartCollapse();
                }
            }
            //远端兜底：端点没了就本地收线，不等包
            if (!endpointsValid && timer > 4) {
                localCollapse = true;
            }

            //宽度包络
            if (Collapsing) {
                collapseTimer++;
                float p = MathHelper.Clamp(collapseTimer / (float)CollapseTime, 0f, 1f);
                beamWidth = MaxWidth * (1f - p) * (1f - p);
                if (collapseTimer >= CollapseTime && Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
            }
            else {
                beamWidth = MaxWidth * QueenMotion.SnapOut(timer / (float)ExpandTime, 4);
            }

            //和弦脉冲推进
            if (chordPulse > 0f) {
                chordPulse = Math.Max(0f, chordPulse - 1f / ChordFrames);
            }

            if (resolvedOnce) {
                Projectile.Center = (endA + endB) * 0.5f;
                RebuildSegmentBounds();
            }

            //沿束点亮
            if (resolvedOnce && beamWidth > MaxWidth * 0.4f) {
                for (int i = 0; i < 4; i++) {
                    Lighting.AddLight(Vector2.Lerp(endA, endB, i / 3f),
                        QueenMotion.PrismHue(HuePhase).ToVector3() * 0.4f);
                }
            }
        }

        /// <summary>线段包围盒(含宽度余量)，碰撞粗筛用</summary>
        private void RebuildSegmentBounds() {
            float pad = beamWidth + 8f;
            float minX = Math.Min(endA.X, endB.X) - pad;
            float minY = Math.Min(endA.Y, endB.Y) - pad;
            float maxX = Math.Max(endA.X, endB.X) + pad;
            float maxY = Math.Max(endA.Y, endB.Y) + pad;
            segmentBounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }

        private void StartCollapse() {
            if (CollapseFlag) {
                return;
            }
            Projectile.ai[2] += 10f;
            Projectile.netUpdate = true;
        }

        /// <summary>新弦入网：全网既有光束同奏一记和弦(本端演出，各端自触发)</summary>
        private void TriggerChordOnSiblings() {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == Projectile.type && proj.owner == Projectile.owner
                    && proj.whoAmI != Projectile.whoAmI
                    && proj.ModProjectile is RefractionWaltzBeamProj sibling) {
                    sibling.chordPulse = 1f;
                }
            }
        }

        /// <summary>展开过半才咬人，收线不咬，端点未解析不咬</summary>
        public override bool? CanDamage() {
            return resolvedOnce && !Collapsing && beamWidth >= MaxWidth * 0.5f ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //粗筛：出线段包围盒直接否，省掉逐敌线段判
            if (!segmentBounds.Intersects(targetHitbox)) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                endA, endB, beamWidth * 0.62f, ref p);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.HasBuff(ModContent.BuffType<RefractionTag>())) {
                modifiers.FinalDamage *= RefractionTag.DamageTakenMult;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //盖折光，骑原版 buff 同步
            target.AddBuff(ModContent.BuffType<RefractionTag>(), RefractionTag.TagFrames);
            //触线火花(owner 端装饰，判定广播由 StrikeNPC 承担)
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.TintableDust, Main.rand.NextVector2Circular(2.5f, 2.5f), 120,
                        QueenMotion.PrismHue(HuePhase + Main.rand.NextFloat(0.3f)), 1.2f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //断弦余韵：沿线洒几点光尘，活过光束本体
            if (VaultUtils.isServer || !resolvedOnce) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 pos = Vector2.Lerp(endA, endB, i / 4f);
                Dust d = Dust.NewDustPerfect(pos, DustID.TintableDust,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 140,
                    QueenMotion.GetQueenDustColor(), 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>光束主体(棱彩色散着色器，Additive 图元)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (!resolvedOnce || beamWidth <= 1.2f) {
                return;
            }
            Effect effect = EffectLoader.QueenPrismBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return; //着色器缺失时由加色层光子/端辉兜底，无隐形判定
            }

            Vector2 a = endA + RefractionBobOf(cachedA);
            Vector2 b = endB + RefractionBobOf(cachedB);
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float halfW = beamWidth * 2.8f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((a + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((a - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((b + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((b - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            //共享参数化 shader：调用点全参数重设；和弦脉冲抬亮全线
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f)
                * (1f + chordPulse * 0.35f));
            effect.Parameters["uHueSeed"]?.SetValue(HuePhase);
            effect.Parameters["seed"]?.SetValue(Projectile.identity * 0.137f % 1f);
            effect.Parameters["uBeamLen"]?.SetValue(Vector2.Distance(a, b));
            //噪声显式绑 s1(shader 内 register(s1))，用毕交还
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
            device.Textures[1] = null;

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>端点水晶的呼吸浮动，与晶壳绘制同式(缓存索引直读)</summary>
        private Vector2 RefractionBobOf(int cacheIdx) {
            if (cacheIdx >= 0 && cacheIdx < Main.maxProjectiles && Main.projectile[cacheIdx].active) {
                return RefractionWaltzCrystalProj.VisualBob(Main.projectile[cacheIdx]);
            }
            return Vector2.Zero;
        }

        /// <summary>端辉、行进光子与和弦亮头(真 Additive 批，染色带 alpha)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (!resolvedOnce || beamWidth <= 1.2f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Color hue = QueenMotion.PrismHue(HuePhase);
            Vector2 a = endA + RefractionBobOf(cachedA) - Main.screenPosition;
            Vector2 b = endB + RefractionBobOf(cachedB) - Main.screenPosition;
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            float dist = Vector2.Distance(a, b);
            float flick = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 36f + Projectile.identity);

            //两端弦点辉光
            spriteBatch.Draw(glow, a, null, hue * (0.55f * opacity), 0f, glow.Size() / 2f, 0.6f * flick, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, b, null, hue * (0.55f * opacity), 0f, glow.Size() / 2f, 0.6f * flick, SpriteEffects.None, 0f);

            //行进光子(读出能量沿弦流动)
            const int photons = 3;
            for (int i = 0; i < photons; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 1.1f + i / (float)photons + Projectile.identity * 0.29f) % 1f;
                Vector2 pos = a + dir * dist * along;
                float pScale = 0.3f * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                spriteBatch.Draw(glow, pos, null, Color.White * (0.55f * opacity), 0f, glow.Size() / 2f, pScale, SpriteEffects.None, 0f);
            }

            //和弦脉冲：白热亮头沿弦扫过，尾随一截拖光
            if (chordPulse > 0f) {
                float head = 1f - chordPulse;
                Vector2 headPos = a + dir * dist * head;
                float pulseGlow = QueenMotion.Bump(head);
                spriteBatch.Draw(glow, headPos, null, Color.White * (0.85f * opacity * pulseGlow), 0f,
                    glow.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, headPos, null, hue * (0.75f * opacity * pulseGlow),
                    head * 9f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
                for (int i = 1; i <= 3; i++) {
                    float t = head - i * 0.05f;
                    if (t <= 0f) {
                        continue;
                    }
                    spriteBatch.Draw(glow, a + dir * dist * t, null,
                        hue * (0.4f * opacity * pulseGlow * (1f - i * 0.28f)), 0f,
                        glow.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
