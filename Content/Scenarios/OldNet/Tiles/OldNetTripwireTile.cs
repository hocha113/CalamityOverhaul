using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 光栅绊网发射桩：成对布设（竖井两壁横梁 / 门洞上下沿竖梁），
    /// 桩间光束亮 2.4s 灭 1.2s，亮相期过线 +8 噪（计量器不是警铃）。
    /// 按住右键 30 tick 剪断整道（+3 噪，永久拆除）。
    /// 对拓扑与判定在 <see cref="OldNetThreatField"/>；光束由锚桩登记
    /// <see cref="Renders.OldNetTileFX.Beams"/> 批绘（CPU 三层线 quad）
    /// </summary>
    internal class OldNetTripwireTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color WarnRed = new(235, 64, 44);
        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color Mint = new(120, 255, 170);
        private static readonly Color DarkShell = new(24, 60, 68);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(220, 90, 60), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetTripwireHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            //注册表里没有这对（异常态）就不开信道，孤儿桩交给巡检清理
            if (!OldNetThreatField.TryGetTripwire(i, j, out _)) {
                return true;
            }
            Main.LocalPlayer.GetModPlayer<OldNetThreatPlayer>()
                .BeginChannel(OldNetThreatPlayer.KindCutTripwire, i, j);
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //桩体微光：亮相期偏红，灭相偏琥珀（读数轻量版）
            bool lit = false;
            if (OldNetThreatField.TryGetTripwire(i, j, out OldNetThreatField.TripwirePair pair)) {
                OldNetThreatField.BeamCycleState(pair.Phase, out lit, out _, out _);
                lit &= pair.RearmTimer <= 0;
            }
            if (lit) {
                r = 0.26f;
                g = 0.05f;
                b = 0.04f;
            }
            else {
                r = 0.14f;
                g = 0.09f;
                b = 0.02f;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //缓存 RT 路径下 PreDraw 非逐帧，只登记特殊绘制点；桩体绘制与光束登记在
            //SpecialDraw（每帧必跑），否则光束一帧有一帧无
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            return false;
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;

            bool tracked = OldNetThreatField.TryGetTripwire(i, j, out OldNetThreatField.TripwirePair pair);
            bool lit = false;
            if (tracked) {
                OldNetThreatField.BeamCycleState(pair.Phase, out lit, out _, out _);
                lit &= pair.RearmTimer <= 0;

                //光束由锚桩登记批绘（对桩只画桩体，防双写）
                if (pair.A.X == i && pair.A.Y == j) {
                    Renders.OldNetTileFX.Beams.Add(new Renders.OldNetTileFX.BeamEntry {
                        A = new Vector2(pair.A.X * 16 + 8, pair.A.Y * 16 + 8),
                        B = new Vector2(pair.B.X * 16 + 8, pair.B.Y * 16 + 8),
                        Phase = pair.Phase,
                        Cooling01 = pair.RearmTimer > 0
                            ? pair.RearmTimer / (float)OldNetMetrics.TripwireRearmTicks : 0f,
                    });
                }
            }

            //臂朝向：指向对桩（横梁水平、竖梁垂直）
            float armRot = 0f;
            if (tracked) {
                Point other = pair.A.X == i && pair.A.Y == j ? pair.B : pair.A;
                armRot = (new Vector2(other.X, other.Y) - new Vector2(i, j)).ToRotation();
            }

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            Color accent = lit ? WarnRed : Amber;

            //斜置方芯（语汇同 TurretICE 吊装座）+ 指向对桩的短臂
            spriteBatch.Draw(px, center, null, DarkShell, MathHelper.PiOver4,
                origin, Size(11f, 11f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center + armRot.ToRotationVector2() * 4f, null, DarkShell * 1.15f,
                armRot, origin, Size(9f, 3.5f), SpriteEffects.None, 0f);
            //发射口芯：亮相白热 / 灭相琥珀待命
            Vector2 muzzle = center + armRot.ToRotationVector2() * 7f;
            spriteBatch.Draw(px, muzzle, null, accent * (lit ? 1f : 0.6f), MathHelper.PiOver4,
                origin, Size(3.2f, 3.2f), SpriteEffects.None, 0f);
            if (lit) {
                spriteBatch.Draw(px, muzzle, null, Color.White * 0.8f, MathHelper.PiOver4,
                    origin, Size(1.6f, 1.6f), SpriteEffects.None, 0f);
            }

            //剪断进度：桩顶薄荷绿短条
            OldNetThreatPlayer channel = Main.LocalPlayer?.active == true
                ? Main.LocalPlayer.GetModPlayer<OldNetThreatPlayer>() : null;
            bool cutting = tracked && channel != null
                && (channel.IsChanneling(OldNetThreatPlayer.KindCutTripwire, pair.A.X, pair.A.Y)
                    || channel.IsChanneling(OldNetThreatPlayer.KindCutTripwire, pair.B.X, pair.B.Y));
            if (cutting) {
                float prog = channel.ChannelProgress;
                Vector2 barTl = center + new Vector2(-10f, -13f);
                spriteBatch.Draw(px, barTl, null, new Color(10, 20, 24) * 0.85f, 0f,
                    Vector2.Zero, Size(20f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, barTl, null, Mint, 0f,
                    Vector2.Zero, Size(20f * prog, 3f), SpriteEffects.None, 0f);
            }

            //口芯辉光（A=0 加色亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float pulse = lit ? 0.55f + 0.15f * MathF.Sin(t * 9f + i) : 0.25f;
                Color glowCol = accent * pulse;
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, muzzle, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.12f, SpriteEffects.None, 0f);
            }
        }
    }
}
