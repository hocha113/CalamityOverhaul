using CalamityOverhaul.Content.NPCs.Modifys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System;
using Terraria.GameContent;
using Terraria;

namespace CalamityOverhaul.Content.UIs
{
    internal class CrabulonMountBar : UIHandle
    {
        public static bool Open => player.GetOverride<CrabulonPlayer>().MountCrabulonIndex != -1;
        private float sengs;
        public override bool Active => Open || sengs > 0f;
        public static readonly List<CrabulonLife> crabulonLives = [];
        public NPC npc;
        public const int liveMargin = 4;
        public const int crabulonLiveCount = 20;
        public const int crabulonLiveColumn = 2;
        public const int crabulonLiveLine = crabulonLiveCount / crabulonLiveColumn;
        public override void OnEnterWorld() {
            crabulonLives.Clear();
            for (int i = 0; i < crabulonLiveCount; i++) {
                crabulonLives.Add(new CrabulonLife() { index = i });
            }
        }
        public override void Update() {
            if (!Open) {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
                return;
            }
            else {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }

            if (!player.GetOverride<CrabulonPlayer>().MountCrabulonIndex.TryGetNPC(out npc)) {
                return;
            }

            Vector2 lifeSize = CrabulonLife.Life.Size();

            Vector2 uiSize = new Vector2((lifeSize.X + liveMargin) * crabulonLiveLine, (lifeSize.Y + liveMargin) * crabulonLiveColumn);

            DrawPosition = new Vector2(((int)(Main.screenWidth / 2 - uiSize.X / 2)), ((int)(Main.screenHeight / 2 + uiSize.X / 2 + Main.screenHeight / 10 * 1)));

            UIHitBox = DrawPosition.GetRectangle(uiSize);

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, UIHitBox.Y);

            for (int i = 0; i < crabulonLiveCount; i++) {
                var crabulonLive = crabulonLives[i];
                crabulonLive.DrawPosition = DrawPosition + CrabulonLife.Life.Size() / 2;
                crabulonLive.npc = npc;
                crabulonLive.DrawPosition.X += (i % crabulonLiveLine) * (lifeSize.X + liveMargin);
                crabulonLive.DrawPosition.Y += (i / crabulonLiveLine) * (lifeSize.Y + liveMargin);
                crabulonLive.sengs = sengs;
                crabulonLive.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!npc.Alives()) {
                return;
            }

            foreach (var crabulonLive in crabulonLives) {
                crabulonLive.Draw(spriteBatch);
            }

            if (hoverInMainPage) {
                string content = $"{npc.life}/{npc.lifeMax}";
                float textScale = 1f;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(content) * textScale;
                Vector2 drawPos = MousePosition + new Vector2(0, 36);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, content
                            , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), textScale);
            }
        }
    }

    internal class CrabulonLife : UIHandle
    {
        [VaultLoaden(CWRConstant.UI + "CrabulonLife")]
        public static Asset<Texture2D> Life;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        public int lifeValue; //存储此生命单元当前拥有的生命值
        public int index;     //此生命单元的索引
        public NPC npc;       //关联的NPC

        internal float sengs;

        //用于实现动态效果的私有字段
        private float shakeTime;        //抖动效果的持续时间计时器
        private float dynamicScale = 1f;    //用于“濒危”状态的动态缩放
        private float dynamicRotation;  //用于抖动的动态旋转
        private Vector2 shakeOffset = Vector2.Zero; //用于抖动的动态位置偏移

        public override void Update() {
            //确保我们有一个有效的NPC实例
            if (npc == null || !npc.active) {
                return;
            }

            //计算每个生命单元能代表的最大生命值
            int maxLifePerUnit = npc.lifeMax / CrabulonMountBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) { //避免除以零的错误
                return;
            }

            //计算当前帧此单元“应该”拥有的生命值
            int newLifeValue = (int)MathHelper.Clamp(npc.life - index * maxLifePerUnit, 0, maxLifePerUnit);

            //实现掉血抖动效果
            //如果新计算的生命值比上一帧的要低，说明掉血了
            if (newLifeValue < lifeValue) {
                shakeTime = 20f; //启动一个持续20帧的抖动效果
            }

            //如果抖动计时器正在生效
            if (shakeTime > 0) {
                shakeTime--;
                float intensity = shakeTime / 20f; //抖动强度随时间衰减
                                                   //生成随机的位置偏移和旋转
                shakeOffset = Main.rand.NextVector2Circular(intensity * 4f, intensity * 4f);
                dynamicRotation = Main.rand.NextFloat(-0.2f, 0.2f) * intensity;
            }
            else {
                //效果结束后，恢复默认值
                shakeOffset = Vector2.Zero;
                dynamicRotation = 0f;
            }

            //更新当前生命值，以便下一帧进行比较
            lifeValue = newLifeValue;

            //实现濒危颤抖效果
            float lifePercent = (float)lifeValue / maxLifePerUnit;

            //如果生命值在0%到35%之间，则触发效果
            if (lifePercent > 0 && lifePercent < 0.35f) {
                float pulseSpeed = 12f; //颤抖速度
                float pulseIntensity = 0.12f; //颤抖幅度
                                              //使用正弦函数制造平滑的、循环的缩放动画
                dynamicScale = 1f + (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f)) * pulseIntensity;
            }
            else {
                dynamicScale = 1f; //不在危险区域时，恢复默认大小
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (npc == null || !npc.active) {
                return;
            }

            int maxLifePerUnit = npc.lifeMax / CrabulonMountBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) {
                return;
            }

            //计算此单元的填充比例，用于决定基础大小和颜色
            float fillRatio = (float)lifeValue / maxLifePerUnit;

            //如果生命完全耗尽，则不绘制
            if (fillRatio <= 0) {
                return;
            }

            //颜色会随着生命值降低而变暗
            Color drawColor = Color.White * fillRatio;
            drawColor.A = (byte)(255 * (0.2f + fillRatio * 0.8f));

            //最终的绘制大小 = 基础大小 * 动态缩放
            float finalScale = 0.5f + fillRatio * dynamicScale * 0.5f;

            //最终的绘制位置 = 基础位置 + 抖动偏移
            Vector2 finalDrawPosition = DrawPosition + shakeOffset;

            //使用所有动态参数进行绘制
            spriteBatch.Draw(Life.Value, finalDrawPosition, null, drawColor * sengs, dynamicRotation, Life.Size() / 2, finalScale, SpriteEffects.None, 0);
        }
    }
}