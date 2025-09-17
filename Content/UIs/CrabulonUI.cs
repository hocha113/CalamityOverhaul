using CalamityOverhaul.Content.NPCs.Modifys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs
{
    internal class CrabulonMountLifeBar : UIHandle
    {
        public static bool Open => player.GetOverride<CrabulonPlayer>().MountCrabulonIndex != -1;
        public static readonly List<CrabulonLife> crabulonLives = [];
        public const int liveMargin = 4;
        public const int crabulonLiveCount = 20;
        public const int crabulonLiveColumn = 2;
        public const int crabulonLiveLine = crabulonLiveCount / crabulonLiveColumn;
        public override bool Active => Open || sengs > 0f;
        private float sengs;
        private NPC npc;
        //用于检测生命值变化
        private int oldLife = -1;
        //用于触发整体波浪抖动效果的计时器
        private float waveShakeTime;

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
                oldLife = -1;//当UI不显示时重置生命值记录
                return;
            }
            else {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }

            if (!player.GetOverride<CrabulonPlayer>().MountCrabulonIndex.TryGetNPC(out npc)) {
                oldLife = -1;//当找不到NPC时也重置
                return;
            }

            //如果是第一次更新或者NPC刚刚切换，则初始化oldLife
            if (oldLife == -1) {
                oldLife = npc.life;
            }

            //检测到NPC受伤
            if (npc.life < oldLife) {
                waveShakeTime = 40f;//启动一个持续40帧的波浪抖动效果
            }
            oldLife = npc.life;//更新生命值记录以备下一帧使用

            //波浪抖动计时器递减
            if (waveShakeTime > 0) {
                waveShakeTime--;
            }

            //判断NPC血量是否低于50%
            bool isLowHealth = npc.life < npc.lifeMax * 0.5f;

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
                crabulonLive.waveShakeTime = waveShakeTime;//将波浪抖动计时器传递给每个生命单元
                crabulonLive.isLowHealth = isLowHealth;//传递低血量状态
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

        public int lifeValue;//存储此生命单元当前拥有的生命值
        public int index;    //此生命单元的索引
        public NPC npc;      //关联的NPC

        internal float sengs;
        internal float waveShakeTime; //从父级接收的波浪抖动计时器
        internal bool isLowHealth;    //从父级接收的是否为低血量状态

        //用于实现动态效果的私有字段
        private float shakeTime;        //抖动效果的持续时间计时器
        private float dynamicScale = 1f;    //用于“濒危”状态的动态缩放
        private float dynamicRotation;  //用于抖动的动态旋转
        private Vector2 damageShakeOffset = Vector2.Zero; //用于单个单元掉血抖动的动态位置偏移
        private Vector2 waveShakeOffset = Vector2.Zero;   //用于整体波浪效果的动态位置偏移

        public override void Update() {
            //确保我们有一个有效的NPC实例
            if (npc == null || !npc.active) {
                return;
            }

            //在更新开始时重置波浪偏移
            waveShakeOffset = Vector2.Zero;

            //计算每个生命单元能代表的最大生命值
            int maxLifePerUnit = npc.lifeMax / CrabulonMountLifeBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) { //避免除以零的错误
                return;
            }

            //计算当前帧此单元“应该”拥有的生命值
            int newLifeValue = (int)MathHelper.Clamp(npc.life - index * maxLifePerUnit, 0, maxLifePerUnit);

            //实现掉血抖动效果
            //如果新计算的生命值比上一帧的要低，说明掉血了
            if (newLifeValue < lifeValue) {
                shakeTime = 20f;//启动一个持续20帧的抖动效果
            }

            //如果抖动计时器正在生效
            if (shakeTime > 0) {
                shakeTime--;
                float intensity = shakeTime / 20f;//抖动强度随时间衰减
                                                  //生成随机的位置偏移和旋转
                damageShakeOffset = Main.rand.NextVector2Circular(intensity * 4f, intensity * 4f);
                dynamicRotation = Main.rand.NextFloat(-0.2f, 0.2f) * intensity;
            }
            else {
                //效果结束后，恢复默认值
                damageShakeOffset = Vector2.Zero;
                dynamicRotation = 0f;
            }

            //更新当前生命值，以便下一帧进行比较
            lifeValue = newLifeValue;

            //实现受伤时从左到右的波浪抖动效果
            float waveDelay = 1.5f;//每个单元之间的延迟帧数
            float timeAfterWaveReach = waveShakeTime - index * waveDelay;
            if (timeAfterWaveReach > 0) {
                float maxIntensityTime = 10f;//波浪在每个单元上最强烈的持续时间
                float intensity = 1f - Math.Abs(timeAfterWaveReach - maxIntensityTime) / maxIntensityTime;
                intensity = MathHelper.Clamp(intensity, 0f, 1f);
                if (intensity > 0) {
                    waveShakeOffset.X += Main.rand.NextFloat(-2f, 2f) * intensity;
                    waveShakeOffset.Y += Main.rand.NextFloat(-2f, 2f) * intensity;
                }
            }

            //实现低血量时常驻的波浪颤抖效果
            if (isLowHealth) {
                float pulseSpeed = 10f;//颤抖速度
                float pulseAmplitude = 2.5f;//颤抖幅度
                float delayFactor = 0.4f;//单元之间的相位延迟
                waveShakeOffset.Y += (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f) + index * delayFactor) * pulseAmplitude;
            }

            //实现濒危颤抖效果
            float lifePercent = (float)lifeValue / maxLifePerUnit;

            //如果生命值在0%到35%之间，则触发效果
            if (lifePercent > 0 && lifePercent < 0.35f) {
                float pulseSpeed = 12f;//颤抖速度
                float pulseIntensity = 0.12f;//颤抖幅度
                                             //使用正弦函数制造平滑的、循环的缩放动画
                dynamicScale = 1f + (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f)) * pulseIntensity;
            }
            else {
                dynamicScale = 1f;//不在危险区域时，恢复默认大小
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (npc == null || !npc.active) {
                return;
            }

            int maxLifePerUnit = npc.lifeMax / CrabulonMountLifeBar.crabulonLiveCount;
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

            //最终的绘制位置 = 基础位置 + 自身掉血抖动偏移 + 整体波浪抖动偏移
            Vector2 finalDrawPosition = DrawPosition + damageShakeOffset + waveShakeOffset;

            //使用所有动态参数进行绘制
            spriteBatch.Draw(Life.Value, finalDrawPosition, null, drawColor * sengs, dynamicRotation, Life.Size() / 2, finalScale, SpriteEffects.None, 0);
        }
    }
}