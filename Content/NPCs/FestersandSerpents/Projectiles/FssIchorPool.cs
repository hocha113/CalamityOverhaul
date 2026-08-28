using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 地面灼金脓池（场地经济的基座）；ai[0]尺寸档 0小 1大；ai[1]引爆引信（0=未引燃，
    /// 权威端经 <see cref="Detonate"/> 写入并同步，到期原地喷发泉柱后消亡）。
    /// 引燃期的池子自己就是预告实体：冒泡转急、升调咔哒、亮度爬升。
    /// 中心即地表点，冒泡渐干；干涸段关伤害。全场上限见 <see cref="TrySpawn"/>。
    /// </summary>
    internal class FssIchorPool : FssModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int DryTime = 70;

        private bool IsBig => Projectile.ai[0] == 1f;
        internal bool Armed => Projectile.ai[1] > 0f;
        private float WidthPx => IsBig ? 172f : 110f;
        /// <summary>0新鲜→1干涸（最后DryTime帧渐干）</summary>
        private float DryProgress => MathHelper.Clamp((DryTime - Projectile.timeLeft) / (float)DryTime, 0f, 1f);
        private float LifeT => 1f - Projectile.timeLeft / (float)FssDirector.PoolLifeFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FssDirector.PoolLifeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        #region 池经济公共口径
        /// <summary>
        /// 生成脓池并执行全场上限：超限时最旧的池（剩余寿命最短）提前进入干涸段。
        /// 权威端调用；ground 为地表点。
        /// </summary>
        internal static void TrySpawn(Terraria.DataStructures.IEntitySource source, Vector2 ground, int damage, bool big) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<FssIchorPool>();
            int count = 0;
            Projectile oldest = null;
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type != type) {
                    continue;
                }
                count++;
                if (oldest == null || p.timeLeft < oldest.timeLeft) {
                    oldest = p;
                }
            }
            if (count >= FssDirector.PoolMaxCount && oldest != null && oldest.timeLeft > DryTime) {
                oldest.timeLeft = DryTime;
                oldest.netUpdate = true;
            }
            Projectile.NewProjectile(source, ground, Vector2.Zero, type, damage, 0f, Main.myPlayer, big ? 1f : 0f);
        }

        /// <summary>
        /// 引燃：fuse 帧后原地喷发泉柱（权威端；已引燃取更短引信）。
        /// 干涸中的池不再引燃（没料可喷）。
        /// </summary>
        internal void Detonate(int fuse, bool tall = false) {
            if (VaultUtils.isClient || Projectile.timeLeft <= DryTime) {
                return;
            }
            fuse = Math.Max(fuse, 2);
            if (!Armed || Projectile.ai[1] > fuse) {
                Projectile.ai[1] = fuse;
                Projectile.localAI[2] = tall ? 1f : 0f;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>对一点半径内的全部存活脓池按距离次序引燃（由近及远的小行波）</summary>
        internal static void IgniteAround(Vector2 center, float radius, int fuseBase, float fusePerPx, bool tall = false) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<FssIchorPool>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type != type) {
                    continue;
                }
                float dist = Vector2.Distance(p.Center, center);
                if (dist > radius) {
                    continue;
                }
                (p.ModProjectile as FssIchorPool)?.Detonate(fuseBase + (int)(dist * fusePerPx), tall);
            }
        }
        #endregion

        public override void AI() {
            //首帧定尺寸：判定框略窄于视觉，顶边贴地
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Vector2 basePos = Projectile.Center;
                Projectile.Resize((int)(WidthPx * 0.82f), 22);
                Projectile.Center = basePos - new Vector2(0f, 8f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4 }, basePos);
                }
            }

            //干涸期无伤害
            if (DryProgress > 0.35f) {
                Projectile.hostile = false;
            }

            //引信推进：各端本地递减做表现（客户端的值会被周期同步纠偏），
            //喷发裁决只在权威端
            if (Armed) {
                Projectile.ai[1]--;
                if (Projectile.ai[1] <= 0f) {
                    if (!VaultUtils.isClient) {
                        Erupt();
                        return;
                    }
                    Projectile.ai[1] = 1f; //客户端钉在临爆帧等服务器的泉柱与销毁包
                }
                //引燃期升调咔哒（临爆转急）
                if (!VaultUtils.isServer && (int)Projectile.ai[1] % (Projectile.ai[1] > 20f ? 9 : 5) == 0) {
                    SoundEngine.PlaySound(SoundID.Item56 with {
                        Volume = 0.35f,
                        Pitch = 0.2f + 0.5f * (1f - MathHelper.Clamp(Projectile.ai[1] / 40f, 0f, 1f)),
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
            }

            //冒泡与金雾（客户端；引燃期密度×3）
            if (!VaultUtils.isServer && OnScreen()) {
                float freshness = 1f - DryProgress;
                int bubbleGap = Armed ? 4 : 13;
                if (Main.rand.NextBool(bubbleGap) && freshness > 0.2f) {
                    Vector2 bubblePos = Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-0.42f, 0.42f) * WidthPx, 2f);
                    Dust bubble = Dust.NewDustPerfect(bubblePos,
                        DustID.Ichor, -Vector2.UnitY * Main.rand.NextFloat(0.6f, Armed ? 3f : 1.4f),
                        40, default, Main.rand.NextFloat(0.7f, 1.1f));
                    bubble.noGravity = false;
                }
                if (Main.rand.NextBool(Armed ? 9 : 26)) {
                    Dust glowFleck = Dust.NewDustPerfect(Projectile.Center - new Vector2(0f, 6f),
                        DustID.IchorTorch, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                        0, default, Main.rand.NextFloat(0.7f, 1.1f));
                    glowFleck.noGravity = true;
                }
                float glowLevel = 0.32f * freshness + (Armed ? 0.35f : 0f);
                Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * glowLevel);
            }
        }

        /// <summary>到期喷发：原地起泉柱，池料耗尽消亡（权威端）</summary>
        private void Erupt() {
            int damage = Projectile.damage > 0 ? (int)(Projectile.damage * 1.35f) : 1;
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                Projectile.Center + new Vector2(0f, 8f), Vector2.Zero,
                ModContent.ProjectileType<FssGeyserColumn>(), damage, 0.5f, Main.myPlayer,
                0f, Projectile.localAI[2]);
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //被引爆的池走泉柱起喷演出；自然干涸只留一小口金沫
            if (Armed) {
                FssVfx.IchorBurst(Projectile.Center, 1.1f, -Vector2.UnitY);
            }
        }

        private bool OnScreen() {
            Vector2 screen = Main.screenPosition;
            return Projectile.Center.X > screen.X - 280f && Projectile.Center.X < screen.X + Main.screenWidth + 280f
                && Projectile.Center.Y > screen.Y - 280f && Projectile.Center.Y < screen.Y + Main.screenHeight + 280f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.EowAcid?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (effect != null) {
                DrawShaderPool(effect, drawPos);
            }
            else {
                DrawSpriteFallback(drawPos);
            }
            return false;
        }

        private void DrawShaderPool(Effect effect, Vector2 drawPos) {
            float heightPx = 46f;
            //引燃期亮度爬升（池子自己是预告）
            float armedBoost = Armed ? 0.35f * (1f - MathHelper.Clamp(Projectile.ai[1] / 60f, 0f, 1f)) + 0.15f : 0f;

            effect.CurrentTechnique = effect.Techniques["TechPool"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * (Armed ? 1.8f : 1f));
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 89 * 0.211f);
            effect.Parameters["uLife"]?.SetValue(DryProgress);
            effect.Parameters["uAspect"]?.SetValue(WidthPx / heightPx);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(LifeT * 7f + armedBoost, 0f, 1f));
            effect.Parameters["uColorDeep"]?.SetValue(FssVfx.IchorDeep.ToVector3());
            effect.Parameters["uColorBright"]?.SetValue(FssVfx.IchorBright.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(WidthPx / pixel.Width, heightPx / pixel.Height);
            //quad中心略沉入地面，上缘为液面
            sb.Draw(pixel, drawPos + new Vector2(0f, 12f), null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>回退：压扁液团双层</summary>
        private void DrawSpriteFallback(Vector2 drawPos) {
            Texture2D tex = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() / 2f;
            float freshness = 1f - DryProgress;
            float alpha = MathHelper.Clamp(LifeT * 7f, 0f, 1f) * (0.25f + freshness * 0.5f);
            Vector2 scale = new Vector2(WidthPx / tex.Width * 1.1f, 0.16f);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(0f, 6f), null,
                FssVfx.IchorDeep with { A = 120 } * alpha, 0f, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(0f, 3f), null,
                FssVfx.IchorGold with { A = 30 } * (alpha * 0.8f), 0f, origin, scale * 0.72f, SpriteEffects.None, 0);
        }
    }
}
