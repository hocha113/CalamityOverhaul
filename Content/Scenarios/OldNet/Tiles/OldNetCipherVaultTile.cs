using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 主控破译矩阵台体（02 交互经济旗舰）：每潜唯一，落深井平台厅地板中央。
    /// 右键开台耗 3 RAM，打开 <see cref="UI.OldNetCipherPanel"/> 环形时序锁面板；
    /// 收手或爆仓后由面板上锁消散（一次性）。
    /// 面板入口单人硬门禁（协议桥 TryCast 内同款门禁双保险）
    /// </summary>
    internal class OldNetCipherVaultTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //暗钢台 + 琥珀待机灯：从既有琥珀族派生，锁盘态走冷青
        private static readonly Color Amber = new(255, 180, 80);
        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color DarkSteel = new(16, 20, 24);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //只允许面板路径，防镐子拆台
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(240, 210, 140), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetVaultHint.Value;
        }

        public override bool RightClick(int i, int j) {
            //单人硬门禁（含服务器早退）：破译面板全部状态为本机语义
            //TODO MP: MP 化时判定移服务器、结果走包，面板只做表现
            if (Main.netMode != NetmodeID.SinglePlayer || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            Vector2 worldPos = new(i * 16 + 8, j * 16 + 8);

            if (UI.OldNetCipherPanel.Visible) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(150, 160, 175), OldNetTexts.OldNetVaultLocked.Value);
                return true;
            }
            //开台座位费：RAM 现货，付不起不开
            if (!RamSystem.TryConsume(player, OldNetMetrics.VaultRamCost)) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(255, 120, 60), OldNetTexts.OldNetVaultNoRam.Value);
                SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.4f }, worldPos);
                return true;
            }

            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = -0.35f }, worldPos);
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.5f }, worldPos);
            UI.OldNetCipherPanel.Open(i, j);
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //面板开启期整台提亮（上行链路在烧）
            float boost = UI.OldNetCipherPanel.Visible ? 1.6f : 1f;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f + i * 0.4f);
            r = 0.30f * pulse * boost;
            g = 0.22f * pulse * boost;
            b = 0.10f * pulse * boost;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            float seed = (i * 11 + j * 5) * 0.31f;
            float open = UI.OldNetCipherPanel.Visible ? 1f : 0f;

            //shader 路径：登记进台体收集器（同文件 RenderHandle 批绘）
            if (OldNetVaultFX.ShaderReady) {
                OldNetVaultFX.Entries.Add(new OldNetVaultFX.Entry {
                    BasePos = new Vector2(i * 16 + 8, j * 16 + 16),
                    Seed = seed,
                    Open = open,
                });
                return false;
            }

            //CPU 回退：暗钢方台 + 缓转十字盘 + 琥珀待机灯
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 1.5f + seed);
            float spinMul = 1f + open * 2f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //台座（暗钢，两层收分）
            spriteBatch.Draw(px, basePos + new Vector2(0f, -3f), null, DarkSteel, 0f,
                origin, Size(20f, 6f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos + new Vector2(0f, -8f), null, DarkSteel * 0.9f, 0f,
                origin, Size(14f, 5f), SpriteEffects.None, 0f);
            //台缘受光线
            spriteBatch.Draw(px, basePos + new Vector2(0f, -6f), null, ColdCyan * 0.30f, 0f,
                origin, Size(20f, 1f), SpriteEffects.None, 0f);
            //琥珀待机灯（开台期快闪）
            float blink = 0.5f + 0.5f * MathF.Sin(t * (2f + open * 6f) + seed);
            spriteBatch.Draw(px, basePos + new Vector2(6f, -4f), null,
                Amber * (0.4f + 0.6f * blink), 0f, origin, Size(2f, 2f), SpriteEffects.None, 0f);

            //缓转十字盘：两根交叉细杆 + 外围四段短弧读作锁盘
            Vector2 discCenter = basePos + new Vector2(0f, -17f);
            float ang = t * 0.5f * spinMul + seed;
            for (int k = 0; k < 2; k++) {
                spriteBatch.Draw(px, discCenter, null, ColdCyan * (0.55f * pulse),
                    ang + k * MathHelper.PiOver2, origin, Size(16f, 1.4f), SpriteEffects.None, 0f);
            }
            for (int k = 0; k < 4; k++) {
                float a = -ang * 0.7f + k * MathHelper.PiOver2 + MathHelper.PiOver4;
                Vector2 p = discCenter + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 9f;
                spriteBatch.Draw(px, p, null, Amber * (0.5f * pulse), a + MathHelper.PiOver2,
                    origin, Size(4f, 1.2f), SpriteEffects.None, 0f);
            }
            //盘芯
            spriteBatch.Draw(px, discCenter, null, Color.White * (0.8f * pulse),
                MathHelper.PiOver4 + ang, origin, Size(2.6f, 2.6f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 台体富层收集器：tile PreDraw 逐帧登记（天然只含可见格），
    /// <see cref="OldNetVaultRender"/> 物块层后批绘。每潜仅 1 台，列表至多 1 项
    /// </summary>
    internal static class OldNetVaultFX
    {
        internal struct Entry
        {
            /// <summary>台底世界坐标（tile 底边中心）</summary>
            internal Vector2 BasePos;
            internal float Seed;
            /// <summary>面板开启 0/1（提速提亮）</summary>
            internal float Open;
        }

        internal static readonly List<Entry> Entries = [];

        internal static bool ShaderReady => !Main.dedServ && EffectLoader.OldNetVault?.Value != null;

        internal static void Clear() => Entries.Clear();
    }

    /// <summary>
    /// 台体 shader 批绘（Weight 1.47 = 分配单 P2 备用槽）。
    /// 用备用槽的理由：既有收集器 OldNetTileFXRender 本波次为 P4 独占文件，
    /// 不可扩展其画布类别，台体富层自带一条与功能同文件的绘制链
    /// </summary>
    internal class OldNetVaultRender : RenderHandle
    {
        public override float Weight => 1.47f;

        //台体画布：64 宽 × 72 高，底锚
        private const float CanvasW = 64f;
        private const float CanvasH = 72f;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap) {
            //本帧登记本帧消费，任何早退都要清空防跨帧堆积
            if (Main.gameMenu) {
                OldNetVaultFX.Clear();
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            Effect fx = EffectLoader.OldNetVault?.Value;
            if (px == null || px.IsDisposed || fx == null) {
                OldNetVaultFX.Clear();
                return;
            }
            if (OldNetVaultFX.Entries.Count == 0) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            float time = (float)Main.timeForVisualEffects / 60f;
            //底锚：origin 在贴图底边中心
            Vector2 origin = new(px.Width * 0.5f, px.Height);
            Vector2 scale = new(CanvasW / px.Width, CanvasH / px.Height);
            foreach (OldNetVaultFX.Entry e in OldNetVaultFX.Entries) {
                //共享参数化 shader：每次调用全参数重设（uniform 残留纪律）
                fx.CurrentTechnique = fx.Techniques["TechVaultRing"];
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(e.Seed);
                fx.Parameters["uOpen"]?.SetValue(e.Open);
                fx.Parameters["uAlpha"]?.SetValue(1f);
                fx.Parameters["uCanvas"]?.SetValue(new Vector2(CanvasW, CanvasH));
                fx.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, e.BasePos - Main.screenPosition, null, Color.White,
                    0f, origin, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
            OldNetVaultFX.Clear();
        }
    }
}
