using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 采收拍飞散的菌伞:直接用蘑菇物品贴图当碎块承体,弧线抛飞带旋转,
    /// 落地弹一跳后落定快速消隐;发光蘑菇碎块自带蓝辉底光
    /// </summary>
    internal class PRT_FarmMushroomCap : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 60;

        private int itemType;
        private float spin;
        private int bounces;

        public PRT_FarmMushroomCap Configure(int mushroomItemType) {
            itemType = mushroomItemType;
            return this;
        }

        public override void Reset() {
            base.Reset();
            itemType = 0;
            spin = 0f;
            bounces = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.26f, 0.26f);
            Opacity = 1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(60, 90);
            }
        }

        public override void AI() {
            if (bounces < 2) {
                Velocity.Y = MathF.Min(Velocity.Y + 0.24f, 8f);
                Velocity.X *= 0.985f;
                Rotation += spin;
                //下落触地:第一次弹一跳,第二次落定
                if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(4f, 2f), 8, 6)) {
                    if (bounces == 0) {
                        Velocity = new Vector2(Velocity.X * 0.5f, Velocity.Y * -0.4f);
                        spin *= 0.5f;
                        bounces = 1;
                    }
                    else {
                        Velocity = Vector2.Zero;
                        spin = 0f;
                        bounces = 2;
                        //落定后掐短剩余寿命,快速消隐不留贴图垃圾
                        if (Lifetime - Time > 16) {
                            Lifetime = Time + 16;
                        }
                    }
                }
            }
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (itemType <= ItemID.None) {
                return false;
            }
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType].Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            if (itemType == ItemID.GlowingMushroom) {
                //发光蘑菇碎块的蓝辉底光(AlphaBlend 批内 A=0 加色技巧),且不吃环境光压暗
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                spriteBatch.Draw(glowTex, drawPos, null, new Color(80, 150, 255, 0) * (Opacity * 0.55f),
                    0f, glowTex.Size() * 0.5f, 0.24f * Scale, SpriteEffects.None, 0f);
                light = Color.Lerp(light, Color.White, 0.6f);
            }
            spriteBatch.Draw(tex, drawPos, null, light * Opacity, Rotation, tex.Size() * 0.5f, 0.62f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
