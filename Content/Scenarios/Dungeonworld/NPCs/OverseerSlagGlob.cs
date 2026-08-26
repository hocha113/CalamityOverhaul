using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 熔渣弹：浇包倾出的熔渣球（抛物线）→ 落地铺成 40f 余渣斑（只烫站入者）。
    /// ai[0]=扇形槽位号（表现错相用），ai[1]=主人 whoAmI，随 spawn 包原子过线。
    /// 熔橙→暗红生命周期变色（材质身份：熔渣的冷却史）；余渣相=弹幕二段生命周期，
    /// 全程不改写 damage/timeLeft（相位由本地确定性时间线推进）
    /// </summary>
    internal class OverseerSlagGlob : OverseerModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int PatchFrames = 40;
        private const float Gravity = 0.3f;

        private int SlotIndex => (int)Projectile.ai[0];

        private ref float Life => ref Projectile.localAI[0];
        /// <summary>相位：0 飞行 / 1 余渣斑（tile 碰撞确定性转相）</summary>
        private ref float Phase => ref Projectile.localAI[1];

        private int patchT;
        private float Seed => Projectile.identity * 0.7391f % 3.7f;

        /// <summary>迟入场对齐：飞行段位置/速度随原生同步走，但已落地的余渣斑
        /// 在迟入端会因速度归零而永远等不到 OnTileCollide——相位与时间线显式过线（单调闩）</summary>
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)Phase);
            writer.Write((short)Life);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            byte serverPhase = reader.ReadByte();
            short serverLife = reader.ReadInt16();
            if (serverPhase > (int)Phase) {
                Phase = serverPhase;
                Projectile.velocity = Vector2.Zero;
                Projectile.Resize(40, 14);
            }
            if (serverLife > (int)Life) {
                Life = serverLife;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 260;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;
            if ((int)Phase == 0) {
                //抛物线：速度拉伸的熔渣球，尾随渣珠
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + Gravity, 14f);
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!Main.dedServ && (int)Life % 3 == 0) {
                    PRTLoader.NewParticle<PRT_SlagBead>(Projectile.Center,
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                        FoundryOverseer.SlagHot, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                }
                return;
            }

            //余渣斑：定住，40f 只烫站入者，随后熄灭
            Projectile.velocity = Vector2.Zero;
            if (++patchT >= PatchFrames) {
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if ((int)Phase == 0) {
                //落地转余渣相（各端对同步 tile 确定性一致）
                Phase = 1;
                Projectile.velocity = Vector2.Zero;
                //判定对齐余渣斑横摊形（宽扁、且窄于视觉边缘，宽度让利于玩家）
                Projectile.Resize(40, 14);
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 4; k++) {
                        PRTLoader.NewParticle<PRT_SlagBead>(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 4f),
                            new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.8f, 2.2f)),
                            FoundryOverseer.SlagHot, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 20));
                    }
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    FoundryOverseer.SteamWhite * 0.4f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制：熔渣双形态 shader（黑壳浮板/热芯/颈缩/结皮渐干）====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            float fade = MathHelper.Clamp(Life / 5f, 0f, 1f);
            if (fade <= 0.02f) {
                return false;
            }

            bool shaderOn = EffectLoader.OverseerSlagFlow?.IsLoaded == true
                && CWRAsset.PerlinNoise?.IsLoaded == true;
            if (shaderOn) {
                DrawSlagShader(sb, glow, fade);
            }
            else {
                DrawSlagFallback(sb, blob, glow, fade);
            }
            return false;
        }

        /// <summary>OverseerSlagFlow：TechGlob 空中渣团 / TechPool 贴地余渣斑（预乘 AlphaBlend 批）</summary>
        private void DrawSlagShader(SpriteBatch sb, Texture2D glow, float fade) {
            Effect fx = EffectLoader.OverseerSlagFlow.Value;
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            if ((int)Phase == 0) {
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.09f, 0f, 1f);
                fx.Parameters["uStretch"]?.SetValue(stretch);
                fx.Parameters["uCool"]?.SetValue(MathHelper.Clamp(Life / 150f, 0f, 0.5f));
                fx.Parameters["uDry"]?.SetValue(0f);
                fx.CurrentTechnique = fx.Techniques["TechGlob"];
                fx.CurrentTechnique.Passes[0].Apply();
                //quad +x=飞行向（rotation 对齐速度）
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, Color.White * fade,
                    Projectile.rotation, glow.Size() * 0.5f,
                    new Vector2(56f / glow.Width, 42f / glow.Height), SpriteEffects.None, 0f);
            }
            else {
                fx.Parameters["uStretch"]?.SetValue(0f);
                fx.Parameters["uCool"]?.SetValue(0.3f);
                fx.Parameters["uDry"]?.SetValue(MathHelper.Clamp(patchT / (float)PatchFrames, 0f, 1f));
                fx.CurrentTechnique = fx.Techniques["TechPool"];
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(glow, Projectile.Center + new Vector2(0f, 3f) - Main.screenPosition, null, Color.White,
                    0f, glow.Size() * 0.5f,
                    new Vector2(74f / glow.Width, 30f / glow.Height), SpriteEffects.None, 0f);
            }

            gd.Textures[1] = null;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无 shader 降级：旧两层乘色/加色画法</summary>
        private void DrawSlagFallback(SpriteBatch sb, Texture2D blob, Texture2D glow, float fade) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            if ((int)Phase == 0) {
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.08f, 0f, 1f);
                Vector2 shape = new(1f + stretch * 1.4f, 1f - stretch * 0.25f);
                sb.Draw(blob, pos, null, FoundryOverseer.SlagDark * (0.9f * fade), Projectile.rotation,
                    blob.Size() * 0.5f, new Vector2(0.16f, 0.13f) * shape, SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, (FoundryOverseer.SlagHot with { A = 0 }) * (0.8f * fade),
                    Projectile.rotation, glow.Size() * 0.5f,
                    new Vector2(16f * shape.X * 2f / glow.Width, 12f * shape.Y * 2f / glow.Height), SpriteEffects.None, 0f);
            }
            else {
                float heat = 1f - patchT / (float)PatchFrames;
                float flick = 0.7f + 0.3f * MathF.Sin(patchT * 0.5f + Seed);
                sb.Draw(blob, pos + new Vector2(0f, 6f), null,
                    Color.Lerp(FoundryOverseer.SlagDark, FoundryOverseer.IronDeep, 1f - heat) * 0.95f,
                    0f, blob.Size() * 0.5f, new Vector2(0.24f, 0.08f), SpriteEffects.None, 0f);
                sb.Draw(glow, pos + new Vector2(0f, 4f), null,
                    (FoundryOverseer.SlagHot with { A = 0 }) * (0.55f * heat * flick),
                    0f, glow.Size() * 0.5f,
                    new Vector2(22f * 2f / glow.Width, 8f * 2f / glow.Height), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>渣珠：熔渣的飞溅珠，重坠、熔橙→暗红冷却、尾段熄芯（真 alpha 布纹贴图）</summary>
    internal class PRT_SlagBead : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 300;

        private Color initialColor;

        public PRT_SlagBead Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 16;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.96f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.2f, 6f);
            float t = LifetimeCompletion;
            Scale *= 0.975f;
            Color = Color.Lerp(initialColor, FoundryOverseer.SlagDark, MathF.Pow(t, 1.2f));
            Opacity = 1f - MathF.Pow(t, 2.4f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.08f, 0f, 0.8f);
            Vector2 scale = new Vector2(0.16f * (1f - stretch * 0.3f), 0.2f * (1f + stretch * 1.5f)) * Scale;
            spriteBatch.Draw(tex, pos, null,
                Color.Lerp(Color, FoundryOverseer.SlagDark, 0.5f) * Opacity, Rotation, origin,
                scale * new Vector2(1.25f, 1.05f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2f, 0f, 1f);
            if (fresh > 0.05f) {
                spriteBatch.Draw(tex, pos, null,
                    (FoundryOverseer.SlagHot with { A = 0 }) * (0.5f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.45f, 0.7f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
