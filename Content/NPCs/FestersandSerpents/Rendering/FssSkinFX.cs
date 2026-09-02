using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Rendering
{
    /// <summary>
    /// 整链绘制：腿 → 残影 → 着色器本体（体节尾→头、颚、头一批，前节压后节）→ 加色辉光层，
    /// 全部压在头的 PreDraw 里，体节/尾自身 PreDraw 返回 false。集中绘制的两个理由：
    /// 1. 体表着色器整链一次批切换（ScrapCommander 合同，禁每节重启）；
    /// 2. 鼓包蠕动/囊肿资源等跨节表现需要链序连续的参数空间。
    /// 全帧固定两次批切换（进 Immediate、回 Deferred）；着色器缺失走手染回退。
    /// </summary>
    internal static class FssSkinFX
    {
        /// <summary>屏外裁剪边距（像素）</summary>
        private const float CullMargin = 340f;

        internal static void DrawChain(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx) {
            if (Main.dedServ) {
                return;
            }
            //腿画最底
            ctx.Owner.LegRig.Draw(sb, screenPos, ctx);

            float clawFade = ctx.LegAlpha * (1f - ctx.Npc.alpha / 255f);
            //远层长镰：压暗垫在整链之下（深度读数）
            ctx.Owner.ClawRig.DrawBack(sb, screenPos, clawFade);

            Effect shader = EffectLoader.FssCorruptSkin?.Value;

            //残影层（Deferred 批加色，画在本体之下）
            DrawHeadGhosts(sb, screenPos, ctx);
            foreach (var seg in ctx.Segments) {
                if (seg.active && OnScreen(seg.Center, screenPos)) {
                    DrawSegmentGhost(sb, screenPos, seg, SegScaleMul(ctx, seg));
                }
            }

            //本体层：着色器一批画整链；缺失时手染回退
            if (shader != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                shader.CurrentTechnique = shader.Techniques["FesterTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成本体贴图，参数式贴图绑定实机失效
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                //尾→头逐节压上（前节扇冠盖后节腹板），颚根藏在头底之下
                for (int i = ctx.Segments.Count - 1; i >= 0; i--) {
                    NPC seg = ctx.Segments[i];
                    if (seg.active && OnScreen(seg.Center, screenPos)) {
                        DrawSegmentCore(sb, screenPos, ctx, seg, shader);
                    }
                }
                DrawJaws(sb, screenPos, ctx, shader);
                DrawHeadCore(sb, screenPos, ctx, shader);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                for (int i = ctx.Segments.Count - 1; i >= 0; i--) {
                    NPC seg = ctx.Segments[i];
                    if (seg.active && OnScreen(seg.Center, screenPos)) {
                        DrawSegmentCoreFallback(sb, screenPos, ctx, seg);
                    }
                }
                DrawJaws(sb, screenPos, ctx, null);
                DrawHeadCoreFallback(sb, screenPos, ctx);
            }

            //加色辉光层（Deferred 批 A=0，画在本体之上：脉冲波/满溢/怒放的即时强调）
            DrawHeadGlowOverlay(sb, screenPos, ctx);
            foreach (var seg in ctx.Segments) {
                if (seg.active && OnScreen(seg.Center, screenPos)) {
                    DrawSegmentGlowOverlay(sb, screenPos, ctx, seg);
                }
            }

            //近层疮杵：盖在整链之上（不对称剪影的主臂）
            ctx.Owner.ClawRig.DrawFront(sb, screenPos, clawFade);
        }

        private static bool OnScreen(Vector2 worldPos, Vector2 screenPos) {
            return worldPos.X > screenPos.X - CullMargin
                && worldPos.X < screenPos.X + Main.screenWidth + CullMargin
                && worldPos.Y > screenPos.Y - CullMargin
                && worldPos.Y < screenPos.Y + Main.screenHeight + CullMargin;
        }

        #region 共享量
        /// <summary>体节绘制缩放（吞沙鼓包放大 + 疮爆瘪缩）</summary>
        private static float SegScaleMul(FssStateContext ctx, NPC seg) {
            int ordinal = (int)seg.ai[0];
            float mul = 1f;
            if (ctx.BulgeStrength > 0.03f && ctx.BulgeOrdinal >= 0f) {
                float bulge = MathHelper.Clamp(1f - Math.Abs(ordinal - ctx.BulgeOrdinal) / 2.5f, 0f, 1f);
                mul += 0.30f * ctx.BulgeStrength * bulge * bulge;
            }
            float spent = ordinal < ctx.CystSpent.Length ? ctx.CystSpent[ordinal] : 0f;
            if (IsCystSeg(ctx, seg) && spent > 0.02f) {
                mul -= 0.10f * spent;
            }
            return mul;
        }

        private static bool IsCystSeg(FssStateContext ctx, NPC seg)
            => seg.type != ModContent.NPCType<FssTail>() && FssStateContext.IsCystOrdinal((int)seg.ai[0]);

        /// <summary>抖动/鞭波/落步下沉/高速微颤的绘制偏移（位置不动）</summary>
        private static Vector2 SegDrawOffset(FssStateContext ctx, NPC seg) {
            int ordinal = (int)seg.ai[0];
            Vector2 offset = Vector2.Zero;
            float perpAng = seg.rotation;
            if (ctx.ShakeStrength > 0.02f) {
                offset = perpAng.ToRotationVector2()
                    * MathF.Sin(Main.GlobalTimeWrappedHourly * 46f + ordinal * 0.9f)
                    * (5f * ctx.ShakeStrength);
            }
            if (ctx.WhipStrength > 0.1f) {
                float local = ctx.WhipAge - ordinal * 2.1f;
                if (local > 0f && local < 34f) {
                    float wave = MathF.Sin(local * 0.42f) * MathF.Exp(-local * 0.085f);
                    offset += perpAng.ToRotationVector2() * wave * ctx.WhipStrength;
                }
            }
            float dip = ctx.SampleStationBob(ordinal);
            if (dip > 0.02f) {
                offset.Y += dip * FssLegRig.StationDipPx;
            }
            //高速微颤：冲刺直线段小幅侧向抖（速度门控，冻结布条变活体的廉价一笔）
            float moved = (seg.position - seg.oldPosition).Length();
            if (moved > 20f) {
                float flut = MathHelper.Clamp((moved - 20f) / 26f, 0f, 1f);
                offset += perpAng.ToRotationVector2()
                    * MathF.Sin(Main.GlobalTimeWrappedHourly * 55f + ordinal * 1.7f) * (2.2f * flut);
            }
            return offset;
        }

        /// <summary>头部绘制偏移：落步下沉 + 出手帧反冲（释放波最初几帧向速度反向缩一记）</summary>
        private static Vector2 HeadDrawOffset(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            Vector2 offset = new(0f, ctx.SampleStationBob(0f) * FssLegRig.StationDipPx);
            if (ctx.GapWaveKind == SerpentChainMath.WaveRelease && ctx.GapWaveAge < 4f
                && npc.velocity.LengthSquared() > 1f) {
                offset -= npc.velocity.SafeNormalize(Vector2.Zero) * ((4f - ctx.GapWaveAge) * 1.4f);
            }
            return offset;
        }

        /// <summary>体节帧（Body 三帧表去掉底部隔帧留白）与归一 uv 区域</summary>
        private static (Rectangle frame, Vector4 uvRect) SegFrame(NPC seg, Texture2D texture) {
            Rectangle frame = seg.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = new Rectangle(0, 0, texture.Width, texture.Height / Math.Max(Main.npcFrameCount[seg.type], 1));
            }
            if (Main.npcFrameCount[seg.type] > 1) {
                frame.Height -= SerpentPortraitRig.BodyFramePad;
            }
            Vector4 uv = new(frame.X / (float)texture.Width, frame.Y / (float)texture.Height,
                frame.Width / (float)texture.Width, frame.Height / (float)texture.Height);
            return (frame, uv);
        }

        /// <summary>体节绘制原点：尾节基部扇冠对齐体节扇冠位（与荒花同一偏移）</summary>
        private static Vector2 SegOrigin(NPC seg, Rectangle frame) {
            Vector2 origin = frame.Size() / 2f;
            if (seg.type == ModContent.NPCType<FssTail>()) {
                origin.Y += BssDirector.TailOriginShift;
            }
            return origin;
        }

        /// <summary>死亡溃爆波是否已扫过该链序</summary>
        private static bool Ruptured(FssStateContext ctx, int ordinal) {
            return ctx.PulseKind == 3 && ctx.TotalSegments > 0
                && ctx.PulsePhase <= ordinal / (float)ctx.TotalSegments;
        }

        /// <summary>脉络强度：阶段递进 + 呼吸</summary>
        private static float VeinLevel(FssStateContext ctx) {
            float baseVein = ctx.Phase >= 3 ? 0.95f : ctx.Phase == 2 ? 0.75f : 0.52f;
            return baseVein * (0.88f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f));
        }
        #endregion

        #region 头
        private static void DrawHeadGhosts(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx) {
            NPC npc = ctx.Npc;
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            float fade = 1f - npc.alpha / 255f;

            float speed = npc.velocity.Length();
            float ghostIntensity = MathHelper.Clamp((speed - 14f) / 22f, 0f, 1f);
            if (ghostIntensity <= 0.05f) {
                return;
            }
            for (int i = npc.oldPos.Length - 1; i >= 1; i -= 2) {
                if (npc.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)npc.oldPos.Length;
                Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                Color ghost = FssVfx.IchorGold with { A = 0 } * (0.18f * t * ghostIntensity * fade);
                sb.Draw(texture, ghostPos, frameRec, ghost, npc.rotation,
                    origin, npc.scale * (0.92f + 0.08f * t), SpriteEffects.None, 0f);
            }
        }

        private static void DrawHeadCore(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx, Effect shader) {
            NPC npc = ctx.Npc;
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos + HeadDrawOffset(ctx);
            float fade = 1f - npc.alpha / 255f;
            if (fade <= 0.01f) {
                return;
            }
            Color light = Lighting.GetColor(npc.Center.ToTileCoordinates());
            bool ruptured = ctx.PulseKind == 3 && ctx.PulsePhase <= 0.02f;
            if (ruptured) {
                light = Color.Lerp(light, FssVfx.NecroShadow, 0.6f);
            }

            shader.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
            shader.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.031f);
            shader.Parameters["uPhase"]?.SetValue(0f);
            shader.Parameters["uSwell"]?.SetValue(MathHelper.Clamp(ctx.CystGlow * 0.6f + ctx.SwallowSuction * 0.5f, 0f, 1f));
            shader.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(Math.Max(ctx.ErodeLevel, ruptured ? 0.85f : 0f), 0f, 1f));
            shader.Parameters["uVein"]?.SetValue(ruptured ? 0f : VeinLevel(ctx));
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(texture, mainPos, frameRec, light * fade, npc.rotation,
                origin, npc.scale, SpriteEffects.None, 0f);
        }

        private static void DrawJaws(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx, Effect shader) {
            NPC npc = ctx.Npc;
            float fade = 1f - npc.alpha / 255f;
            if (fade <= 0.01f) {
                return;
            }
            float jawOpen = BssJawDraw.ResolveOpen(ctx.ClawCommand, ctx.ClawPhase, ctx.ClawBurst, ctx.GaitPhase);
            Vector2 headWorld = npc.Center + HeadDrawOffset(ctx);
            Color tint = shader != null
                ? Lighting.GetColor(npc.Center.ToTileCoordinates()) * fade
                : Lighting.GetColor(npc.Center.ToTileCoordinates()).MultiplyRGB(FssVfx.SkinMul) * fade;
            if (shader != null) {
                shader.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                shader.CurrentTechnique.Passes[0].Apply();
            }
            BssJawDraw.Draw(sb, headWorld, npc.rotation, jawOpen, tint, screenPos, npc.scale);
        }

        private static void DrawHeadCoreFallback(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx) {
            NPC npc = ctx.Npc;
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos + HeadDrawOffset(ctx);
            float fade = 1f - npc.alpha / 255f;
            Color light = Lighting.GetColor(npc.Center.ToTileCoordinates());
            sb.Draw(texture, mainPos, frameRec, light.MultiplyRGB(FssVfx.SkinMul) * fade, npc.rotation,
                origin, npc.scale, SpriteEffects.None, 0f);
        }

        private static void DrawHeadGlowOverlay(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx) {
            NPC npc = ctx.Npc;
            if (ctx.CystGlow <= 0.03f) {
                return;
            }
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos + HeadDrawOffset(ctx);
            float fade = 1f - npc.alpha / 255f;
            Color glow = FssVfx.IchorBright with { A = 0 } * (0.45f * ctx.CystGlow * fade);
            sb.Draw(texture, mainPos, frameRec, glow, npc.rotation,
                origin, npc.scale * 1.04f, SpriteEffects.None, 0f);
            Lighting.AddLight(npc.Center, FssVfx.IchorGold.ToVector3() * 0.4f * ctx.CystGlow);
        }
        #endregion

        #region 体节
        private static void DrawSegmentGhost(SpriteBatch sb, Vector2 screenPos, NPC seg, float scaleMul) {
            float moved = (seg.position - seg.oldPosition).Length();
            float ghostIntensity = MathHelper.Clamp((moved - 15f) / 22f, 0f, 1f);
            float fade = 1f - seg.alpha / 255f;
            if (ghostIntensity <= 0.05f || fade <= 0.01f) {
                return;
            }
            Main.instance.LoadNPC(seg.type);
            Texture2D texture = TextureAssets.Npc[seg.type].Value;
            (Rectangle frame, _) = SegFrame(seg, texture);
            Vector2 origin = SegOrigin(seg, frame);

            Vector2 back = seg.Center - (seg.position - seg.oldPosition) * 1.5f - screenPos;
            sb.Draw(texture, back, frame,
                FssVfx.IchorGold with { A = 0 } * (0.2f * ghostIntensity * fade),
                seg.rotation, origin, seg.scale * scaleMul * 0.95f, SpriteEffects.None, 0f);
            Vector2 back2 = seg.Center - (seg.position - seg.oldPosition) * 2.8f - screenPos;
            sb.Draw(texture, back2, frame,
                FssVfx.IchorGold with { A = 0 } * (0.1f * ghostIntensity * fade),
                seg.rotation, origin, seg.scale * scaleMul * 0.9f, SpriteEffects.None, 0f);
        }

        private static void DrawSegmentCore(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx, NPC seg, Effect shader) {
            int ordinal = (int)seg.ai[0];
            float fade = 1f - seg.alpha / 255f;
            if (fade <= 0.01f) {
                return;
            }
            Texture2D texture = TextureAssets.Npc[seg.type].Value;
            (Rectangle frame, Vector4 uvRect) = SegFrame(seg, texture);
            Vector2 origin = SegOrigin(seg, frame);
            Vector2 drawPos = seg.Center + SegDrawOffset(ctx, seg) - screenPos;
            float scaleMul = SegScaleMul(ctx, seg);

            bool isCyst = IsCystSeg(ctx, seg);
            float spent = ordinal < ctx.CystSpent.Length ? ctx.CystSpent[ordinal] : 0f;
            bool ruptured = Ruptured(ctx, ordinal);

            Color light = Lighting.GetColor(seg.Center.ToTileCoordinates());
            if (ruptured) {
                light = Color.Lerp(light, FssVfx.NecroShadow, 0.66f);
            }

            //囊肿充能热点：阶段越深底光越足，爆过瘪着的不亮；鼓包波推高全节
            float swell = 0f;
            if (isCyst) {
                swell = (1f - spent) * (ctx.Phase >= 3 ? 0.75f : ctx.Phase == 2 ? 0.5f : 0.34f);
            }
            if (ctx.BulgeStrength > 0.03f && ctx.BulgeOrdinal >= 0f) {
                float bulge = MathHelper.Clamp(1f - Math.Abs(ordinal - ctx.BulgeOrdinal) / 2.5f, 0f, 1f);
                swell = Math.Max(swell, ctx.BulgeStrength * bulge);
            }
            //裂躯断口：领节挂伪头热点，缝两端裂隙渗光满值
            bool seamLead = ctx.SplitLeaderOrdinal >= 0 && ordinal == ctx.SplitLeaderOrdinal;
            bool seamRear = ctx.SplitLeaderOrdinal >= 0 && ordinal == ctx.SplitLeaderOrdinal - 1;
            if (seamLead) {
                swell = Math.Max(swell, 0.9f);
            }
            float crack = Math.Max(ctx.ErodeLevel, ruptured ? 0.85f : 0f);
            if (seamLead || seamRear) {
                crack = 1f;
            }

            shader.Parameters["uUvRect"]?.SetValue(uvRect);
            shader.Parameters["uSeed"]?.SetValue(ordinal * 0.173f);
            shader.Parameters["uPhase"]?.SetValue(ordinal + 1f);
            shader.Parameters["uSwell"]?.SetValue(MathHelper.Clamp(swell, 0f, 1f));
            shader.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(crack, 0f, 1f));
            shader.Parameters["uVein"]?.SetValue(ruptured ? 0f : VeinLevel(ctx));
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(texture, drawPos, frame, light * fade, seg.rotation,
                origin, seg.scale * scaleMul, SpriteEffects.None, 0f);
        }

        private static void DrawSegmentCoreFallback(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx, NPC seg) {
            int ordinal = (int)seg.ai[0];
            float fade = 1f - seg.alpha / 255f;
            if (fade <= 0.01f) {
                return;
            }
            Main.instance.LoadNPC(seg.type);
            Texture2D texture = TextureAssets.Npc[seg.type].Value;
            (Rectangle frame, _) = SegFrame(seg, texture);
            Vector2 origin = SegOrigin(seg, frame);
            Vector2 drawPos = seg.Center + SegDrawOffset(ctx, seg) - screenPos;
            Color body = Lighting.GetColor(seg.Center.ToTileCoordinates()).MultiplyRGB(FssVfx.SkinMul);
            if (Ruptured(ctx, ordinal)) {
                body = Color.Lerp(body, FssVfx.NecroShadow, 0.66f);
            }
            sb.Draw(texture, drawPos, frame, body * fade, seg.rotation,
                origin, seg.scale * SegScaleMul(ctx, seg), SpriteEffects.None, 0f);
        }

        private static void DrawSegmentGlowOverlay(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx, NPC seg) {
            int ordinal = (int)seg.ai[0];
            bool seamPiece = ctx.SplitLeaderOrdinal >= 0
                && (ordinal == ctx.SplitLeaderOrdinal || ordinal == ctx.SplitLeaderOrdinal - 1);
            if ((!IsCystSeg(ctx, seg) && !seamPiece) || ctx.TotalSegments <= 0 || Ruptured(ctx, ordinal)) {
                return;
            }
            float spent = ordinal < ctx.CystSpent.Length ? ctx.CystSpent[ordinal] : 0f;
            if (spent >= 0.6f && !seamPiece) {
                return;
            }
            float fade = 1f - seg.alpha / 255f;

            //脉冲波/满溢闪/怒放的即时强调（着色器底光之上的事件层）
            float glow = 0f;
            float fraction = ordinal / (float)ctx.TotalSegments;
            if (ctx.PulseKind is 1 or 2) {
                float dist = Math.Abs(fraction - ctx.PulsePhase);
                glow = MathHelper.Clamp(1f - dist / 0.16f, 0f, 1f);
            }
            else if (ctx.PulseKind == 4) {
                glow = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + ordinal * 0.8f);
            }
            glow = Math.Max(glow, ctx.CystGlow * 0.6f) * (1f - spent);
            //断口常亮：伪头/裂端的伤口辉光（脉动）
            if (seamPiece) {
                glow = Math.Max(glow, 0.6f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + ordinal));
            }
            if (glow <= 0.03f) {
                return;
            }

            Texture2D texture = TextureAssets.Npc[seg.type].Value;
            (Rectangle frame, _) = SegFrame(seg, texture);
            Vector2 origin = SegOrigin(seg, frame);
            Vector2 drawPos = seg.Center + SegDrawOffset(ctx, seg) - screenPos;
            Color gold = FssVfx.IchorBright with { A = 0 } * (0.55f * glow * fade);
            sb.Draw(texture, drawPos, frame, gold, seg.rotation,
                origin, seg.scale * SegScaleMul(ctx, seg) * 1.05f, SpriteEffects.None, 0f);
            Lighting.AddLight(seg.Center, FssVfx.IchorGold.ToVector3() * 0.3f * glow);
        }
        #endregion
    }
}
