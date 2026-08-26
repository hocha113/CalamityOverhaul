using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 环绕枪阵绘制装配:金丝带轨道(顶点条带)+幻影燧发枪(TechMusket)+枪口魔法阵(TechCircle)
    /// +终曲巨炮，按 z 分前后两趟画(画家算法，血统:CultistOrreryRenderer)。<br/>
    /// 远半环由 <see cref="TiroFinaleFarRender"/> 在玩家层之前触发，近半环走
    /// <see cref="IPrimitiveDrawable"/> 实体层之后；着色器缺失时全线精灵回退
    /// </summary>
    internal static class TiroFinaleRenderer
    {
        //——金色板:巴麻美的丝带金——
        internal static readonly Color ColDeep = new(112, 68, 22);
        internal static readonly Color ColMid = new(236, 182, 78);
        internal static readonly Color ColBright = new(255, 228, 142);
        internal static readonly Color ColHot = new(255, 250, 232);

        /// <summary>枪口阵盘径契约:可见半径 = 画布半宽 × 0.42(同 CultistPlanet/ShockRing)</summary>
        internal const float CircleDiskFrac = 0.42f;

        private const int RibbonSegments = 64;
        private static readonly List<int> sortBuf = new(TiroFinaleRig.SlotCount);
        private static VertexPositionColorTexture[] stripBuf = new VertexPositionColorTexture[(RibbonSegments + 2) * 2];

        internal static bool ShaderReady => EffectLoader.TiroFinaleFX?.Value != null
            && CWRAsset.PerlinNoise?.Value != null;

        /// <summary>
        /// 单趟绘制:zSign=+1 远半(玩家身后)，-1 近半(实体层之上，含手上枪口阵与终曲巨炮)<br/>
        /// 调用方须不在 SpriteBatch 批内
        /// </summary>
        internal static void DrawHeldLayer(TiroFinaleHeld held, int zSign) {
            if (Main.gameMenu || Main.dedServ || held.Owner == null || !held.Owner.active) {
                return;
            }

            //丝带轨道压最底
            DrawRibbonBand(held, zSign);

            //收集本半的枪位并按 z 远→近排序
            sortBuf.Clear();
            Span<TiroFinaleHeld.MusketPose> poses = stackalloc TiroFinaleHeld.MusketPose[TiroFinaleRig.SlotCount];
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (!held.ComputeMusketPose(i, out poses[i])) {
                    continue;
                }
                bool inHalf = zSign > 0 ? poses[i].Z >= 0f : poses[i].Z < 0f;
                if (inHalf) {
                    sortBuf.Add(i);
                }
            }

            bool nearPass = zSign < 0;
            bool giantVisible = nearPass && held.finalePhase >= TiroFinaleHeld.FinaleManifest;
            bool handCircleVisible = nearPass && held.handCircle > 0f;
            if (sortBuf.Count == 0 && !giantVisible && !handCircleVisible) {
                return;
            }

            for (int a = 0; a < sortBuf.Count - 1; a++) {
                for (int b = a + 1; b < sortBuf.Count; b++) {
                    if (poses[sortBuf[b]].Z > poses[sortBuf[a]].Z) {
                        (sortBuf[a], sortBuf[b]) = (sortBuf[b], sortBuf[a]);
                    }
                }
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (int slot in sortBuf) {
                if (held.slotPhase[slot] != TiroFinaleHeld.PhaseEmpty) {
                    DrawMusket(held, in poses[slot], slot);
                }
                if (held.slotCircle[slot] > 0f && held.slotPhase[slot] != TiroFinaleHeld.PhaseEmpty) {
                    float t = 1f - held.slotCircle[slot] / TiroFinaleHeld.CircleLife;
                    DrawCircle(poses[slot].MuzzleWorld, poses[slot].Rotation
                        , 26f * poses[slot].PScale, TiroFinaleRig.CircleMinorRatio(poses[slot].AxialK)
                        , CircleEnvelope(t), 0f, slot * 0.61f);
                }
            }

            if (handCircleVisible) {
                float t = 1f - held.handCircle / TiroFinaleHeld.CircleLife;
                DrawCircle(held.ShootPos, held.Projectile.rotation, 30f
                    , TiroFinaleRig.CircleMinorRatio(1f), CircleEnvelope(t), 0f, 3.7f);
            }

            if (giantVisible) {
                DrawGiant(held);
            }

            sb.End();
        }

        /// <summary>枪口阵开合包络:5f 内绽开，尾段收拢</summary>
        private static float CircleEnvelope(float t) {
            float open = MathHelper.Clamp(t / 0.22f, 0f, 1f);
            float close = 1f - MathHelper.Clamp((t - 0.62f) / 0.38f, 0f, 1f);
            return TiroFinaleHeld.EaseOutCubic(open) * close * close;
        }

        #region 幻影枪
        private static void DrawMusket(TiroFinaleHeld held, in TiroFinaleHeld.MusketPose pose, int slot) {
            Texture2D tex = TextureAssets.Projectile[held.Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            SpriteEffects fx = pose.FacingRight ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 scale = new(pose.Scale * pose.AxialK, pose.Scale);

            if (ShaderReady) {
                Effect effect = EffectLoader.TiroFinaleFX.Value;
                effect.CurrentTechnique = effect.Techniques["TechMusket"];
                SetCommonParams(effect);
                effect.Parameters["uAlpha"]?.SetValue(pose.Alpha);
                effect.Parameters["uForm"]?.SetValue(pose.Form);
                effect.Parameters["uFire"]?.SetValue(pose.Fire);
                effect.Parameters["uLit"]?.SetValue(pose.Lit);
                effect.Parameters["uSeed"]?.SetValue(slot * 0.37f + 0.13f);
                effect.Parameters["uOpen"]?.SetValue(1f);
                effect.Parameters["uCharge"]?.SetValue(0f);
                effect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                BindNoise();
                effect.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(tex, pose.World - Main.screenPosition, null, Color.White
                    , pose.Rotation, origin, scale, fx, 0f);
                return;
            }

            //回退:金色半透明体+加光复写
            Color body = new Color(255, 208, 122) * (0.6f * pose.Alpha * MathHelper.Clamp(pose.Lit, 0.4f, 1f));
            Main.spriteBatch.Draw(tex, pose.World - Main.screenPosition, null, body
                , pose.Rotation, origin, scale, fx, 0f);
            Color shine = new Color(255, 224, 150) with { A = 0 };
            Main.spriteBatch.Draw(tex, pose.World - Main.screenPosition, null, shine * (0.3f * pose.Alpha + 0.5f * pose.Fire)
                , pose.Rotation, origin, scale, fx, 0f);
        }
        #endregion

        #region 枪口魔法阵
        /// <summary>
        /// 金色枪口阵:majorR=长轴可见半径(px)，minorRatio=短轴比(枪管越朝镜头越圆)，
        /// 长轴恒垂直枪管(3D 圆盘侧视投影)
        /// </summary>
        internal static void DrawCircle(Vector2 worldPos, float barrelRot, float majorR, float minorRatio
            , float envelope, float charge, float seed) {
            if (envelope <= 0.01f) {
                return;
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (canvas == null) {
                return;
            }
            float quadPx = majorR / CircleDiskFrac * 2f;
            Vector2 screenPos = worldPos - Main.screenPosition;

            if (ShaderReady) {
                Effect effect = EffectLoader.TiroFinaleFX.Value;
                effect.CurrentTechnique = effect.Techniques["TechCircle"];
                SetCommonParams(effect);
                effect.Parameters["uAlpha"]?.SetValue(1f);
                effect.Parameters["uForm"]?.SetValue(1f);
                effect.Parameters["uFire"]?.SetValue(0f);
                effect.Parameters["uLit"]?.SetValue(1f);
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uOpen"]?.SetValue(envelope);
                effect.Parameters["uCharge"]?.SetValue(charge);
                BindNoise();
                effect.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(canvas, screenPos, null, Color.White, barrelRot
                    , canvas.Size() * 0.5f, new Vector2(quadPx * minorRatio, quadPx) / canvas.Width, SpriteEffects.None, 0f);
                return;
            }

            //回退:真 alpha 扩散环双层加光
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring == null) {
                return;
            }
            float rScale = majorR * 2f / ring.Width;
            Color gold = ColBright with { A = 0 };
            Main.spriteBatch.Draw(ring, screenPos, null, gold * (0.7f * envelope), barrelRot
                , ring.Size() * 0.5f, new Vector2(rScale * minorRatio, rScale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(ring, screenPos, null, (ColMid with { A = 0 }) * (0.5f * envelope), barrelRot
                , ring.Size() * 0.5f, new Vector2(rScale * minorRatio, rScale) * 0.66f, SpriteEffects.None, 0f);
        }
        #endregion

        #region 终曲巨炮
        private static void DrawGiant(TiroFinaleHeld held) {
            held.ComputeGiantPose(out Vector2 world, out float rot, out float scale, out float reveal
                , out Vector2 muzzle, out bool facingRight);
            if (reveal <= 0.01f) {
                return;
            }

            Texture2D tex = TextureAssets.Projectile[held.Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            SpriteEffects fxFlip = facingRight ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 screenPos = world - Main.screenPosition;

            float charge = held.finalePhase switch {
                TiroFinaleHeld.FinaleCharge => held.finaleTimer / TiroFinaleHeld.ChargeTime,
                TiroFinaleHeld.FinaleBlast => 1f,
                _ => 0f,
            };
            float fire = held.finalePhase switch {
                TiroFinaleHeld.FinaleCharge => 0.55f * (held.finaleTimer / TiroFinaleHeld.ChargeTime),
                TiroFinaleHeld.FinaleBlast => 1f,
                TiroFinaleHeld.FinaleFade => MathHelper.Clamp(1f - held.finaleTimer / 9f, 0f, 1f),
                _ => 0f,
            };

            if (ShaderReady) {
                Effect effect = EffectLoader.TiroFinaleFX.Value;
                effect.CurrentTechnique = effect.Techniques["TechMusket"];
                SetCommonParams(effect);
                float alpha = held.finalePhase == TiroFinaleHeld.FinaleFade
                    ? 1f - held.finaleTimer / TiroFinaleHeld.FinaleFadeTime : 1f;
                effect.Parameters["uAlpha"]?.SetValue(alpha);
                effect.Parameters["uForm"]?.SetValue(reveal);
                effect.Parameters["uFire"]?.SetValue(fire);
                effect.Parameters["uLit"]?.SetValue(1.08f);
                effect.Parameters["uSeed"]?.SetValue(7.31f);
                effect.Parameters["uOpen"]?.SetValue(1f);
                effect.Parameters["uCharge"]?.SetValue(charge);
                effect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                BindNoise();
                effect.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(tex, screenPos, null, Color.White, rot, origin, scale, fxFlip, 0f);
            }
            else {
                Color body = new Color(255, 208, 122) * (0.7f * reveal);
                Main.spriteBatch.Draw(tex, screenPos, null, body, rot, origin, scale, fxFlip, 0f);
                Main.spriteBatch.Draw(tex, screenPos, null, (ColBright with { A = 0 }) * (0.35f * reveal + 0.6f * fire)
                    , rot, origin, scale, fxFlip, 0f);
            }

            //巨型枪口阵:显现期张开，蓄势期收缩聚能，发射帧顶到最亮
            if (held.finalePhase is TiroFinaleHeld.FinaleManifest or TiroFinaleHeld.FinaleCharge or TiroFinaleHeld.FinaleBlast) {
                float open = held.finalePhase == TiroFinaleHeld.FinaleManifest
                    ? TiroFinaleHeld.EaseOutCubic(held.finaleTimer / TiroFinaleHeld.ManifestTime) : 1f;
                float majorR = 92f * (1f - 0.28f * charge);
                DrawCircle(muzzle, rot, majorR, 0.3f, open, charge, 11.7f);
                //蓄势期再叠一圈内环，读作"聚能"
                if (charge > 0.05f) {
                    DrawCircle(muzzle, rot, majorR * 0.55f, 0.34f, charge, charge, 5.9f);
                }
            }
        }
        #endregion

        #region 金丝带轨道
        /// <summary>
        /// 环阵轨道带:闭环丝带沿 3D 圆分段投影，vs+ps 顶点条带；
        /// u 取角度整圈归一(0~1)，shader 内只乘整数频率保跨缝连续
        /// </summary>
        private static void DrawRibbonBand(TiroFinaleHeld held, int zSign) {
            if (!ShaderReady) {
                return;//回退模式无轨道带
            }
            int live = held.LiveMusketCount();
            float bandAlpha = MathHelper.Clamp(live / 2f, 0f, 1f) * 0.72f;
            if (held.finalePhase == TiroFinaleHeld.FinaleGather) {
                bandAlpha *= 1f - MathHelper.Clamp(held.finaleTimer / TiroFinaleHeld.GatherTime, 0f, 1f);
            }
            else if (held.finalePhase != TiroFinaleHeld.FinaleNone) {
                bandAlpha = 0f;
            }
            if (bandAlpha <= 0.02f) {
                return;
            }

            float time = held.Time;
            TiroFinaleRig.GetBasis(time, out Vector3 e1, out Vector3 e2);
            Vector2 ringCenter = held.RingCenter;
            Vector2 screenCenter = ringCenter - Main.screenPosition;

            //收集活槽相位角，给占用辉光
            Span<float> liveAngles = stackalloc float[TiroFinaleRig.SlotCount];
            int liveN = 0;
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (held.slotPhase[i] is TiroFinaleHeld.PhaseForming or TiroFinaleHeld.PhaseReady or TiroFinaleHeld.PhaseAiming) {
                    liveAngles[liveN++] = MathHelper.WrapAngle(TiroFinaleRig.SlotAngle(i, time));
                }
            }

            //逐段投影,只留本半 z 的段(条带可能断成多段,用退化三角连接)
            int vi = 0;
            bool inRun = false;
            for (int j = 0; j <= RibbonSegments; j++) {
                float t = j / (float)RibbonSegments * MathHelper.TwoPi;
                Vector3 d = e1 * MathF.Cos(t) + e2 * MathF.Sin(t);
                Vector3 p3 = d * TiroFinaleRig.Radius;
                bool inHalf = zSign > 0 ? p3.Z >= 0f : p3.Z < 0f;
                if (!inHalf) {
                    if (inRun && vi >= 2) {
                        //断段:重复末点做退化三角
                        stripBuf[vi] = stripBuf[vi - 1];
                        vi++;
                        inRun = false;
                    }
                    continue;
                }

                Vector2 p2 = screenCenter + TiroFinaleRig.Project(p3, out float pscale);
                //切向求法向
                Vector3 tan3 = -e1 * MathF.Sin(t) + e2 * MathF.Cos(t);
                Vector2 tan2 = (TiroFinaleRig.Project(p3 + tan3 * 8f, out _) - TiroFinaleRig.Project(p3, out _));
                Vector2 normal = tan2.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);

                float hw = 4.2f * pscale;
                float lit = TiroFinaleRig.DepthLit(p3.Z, TiroFinaleRig.Radius);
                //占用辉光:靠近活枪的段更亮
                float occupy = 0f;
                float wrapT = MathHelper.WrapAngle(t);
                for (int k = 0; k < liveN; k++) {
                    float da = MathF.Abs(MathHelper.WrapAngle(wrapT - liveAngles[k]));
                    occupy = MathF.Max(occupy, 1f - MathHelper.Clamp(da / 0.85f, 0f, 1f));
                }
                float a = bandAlpha * (0.38f + 0.62f * occupy);

                if (!inRun && vi > 0) {
                    //新段起点:先补一个退化三角
                    stripBuf[vi] = new VertexPositionColorTexture(new Vector3(p2 + normal * hw, 0f)
                        , new Color(lit, occupy, 0f, 0f), new Vector2(j / (float)RibbonSegments, 0f));
                    vi++;
                }
                inRun = true;

                float u = j / (float)RibbonSegments;
                Color vc = new(lit, occupy, 0f, a);
                if (vi + 2 > stripBuf.Length) {
                    Array.Resize(ref stripBuf, stripBuf.Length * 2);
                }
                stripBuf[vi++] = new VertexPositionColorTexture(new Vector3(p2 + normal * hw, 0f), vc, new Vector2(u, 0f));
                stripBuf[vi++] = new VertexPositionColorTexture(new Vector3(p2 - normal * hw, 0f), vc, new Vector2(u, 1f));
            }
            if (vi < 4) {
                return;
            }

            Effect effect = EffectLoader.TiroFinaleFX.Value;
            effect.CurrentTechnique = effect.Techniques["TechRibbon"];
            SetCommonParams(effect);
            effect.Parameters["uAlpha"]?.SetValue(1f);
            effect.Parameters["uForm"]?.SetValue(1f);
            effect.Parameters["uFire"]?.SetValue(0f);
            effect.Parameters["uLit"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(0.77f);
            effect.Parameters["uOpen"]?.SetValue(1f);
            effect.Parameters["uCharge"]?.SetValue(0f);
            effect.Parameters["transformMatrix"]?.SetValue(Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1));

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            BindNoise();
            effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripBuf, 0, vi - 2);
        }
        #endregion

        private static void SetCommonParams(Effect effect) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uColDeep"]?.SetValue(ColDeep.ToVector3());
            effect.Parameters["uColMid"]?.SetValue(ColMid.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(ColBright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(ColHot.ToVector3());
        }

        private static void BindNoise() {
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
        }
    }

    /// <summary>
    /// 远半环玩家身后图层。DrawBeforePlayers 每帧被 BehindNPCs 与主玩家层各触发一次，
    /// 用 DrawAfterTiles 上膛 + 首次消费闩锁保证只画一次(Unsunghero 同款)
    /// </summary>
    internal sealed class TiroFinaleFarRender : RenderHandle
    {
        private static bool armed;

        public override float Weight => 1.14f;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu) {
                return;
            }
            armed = false;
            foreach (Projectile proj in Main.projectile) {
                if (!proj.active || proj.ModProjectile is not TiroFinaleHeld held) {
                    continue;
                }
                held.DrawFarLayer();
            }
        }
    }
}
