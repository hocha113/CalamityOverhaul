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
    /// 沉波狱吏散落钥匙：死亡散架时钥匙串崩飞的单枚钥匙。
    /// 旋转抛物线坠落，首次触地弹跳一声金属脆响，二次落定躺平渐隐；
    /// 注册贴图占位灰度，实际绘制借金钥匙物品图（LoadItem 惰性加载）。
    /// </summary>
    internal class PRT_TurnkeyKey : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        private float spin;
        private int bounces;
        private bool settled;
        private int settleTicks;

        public PRT_TurnkeyKey Configure(int lifetime) {
            Lifetime = lifetime;
            spin = Main.rand.NextFloat(-0.3f, 0.3f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            bounces = 0;
            settled = false;
            settleTicks = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 240;
            }
        }

        public override void AI() {
            if (settled) {
                settleTicks++;
                //躺平：转角就近落到水平位
                float lie = MathF.Round(Rotation / MathHelper.Pi) * MathHelper.Pi + MathHelper.PiOver2;
                Rotation = MathHelper.Lerp(Rotation, lie, 0.2f);
                if (settleTicks > 70) {
                    active = false;
                }
                return;
            }

            Velocity.X *= 0.99f;
            Velocity.Y = Math.Min(Velocity.Y + 0.38f, 12f);
            Rotation += spin * (1f + Velocity.Length() * 0.05f);

            if (Velocity.Y > 0.8f && Collision.SolidCollision(Position - new Vector2(2f, 0f), 4, 6)) {
                bounces++;
                if (bounces == 1) {
                    //首弹：钥匙落石地的脆响
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.85f, MaxInstances = 3 }, Position);
                    Velocity = new Vector2(Velocity.X * 0.5f, -Velocity.Y * 0.4f);
                    spin *= 0.55f;
                }
                else {
                    settled = true;
                    Velocity = Vector2.Zero;
                }
            }
            //坠进水里：直接沉没收场（钥匙回到水牢，正合它意）
            if (Collision.WetCollision(Position, 4, 6)) {
                Velocity.X *= 0.9f;
                Velocity.Y = Math.Min(Velocity.Y, 1.6f);
                if (Time > Lifetime - 30) {
                    active = false;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Main.instance.LoadItem(ItemID.GoldenKey);
            Texture2D tex = TextureAssets.Item[ItemID.GoldenKey].Value;
            float fade = settled ? 1f - settleTicks / 70f : 1f;
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, lit * fade, Rotation,
                tex.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            //湿钥匙一线水光
            spriteBatch.Draw(tex, Position - Main.screenPosition, null,
                new Color(140, 180, 175, 0) * (0.35f * fade), Rotation,
                tex.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
