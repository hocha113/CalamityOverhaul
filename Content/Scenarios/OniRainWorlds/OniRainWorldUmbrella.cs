using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.Actors;
using InnoVault.Cinematics;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 插在地上的鬼雨伞，锚点=<see cref="Actor.Position"/>地表中心，
    /// 由 <see cref="OniUmbrellaWorldSpawn"/> 在出生点附近权威放置。<br/>
    /// 靠近右键触发入雨演出（<see cref="OniRainWorldTransition"/> + <see cref="OniRainWorldCutscene"/>），
    /// 交互纯本地；下潜交给雨世界里的夺伞（<see cref="KasaOnis.KasaOniActor"/>）。<br/>
    /// 已真正获得鬼伞的玩家不再看见这把伞，鬼雨初遇是一次性叙事空间，
    /// 可见性按玩家各自判（<see cref="ShouldShowForLocalPlayer"/>），Actor 本体全端常驻。<br/>
    /// 伞体=鬼伞正式贴图（<see cref="KikasaItem"/>）+ KikasaUmbrella TechCanopy 湿光鬼眼着色；
    /// 伞底一摊黑色积水（倒影由 <see cref="OniUmbrellaPuddleRender"/> 屏幕空间镜像接管），
    /// 水洼呼出上浮的黑水滴，偶发一粒被拽回伞里，水在回流入伞。
    /// </summary>
    internal class OniRainWorldUmbrella : Actor
    {
        private const float InteractDistance = 150f;
        /// <summary>鬼伞贴图 98×128 原尺寸直画：立在地上是一座入口地标</summary>
        private const float DrawScale = 1f;

        /// <summary>伞面鬼眼锚点（帧内归一 uv）与半径，与悬伞同一组素材校准点</summary>
        private static readonly Vector2 EyeCenter = new(0.5f, 0.34f);
        private const float EyeRadius = 0.2f;

        /// <summary>水洼半宽/半高（世界像素），倒影渲染层与粒子生成共用</summary>
        internal const float PuddleHalfWidth = 64f;
        internal const float PuddleHalfHeight = 7f;

        //湿墨色板，与鬼雨体系一致
        private static readonly Color PaleTint = new(190, 205, 208);
        private static readonly Color MistDamp = new(58, 66, 70);
        //积水比伞鬼污潭更黑：一摊真正的黑水
        private static readonly Color PoolBlack = new(16, 21, 24);
        private static readonly Color PoolTeal = new(120, 150, 146);

        private static bool canopyFailureLogged;

        private float swayTimer;
        private int dripTimer;
        private int riseTimer;
        private float promptAlpha;
        //触发确认帧：右键瞬间伞猛地一颤
        private int triggerKick;

        //鬼眼：接近缓睁、瞳向追人、触发全睁红芒
        private float eyeOpen;
        private float eyeGlow;
        private Vector2 eyeLook = new(0f, 1f);
        private int blinkTimer = 180;

        /// <summary>伞盖中心，交互距离与提示锚点（98×128 贴图的盖心）</summary>
        public Vector2 CanopyAnchor => Position + new Vector2(0f, -92f);

        /// <summary>伞盖底沿：回流水滴的喉点</summary>
        internal Vector2 CanopyThroat => Position + new Vector2(0f, -66f);

        /// <summary>水洼中心（世界坐标），贴地略沉；倒影镜面绕这条线翻转</summary>
        internal Vector2 PuddleCenter => Position + new Vector2(0f, 3f);

        /// <summary>水洼张开度：常驻 1，演出躁动/触发时涨大</summary>
        internal float PuddleSwell => 1f + CurrentAgitation * 0.25f + KickAmount * 0.35f;

        public override void OnSpawn(params object[] args) {
            Width = 48;
            Height = 72;
            //剔除扩张防半入屏弹出
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.AfterTiles;
            swayTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>
        /// 本地玩家是否还看得见这把伞：真正获得鬼伞后它就不在了（镜像鸟居拔刀后隐藏）。
        /// Actor 本体全端常驻，这里只裁本地表现与交互。
        /// </summary>
        internal static bool ShouldShowForLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            return !ShenyoStorySync.KikasaGranted
                && !player.HasItem(ModContent.ItemType<KikasaItem>());
        }

        public override void AI() {
            swayTimer += 0.016f;
            if (swayTimer > MathHelper.TwoPi) {
                swayTimer -= MathHelper.TwoPi;
            }

            if (Main.dedServ) {
                return;
            }

            if (!ShouldShowForLocalPlayer()) {
                promptAlpha = 0f;
                return;
            }

            if (triggerKick > 0) {
                triggerKick--;
            }

            Lighting.AddLight(CanopyAnchor, 0.10f, 0.14f, 0.15f);
            if (eyeGlow > 0.05f) {
                //鬼眼红芒渗出来一点
                Lighting.AddLight(CanopyAnchor, 0.14f * eyeGlow, 0.02f, 0.03f);
            }
            UpdateAmbience();
            UpdateEye();
            UpdateInteraction();
        }

        /// <summary>演出躁动包络：入雨与深潜谁在演出谁说了算</summary>
        private static float CurrentAgitation => MathF.Max(
            OniRainWorldTransition.Active ? OniRainWorldTransition.UmbrellaAgitation : 0f,
            OniRainDescentTransition.Active ? OniRainDescentTransition.UmbrellaAgitation : 0f);

        //伞下漏雨与潮气 + 水洼呼出的上浮黑水滴；演出期间随躁动加密
        private void UpdateAmbience() {
            float agitation = CurrentAgitation;
            int interval = Math.Max(8, 42 - (int)((agitation + KickAmount) * 30f));

            dripTimer++;
            if (dripTimer >= interval) {
                dripTimer = 0;
                Vector2 pos = CanopyAnchor + new Vector2(Main.rand.NextFloat(-30f, 30f), 10f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    new Vector2(0f, Main.rand.NextFloat(1.5f, 2.5f)),
                    PaleTint * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(20, 34), 0f);

                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Position + new Vector2(Main.rand.NextFloat(-30f, 30f), -6f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.05f),
                        MistDamp * Main.rand.NextFloat(0.5f, 0.8f),
                        Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(80, 130));
                }
            }

            //水洼呼出的上浮黑水滴：倒着落的雨，低频常驻、躁动加密
            int riseInterval = Math.Max(5, 15 - (int)((agitation + KickAmount) * 9f));
            riseTimer++;
            if (riseTimer >= riseInterval) {
                riseTimer = 0;
                float x = Main.rand.NextFloat(-0.85f, 0.85f) * PuddleHalfWidth * PuddleSwell;
                Vector2 from = PuddleCenter + new Vector2(x, -1f);
                PRTLoader.NewParticle<PRT_OniPuddleRise>(from,
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.5f)),
                    Color.Lerp(PoolBlack, MistDamp, Main.rand.NextFloat(0.6f))
                        * Main.rand.NextFloat(0.65f, 0.95f),
                    Main.rand.NextFloat(0.55f, 1f))
                    ?.Configure(Main.rand.Next(46, 90));

                //偶发一粒被拽回伞底，洼里的水在回流入伞
                if (Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_GhostRainYank>(from,
                        new Vector2(0f, -Main.rand.NextFloat(1f, 1.8f)),
                        PaleTint * Main.rand.NextFloat(0.3f, 0.5f),
                        Main.rand.NextFloat(0.4f, 0.65f))
                        ?.Configure(CanopyThroat, Main.rand.Next(26, 40));
                }
            }
        }

        /// <summary>
        /// 鬼眼：平时眼睑垂着一线；本地玩家接近缓睁、瞳向追人；
        /// 触发瞬间全睁+红芒一闪；演出躁动期一直睁着；偶发眨眼保活性
        /// </summary>
        private void UpdateEye() {
            float openTarget = 0.06f;
            Player player = Main.LocalPlayer;
            if (player != null && player.Alives()) {
                float dist = player.Center.Distance(CanopyAnchor);
                float near = MathHelper.Clamp(1f - (dist - 90f) / 180f, 0f, 1f);
                openTarget = MathF.Max(openTarget, near * 0.72f);
                Vector2 look = (player.Center - CanopyAnchor).SafeNormalize(Vector2.UnitY);
                eyeLook = Vector2.Lerp(eyeLook, look, 0.12f).SafeNormalize(Vector2.UnitY);
            }
            openTarget = MathF.Max(openTarget, CurrentAgitation * 0.9f);

            if (--blinkTimer <= 0) {
                blinkTimer = Main.rand.Next(170, 320);
            }
            if (blinkTimer < 5) {
                openTarget = 0f;
            }
            eyeOpen = MathHelper.Lerp(eyeOpen, openTarget, 0.16f);
            eyeGlow *= 0.9f;
        }

        private void UpdateInteraction() {
            Player player = Main.LocalPlayer;
            //只管入雨（雨世界外撑伞）；雨中的下潜交给夺伞
            bool eligible = player != null && player.Alives()
                && !OniRainWorldState.LocalIn
                && !OniRainWorldTransition.Active
                && !OniRainDescentTransition.Active
                && !CutsceneDirector.IsPlaying;
            bool near = eligible && player.Center.Distance(CanopyAnchor) < InteractDistance;

            promptAlpha = MathHelper.Clamp(promptAlpha + (near && CanTrigger(player) ? 0.05f : -0.05f), 0f, 1f);

            if (near && promptAlpha > 0.5f && CanTrigger(player)
                && Main.mouseRight && Main.mouseRightRelease) {
                Trigger(player);
            }
        }

        private static bool CanTrigger(Player player)
            => !Main.mapFullscreen && !player.mouseInterface;

        private float KickAmount => triggerKick > 0 ? triggerKick / 26f : 0f;

        private void Trigger(Player player) {
            //触发确认帧：伞骨绷响 + 猛颤 + 鬼眼全睁 + 伞沿甩出一圈水珠
            triggerKick = 26;
            eyeOpen = 1f;
            eyeGlow = 1f;
            SoundEngine.PlaySound(SoundID.Dig with {
                Pitch = 0.3f,
                Volume = 0.55f,
                MaxInstances = 3,
            }, CanopyAnchor);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.2f,
                Volume = 0.5f,
                MaxInstances = 3,
            }, CanopyAnchor);

            for (int i = 0; i < 9; i++) {
                float angle = -MathHelper.Pi * (0.1f + 0.8f * i / 8f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.8f, 3.8f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    CanopyAnchor + new Vector2(Main.rand.NextFloat(-26f, 26f), 0f),
                    vel, PaleTint * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), vel.X);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Position + new Vector2(Main.rand.NextFloat(-20f, 20f), -8f),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -0.08f),
                    MistDamp * Main.rand.NextFloat(0.55f, 0.85f),
                    Main.rand.NextFloat(0.6f, 0.95f))
                    ?.Configure(Main.rand.Next(70, 110));
            }
            //洼里也惊起一片上浮滴
            for (int i = 0; i < 6; i++) {
                float x = Main.rand.NextFloat(-0.85f, 0.85f) * PuddleHalfWidth;
                PRTLoader.NewParticle<PRT_OniPuddleRise>(
                    PuddleCenter + new Vector2(x, -1f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.2f)),
                    Color.Lerp(PoolBlack, MistDamp, Main.rand.NextFloat(0.6f))
                        * Main.rand.NextFloat(0.7f, 1f),
                    Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(40, 70));
            }

            //运镜失败不致命，演出照走
            OniRainWorldTransition.Begin(player, Position);
            CutsceneDirector.Play<OniRainWorldCutscene>(player);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //已获伞的玩家看不见它，Actor 本体留给其他玩家
            if (!ShouldShowForLocalPlayer()) {
                return false;
            }

            DrawPuddle(spriteBatch);

            int itemType = ModContent.ItemType<KikasaItem>();
            Main.instance.LoadItem(itemType);
            Texture2D umbrella = TextureAssets.Item[itemType]?.Value;
            if (umbrella == null || umbrella.IsDisposed) {
                return false;
            }

            //演出期伞体颤抖：压镜段起颤、浮现段拉满，触发瞬间猛地一颤
            float agitation = CurrentAgitation;
            float shiver = (agitation * 0.05f + KickAmount * 0.09f)
                * MathF.Sin(Main.GlobalTimeWrappedHourly * 46f);
            float rotation = -0.13f + MathF.Sin(swayTimer) * 0.03f + shiver;

            //柄尾钩落在地表锚点
            Vector2 drawPos = Position - Main.screenPosition
                - new Vector2(0f, umbrella.Height * 0.5f * DrawScale - 4f);

            //冷灰青软辉衬底
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(swayTimer * 2.4f) * 0.5f + 0.5f;
            Color backing = new Color(96, 122, 128) with { A = 0 } * (0.16f + pulse * 0.08f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f,
                new Vector2(150f * DrawScale / glow.Width, 140f * DrawScale / glow.Height),
                SpriteEffects.None, 0f);

            //本体：环境光染向湿墨灰白，夜里保轮廓
            Color body = Lighting.GetColor((Position / 16f).ToPoint());
            body = Color.Lerp(body, PaleTint, 0.32f);
            DrawCanopy(spriteBatch, umbrella, drawPos, rotation, body);

            return false;
        }

        /// <summary>
        /// 伞体：KikasaUmbrella TechCanopy（湿光扫掠+伞骨水膜+鬼眼），
        /// 切 Immediate 批、异常回退裸贴图，收尾恢复 Actor 层 Deferred 批。
        /// 批次形制镜 KasaOniRenderer
        /// </summary>
        private void DrawCanopy(SpriteBatch sb, Texture2D tex, Vector2 drawPos,
            float rotation, Color light) {

            Effect fx = EffectLoader.KikasaUmbrella?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice device = Main.instance?.GraphicsDevice;
            Rectangle frame = tex.Frame();
            Vector2 origin = frame.Size() * 0.5f;
            if (fx == null || noise == null || noise.IsDisposed || device == null) {
                sb.Draw(tex, drawPos, frame, light, rotation, origin, DrawScale,
                    SpriteEffects.None, 0f);
                return;
            }

            Texture previousTexture1 = device.Textures[1];
            SamplerState previousSampler1 = device.SamplerStates[1];
            bool callerBatchEnded = false;
            bool canopyBatchOpen = false;
            bool actorBatchRestored = false;
            bool drawFallback = false;

            try {
                sb.End();
                callerBatchEnded = true;

                SetCanopyParams(fx, tex, frame);

                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx, Main.GameViewMatrix.TransformationMatrix);
                canopyBatchOpen = true;
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;

                sb.Draw(tex, drawPos, frame, light, rotation, origin, DrawScale,
                    SpriteEffects.None, 0f);

                sb.End();
                canopyBatchOpen = false;
            } catch (Exception exception) {
                drawFallback = true;
                LogCanopyFailure(exception);
            } finally {
                if (canopyBatchOpen) {
                    TryEnd(sb);
                }

                device.Textures[1] = previousTexture1;
                device.SamplerStates[1] = previousSampler1;

                if (callerBatchEnded) {
                    actorBatchRestored = TryBeginActorBatch(sb);
                }
            }

            if (drawFallback && actorBatchRestored) {
                sb.Draw(tex, drawPos, frame, light, rotation, origin, DrawScale,
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>TechCanopy 全参数上载（uniform 是设备全局状态，每个调用点必须全参数重设）</summary>
        private void SetCanopyParams(Effect fx, Texture2D tex, Rectangle frame) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uColInk"]?.SetValue(KikasaInk.InkBody.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            //立伞不自旋：扫光位置随摇摆缓慢游走，读作雨光在湿伞面上滑
            fx.Parameters["uSpinPhase"]?.SetValue(MathF.Sin(swayTimer) * 0.4f);
            fx.Parameters["uSpinSpeed"]?.SetValue(0f);
            fx.Parameters["uWet"]?.SetValue(1f);
            fx.Parameters["uSeed"]?.SetValue(WhoAmI * 0.173f % 4f);
            fx.Parameters["uEye"]?.SetValue(eyeOpen);
            fx.Parameters["uEyeLook"]?.SetValue(eyeLook);
            fx.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            fx.Parameters["uEyeCenter"]?.SetValue(EyeCenter);
            fx.Parameters["uEyeR"]?.SetValue(EyeRadius);
            //TechFill 的参数一并归零，防残值串场
            fx.Parameters["uFill"]?.SetValue(0f);
            fx.Parameters["uSlosh"]?.SetValue(0f);
            fx.CurrentTechnique = fx.Techniques["TechCanopy"];
        }

        /// <summary>
        /// 伞底黑水洼：真 alpha 深色水渍双层压出深度 + 尸斑青一线光沿，
        /// 缓慢呼吸、躁动/触发时涨大；倒影由屏幕空间镜像层叠加
        /// </summary>
        private void DrawPuddle(SpriteBatch sb) {
            Texture2D mask = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (mask == null) {
                return;
            }

            float swell = PuddleSwell;
            float breathe = 1f + MathF.Sin(swayTimer * 1.7f) * 0.05f;
            Vector2 pos = PuddleCenter - Main.screenPosition;
            float width = PuddleHalfWidth * 2f * swell * breathe;
            float height = PuddleHalfHeight * 2f * swell;
            Vector2 origin = mask.Size() * 0.5f;
            Vector2 scale = new(width / mask.Width, height / mask.Height);

            //黑水底双层：外层摊开、内层更沉
            sb.Draw(mask, pos, null, PoolBlack * 0.88f, 0f, origin, scale,
                SpriteEffects.None, 0f);
            sb.Draw(mask, pos + new Vector2(0f, 1f), null, PoolBlack * 0.6f, 0f, origin,
                scale * new Vector2(0.72f, 0.72f), SpriteEffects.None, 0f);
            //水面那一线尸斑青反光
            sb.Draw(mask, pos - new Vector2(0f, 2f), null,
                (PoolTeal with { A = 0 }) * 0.16f, 0f, origin,
                scale * new Vector2(0.86f, 0.34f), SpriteEffects.None, 0f);
        }

        private static bool TryBeginActorBatch(SpriteBatch spriteBatch) {
            try {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
                return true;
            } catch (Exception exception) {
                LogCanopyFailure(exception);
                return false;
            }
        }

        private static void TryEnd(SpriteBatch spriteBatch) {
            try {
                spriteBatch.End();
            } catch (Exception exception) {
                LogCanopyFailure(exception);
            }
        }

        private static void LogCanopyFailure(Exception exception) {
            if (canopyFailureLogged) {
                return;
            }
            canopyFailureLogged = true;
            CWRMod.Instance.Logger.Warn($"OniRainWorldUmbrella canopy fallback: {exception.Message}");
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            DrawInteractPrompt(spriteBatch);
        }

        /// <summary>交互提示，柔光衬底+描边文字，湿墨冷灰青</summary>
        private void DrawInteractPrompt(SpriteBatch sb) {
            if (promptAlpha <= 0.01f) {
                return;
            }

            Vector2 textPos = CanopyAnchor - Main.screenPosition + new Vector2(0f, -64f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hint = OniRainWorldSystem.InteractHint.Value;
            Vector2 textSize = font.MeasureString(hint) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            Vector2 backingScale = new((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(70, 92, 98) with { A = 0 } * (promptAlpha * (0.32f + pulse * 0.1f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            Color textColor = new Color(214, 228, 230) * promptAlpha;
            Utils.DrawBorderString(sb, hint, textPos - textSize / 2f, textColor, 0.9f);

            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0f, textSize.Y / 2f + 6f);
            Color lineColor = new Color(150, 176, 180) with { A = 0 } * (promptAlpha * 0.55f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f,
                new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 调试放伞：世界态归 <see cref="OniUmbrellaWorldSpawn"/> 权威维护，
        /// 直接放裸 Actor 会被自检纠回锚点，故委托给世界系统重建（仅单人）
        /// </summary>
        internal static void DebugPlaceAt(Vector2 world) {
            if (!VaultUtils.isSinglePlayer) {
                Main.NewText("仅单人可调试重建世界伞", Color.IndianRed);
                return;
            }
            OniUmbrellaWorldSpawn.DebugRebuildAt(world);
        }
    }
}
