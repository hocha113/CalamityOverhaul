using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 回收机锭落斗:画真实锭贴图走弧线坠入分选斗,触斗 Tink 叮当+反弹,
    /// 二弹后躺平渐隐。AlphaBlend 实体
    /// </summary>
    internal class PRT_ProcBarDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        private int itemType;
        private float floorY;
        private int bounces;
        private float spin;

        public PRT_ProcBarDrop Configure(int barItemType, float floorWorldY, int lifetime) {
            itemType = barItemType;
            floorY = floorWorldY;
            Lifetime = lifetime;
            //SetProperty 先于 Configure 执行,贴图加载只能放这里
            if (itemType > ItemID.None && itemType < TextureAssets.Item.Length) {
                Main.instance.LoadItem(itemType);
            }
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(0.14f, 0.24f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(-0.5f, 0.5f);
        }

        public override void Reset() {
            base.Reset();
            itemType = 0;
            floorY = 0f;
            bounces = 0;
            spin = 0f;
        }

        public override void AI() {
            Velocity.Y = Math.Min(Velocity.Y + 0.28f, 10f);
            if (bounces == 0) {
                Rotation += spin;
            }
            else {
                //落斗后转角回正躺平
                Rotation *= 0.82f;
                Velocity.X *= 0.9f;
            }

            if (floorY > 0f && Position.Y >= floorY && Velocity.Y > 0f && bounces < 2) {
                bounces++;
                Position.Y = floorY;
                Velocity.Y *= -0.32f;
                Velocity.X *= 0.55f;
                if (bounces == 1) {
                    //锭落斗的叮当
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.38f, Pitch = 0.24f }, Position);
                    PRTLoader.NewParticle<PRT_Sparkle>(Position + new Vector2(0f, -3f), new Vector2(0f, -0.4f),
                        new Color(255, 232, 170), 0.30f)?.Configure(new Color(255, 214, 120), 14, 0.1f, 0.8f);
                }
            }

            float t = LifetimeCompletion;
            Opacity = 1f - MathHelper.Clamp((t - 0.75f) / 0.25f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (itemType <= ItemID.None || itemType >= TextureAssets.Item.Length) {
                return false;
            }
            Texture2D tex = TextureAssets.Item[itemType].Value;
            if (tex == null) {
                return false;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            float fit = Math.Min(1f, 13f / Math.Max(frame.Width, frame.Height));
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, frame, Color * Opacity, Rotation,
                frame.Size() * 0.5f, fit * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
