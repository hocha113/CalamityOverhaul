using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

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

        //==================== 绘制：熔橙→暗红的冷却史 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Life / 5f, 0f, 1f);

            if ((int)Phase == 0) {
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.08f, 0f, 1f);
                Vector2 shape = new(1f + stretch * 1.4f, 1f - stretch * 0.25f);
                //渣体（实色）+ 热芯（A=0 加色点缀，预乘批技法）
                sb.Draw(blob, pos, null, FoundryOverseer.SlagDark * (0.9f * fade), Projectile.rotation,
                    blob.Size() * 0.5f, new Vector2(0.16f, 0.13f) * shape, SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, (FoundryOverseer.SlagHot with { A = 0 }) * (0.8f * fade),
                    Projectile.rotation, glow.Size() * 0.5f,
                    new Vector2(16f * shape.X * 2f / glow.Width, 12f * shape.Y * 2f / glow.Height), SpriteEffects.None, 0f);
            }
            else {
                //余渣斑：压扁摊平，热度随余寿衰减，尾段只剩暗红壳
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
            return false;
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
