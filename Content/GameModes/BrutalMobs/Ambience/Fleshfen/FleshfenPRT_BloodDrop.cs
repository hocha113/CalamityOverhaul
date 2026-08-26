using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 血露雨珠：快成丝、慢成珠，触地摊成血渍并缓慢蒸散（余韵内建在每一滴里）。
    /// Extra_98 真 alpha 非加色，暗红体+亮红芯双层；近本地玩家淡出保战斗可读。
    /// 镜像 PRT_GhostRainDrop 的已验收雨滴配方，材质换成血液（更暗、更黏、落地留渍）
    /// </summary>
    internal class FleshfenPRT_BloodDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 240;

        /// <summary>下落终速（血比水黏，略低于鬼雨的 16.5）</summary>
        private const float TerminalFall = 15f;

        /// <summary>血珠亮芯（只读引用猩红风味表）</summary>
        private static readonly Color BrightCore = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCrimson);

        private Color initialColor;
        private float windX;
        /// <summary>已落地成渍</summary>
        private bool staining;
        private int stainTicks;
        private int stainMax;
        /// <summary>蒸散水汽只放一次</summary>
        private bool vaporDone;

        public FleshfenPRT_BloodDrop Configure(int lifetime, float wind) {
            Lifetime = lifetime;
            windX = wind;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            windX = 0f;
            staining = false;
            stainTicks = 0;
            stainMax = 0;
            vaporDone = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 140;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (staining) {
                Velocity = Vector2.Zero;
                stainTicks++;
                float k = stainTicks / (float)stainMax;
                Color = Color.Lerp(initialColor, Color.Transparent, k);
                //渍干过半时冒一缕蒸散水汽（每滴至多一次，约半数滴出）
                if (!vaporDone && k > 0.55f) {
                    vaporDone = true;
                    if (Main.rand.NextBool(2)) {
                        Dust vapor = Dust.NewDustPerfect(Position - new Vector2(0f, 2f),
                            DustID.CrimsonTorch, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.4f, 0.9f)),
                            160, default, 0.7f);
                        vapor.noGravity = true;
                    }
                }
                if (stainTicks >= stainMax) {
                    active = false;
                }
                return;
            }

            Velocity.X = windX;
            Velocity.Y = Math.Min(Velocity.Y + 0.5f, TerminalFall);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //触地转血渍（檐面/地面都拦得住，物理层的免疫由此而来）
            if (Velocity.Y > 2f && Collision.SolidCollision(Position - new Vector2(1f, 1f), 2, 2)) {
                staining = true;
                stainMax = 34 + Main.rand.Next(26);
                return;
            }

            float t = LifetimeCompletion;
            if (t > 0.85f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.85f) / 0.15f);
            }
        }

        /// <summary>近本地玩家淡出：110px 内压到 42%，320px 外全强（雨仍在，脸前留可读窗）</summary>
        private float NearFade() {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return 1f;
            }
            float dist = Vector2.Distance(Position, player.Center);
            return MathHelper.Lerp(0.42f, 1f, MathHelper.Clamp((dist - 110f) / 210f, 0f, 1f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float fade = NearFade();

            if (staining) {
                //血渍：横向摊开的一小片暗红，随蒸散收拢变淡
                float k = stainTicks / (float)stainMax;
                Vector2 scale = new Vector2(0.30f * (1f + k * 1.1f), 0.075f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color * (0.9f * fade), 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            //快成丝、慢成珠：暗红体 + 亮红芯
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.055f, 0f, 1f);
            Vector2 body = new Vector2(0.12f * (1f - stretch * 0.3f),
                0.40f * (1f + stretch * 2.2f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color * fade, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, BrightCore * (0.5f * fade), Rotation, origin,
                body * new Vector2(0.42f, 1.04f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
