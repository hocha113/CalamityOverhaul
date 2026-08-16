using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 湖畔村图：鬼伞主界面。一幅活的横卷——红天血湖畔的村落，
    /// 自左下掌中缩影放大铺开。画即状态：湖水位=涨水进度、湖底沉着记忆沉影
    /// （干湖=泥上的形，可驱使/未驯服/在外三态可辨）、恶犬随倒影/鬼梦改姿态、
    /// 村中窗火随湖藏渐次点亮、鬼雨形态岸上立着在场伞奴。
    /// 画内血湖/恶犬/伞奴排是热区：点湖开湖窗（干湖也开，只读），点犬出鬼梦题跋卡，
    /// 悬伞奴报在场数；非热区点击荡开一圈墨涟漪——不存在无反馈点击。
    /// </summary>
    internal class KikasaSceneUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaSceneUI Instance => UIHandleLoader.GetUIHandleOfType<KikasaSceneUI>();

        #region 本地化
        public static LocalizedText LakeTag { get; private set; }
        public static LocalizedText HoundTag { get; private set; }
        public static LocalizedText MemoryEmpty { get; private set; }
        public static LocalizedText ServantOutTag { get; private set; }
        public static LocalizedText DryHintFormat { get; private set; }
        public static LocalizedText ReflectAwake { get; private set; }
        public static LocalizedText ReflectAsleep { get; private set; }
        public static LocalizedText DreamTitle { get; private set; }
        public static LocalizedText InDreamLine { get; private set; }
        public static LocalizedText HoundCountFormat { get; private set; }
        public static LocalizedText DreamHint { get; private set; }
        public static LocalizedText ThrallTag { get; private set; }
        public static LocalizedText ThrallCountFormat { get; private set; }

        public override void SetStaticDefaults() {
            LakeTag = this.GetLocalization(nameof(LakeTag), () => "血湖");
            HoundTag = this.GetLocalization(nameof(HoundTag), () => "恶犬");
            MemoryEmpty = this.GetLocalization(nameof(MemoryEmpty), () => "湖底还空着");
            ServantOutTag = this.GetLocalization(nameof(ServantOutTag), () => "它替你出手去了");
            DryHintFormat = this.GetLocalization(nameof(DryHintFormat), () => "按 {0} 撑开血湖");
            ReflectAwake = this.GetLocalization(nameof(ReflectAwake), () => "倒影醒着");
            ReflectAsleep = this.GetLocalization(nameof(ReflectAsleep), () => "倒影睡着");
            DreamTitle = this.GetLocalization(nameof(DreamTitle), () => "鬼 梦");
            InDreamLine = this.GetLocalization(nameof(InDreamLine), () => "你正身在梦中");
            HoundCountFormat = this.GetLocalization(nameof(HoundCountFormat), () => "在场恶犬 {0} / {1}");
            DreamHint = this.GetLocalization(nameof(DreamHint), () => "梦中按住左键，黑水会不断吐出恶犬");
            ThrallTag = this.GetLocalization(nameof(ThrallTag), () => "伞奴");
            ThrallCountFormat = this.GetLocalization(nameof(ThrallCountFormat), () => "在场伞奴 {0} / {1}");
        }
        #endregion

        public override bool Active => IsOpen || OpenProgress > 0.01f;

        public override bool CloseOnEscape => true;

        public override SoundStyle? OpenSound
            => SoundID.Grass with { Volume = 0.55f, Pitch = -0.55f };

        public override SoundStyle? CloseSound
            => SoundID.Grass with { Volume = 0.45f, Pitch = -0.8f };

        //==================== 状态 ====================

        private enum Hotspot { None, Lake, Hound, Thrall }

        private Rectangle canvasRect;
        private Hotspot hover = Hotspot.None;
        private float lakeHoverLerp;
        private float houndHoverLerp;
        private float thrallHoverLerp;
        private bool dreamCardOpen;
        private float dreamCardLerp;
        //本帧在场伞奴数（鬼雨形态的岸位读数），Update 里点一次两处共用
        private int thrallCount;

        //画内水语包络
        private float stir;
        private float memoryPulse;
        private float lightPulse;
        private float lightGateSmooth;
        //事件边沿缓存
        private int lastMemoryType;
        private int lastVaultCount;

        //墨涟漪（非热区点击回应，暗色普通批）与水涟漪（事件，加色批）
        private struct Ripple
        {
            public Vector2 Pos;
            public int Timer;
            public bool Ink;
        }

        private const int RippleLife = 26;
        private readonly List<Ripple> ripples = [];

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaServantPlayer Servant => player.GetModPlayer<KikasaServantPlayer>();
        private KikasaDreamPlayer Dream => player.GetModPlayer<KikasaDreamPlayer>();

        /// <summary>翻转期把镜面预览混进浸染，色先于形态半步（缩影与大画共用）</summary>
        internal static float EffectiveRain(KikasaDomainPlayer domain) {
            float rain = domain.RainBlend;
            if (domain.Phase == KikasaDomainPhase.Flipping) {
                rain = MathHelper.Lerp(rain, domain.FlipToRain ? 1f : 0f, domain.FlipMix * 0.65f);
            }
            return MathHelper.Clamp(rain, 0f, 1f);
        }

        protected override void OnOpen() {
            Main.playerInventory = false;
            hover = Hotspot.None;
            lakeHoverLerp = houndHoverLerp = thrallHoverLerp = 0f;
            dreamCardOpen = false;
            dreamCardLerp = 0f;
            ripples.Clear();
            stir = 0.5f;
            //边沿基线取当前值，开画瞬间不虚报事件
            lastMemoryType = Servant.LastDrownedType;
            lastVaultCount = Vault.Stored.Count;
        }

        //==================== 更新 ====================

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            if (IsOpen && (player.dead || !player.active || HackTime.Active)) {
                Close();
            }

            //画心自风铃铃身放大铺开：空间连续
            Rectangle full = KikasaSceneTheme.CanvasRect();
            Rectangle mini = KikasaHud.BellRect;
            float ease = 1f - MathF.Pow(1f - a, 3f);
            canvasRect = new Rectangle(
                (int)MathHelper.Lerp(mini.X, full.X, ease),
                (int)MathHelper.Lerp(mini.Y, full.Y, ease),
                (int)MathHelper.Lerp(mini.Width, full.Width, ease),
                (int)MathHelper.Lerp(mini.Height, full.Height, ease));

            //====== 事件进画：一切反馈说水的语言 ======
            int memoryType = Servant.LastDrownedType;
            int vaultCount = Vault.Stored.Count;
            if (memoryType != lastMemoryType && memoryType > 0) {
                memoryPulse = 1f;
                stir = MathF.Max(stir, 0.7f);
                AddRipple(KikasaSceneTheme.UvToScreen(canvasRect, KikasaSceneTheme.MemoryUv), false);
            }
            if (vaultCount != lastVaultCount) {
                //湖藏变动：窗火一颤，湖面一圈
                lightPulse = 1f;
                stir = MathF.Max(stir, 0.4f);
                AddRipple(KikasaSceneTheme.UvToScreen(canvasRect,
                    new Vector2(0.5f, KikasaSceneTheme.WaterFullY + 0.04f)), false);
            }
            lastMemoryType = memoryType;
            lastVaultCount = vaultCount;

            lightGateSmooth = MathHelper.Lerp(lightGateSmooth,
                vaultCount / (float)KikasaVaultPlayer.Capacity, 0.08f);
            float restStir = Domain.Phase == KikasaDomainPhase.Opening
                || Domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.12f;
            if (hover == Hotspot.Lake) {
                restStir = MathF.Max(restStir, 0.32f);
            }
            stir = MathHelper.Lerp(stir, restStir, 0.06f);
            memoryPulse *= 0.94f;
            lightPulse *= 0.93f;

            //====== 悬停与输入 ======
            Vector2 mouse = KikasaHudTheme.UIMouse;
            bool overCanvas = canvasRect.Contains(mouse.ToPoint());
            bool inputAvailable = IsOpen && a > 0.9f;

            //岸位读数：鬼雨形态下在场的伞奴
            thrallCount = KikasaThrall.CountActive(player.whoAmI);

            hover = Hotspot.None;
            if (inputAvailable && overCanvas) {
                Rectangle houndRect = KikasaSceneTheme.UvToScreen(canvasRect, KikasaSceneTheme.HoundHotspot);
                Rectangle thrallRect = KikasaSceneTheme.UvToScreen(canvasRect, KikasaSceneTheme.ThrallHotspot);
                Rectangle lakeRect = KikasaSceneTheme.UvToScreen(canvasRect, KikasaSceneTheme.LakeHotspot);
                bool thrallRowLive = Domain.IsRainForm && thrallCount > 0;
                if (houndRect.Contains(mouse.ToPoint())) {
                    hover = Hotspot.Hound;
                }
                else if (thrallRowLive && thrallRect.Contains(mouse.ToPoint())) {
                    hover = Hotspot.Thrall;
                }
                else if (lakeRect.Contains(mouse.ToPoint())) {
                    hover = Hotspot.Lake;
                }
            }
            lakeHoverLerp = MathHelper.Lerp(lakeHoverLerp, hover == Hotspot.Lake ? 1f : 0f, 0.15f);
            houndHoverLerp = MathHelper.Lerp(houndHoverLerp, hover == Hotspot.Hound ? 1f : 0f, 0.15f);
            thrallHoverLerp = MathHelper.Lerp(thrallHoverLerp, hover == Hotspot.Thrall ? 1f : 0f, 0.15f);

            if (IsOpen && overCanvas) {
                player.mouseInterface = true;
                //滚轮双锁：换武器与配方栏滚动都按住，挂在悬停判定上
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/KikasaScene");
            }

            //====== 点击分发：点哪都有回应 ======
            if (inputAvailable && keyLeftPressState == KeyPressState.Pressed) {
                if (hover == Hotspot.Hound) {
                    dreamCardOpen = !dreamCardOpen;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.3f });
                }
                else if (hover == Hotspot.Lake) {
                    //干湖也开：湖窗只读模式自己接手
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.4f });
                    Close();
                    KikasaVaultUI.Instance?.OpenFromScene();
                }
                else if (overCanvas) {
                    //非热区：墨涟漪答话
                    AddRipple(mouse, true);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = -0.7f, MaxInstances = 2 });
                }
                else {
                    Close();
                }
            }

            dreamCardLerp = MathHelper.Lerp(dreamCardLerp, dreamCardOpen && IsOpen ? 1f : 0f, 0.16f);

            for (int i = ripples.Count - 1; i >= 0; i--) {
                Ripple r = ripples[i];
                if (++r.Timer >= RippleLife) {
                    ripples.RemoveAt(i);
                }
                else {
                    ripples[i] = r;
                }
            }
        }

        private void AddRipple(Vector2 pos, bool ink) {
            if (ripples.Count < 10) {
                ripples.Add(new Ripple { Pos = pos, Ink = ink });
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            KikasaDomainPlayer domain = Domain;
            float time = Main.GlobalTimeWrappedHourly;
            float rain = EffectiveRain(domain);
            float rise = domain.AnyActive ? domain.RiseProgress : 0f;
            float waterUv = KikasaSceneTheme.WaterUv(rise);
            Rectangle canvas = canvasRect;

            //幕后压暗：画铺开多少，幕落多少
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, (int)KikasaHudTheme.UIScreenW + 2, (int)KikasaHudTheme.UIScreenH + 2),
                Color.Black * (0.42f * a));

            //1 画心（着色器场景 / CPU 回退）
            float effStir = MathHelper.Clamp(stir + (1f - a) * 0.4f, 0f, 1f);
            float lightGate = MathHelper.Clamp(lightGateSmooth + lightPulse * 0.25f, 0f, 1f);
            DrawVista(spriteBatch, canvas, a, rain, waterUv, 1f - rise, effStir,
                domain.FlipBoil, domain.FlipFlash, lightGate);

            //2 装裱：左右卷杆
            KikasaSceneRenderer.DrawRollers(spriteBatch, canvas,
                KikasaHudTheme.Void(rain) * 0.95f, KikasaHudTheme.Accent(rain), a);

            //铺开到能看清内容才落笔画细节
            float detailA = MathHelper.Clamp((a - 0.55f) / 0.45f, 0f, 1f);
            if (detailA > 0.02f) {
                DrawHound(spriteBatch, canvas, detailA, rain, waterUv, time);
                DrawThralls(spriteBatch, canvas, detailA, rain, time);
                DrawMemory(spriteBatch, canvas, detailA, rain, waterUv);
                DrawAdditiveBits(spriteBatch, canvas, detailA, rain, waterUv, time);
                DrawInkRipples(spriteBatch, detailA, rain);
                DrawTextLayer(spriteBatch, canvas, detailA, rain, time);
                DrawDreamCard(spriteBatch, canvas, detailA, rain, time);
            }
        }

        //====== 画心 ======

        private void DrawVista(SpriteBatch sb, Rectangle rect, float a, float rain,
            float waterUv, float dry, float effStir, float boil, float flash,
            float lightGate) {
            Effect effect = EffectLoader.KikasaScene?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || effect.Techniques["TechVista"] == null) {
                DrawVistaCPU(sb, rect, a, rain, waterUv);
                return;
            }
            effect.CurrentTechnique = effect.Techniques["TechVista"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(a);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uWaterY"]?.SetValue(waterUv);
            effect.Parameters["uDry"]?.SetValue(MathHelper.Clamp(dry, 0f, 1f));
            effect.Parameters["uRain"]?.SetValue(rain);
            effect.Parameters["uStir"]?.SetValue(effStir);
            effect.Parameters["uBoil"]?.SetValue(boil);
            effect.Parameters["uFlash"]?.SetValue(flash);
            effect.Parameters["uLightGate"]?.SetValue(lightGate);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(VaultAsset.placeholder2.Value, rect, Color.White);
            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        //CPU 回退：三段平涂（天/床/水）+ 岸线一划

        private static void DrawVistaCPU(SpriteBatch sb, Rectangle rect, float a,
            float rain, float waterUv) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int shoreY = rect.Y + (int)(rect.Height * KikasaSceneTheme.ShoreY);
            int waterY = rect.Y + (int)(rect.Height * MathHelper.Clamp(waterUv, 0f, 1f));
            Rectangle sky = new(rect.X, rect.Y, rect.Width, shoreY - rect.Y);
            Rectangle bedR = new(rect.X, shoreY, rect.Width, rect.Height - (shoreY - rect.Y));
            sb.Draw(px, sky, KikasaHudTheme.Mid(rain) * (0.9f * a));
            sb.Draw(px, bedR, KikasaHudTheme.Void(rain) * (0.92f * a));
            if (waterY < rect.Bottom - 2) {
                Rectangle water = new(rect.X, Math.Max(waterY, shoreY), rect.Width,
                    rect.Bottom - Math.Max(waterY, shoreY));
                sb.Draw(px, water, KikasaHudTheme.Deep(rain) * (0.85f * a));
                KikasaVaultRenderer.DrawLine(sb, new Vector2(rect.X + 3, water.Y),
                    new Vector2(rect.Right - 3, water.Y), 1.4f, KikasaHudTheme.Glow(rain) * (0.5f * a));
            }
            KikasaVaultRenderer.DrawLine(sb, new Vector2(rect.X, shoreY),
                new Vector2(rect.Right, shoreY), 1f, KikasaHudTheme.Accent(rain) * (0.4f * a));
        }

        //====== 画中恶犬（贴图实绘） ======

        private void DrawHound(SpriteBatch sb, Rectangle canvas, float a, float rain,
            float waterUv, float time) {
            //姿态渐变；悬停也会让它睁眼看你
            (float idleA, float alertA, float howlA) = HoundPose();
            Vector2 pos = KikasaSceneTheme.UvToScreen(canvas, KikasaSceneTheme.HoundUv);
            float waterPixY = canvas.Y + waterUv * canvas.Height;
            //倒影只在水位接近满时可见，免得镜像探出画底
            float reflGate = MathHelper.Clamp((0.72f - waterUv) / 0.05f, 0f, 1f);
            KikasaSceneRenderer.DrawInkHound(sb, pos,
                canvas.Height * KikasaSceneTheme.HoundHeight,
                idleA, alertA, howlA, houndHoverLerp, rain,
                MathHelper.Clamp(stir, 0f, 1f), Domain.FlipBoil,
                waterPixY, reflGate, a, time);
        }

        /// <summary>犬姿态权重：鬼梦立嚎 > 倒影醒/被注视昂首 > 垂首打盹</summary>
        private (float idle, float alert, float howl) HoundPose() {
            KikasaDomainPlayer domain = Domain;
            float howl = domain.Phase == KikasaDomainPhase.Dreaming ? 1f : 0f;
            float alert = MathF.Max(domain.HoundReflection ? 1f : 0f, houndHoverLerp) * (1f - howl);
            float idle = 1f - MathF.Max(alert, howl);
            return (idle, alert, howl);
        }

        //====== 岸上伞奴（鬼雨形态的岸位读数） ======

        private void DrawThralls(SpriteBatch sb, Rectangle canvas, float a, float rain, float time) {
            //鬼雨浸染过半它们才立起来——血湖侧没有伞奴
            float gate = MathHelper.Clamp((rain - 0.55f) / 0.30f, 0f, 1f);
            if (gate <= 0.02f || thrallCount <= 0) {
                return;
            }
            Texture2D tex = KikasaThrallRenderer.BodyTexture;
            if (tex == null) {
                return;
            }
            Rectangle frame = KikasaThrallRenderer.FrameOf(tex, 0);
            int count = Math.Min(thrallCount, KikasaThrall.MaxPerOwner);
            float stir01 = MathHelper.Clamp(stir, 0f, 1f);
            for (int i = 0; i < count; i++) {
                //站位手排：脚跟、身量与朝向逐个错开，不站成仪仗队
                float h0 = MathF.Sin(i * 17.39f) * 0.5f + 0.5f;
                float h1 = MathF.Sin(i * 9.71f + 4.2f) * 0.5f + 0.5f;
                Vector2 uv = new(
                    KikasaSceneTheme.ThrallRowUv.X + i * KikasaSceneTheme.ThrallSpacingX
                        + (h0 - 0.5f) * 0.012f,
                    KikasaSceneTheme.ThrallRowUv.Y + (h1 - 0.5f) * 0.012f);
                Vector2 pos = KikasaSceneTheme.UvToScreen(canvas, uv)
                    + new Vector2(0f, MathF.Sin(time * 0.9f + i * 2.1f) * 1.2f);
                float fit = canvas.Height * KikasaSceneTheme.ThrallHeight * (0.92f + h0 * 0.16f);
                //尸斑青幽光衬底（under-layer）：把湿墨小影从雾里托出来，与世界侧行走衬光同源
                SvgPathPen.SoftDot(sb, pos, fit * 0.62f, KikasaThrall.CorpseTeal,
                    (0.08f + thrallHoverLerp * 0.05f) * gate * a);
                KikasaVaultRenderer.DrawSunkEffigy(sb, tex, frame, pos, fit, a * gate,
                    submerge: 0f, depth: 0f, tamed: true, absent: false,
                    rain, stir01, 3.17f + i * 1.73f, KikasaHudTheme.Accent(rain),
                    h1 > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            }
        }

        //====== 湖底记忆 ======

        private void DrawMemory(SpriteBatch sb, Rectangle canvas, float a, float rain, float waterUv) {
            int memoryType = Servant.LastDrownedType;
            if (memoryType <= 0) {
                return;
            }
            //鬼奴在外=负形空位，不再整个消失
            bool absent = Servant.FindActiveServant() != null;
            bool tamed = KikasaServantIndex.TryGet(memoryType, out _);
            //水漫过湖床多少，它就化进水里多少；干湖时留成泥上的形，不再有看不见的档
            float submerge = MathHelper.Clamp(
                (KikasaSceneTheme.MemoryUv.Y - waterUv) * canvas.Height / 26f, 0f, 1f);
            //满水位时它沉在最深处
            float depth = MathHelper.Clamp((KikasaSceneTheme.MemoryUv.Y - waterUv)
                / (KikasaSceneTheme.MemoryUv.Y - KikasaSceneTheme.WaterFullY), 0f, 1f);
            //泥上不漂，入水才随水呼吸
            Vector2 pos = KikasaSceneTheme.UvToScreen(canvas, KikasaSceneTheme.MemoryUv)
                + new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f) * 2.2f * submerge);
            float fit = canvas.Height * 0.115f;
            //湖床落影：沉影的座，在外时读作空位的口
            KikasaVaultRenderer.DrawRing(sb, pos + new Vector2(0f, fit * 0.46f),
                fit * 0.4f, fit * 0.11f, KikasaHudTheme.Void(rain) * (0.5f * a));
            //悬停与记忆更替的涌浪都灌进水面活性，沉影跟着更躁
            float effStir = MathHelper.Clamp(stir + lakeHoverLerp * 0.4f + memoryPulse, 0f, 1f);
            KikasaVaultRenderer.DrawSunkEffigy(sb, memoryType, pos, fit, a,
                submerge, depth, tamed, absent, rain, effStir, KikasaHudTheme.Accent(rain));
        }

        //====== 加色小件 ======

        private void DrawAdditiveBits(SpriteBatch sb, Rectangle canvas, float a,
            float rain, float waterUv, float time) {
            //村中窗火与檐灯已由 villageRow 程序化承担（uLightGate），
            //恶犬烬目由 KikasaHound.fx 内建，这里只剩水语亮件
            Color glow = KikasaHudTheme.Glow(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            float waterPixY = canvas.Y + waterUv * canvas.Height;
            bool hasWater = waterUv < 0.94f;

            KikasaVaultRenderer.BeginAdditive(sb);

            if (hasWater) {
                //水面泡沫线 + 两点游光
                float lineHalf = canvas.Width * 0.46f;
                KikasaVaultRenderer.DrawLine(sb,
                    new Vector2(canvas.Center.X - lineHalf, waterPixY),
                    new Vector2(canvas.Center.X + lineHalf, waterPixY), 1f,
                    glow * ((0.12f + stir * 0.16f) * a));
                for (int k = 0; k < 2; k++) {
                    float drift = (time * (0.05f + k * 0.02f) + k * 0.5f) % 1f;
                    float gx = MathHelper.Lerp(canvas.X + 30f, canvas.Right - 30f,
                        k == 0 ? drift : 1f - drift);
                    KikasaVaultRenderer.DrawGlowDot(sb, new Vector2(gx, waterPixY), 4.5f,
                        glow * (0.12f * KikasaSceneTheme.Breath(time, k * 3.7f, 2.4f) * a));
                }
                //悬停湖面：光标下荡一圈慢涟漪
                if (lakeHoverLerp > 0.05f) {
                    Vector2 mouse = KikasaHudTheme.UIMouse;
                    float ringP = (time * 0.7f) % 1f;
                    KikasaVaultRenderer.DrawRing(sb,
                        new Vector2(MathHelper.Clamp(mouse.X, canvas.X + 24f, canvas.Right - 24f),
                            MathF.Max(mouse.Y, waterPixY + 4f)),
                        6f + ringP * 18f, (6f + ringP * 18f) * 0.36f,
                        glow * (0.25f * (1f - ringP) * lakeHoverLerp * a));
                }
            }

            //鬼奴外出：记忆位留一个慢旋涡
            int memoryType = Servant.LastDrownedType;
            if (memoryType > 0 && Servant.FindActiveServant() != null && hasWater) {
                Vector2 vc = KikasaSceneTheme.UvToScreen(canvas, KikasaSceneTheme.MemoryUv);
                for (int ring = 0; ring < 2; ring++) {
                    float rp = (time * 0.35f + ring * 0.5f) % 1f;
                    float r = MathHelper.Lerp(15f, 4f, rp);
                    KikasaVaultRenderer.DrawRing(sb, vc, r, r * 0.4f,
                        glow * (0.22f * (1f - rp) * a));
                }
            }

            //水涟漪（事件反馈）
            foreach (Ripple r in ripples) {
                if (r.Ink) {
                    continue;
                }
                float t = r.Timer / (float)RippleLife;
                KikasaVaultRenderer.DrawRing(sb, r.Pos, 4f + t * 22f, (4f + t * 22f) * 0.38f,
                    glow * (0.35f * (1f - t) * a));
            }

            //热区悬停时热区名牌下的微光衬底
            if (houndHoverLerp > 0.05f) {
                KikasaVaultRenderer.DrawGlowDot(sb,
                    KikasaSceneTheme.UvToScreen(canvas, KikasaSceneTheme.HoundUv),
                    canvas.Height * 0.055f, accent * (0.10f * houndHoverLerp * a));
            }

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        //墨涟漪：非热区点击的答话，暗色真阿尔法，普通批

        private void DrawInkRipples(SpriteBatch sb, float a, float rain) {
            foreach (Ripple r in ripples) {
                if (!r.Ink) {
                    continue;
                }
                float t = r.Timer / (float)RippleLife;
                float radius = 3f + t * 20f;
                KikasaVaultRenderer.DrawRing(sb, r.Pos, radius, radius * 0.5f,
                    KikasaHudTheme.Void(rain) * (0.55f * (1f - t) * a));
            }
        }

        //====== 文字层 ======

        private void DrawTextLayer(SpriteBatch sb, Rectangle canvas, float a, float rain, float time) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Color text = KikasaHudTheme.Text(rain);
            Color dim = KikasaHudTheme.TextDim(rain);

            //题签：右上一枚伞章，画上不落可读汉字——章随画铺开描完自己，与湖窗同一支笔
            KikasaVaultRenderer.DrawSeal(sb,
                new Vector2(canvas.Right - 26f, canvas.Y + 34f), 14f,
                0.9f * a, time, reveal: a,
                dim, KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain));

            //干湖提示：湖床中央一行水语
            KikasaDomainPlayer domain = Domain;
            if (!domain.AnyActive && !KikasaHudLead.CardVisible) {
                string hint = string.Format(DryHintFormat.Value,
                    CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
                Vector2 size = font.MeasureString(hint) * 0.8f;
                float breathe = 0.55f + 0.25f * KikasaSceneTheme.Breath(time, 0.9f, 1.5f);
                Utils.DrawBorderString(sb, hint,
                    KikasaSceneTheme.UvToScreen(canvas, new Vector2(0.5f, 0.80f)) - size * 0.5f,
                    dim * (breathe * a), 0.8f);
            }

            //悬停名牌：贴着光标，题头 + 细行
            if (hover == Hotspot.None) {
                return;
            }
            List<(string line, Color col, float scale)> lines = [];
            if (hover == Hotspot.Lake) {
                lines.Add((LakeTag.Value, text, 0.82f));
                lines.Add((string.Format(KikasaVaultUI.CountFormat.Value,
                    Vault.Stored.Count, KikasaVaultPlayer.Capacity), dim, 0.7f));
                int memoryType = Servant.LastDrownedType;
                if (memoryType <= 0) {
                    lines.Add((MemoryEmpty.Value, dim, 0.7f));
                }
                else {
                    bool outSide = Servant.FindActiveServant() != null;
                    string memLine = Lang.GetNPCNameValue(memoryType);
                    if (outSide) {
                        memLine += " · " + ServantOutTag.Value;
                    }
                    else if (!KikasaServantIndex.TryGet(memoryType, out _)) {
                        memLine += " · " + KikasaServantPlayer.ServantUnknown.Value;
                    }
                    lines.Add((memLine, dim, 0.7f));
                }
            }
            else if (hover == Hotspot.Thrall) {
                lines.Add((ThrallTag.Value, text, 0.82f));
                lines.Add((string.Format(ThrallCountFormat.Value,
                    thrallCount, KikasaThrall.MaxPerOwner), dim, 0.7f));
            }
            else {
                lines.Add((HoundTag.Value, text, 0.82f));
                lines.Add((Domain.HoundReflection ? ReflectAwake.Value : ReflectAsleep.Value,
                    dim, 0.7f));
            }

            Vector2 mouse = KikasaHudTheme.UIMouse;
            float y = mouse.Y + 20f;
            foreach ((string line, Color col, float scale) in lines) {
                Vector2 size = font.MeasureString(line) * scale;
                float x = MathHelper.Clamp(mouse.X + 16f, canvas.X + 8f,
                    MathF.Max(canvas.X + 8f, canvas.Right - 8f - size.X));
                Utils.DrawBorderString(sb, line, new Vector2(x, y), col * a, scale);
                y += size.Y + 2f;
            }
        }

        //====== 鬼梦题跋卡 ======

        private void DrawDreamCard(SpriteBatch sb, Rectangle canvas, float a, float rain, float time) {
            float ca = dreamCardLerp * a;
            if (ca < 0.02f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            KikasaDomainPlayer domain = Domain;
            KikasaDreamPlayer dream = Dream;
            bool awake = domain.HoundReflection;
            bool dreaming = domain.Phase == KikasaDomainPhase.Dreaming;

            //在场犬数：场上属于自己的梦犬
            int hounds = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == player.whoAmI
                    && proj.ModProjectile is KikasaDreamHound) {
                    hounds++;
                }
            }

            const int cardW = 236;
            float lineT = font.MeasureString("A").Y;
            List<(string line, Color col, float scale)> lines = [];
            lines.Add((awake ? ReflectAwake.Value : ReflectAsleep.Value,
                awake ? new Color(235, 150, 90) : KikasaHudTheme.TextDim(rain), 0.72f));
            if (dreaming) {
                lines.Add((InDreamLine.Value, KikasaHudTheme.Glow(rain), 0.72f));
                lines.Add((string.Format(HoundCountFormat.Value, hounds, KikasaDreamPlayer.MaxHounds),
                    KikasaHudTheme.TextDim(rain), 0.72f));
            }
            lines.Add((DreamHint.Value, KikasaHudTheme.TextDim(rain) * 0.85f, 0.62f));

            float cardH = 14f + lineT * 0.84f + 8f;
            foreach ((string line, _, float scale) in lines) {
                cardH += lineT * scale + 4f;
            }
            //梦中给冷却条留一行
            if (dreaming) {
                cardH += 12f;
            }
            cardH += 10f;

            Rectangle card = new(canvas.Right - cardW - 22, canvas.Y + 18, cardW, (int)cardH);
            float slide = (1f - dreamCardLerp) * 12f;
            card.Y += (int)slide;

            DrawCardBg(sb, card, ca, rain);

            float px = card.X + 14f;
            float py = card.Y + 12f;
            Utils.DrawBorderString(sb, DreamTitle.Value, new Vector2(px, py),
                KikasaHudTheme.Glow(rain) * ca, 0.84f);
            py += lineT * 0.84f + 4f;
            KikasaVaultRenderer.DrawLine(sb, new Vector2(px, py),
                new Vector2(card.Right - 14f, py), 1f, KikasaHudTheme.Accent(rain) * (0.4f * ca));
            py += 6f;
            foreach ((string line, Color col, float scale) in lines) {
                Utils.DrawBorderString(sb, line, new Vector2(px, py), col * ca, scale);
                py += lineT * scale + 4f;
            }
            if (dreaming) {
                //唤犬冷却条：满=刚唤出，退尽=可再唤
                float cd = dream.HoundCooldown01;
                float barW = card.Width - 28f;
                KikasaVaultRenderer.DrawLine(sb, new Vector2(px, py + 4f),
                    new Vector2(px + barW, py + 4f), 2f, KikasaHudTheme.TextDim(rain) * (0.18f * ca));
                if (cd > 0.01f) {
                    KikasaVaultRenderer.DrawLine(sb, new Vector2(px, py + 4f),
                        new Vector2(px + barW * cd, py + 4f), 2f,
                        new Color(230, 96, 40) * (0.7f * ca));
                }
            }
        }

        /// <summary>湿纸卡底（TechCard）；缺编回退平底 + 边线。引导卡也走这个入口</summary>
        internal static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha, float rain) {
            Effect effect = EffectLoader.KikasaScene?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null && effect.Techniques["TechCard"] != null) {
                Rectangle ext = card;
                ext.Inflate(8, 8);
                effect.CurrentTechnique = effect.Techniques["TechCard"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uTear"]?.SetValue(alpha);
                effect.Parameters["uRain"]?.SetValue(rain);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
            else {
                sb.Draw(VaultAsset.placeholder2.Value, card,
                    KikasaHudTheme.Void(rain) * (0.9f * alpha));
                Color edgeC = KikasaHudTheme.Accent(rain) * (0.5f * alpha);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Right, card.Top), 1f, edgeC);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Bottom),
                    new Vector2(card.Right, card.Bottom), 1f, edgeC * 0.7f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Left, card.Bottom), 1f, edgeC * 0.85f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Right, card.Top),
                    new Vector2(card.Right, card.Bottom), 1f, edgeC * 0.85f);
            }
        }
    }
}
