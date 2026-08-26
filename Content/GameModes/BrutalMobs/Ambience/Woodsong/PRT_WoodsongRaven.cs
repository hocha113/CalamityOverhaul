using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 惊鸦双演员：模式 0=乌鸦剪影（原版渡鸦贴图逐帧扑翼，近黑剪影振翅逃离），
    /// 模式 1=树冠黑影（Extra_98 真 alpha 暗纺锤横掠，"有什么东西刚跑过"的第一拍）。
    /// 纯氛围惊吓，AlphaBlend 暗层绘制。
    /// </summary>
    internal class PRT_WoodsongRaven : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 16;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_98 = null;

        internal const int ModeBird = 0;
        internal const int ModeShade = 1;

        private static readonly Color BirdShadow = new(15, 17, 23);
        private static readonly Color CanopyShade = new(8, 10, 14);

        private int mode;
        private float phase;

        public PRT_WoodsongRaven Configure(int actorMode, int lifetime) {
            mode = actorMode;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            mode = ModeBird;
            phase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            phase = Main.rand.NextFloat(100f);
            if (Lifetime <= 0) {
                Lifetime = 100;
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            if (mode == ModeBird) {
                //横向逐步加速逃离，纵向扑翼起伏爬升
                Velocity.X = MathHelper.Clamp(Velocity.X * 1.012f, -4.2f, 4.2f);
                Velocity.Y = -(1.15f + MathF.Sin((Time + phase) * 0.5f) * 0.75f);
                Opacity = MathHelper.Clamp(lc / 0.06f, 0f, 1f) * MathHelper.Clamp((1f - lc) / 0.30f, 0f, 1f);
            }
            else {
                //树冠黑影：匀掠+轻微起伏，正弦包络一闪而过
                Velocity.Y = MathF.Sin((Time + phase) * 0.4f) * 0.15f;
                Opacity = MathF.Sin(MathHelper.Pi * lc);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f) {
                return false;
            }
            Vector2 pos = Position - Main.screenPosition;

            if (mode == ModeShade) {
                Texture2D shade = Extra_98?.Value;
                if (shade == null) {
                    return false;
                }
                //纺锤长轴横放，压扁成掠过树冠的暗影
                spriteBatch.Draw(shade, pos, null, CanopyShade * (0.32f * Opacity),
                    MathHelper.PiOver2, shade.Size() * 0.5f,
                    new Vector2(1.2f, 2.3f) * Scale, SpriteEffects.None, 0f);
                return false;
            }

            Main.instance.LoadNPC(NPCID.Raven);
            Texture2D tex = TextureAssets.Npc[NPCID.Raven].Value;
            int frames = Math.Max(Main.npcFrameCount[NPCID.Raven], 1);
            //跳过第 0 帧（栖息姿态），循环飞行帧
            int frame = frames > 2 ? 1 + (Time / 4) % (frames - 1) : 0;
            Rectangle src = tex.Frame(1, frames, 0, frame);
            SpriteEffects flip = Velocity.X > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, pos, src, BirdShadow * (0.88f * Opacity),
                Velocity.X * 0.04f, src.Size() * 0.5f, Scale, flip, 0f);
            return false;
        }
    }
}
