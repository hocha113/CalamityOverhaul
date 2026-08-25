using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 镭射扫削射线：30 帧虚线预扫（无伤害，宽带展示完整弧程与真实覆盖区）→
    /// 6 帧点燃拍（角度跳回起扫位冻结，聚能静默的吸气）→ 34 帧热光柱同弧回扫
    /// （宽度 2→36px 缓动展开）→ 10 帧塌缩收束。
    /// 锚在统帅镭射臂口，角度是本地计时的确定性函数。
    /// ai[0]=统帅 whoAmI，ai[1]=扫向 ±1；生成时的 velocity 携带瞄准向量
    /// </summary>
    internal class ScrapSweepBeam : ScrapModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int TelegraphFrames = 30;
        internal const int IgniteFrames = 6;
        internal const int FireFrames = 34;
        internal const int CollapseFrames = 10;
        /// <summary>全程帧数，状态侧对表用</summary>
        internal const int TotalFrames = TelegraphFrames + IgniteFrames + FireFrames + CollapseFrames;

        private const float HalfArc = 0.5f;
        private const float BeamLength = 880f;
        /// <summary>满展宽度：可见亮体 ~30px 档（判定另乘 0.72 宽容系数）</summary>
        private const float MaxWidth = 36f;
        /// <summary>点燃后宽度展开帧数</summary>
        private const int ExpandFrames = 8;

        //焊橙内层 → 锈红外缘（废钢配色，喂给死光着色器）
        private static readonly Vector3 CoreOrange = new(1f, 0.58f, 0.22f);
        private static readonly Vector3 SheathRust = new(1f, 0.3f, 0.1f);

        private NPC Boss => Main.npc[(int)Projectile.ai[0]];
        private float SweepDir => Projectile.ai[1];
        private ref float LocalTimer => ref Projectile.localAI[0];
        private ref float StartAngle => ref Projectile.localAI[1];
        private bool aimed;
        private bool ignited;
        private bool vented;
        private float beamWidth;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = TotalFrames + 4;
        }

        //==================== 相位 ====================

        private bool InTelegraph => LocalTimer <= TelegraphFrames;
        private bool InIgnite => LocalTimer > TelegraphFrames && LocalTimer <= TelegraphFrames + IgniteFrames;
        private bool InFire => LocalTimer > TelegraphFrames + IgniteFrames
            && LocalTimer <= TelegraphFrames + IgniteFrames + FireFrames;
        private bool InCollapse => LocalTimer > TelegraphFrames + IgniteFrames + FireFrames;

        /// <summary>只有热扫段且宽度展满六成才咬人；预扫/点燃/塌缩都是读程</summary>
        public override bool? CanDamage() => InFire && beamWidth > MaxWidth * 0.6f ? null : false;

        /// <summary>当前扫掠角：预扫与热扫走同一条弧（同速同程），点燃拍冻在起扫角，塌缩冻在收扫角</summary>
        private float CurrentAngle() {
            float t = LocalTimer;
            float progress;
            if (t <= TelegraphFrames) {
                progress = t / TelegraphFrames;
            }
            else if (t <= TelegraphFrames + IgniteFrames) {
                progress = 0f;
            }
            else if (t <= TelegraphFrames + IgniteFrames + FireFrames) {
                progress = (t - TelegraphFrames - IgniteFrames) / FireFrames;
            }
            else {
                progress = 1f;
            }
            return StartAngle + SweepDir * progress * HalfArc * 2f;
        }

        private Vector2 Muzzle() {
            NPC boss = Boss;
            if (boss != null && boss.active && boss.ModNPC is ScrapCommander owner) {
                return owner.GetArmPos(ScrapCommander.ArmLaser)
                    + CurrentAngle().ToRotationVector2() * 24f;
            }
            return Projectile.Center;
        }

        /// <summary>宽度包络：点燃后 EaseOutCubic 展开，塌缩段 EaseInQuad 归零，全程小幅呼吸</summary>
        private void UpdateWidth() {
            if (InFire) {
                float t = LocalTimer - TelegraphFrames - IgniteFrames;
                float expand = MathHelper.Clamp(t / ExpandFrames, 0f, 1f);
                beamWidth = MathHelper.Lerp(2f, MaxWidth, VaultUtils.EaseOutCubic(expand));
            }
            else if (InCollapse) {
                float t = (LocalTimer - TelegraphFrames - IgniteFrames - FireFrames) / CollapseFrames;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(MathHelper.Clamp(t, 0f, 1f)));
            }
            else {
                beamWidth = 0f;
                return;
            }
            beamWidth *= 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 36f + Projectile.identity);
        }

        public override void AI() {
            NPC boss = Boss;
            if (boss == null || !boss.active) {
                Projectile.Kill();
                return;
            }
            ScrapCommander owner = boss.ModNPC as ScrapCommander;
            if (!aimed) {
                //生成 velocity 携带瞄准向量：从弧线中点回推起扫角
                aimed = true;
                StartAngle = Projectile.velocity.ToRotation() - SweepDir * HalfArc;
                Projectile.velocity = Vector2.Zero;
            }
            LocalTimer++;
            Projectile.rotation = CurrentAngle();
            Projectile.Center = Muzzle();
            UpdateWidth();

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();

            if (InTelegraph) {
                //机械上弦：预扫期升调的应答声
                if (!Main.dedServ && LocalTimer % 8 == 4) {
                    SoundEngine.PlaySound(SoundID.Item15 with {
                        Volume = 0.22f,
                        Pitch = -0.4f + LocalTimer / TelegraphFrames * 0.6f,
                        MaxInstances = 2
                    }, Projectile.Center);
                }
            }

            if (InFire && !ignited) {
                //==================== 点燃拍：闪光 + 冲击环 + 后坐 + 震屏 ====================
                ignited = true;
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.85f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                owner?.ImpulseArm(ScrapCommander.ArmLaser, -beamDir * 9f);
                ScrapVfx.ShakeNearby(Projectile.Center, 4f);
                if (!Main.dedServ) {
                    var wave = PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        ScrapCommander.WeldOrange * 0.9f, 0.12f);
                    wave?.Configure(Vector2.One, 0f, 0.5f, 10);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            beamDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(5f, 12f),
                            Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(10, 16));
                    }
                }
            }

            if (InCollapse && !vented) {
                //收束起点：镭射口挂余温 + 泄压嘶声 + 一口蒸汽
                vented = true;
                if (owner != null) {
                    owner.LaserHeat = 45;
                }
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                            new Vector2(0f, -0.9f) + Main.rand.NextVector2Circular(0.5f, 0.2f),
                            ScrapCommander.SmokeGray * 0.9f, Main.rand.NextFloat(0.45f, 0.65f))
                            ?.Configure(Main.rand.Next(30, 46), 0.5f, Main.rand.NextFloat(-0.02f, 0.02f));
                    }
                }
            }

            if (InIgnite || InFire) {
                //保持开火帧（枪口大炫光由本弹自己画）
                if (owner != null && owner.LaserFlash < 2) {
                    owner.LaserFlash = 2;
                }
            }

            float widthRatio = beamWidth / MaxWidth;
            if (widthRatio > 0.05f) {
                //沿光束的光照
                for (int i = 0; i < 6; i++) {
                    Lighting.AddLight(Projectile.Center + beamDir * (BeamLength / 6f * i),
                        0.52f * widthRatio, 0.26f * widthRatio, 0.09f * widthRatio);
                }
            }

            if (!Main.dedServ && InFire) {
                //沿线灼烧火花
                if (Main.rand.NextBool(2)) {
                    float d = Main.rand.NextFloat(60f, BeamLength);
                    Vector2 at = Projectile.Center + beamDir * d
                        + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                    PRTLoader.NewParticle<PRT_Spark>(at, Main.rand.NextVector2Circular(2.5f, 2.5f) + beamDir * 2f,
                        new Color(255, 150, 58) * 0.9f, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(true, Main.rand.Next(8, 14));
                }
                //熔渣：从光柱上淌下来的热滴
                if (Main.rand.NextBool(4)) {
                    float d = Main.rand.NextFloat(100f, BeamLength);
                    PRTLoader.NewParticle<PRT_SHPCThermalEmber>(Projectile.Center + beamDir * d,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f)),
                        ScrapCommander.WeldOrange, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(new Color(120, 46, 26), Main.rand.Next(24, 40));
                }
                //扫掠期低频持续震感 + 顶回后坐 + 底噪
                if (LocalTimer % 6 == 0) {
                    ScrapVfx.ShakeNearby(Projectile.Center, 0.8f);
                }
                if (LocalTimer % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.3f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                }
                owner?.ImpulseArm(ScrapCommander.ArmLaser, -beamDir * 0.4f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //碰撞宽度略小于视觉亮体，宽容判定
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BeamLength;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, beamWidth * 0.72f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();

            if (InTelegraph || InIgnite) {
                //==================== 预警：与未来光柱同宽的半透明虚线带 ====================
                float alpha = InTelegraph
                    ? MathHelper.Clamp(LocalTimer / 10f, 0f, 0.5f)
                    : 0.5f + 0.28f * ((LocalTimer - TelegraphFrames) / (float)IgniteFrames);
                //点燃拍虚线并实、增亮：读作"就在这条线上点火"
                float dash = InIgnite ? 0.4f : 1f;
                SpriteBatch sb = Main.spriteBatch;
                ScrapVfx.BeginBeamBatch(sb);
                ScrapVfx.DrawBeam(sb, Projectile.Center, Projectile.Center + dir * BeamLength,
                    MaxWidth, InIgnite ? 0.7f : 0.4f, dash,
                    Projectile.identity * 0.61f, ScrapVfx.BeamCoreWarm, ScrapVfx.BeamEdgeRed,
                    0.01f, 0.08f, alpha);
                ScrapVfx.EndBeamBatch(sb);
                return false;
            }

            if (beamWidth <= 0.5f) {
                return false;
            }

            //==================== 热光柱：焊炬材质厚光柱 ====================
            if (EffectLoader.ScrapHeavyBeam?.Value != null) {
                DrawShaderBeam();
            }
            else {
                DrawFallbackBeam();
            }
            DrawMuzzleFlare();
            return false;
        }

        /// <summary>着色器光柱：电弧芯游走 + 团块热流 + 熔渣崩边 + 锈烟护鞘（专属焊炬材质）</summary>
        private void DrawShaderBeam() {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //光柱面片：崩边与护鞘需要亮体外的余量
            Texture2D quad = VaultAsset.placeholder2.Value;
            float visualWidth = beamWidth * 2.6f;

            Effect shader = EffectLoader.ScrapHeavyBeam.Value;
            float expandProgress = MathHelper.Clamp(
                (LocalTimer - TelegraphFrames - IgniteFrames) / ExpandFrames, 0f, 1f);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            shader.Parameters["uAspect"]?.SetValue(BeamLength / visualWidth);
            shader.Parameters["uExpand"]?.SetValue(expandProgress);
            shader.Parameters["uOpacity"]?.SetValue(InCollapse ? 0.15f + beamWidth / MaxWidth * 0.85f : 1f);
            shader.Parameters["uHeat"]?.SetValue(beamWidth / MaxWidth);
            shader.Parameters["uCoreColor"]?.SetValue(CoreOrange);
            shader.Parameters["uEdgeColor"]?.SetValue(SheathRust);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, new Vector2(0, quad.Height / 2f),
                new Vector2(BeamLength / quad.Width, visualWidth / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>兜底光柱：着色器缺失时退回多层贴图拉伸</summary>
        private void DrawFallbackBeam() {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float lenScale = BeamLength / line.Width;
            float flicker = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 47f);
            Color outer = new Color(255, 76, 26) with { A = 0 };
            Color mid = ScrapCommander.WeldOrange with { A = 0 };
            Color core = Color.White with { A = 0 };

            Main.EntitySpriteDraw(line, drawPos, null, outer * 0.45f, Projectile.rotation, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 3.1f * flicker), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, mid * 0.85f, Projectile.rotation, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 1.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, core * 0.95f, Projectile.rotation, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 0.75f * flicker), SpriteEffects.None, 0);
        }

        /// <summary>枪口炫光：焊橙外晕 + 白热点 + 旋转星芒</summary>
        private void DrawMuzzleFlare() {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float k = beamWidth / MaxWidth;
            float flicker = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 47f);
            Color outer = new Color(255, 76, 26) with { A = 0 };
            Color mid = ScrapCommander.WeldOrange with { A = 0 };
            Color core = Color.White with { A = 0 };

            Main.EntitySpriteDraw(glow, drawPos, null, outer * (0.9f * k), 0f, glow.Size() / 2f,
                k * 1.7f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * (0.85f * k), 0f, glow.Size() / 2f,
                k * 0.85f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, mid * (0.8f * k), Main.GlobalTimeWrappedHourly * 3.4f,
                star.Size() / 2f, k * 0.44f * flicker, SpriteEffects.None, 0);
        }
    }
}
