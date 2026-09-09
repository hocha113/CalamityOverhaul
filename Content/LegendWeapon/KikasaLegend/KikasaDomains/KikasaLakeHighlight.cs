using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖只照敌意：沉在观看域湖里的敌怪与敌方弹幕按完整绘制链重画进一张屏幕尺寸的掩膜 RT，
    /// 喂给 KikasaGrade.fx TechUnify 的 s2，在湖体合成阶段做透水显形与血膜勾边。
    /// 纯本机表现量，只看 <see cref="KikasaDomain.Viewed"/>，无网络。
    /// 掩膜来源选显式名单而不是前后帧差：原版前景液体画在实体之后，帧差会把真实水潭当成实体勾出来。
    /// 重绘守卫同肢解剪影的 CaptureNpcAppearance：单个钩子异常不拖垮，批次收尾失败即本帧掩膜作废。
    /// 掩膜色值是实体自己的受光颜色（预乘），alpha 即覆盖，着色器据此合成本体与外侧勾边
    /// </summary>
    internal static class KikasaLakeHighlight
    {
        //==================== 调参 ====================

        /// <summary>勾边亮度 0~1（沙盒 0.85 判偏淡，抬到 1.0）</summary>
        public const float RimStrength = 1.0f;

        /// <summary>贴缝处身体透水度：近水面看得见生物本体</summary>
        public const float BodyShowNear = 0.85f;

        /// <summary>深处身体透水度：深处渐成一具被照亮的剪影</summary>
        public const float BodyShowDeep = 0.45f;

        /// <summary>勾边宽度（缩放 1 下的屏幕像素，约一个美术像素），随缩放放大后钳在 1.5~3.5</summary>
        public const float RimPx = 2.0f;

        /// <summary>单帧最多重画的敌怪数，boss 优先、再按面积</summary>
        public const int MaxNpcs = 24;

        /// <summary>单帧最多重画的敌方弹幕数，离施术者近的优先</summary>
        public const int MaxProjectiles = 40;

        /// <summary>敌方弹幕是否一并入掩膜（水下躲弹靠它）</summary>
        public const bool IncludeHostileProjectiles = true;

        /// <summary>碰撞盒下沿外扩比例：boss 贴图常比碰撞盒大，贴图沾水碰撞盒还没沾</summary>
        private const float HitboxGrow = 0.25f;

        /// <summary>视口筛选余量（世界像素），大体型贴图中心在屏外也可能有一角在屏内</summary>
        private const int ViewPad = 320;

        //==================== 状态 ====================

        private static RenderTarget2D maskRT;
        //显存异常等无法建 RT：本次进世界内不再尝试，回主菜单清理后复位
        private static bool rtDegraded;
        //RT 当前是否已是空白，名单连续为空时省掉逐帧清空
        private static bool maskClean = true;
        //本帧掩膜可用（捕获成功且有内容）
        private static bool maskValid;

        private static readonly List<(int Index, float Key)> npcList = [];
        private static readonly List<(int Index, float Key)> projList = [];

        private static readonly Comparison<(int Index, float Key)> byKeyDesc =
            (a, b) => b.Key.CompareTo(a.Key);

        /// <summary>本帧掩膜是否可用，供渲染端决定 uniform 是否归零</summary>
        internal static bool MaskReady => maskValid && maskRT != null && !maskRT.IsDisposed;

        public static void Clear() {
            maskRT?.Dispose();
            maskRT = null;
            rtDegraded = false;
            maskClean = true;
            maskValid = false;
            npcList.Clear();
            projList.Clear();
        }

        //==================== 名单 ====================

        /// <summary>绘制时收集沉在这面湖里的敌意实体，不耦合 PostAI</summary>
        private static void Collect(KikasaDomainPlayer kdp) {
            npcList.Clear();
            projList.Clear();

            float lakeY = kdp.VisualLakeY;
            float casterX = kdp.Player.Center.X;
            Rectangle view = new(
                (int)Main.screenPosition.X - ViewPad, (int)Main.screenPosition.Y - ViewPad,
                Main.screenWidth + ViewPad * 2, Main.screenHeight + ViewPad * 2);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!QualifiesNpc(npc, lakeY, casterX, view)) {
                    continue;
                }
                //boss 抬到最前，其余按面积
                float key = npc.width * npc.height * npc.scale;
                if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) {
                    key += 1_000_000f;
                }
                npcList.Add((i, key));
            }
            if (npcList.Count > MaxNpcs) {
                npcList.Sort(byKeyDesc);
                npcList.RemoveRange(MaxNpcs, npcList.Count - MaxNpcs);
            }

            if (!IncludeHostileProjectiles) {
                return;
            }
            Vector2 casterCenter = kdp.Player.Center;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!QualifiesProj(proj, lakeY, casterX, view)) {
                    continue;
                }
                //近的优先：键取负距离，与 NPC 共用降序排序
                projList.Add((i, -Vector2.DistanceSquared(proj.Center, casterCenter)));
            }
            if (projList.Count > MaxProjectiles) {
                projList.Sort(byKeyDesc);
                projList.RemoveRange(MaxProjectiles, projList.Count - MaxProjectiles);
            }
        }

        private static bool QualifiesNpc(NPC npc, float lakeY, float casterX, Rectangle view) {
            if (npc?.active != true || npc.type <= NPCID.None) {
                return false;
            }
            //湖只照敌意：友方/城镇/小动物照旧沉在血里
            if (npc.friendly || NPCID.Sets.CountsAsCritter[npc.type]) {
                return false;
            }
            //hide 由别的层代画；behindTiles 画在物块后，入掩膜会透过实体物块勾出 X 光轮廓
            if (npc.hide || npc.behindTiles) {
                return false;
            }
            if (npc.lifeMax <= 5 && npc.type != NPCID.TargetDummy) {
                return false;
            }
            if (MathF.Abs(npc.Center.X - casterX) > KikasaLakeSurface.HalfWidth) {
                return false;
            }
            if (npc.Bottom.Y + npc.height * HitboxGrow <= lakeY) {
                return false;
            }
            if (!view.Intersects(npc.Hitbox)) {
                return false;
            }
            //接管中的目标各有自己的覆绘：肢解/放逐的 PreDraw 会在批里换矩阵，沉溺目标身上压着鬼手
            if (OniDismember.IsDismembered(npc.whoAmI)
                || CyberBanish.IsBanishing(npc.whoAmI)
                || KikasaDrownFX.IsShowTarget(npc.whoAmI)) {
                return false;
            }
            return true;
        }

        private static bool QualifiesProj(Projectile proj, float lakeY, float casterX, Rectangle view) {
            if (proj?.active != true || proj.type <= ProjectileID.None) {
                return false;
            }
            if (!proj.hostile || proj.friendly || proj.hide) {
                return false;
            }
            if (MathF.Abs(proj.Center.X - casterX) > KikasaLakeSurface.HalfWidth) {
                return false;
            }
            if (proj.Bottom.Y + proj.height * HitboxGrow <= lakeY) {
                return false;
            }
            return view.Intersects(proj.Hitbox);
        }

        //==================== 捕获 ====================

        /// <summary>
        /// 在 ApplyUnify 的保屏窗口内调用（主屏已拷入交换缓冲）：按名单把实体重画进掩膜 RT。
        /// 返回后活动 RT 是掩膜 RT，调用方随后自行回绑主屏
        /// </summary>
        internal static void CaptureMask(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, KikasaDomainPlayer kdp) {
            maskValid = false;
            if (rtDegraded || spriteBatch == null || graphicsDevice == null || kdp == null) {
                return;
            }
            if (!EnsureTarget(graphicsDevice)) {
                return;
            }

            Collect(kdp);
            if (npcList.Count == 0 && projList.Count == 0) {
                if (!maskClean) {
                    graphicsDevice.SetRenderTarget(maskRT);
                    graphicsDevice.Clear(Color.Transparent);
                    maskClean = true;
                }
                return;
            }

            bool batchBegan = false;
            bool batchEnded = false;
            bool drewAny = false;
            try {
                graphicsDevice.SetRenderTarget(maskRT);
                graphicsDevice.Clear(Color.Transparent);
                maskClean = false;
                //与原版 DoDraw_DrawNPCsOverTiles / DrawProjectiles 同参：掩膜与主屏逐像素对齐
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                batchBegan = true;

                Vector2 screenPos = Main.screenPosition;
                for (int i = 0; i < npcList.Count; i++) {
                    NPC npc = Main.npc[npcList[i].Index];
                    if (!npc.active) {
                        continue;
                    }
                    //原版 DrawNPCs 画前加 netOffset、画后减回，联机平滑位与主屏一致
                    npc.position += npc.netOffset;
                    try {
                        Main.instance.DrawNPCDirect(spriteBatch, npc, false, screenPos);
                        drewAny = true;
                    }
                    catch {
                        //单个 NPC 绘制钩子异常不拖垮掩膜
                    }
                    finally {
                        npc.position -= npc.netOffset;
                    }
                }

                for (int i = 0; i < projList.Count; i++) {
                    Projectile proj = Main.projectile[projList[i].Index];
                    if (!proj.active) {
                        continue;
                    }
                    try {
                        Main.instance.DrawProjDirect(proj);
                        drewAny = true;
                    }
                    catch {
                        //单个弹幕绘制钩子异常不拖垮掩膜
                    }
                }
                //DrawProjDirect 会写当前绘制实体，照原版 DrawProjectiles 收尾清掉
                Main.CurrentDrawnEntity = null;
                Main.CurrentDrawnEntityShader = 0;
            }
            catch {
                //RT 绑定或批次开启异常：本帧无掩膜
            }
            finally {
                if (batchBegan) {
                    try {
                        spriteBatch.End();
                        batchEnded = true;
                    }
                    catch {
                        //钩子破坏了批次状态：本帧掩膜作废
                    }
                }
            }
            maskValid = drewAny && batchEnded;
        }

        private static bool EnsureTarget(GraphicsDevice graphicsDevice) {
            RenderTarget2D screen = Main.screenTarget;
            if (screen == null || screen.IsDisposed) {
                return false;
            }
            int width = screen.Width;
            int height = screen.Height;
            if (maskRT != null && !maskRT.IsDisposed && maskRT.Width == width && maskRT.Height == height) {
                return true;
            }
            maskRT?.Dispose();
            maskRT = null;
            maskClean = true;
            try {
                maskRT = new RenderTarget2D(graphicsDevice, width, height, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
            catch {
                //显存不足等异常：本次进世界不再勾边
                rtDegraded = true;
                return false;
            }
            return true;
        }

        //==================== 着色器参数 ====================

        /// <summary>
        /// uEntity 打包：x=勾边亮度（沸腾期让位） y=贴缝透水度 z=勾边宽度像素 w=深处透水度；
        /// 掩膜不可用时全零（着色器采样仍无条件，乘零即无），并把 s2 绑到掩膜或占位图
        /// </summary>
        internal static void FillUniforms(Effect effect, GraphicsDevice graphicsDevice, KikasaDomainPlayer kdp) {
            bool on = MaskReady;
            float rim = on ? RimStrength * (1f - 0.6f * kdp.FlipBoil) : 0f;
            float rimPx = MathHelper.Clamp(RimPx * Main.GameViewMatrix.Zoom.X, 1.5f, 3.5f);
            effect.Parameters["uEntity"]?.SetValue(new Vector4(
                rim, on ? BodyShowNear : 0f, rimPx, on ? BodyShowDeep : 0f));

            Texture2D bind = maskRT != null && !maskRT.IsDisposed ? maskRT : VaultAsset.placeholder2?.Value;
            if (bind != null) {
                graphicsDevice.Textures[2] = bind;
                graphicsDevice.SamplerStates[2] = SamplerState.LinearClamp;
            }
        }
    }
}
