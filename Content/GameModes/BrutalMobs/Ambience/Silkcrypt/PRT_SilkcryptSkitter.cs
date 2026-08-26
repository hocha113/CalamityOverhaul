using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt
{
    /// <summary>
    /// 掠影蛛形：背景暗处快速掠过的蜘蛛剪影，纯粒子演出，不生成敌怪。
    /// 借原版爬墙蛛贴图压成近黑半透剪影（真 alpha，暗层只能这样画），
    /// 沿速度方向拖两枚残影承载动感；冲进亮处会加速消隐（影子怕光）
    /// </summary>
    internal class PRT_SilkcryptSkitter : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 8;

        private int frameTick;
        private int frame;
        private int lightCheckTimer;
        private float wobblePhase;
        private Color baseColor;

        public PRT_SilkcryptSkitter Configure(int lifetime) {
            Lifetime = lifetime;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            frameTick = 0;
            frame = 0;
            lightCheckTimer = 0;
            wobblePhase = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 32;
            }
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (baseColor == default) {
                baseColor = Color;
            }
            Main.instance.LoadNPC(NPCID.WallCreeperWall);
        }

        public override void AI() {
            //足步换帧 + 微小竖向摆动（多足爬行的碎步感）
            if (++frameTick >= 3) {
                frameTick = 0;
                frame++;
            }
            Position.Y += MathF.Sin(Time * 0.55f + wobblePhase) * 0.5f;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //每 5 帧探一次亮度，亮处加速消隐（不做逐帧光照查询）
            if (--lightCheckTimer <= 0) {
                lightCheckTimer = 5;
                Point tile = Position.ToTileCoordinates();
                if (WorldGen.InWorld(tile.X, tile.Y, 10)
                    && Lighting.Brightness(tile.X, tile.Y) > 0.34f
                    && Time < Lifetime - 6) {
                    Time += 5;
                }
            }

            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.14f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
            Color = baseColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Npc[NPCID.WallCreeperWall].Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.WallCreeperWall], 1);
            Rectangle src = tex.Frame(1, frameCount, 0, frame % frameCount);
            Vector2 origin = src.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //残影在前、本体在后叠画：速度拖影承载"掠过"的动感
            spriteBatch.Draw(tex, pos - Velocity * 2.4f, src, Color * 0.16f,
                Rotation, origin, Scale * 0.94f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos - Velocity * 1.2f, src, Color * 0.38f,
                Rotation, origin, Scale * 0.97f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, src, Color,
                Rotation, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
