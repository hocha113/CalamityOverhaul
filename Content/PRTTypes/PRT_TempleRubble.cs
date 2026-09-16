using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 神庙石块：借原版贴图（蜥蜴砖格 / 石巨人 gore / 石巨人本体切片）画不透明真石。
    /// 吃场景光，真重力落地：首弹一声石响并扬起卡其尘，二次触地落定归平，停留后渐隐；
    /// 新断口带岩浆热光并指数冷却，冷却期间自身微弱发光。
    /// 注册贴图占位灰度，实际绘制走 <see cref="Configure"/> 传入的贴图与源矩形
    /// </summary>
    internal class PRT_TempleRubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        //整场死亡演出约 170 块（切片/砖/gore/拳头清场叠在 218~242 拍），留余量防拳碎、头碎被上限吞掉
        public override int InGame_World_MaxCount => 260;

        //落地石响整类共用节流，避免上百块同时落地变成噪音
        private static uint lastTinkTick;
        private const int TinkGapTicks = 3;
        private const int FadeTicks = 40;

        private Texture2D source;
        private Rectangle sourceRect;
        private float heat;
        private float spin;
        private float gravity;
        private int bounces;
        private bool settled;
        private int settleTicks;
        private int restTicks;

        /// <summary>
        /// 指定绘制贴图与源矩形；heat 为断口初始热度 0~1，restTicks 为落定后停留帧数
        /// </summary>
        public PRT_TempleRubble Configure(Texture2D tex, Rectangle src, float heat = 0f, int restTicks = 120, float gravity = 0.36f) {
            source = tex;
            sourceRect = src;
            this.heat = MathHelper.Clamp(heat, 0f, 1f);
            this.restTicks = Math.Max(restTicks, 0);
            this.gravity = gravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            source = null;
            sourceRect = default;
            heat = 0f;
            spin = 0f;
            gravity = 0f;
            bounces = 0;
            settled = false;
            settleTicks = 0;
            restTicks = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.08f, 0.22f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Scale <= 0f) {
                Scale = 1f;
            }
            //寿命只是安全上限，正常收场靠落定渐隐
            if (Lifetime <= 0) {
                Lifetime = 600;
            }
        }

        public override void AI() {
            if (source == null) {
                active = false;
                return;
            }

            heat *= 0.95f;
            if (heat > 0.08f) {
                Lighting.AddLight(Position, 0.55f * heat, 0.26f * heat, 0.06f * heat);
            }

            if (settled) {
                settleTicks++;
                //躺平：转角就近落到水平位
                float lie = MathF.Round(Rotation / MathHelper.Pi) * MathHelper.Pi;
                Rotation = MathHelper.Lerp(Rotation, lie, 0.18f);
                if (settleTicks > restTicks + FadeTicks) {
                    active = false;
                }
                return;
            }

            Velocity.X *= 0.985f;
            Velocity.Y = Math.Min(Velocity.Y + gravity, 13f);
            Rotation += spin * (0.6f + Velocity.Length() * 0.06f);

            //碰撞盒取源矩形短边，避免长条切片在斜转时提前卡地
            int side = Math.Max(4, (int)(Math.Min(sourceRect.Width, sourceRect.Height) * Scale * 0.7f));
            Vector2 boxTopLeft = Position - new Vector2(side * 0.5f, side * 0.5f);

            //侧向撞墙：X 反弹
            if (Math.Abs(Velocity.X) > 0.4f) {
                Vector2 probe = boxTopLeft + new Vector2(Velocity.X > 0f ? side : -2f, 0f);
                if (Collision.SolidCollision(probe, 2, side)) {
                    Velocity.X *= -0.4f;
                }
            }

            if (Velocity.Y > 0.6f && Collision.SolidCollision(boxTopLeft, side, side)) {
                bounces++;
                if (bounces == 1) {
                    Velocity = new Vector2(Velocity.X * 0.55f, -Velocity.Y * 0.35f);
                    spin *= 0.5f;
                    PlayLandingFeedback();
                }
                else {
                    settled = true;
                    Velocity = Vector2.Zero;
                    //从实心块里抬出来，别半截埋进地
                    for (int i = 0; i < 8 && Collision.SolidCollision(boxTopLeft, side, side); i++) {
                        Position.Y -= 2f;
                        boxTopLeft.Y -= 2f;
                    }
                }
            }
        }

        private void PlayLandingFeedback() {
            if (Main.GameUpdateCount - lastTinkTick >= TinkGapTicks) {
                lastTinkTick = Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.22f,
                    Pitch = Main.rand.NextFloat(-0.35f, 0.15f),
                    MaxInstances = 3
                }, Position);
            }
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustPerfect(Position, DustID.Lihzahrd,
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.6f, -0.4f)), 80, default, 0.9f);
                dust.noGravity = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (source == null) {
                return false;
            }
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = sourceRect.Size() * 0.5f;
            float fade = settled ? 1f - MathHelper.Clamp((settleTicks - restTicks) / (float)FadeTicks, 0f, 1f) : 1f;

            Color lit = Lighting.GetColor(Position.ToTileCoordinates());
            spriteBatch.Draw(source, pos, sourceRect, lit * fade, Rotation, origin, Scale, SpriteEffects.None, 0f);

            //断口热光：同贴图加色叠层，冷却即熄（预乘 AlphaBlend 批里 A=0 即加亮）
            if (heat > 0.03f) {
                Color ember = new Color(255, 120, 30, 0) * (heat * 0.85f * fade);
                spriteBatch.Draw(source, pos, sourceRect, ember, Rotation, origin, Scale * 1.04f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
