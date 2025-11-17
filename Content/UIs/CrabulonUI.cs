using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs
{
    internal class CrabulonMountLifeBar : UIHandle
    {
        public bool Open {
            get {
                if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                    return false;
                }
                npc = null;
                if (crabulonPlayer.MountCrabulon != null) {
                    npc = crabulonPlayer.MountCrabulon.npc;
                    return true;
                }
                return false;
            }
        }
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

            if (!npc.Alives()) {
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
        public static Asset<Texture2D> Life = null;
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

    internal class CrouchBotton : UIHandle
    {
        //私有字段，用于管理UI状态
        private ModifyCrabulon modify;
        private bool _shouldBeOpen;
        private float sengs;
        private float hoverSengs;

        //Active属性现在依赖于内部状态，而不是每次都重新计算
        //这能让UI在关闭后平滑消失
        public override bool Active {
            get {
                _shouldBeOpen = FindClosestCrabulon();
                return _shouldBeOpen || sengs > 0.01f;
            }
        }

        /// <summary>
        /// 每帧寻找并设置最近的有效Crabulon NPC
        /// </summary>
        /// <returns>如果找到了有效目标则返回true，否则返回false</returns>
        private bool FindClosestCrabulon() {
            if (player == null) {
                return false;
            }

            //尝试获取玩家组件，如果失败则直接返回
            if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                modify = null;
                return false;
            }

            if (crabulonPlayer == null) {
                return false;
            }

            List<ModifyCrabulon> modifys = crabulonPlayer.ModifyCrabulons;
            ModifyCrabulon closestModify = null;
            float minDistSq = 90000f;//用平方避免开方运算

            foreach (var hover in modifys) {
                //跳过无效或不属于当前玩家的NPC
                if (!hover.npc.Alives() || hover.Owner.whoAmI != player.whoAmI) {
                    continue;
                }

                float distSq = hover.npc.DistanceSQ(player.Center);

                //寻找最近的目标
                if (distSq < minDistSq) {
                    minDistSq = distSq;
                    closestModify = hover;
                }
            }

            modify = closestModify;
            return modify != null;
        }

        public override void Update() {
            //使用Lerp平滑更新UI的出现/消失动画
            sengs = MathHelper.Lerp(sengs, _shouldBeOpen ? 1f : 0f, 0.15f);
            if (sengs < 0.01f) {
                sengs = 0f;//当值足够小时，直接归零以停止活动
                return;//完全透明时，不需要处理后续逻辑
            }

            //动态计算UI尺寸和位置
            Vector2 baseSize = new Vector2(100, 40);
            float dynamicScale = 1f + hoverSengs * 0.1f;//悬浮时放大10%
            Vector2 size = baseSize * dynamicScale;
            DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 12 * 11) - size / 2;
            UIHitBox = DrawPosition.GetRectangle(size);

            //检测鼠标悬浮
            bool isHovering = UIHitBox.Intersects(MouseHitBox);

            //平滑更新悬浮动画
            hoverSengs = MathHelper.Lerp(hoverSengs, isHovering && _shouldBeOpen ? 1f : 0f, 0.15f);

            if (isHovering && _shouldBeOpen && !modify.Mount) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    modify.Crouch = !modify.Crouch;
                    modify.SendNetWork();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //如果UI完全透明或没有目标，则不绘制
            if (sengs <= 0f || modify == null) {
                return;
            }

            float textScale = 1f;
            float overallAlpha = sengs;//所有绘制都基于这个透明度
            float dynamicScale = 1f + hoverSengs * 0.1f;

            //主UI绘制
            if (!modify.Mount) {
                //根据悬浮状态在两种颜色间平滑过渡
                Color bgColor = Color.Lerp(Color.AliceBlue, Color.CadetBlue, hoverSengs);
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 10, UIHitBox, bgColor * overallAlpha, Color.CadetBlue * overallAlpha);

                string content = modify.Crouch ? ModifyCrabulon.CrouchAltText.Value : ModifyCrabulon.CrouchText.Value;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(content) * textScale;
                Vector2 centerPos = UIHitBox.Center.ToVector2();

                Utils.DrawBorderStringFourWay(
                    spriteBatch, FontAssets.MouseText.Value,
                    content, centerPos.X, centerPos.Y,
                    Color.White * overallAlpha, Color.Black * overallAlpha,
                    textSize / 2, textScale * dynamicScale
                );
            }

            //绘制鼠标悬浮时的提示信息和物品图标
            DrawHoverInfo(spriteBatch, overallAlpha, textScale);
        }

        /// <summary>
        /// 统一绘制鼠标悬浮时的提示信息
        /// </summary>
        private void DrawHoverInfo(SpriteBatch spriteBatch, float alpha, float baseScale) {
            //如果目标NPC未悬浮或玩家手上没有相关物品，则不绘制
            if (!modify.hoverNPC) {
                return;
            }

            if (player.Alives() && player.CWR().IsRotatingDuringDash) {
                return;
            }

            Item currentItem = player.GetItem();
            Item saddleToDraw = null;
            string hoverContent = "";
            bool canDraw = false;

            if (currentItem.type == ModContent.ItemType<MushroomSaddle>()) {
                canDraw = true;
                saddleToDraw = currentItem;
                hoverContent = modify.SaddleItem.Alives() ? ModifyCrabulon.ChangeSaddleText.Value : ModifyCrabulon.MountHoverText.Value;
            }
            else if (modify.SaddleItem.Alives()) {
                canDraw = true;
                saddleToDraw = modify.SaddleItem;
                hoverContent = modify.Mount ? ModifyCrabulon.DismountText.Value : ModifyCrabulon.RideHoverText.Value;
            }

            if (!canDraw) {
                return;
            }

            //在鼠标下方绘制物品图标
            Vector2 itemPos = MousePosition + new Vector2(0, 32);
            if (saddleToDraw.Alives()) {
                saddleToDraw.BeginDyeEffectForUI(saddleToDraw.CWR().DyeItemID);
                VaultUtils.SimpleDrawItem(spriteBatch, saddleToDraw.type, itemPos, 32, 1f, 0, Color.White * alpha);
                saddleToDraw.EndDyeEffectForUI();
            }

            //在图标下方绘制提示文字
            Color textColor = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.02f)), Color.CadetBlue, Color.SkyBlue);
            Vector2 hoverSize = FontAssets.MouseText.Value.MeasureString(hoverContent) * baseScale * 0.9f;
            Vector2 hoverPos = itemPos + new Vector2(0, 36);

            Utils.DrawBorderStringFourWay(
                spriteBatch, FontAssets.MouseText.Value,
                hoverContent, hoverPos.X, hoverPos.Y,
                textColor * alpha, Color.Black * alpha,
                hoverSize / 2, baseScale
            );
        }
    }

    internal class CrabulonFriendBossBar : ModBossBar//友好状态下隐藏Boss血条
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) {
            if (npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                if (modifyCrabulon.FeedValue > 0f) {
                    return false;
                }
            }
            return true;
        }
    }
}