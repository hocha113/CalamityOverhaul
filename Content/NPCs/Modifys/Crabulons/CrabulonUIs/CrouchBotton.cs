using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons.CrabulonUIs
{
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
                if (hover is null || !hover.npc.Alives() || !hover.Owner.Alives() || hover.Owner.whoAmI != player.whoAmI) {
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
}
