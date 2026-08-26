using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.SkeletronPrime
{
    /// <summary>
    /// 过载电弧链段：ai[0]=本段落点 whoAmI，ai[1]=起点 whoAmI，ai[2]=剩余跳跃数。<br/>
    /// 电弧语汇复用 PrimeArcChain（ThunderTrail 双层 + 着色器体积束带），换离子青。<br/>
    /// 续跳只在 owner 端发起（命中钩子本就跑在 owner），NPC 槽位是服务器权威索引，
    /// 各端可见性天然一致；只伤本段落点一次
    /// </summary>
    internal class OverloadArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int LifeTime = 16;
        /// <summary>满亮帧，其后冻结淡出</summary>
        internal const int BrightTime = 7;
        internal const int ArcPointCount = 12;
        /// <summary>线判定宽 px</summary>
        internal const float HitWidth = 26f;
        /// <summary>续跳伤害衰减</summary>
        internal const float JumpFalloff = 0.8f;

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        private Vector2 startPos;
        private Vector2 endPos;
        private ref float Timer => ref Projectile.localAI[0];
        private float Fade => MathHelper.Clamp(Projectile.timeLeft / (float)(LifeTime - BrightTime), 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //每目标一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //端点跟 NPC，失效停末位
            if (((int)Projectile.ai[1]).TryGetNPC(out NPC source) && source.Alives()) {
                startPos = source.Center;
            }
            else if (startPos == Vector2.Zero) {
                startPos = Projectile.Center;
            }
            if (((int)Projectile.ai[0]).TryGetNPC(out NPC jump) && jump.Alives()) {
                endPos = jump.Center;
            }
            else if (endPos == Vector2.Zero) {
                endPos = startPos;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                    Volume = 0.5f, Pitch = 0.45f, MaxInstances = 4
                }, endPos);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(endPos + Main.rand.NextVector2Circular(9f, 9f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                        OverloadCommandCore.IonCyan, Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(false, Main.rand.Next(9, 15));
                }
            }

            //亮相期每2帧重掷电弧路径，其后冻结淡出
            if (!VaultUtils.isServer && Projectile.timeLeft > LifeTime - BrightTime && (int)Timer % 2 == 0) {
                BuildArcPath();
            }
            Timer++;

            for (int i = 0; i < 3; i++) {
                Lighting.AddLight(Vector2.Lerp(startPos, endPos, i / 2f),
                    OverloadCommandCore.IonCyan.ToVector3() * 0.4f * Fade);
            }
        }

        private void BuildArcPath() {
            Vector2 dir = endPos - startPos;
            if (dir.Length() < 8f) {
                return;
            }
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            Vector2[] points = new Vector2[ArcPointCount];
            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                points[i] = startPos + dir * t + perp * Main.rand.NextFloat(-11f, 11f) * envelope;
            }
            if (mainTrail == null) {
                mainTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetMainWidth,
                    _ => OverloadCommandCore.IonCyan, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                mainTrail.SetRange((0f, 8f));
                mainTrail.SetExpandWidth(4f);
                coreTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetCoreWidth,
                    _ => Color.White, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0f, 4f));
                coreTrail.SetExpandWidth(2f);
            }
            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            mainTrail.RandomThunder();
            coreTrail.RandomThunder();
        }

        private float GetMainWidth(float f) => (10f + 6f * (float)Math.Sin(f * MathHelper.Pi)) * Fade;
        private float GetCoreWidth(float f) => (4f + 2.4f * (float)Math.Sin(f * MathHelper.Pi)) * Fade;
        private float GetArcAlpha(float f) => Fade;

        //==================== 判定与续跳 ====================

        public override bool? CanHitNPC(NPC target) {
            if (target.whoAmI == (int)Projectile.ai[0]) {
                return null;
            }
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (startPos == Vector2.Zero || endPos == Vector2.Zero) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                startPos, endPos, HitWidth, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //续跳：owner 端从新落点找下一环，排除来路与自身防乒乓
            if (!Projectile.IsOwnedByLocalPlayer() || Projectile.ai[2] <= 0f) {
                return;
            }
            ((int)Projectile.ai[1]).TryGetNPC(out NPC source);
            NPC next = target.Center.FindClosestNPC(OverloadCommandCore.ArcJumpRange,
                onHitNPCs: source != null ? new[] { target, source } : new[] { target });
            if (next == null) {
                return;
            }
            int damage = Math.Max((int)(Projectile.damage * JumpFalloff), 1);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<OverloadArcProj>(), damage, 0f, Projectile.owner,
                next.whoAmI, target.whoAmI, Projectile.ai[2] - 1f);
        }

        //==================== 绘制：体积束带（PrimeArcChain 着色器换青）+ 双层闪电 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (Fade <= 0.05f) {
                return false;
            }

            if (startPos != Vector2.Zero && endPos != Vector2.Zero
                && EffectLoader.PrimeArcChain?.Value != null) {
                DrawShaderRibbon();
            }

            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = OverloadCommandCore.IonCyan with { A = 0 };
            Main.EntitySpriteDraw(glow, startPos - Main.screenPosition, null, glowColor * (0.6f * Fade),
                0f, glow.Size() / 2f, 0.1f + 0.22f * Fade, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, endPos - Main.screenPosition, null, glowColor * (0.85f * Fade),
                0f, glow.Size() / 2f, 0.12f + 0.3f * Fade, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>体积束带底层：协议同 PrimeArcChainProj.DrawShaderRibbon，色板换离子青</summary>
        private void DrawShaderRibbon() {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.PrimeArcChain.Value;
            shader.Parameters["uColor"]?.SetValue(OverloadCommandCore.IonCyan.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(OverloadCommandCore.IonHot.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.8f);
            shader.Parameters["uIntensity"]?.SetValue(0.8f);
            shader.Parameters["uProgress"]?.SetValue(Fade);
            shader.Parameters["uSeed"]?.SetValue(Projectile.identity % 7f / 7f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            Vector2 dir = endPos - startPos;
            float dist = dir.Length();
            sb.Draw(quad, startPos - Main.screenPosition, null, Color.White, dir.ToRotation(),
                new Vector2(0, quad.Height / 2f),
                new Vector2(dist / quad.Width, 64f / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
