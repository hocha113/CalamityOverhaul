using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 伞下水鏡：鬼伞常驻 HUD（左下，持伞或领域激活时浮现）。
    /// 鏡体是血湖领域的微缩倒影——水位即涨水进度，色板随 RainBlend 血湖⇄鬼雨浸染，
    /// 翻转期沸腾/倾荡/白闪与世界同拍；湖底沉睡着湖的记忆（最后被沉溺的生物），
    /// 鬼奴外出时鏡底空着、只留一个旋涡；伞拱缘的冷却弧是沉溺之手的余悸；
    /// 鏡底沉积线记着湖藏几件。一切状态转换说水的语言：涨退水、涟漪、气泡，不做 UI 式淡滑。
    /// </summary>
    internal class KikasaHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaHud Instance => UIHandleLoader.GetUIHandleOfType<KikasaHud>();

        public static LocalizedText MemoryEmpty { get; private set; }
        public static LocalizedText ServantOutTag { get; private set; }
        public static LocalizedText DomainHintFormat { get; private set; }

        public override void SetStaticDefaults() {
            MemoryEmpty = this.GetLocalization(nameof(MemoryEmpty), () => "湖底还空着");
            ServantOutTag = this.GetLocalization(nameof(ServantOutTag), () => "它替你出手去了");
            DomainHintFormat = this.GetLocalization(nameof(DomainHintFormat), () => "按 {0} 撑开血湖");
        }

        //==================== 可见性 ====================

        private float appear;

        private static bool HoldingUmbrella(Player p) {
            Item item = p.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        private static bool WantVisible(Player p)
            => HoldingUmbrella(p) || p.GetModPlayer<KikasaDomainPlayer>().AnyActive;

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead || Main.dedServ) {
                    return false;
                }
                return WantVisible(p) || appear > 0.01f;
            }
        }

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上覆伞章、下覆沉物计数与提示行
        float IBottomLeftHud.HudStackTopExtent => 94f;
        float IBottomLeftHud.HudStackBottomExtent => 86f;
        #endregion

        /// <summary>自然锚点（鏡心），未参与左下队列避让时的原始位置</summary>
        public static Vector2 NaturalAnchor => new(KikasaHudTheme.AnchorOffset.X,
            KikasaHudTheme.UIScreenH + KikasaHudTheme.AnchorOffset.Y);

        /// <summary>鏡心锚点，经左下队列避让后的最终位；绘制/命中统一用本属性</summary>
        public static Vector2 Anchor {
            get {
                KikasaHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        //==================== 状态 ====================

        //事件边沿检测的上一帧缓存
        private int lastMemoryType;
        private bool lastServantOut;
        private int lastVaultCount;
        private float lastDrownCd;
        private bool lastLakeReady;

        //水语反馈包络
        private float stir;          //鏡水活性，事件时更躁
        private float memoryPulse;   //新记忆入湖：剪影短暂凝向真身
        private float sedimentPulse; //湖藏变动：沉积线一亮
        private float readyFlash;    //沉溺手冷却结束：拱缘泛光
        private float vortexSpin;    //鬼奴外出旋涡相位
        private float servantOutLerp;//旋涡出没的平滑
        private float reflectPulse;  //倒影醒/睡的切换脉冲
        private bool lastReflectOn;
        private bool lastDreaming;

        private bool hoverMirror;
        private int frame;

        //鏡内气泡（事件反馈，升到水线即破）
        private struct Mote
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Phase;
            public float Size;
            public int Life;
            public int MaxLife;
        }

        private const int MoteCap = 24;
        private readonly List<Mote> motes = [];

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaServantPlayer Servant => player.GetModPlayer<KikasaServantPlayer>();
        private KikasaDreams.KikasaDreamPlayer Dream => player.GetModPlayer<KikasaDreams.KikasaDreamPlayer>();

        /// <summary>翻转期把镜面预览混进浸染，色先于形态半步——与领域镜面同拍</summary>
        private static float EffectiveRain(KikasaDomainPlayer domain) {
            float rain = domain.RainBlend;
            if (domain.Phase == KikasaDomainPhase.Flipping) {
                rain = MathHelper.Lerp(rain, domain.FlipToRain ? 1f : 0f, domain.FlipMix * 0.65f);
            }
            return MathHelper.Clamp(rain, 0f, 1f);
        }

        /// <summary>当前水面在鏡内的 uv.y</summary>
        private static float WaterUv(KikasaDomainPlayer domain)
            => domain.AnyActive
                ? MathHelper.Lerp(KikasaHudTheme.WaterEmptyY, KikasaHudTheme.WaterLineY, domain.RiseProgress)
                : KikasaHudTheme.WaterEmptyY;

        /// <summary>记忆剪影中心（含漂浮）</summary>
        private Vector2 MemoryCenter(Vector2 anchor) {
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.25f) * 1.8f;
            return anchor + KikasaHudTheme.MemoryOffset + new Vector2(0f, bob);
        }

        //==================== 更新 ====================

        public override void Update() {
            frame++;
            Player p = player;
            KikasaDomainPlayer domain = Domain;
            bool want = WantVisible(p);
            appear = MathHelper.Clamp(appear + (want ? 0.06f : -0.06f), 0f, 1f);

            Vector2 anchor = Anchor;
            Size = new Vector2(KikasaHudTheme.MirrorW + 24f, KikasaHudTheme.MirrorH + 60f);
            DrawPosition = anchor - Size * 0.5f - new Vector2(0f, 12f);
            UIHitBox = DrawPosition.GetRectangle(Size);

            //====== 事件边沿：一切反馈说水的语言 ======
            KikasaVaultPlayer vault = Vault;
            KikasaServantPlayer servant = Servant;
            int memoryType = servant.LastDrownedType;
            bool servantOut = memoryType > 0 && servant.FindActiveServant() != null;
            int vaultCount = vault.Stored.Count;
            float drownCd = KikasaDrown.LocalCooldown01;
            bool lakeReady = vault.LakeReady;

            if (memoryType != lastMemoryType && memoryType > 0 && appear > 0.1f) {
                //湖记住了新的溺亡者：剪影短暂凝出真身，一串泡从它身上升起
                memoryPulse = 1f;
                stir = MathF.Max(stir, 0.7f);
                BurstBubbles(MemoryCenter(anchor), 6);
            }
            if (servantOut != lastServantOut && appear > 0.1f) {
                //出湖/归湖都搅一阵水
                stir = MathF.Max(stir, 0.55f);
                BurstBubbles(MemoryCenter(anchor), servantOut ? 4 : 7);
            }
            if (vaultCount != lastVaultCount && appear > 0.1f) {
                sedimentPulse = 1f;
                stir = MathF.Max(stir, 0.4f);
                BurstBubbles(anchor + new Vector2(Main.rand.NextFloat(-30f, 30f), 42f), 2);
            }
            if (lastDrownCd > 0.02f && drownCd <= 0.001f && appear > 0.1f) {
                //沉溺之手缓过来了：拱缘泛一圈光
                readyFlash = 1f;
            }
            if (lakeReady && !lastLakeReady && appear > 0.1f) {
                stir = MathF.Max(stir, 0.6f);
            }
            //鬼梦：倒影醒/睡切换一记脉冲，被拉入梦时鏡水大躁
            bool reflectOn = domain.HoundReflection;
            bool dreaming = domain.Phase == KikasaDomainPhase.Dreaming;
            if (reflectOn != lastReflectOn && appear > 0.1f) {
                reflectPulse = 1f;
                stir = MathF.Max(stir, 0.65f);
            }
            if (dreaming && !lastDreaming && appear > 0.1f) {
                stir = MathF.Max(stir, 1f);
            }
            lastReflectOn = reflectOn;
            lastDreaming = dreaming;
            lastMemoryType = memoryType;
            lastServantOut = servantOut;
            lastVaultCount = vaultCount;
            lastDrownCd = drownCd;
            lastLakeReady = lakeReady;

            //====== 包络推进 ======
            servantOutLerp = MathHelper.Lerp(servantOutLerp, servantOut ? 1f : 0f, 0.10f);
            vortexSpin += 0.045f + servantOutLerp * 0.02f;
            float restStir = domain.Phase == KikasaDomainPhase.Opening
                || domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.10f;
            stir = MathHelper.Lerp(stir, restStir, 0.06f);
            memoryPulse *= 0.94f;
            sedimentPulse *= 0.93f;
            readyFlash = MathF.Max(readyFlash - 0.03f, 0f);
            reflectPulse *= 0.94f;

            //悬停只亮名牌，不占鼠标——纯展示件不挡战斗点击
            Vector2 mouse = KikasaHudTheme.UIMouse;
            Rectangle mirrorRect = new(
                (int)(anchor.X - KikasaHudTheme.MirrorW * 0.5f),
                (int)(anchor.Y - KikasaHudTheme.MirrorH * 0.5f),
                KikasaHudTheme.MirrorW, KikasaHudTheme.MirrorH);
            hoverMirror = appear > 0.6f && mirrorRect.Contains(mouse.ToPoint());

            //悬停记忆冒泡：湖底的它知道你在看
            if (hoverMirror && memoryType > 0 && !servantOut
                && frame % 9 == 0 && motes.Count < MoteCap) {
                BurstBubbles(MemoryCenter(anchor), 1);
            }

            UpdateMotes(anchor);
        }

        private void BurstBubbles(Vector2 from, int count) {
            for (int i = 0; i < count && motes.Count < MoteCap; i++) {
                motes.Add(new Mote {
                    Pos = from + new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-4f, 4f)),
                    Vel = new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.8f)),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Size = Main.rand.NextFloat(1.2f, 2.4f),
                    MaxLife = Main.rand.Next(70, 110),
                });
            }
        }

        private void UpdateMotes(Vector2 anchor) {
            float waterPixY = anchor.Y + (WaterUv(Domain) - 0.5f) * KikasaHudTheme.MirrorH;
            for (int i = motes.Count - 1; i >= 0; i--) {
                Mote m = motes[i];
                m.Life++;
                m.Vel.Y = MathF.Max(m.Vel.Y - 0.010f, -1.2f);
                m.Pos.X += MathF.Sin(m.Life * 0.2f + m.Phase) * 0.3f;
                m.Pos.Y += m.Vel.Y;
                //升到水线或出鏡即破
                if (m.Pos.Y <= waterPixY + 2f || m.Life >= m.MaxLife
                    || m.Pos.Y < anchor.Y - KikasaHudTheme.MirrorH * 0.5f) {
                    motes.RemoveAt(i);
                    continue;
                }
                motes[i] = m;
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = appear;
            if (a < 0.01f) {
                return;
            }
            KikasaDomainPlayer domain = Domain;
            float time = Main.GlobalTimeWrappedHourly;
            float rain = EffectiveRain(domain);
            float waterUv = WaterUv(domain);
            Vector2 anchor = Anchor;

            //出没也说水的语言：浮现期水更躁，整鏡自下轻托
            float effStir = MathHelper.Clamp(stir + (1f - a) * 0.5f, 0f, 1f);
            anchor.Y += (1f - a) * 10f;

            DrawMirror(spriteBatch, anchor, domain, a, rain, waterUv, effStir);
            DrawMemory(spriteBatch, anchor, domain, a, rain, waterUv);
            DrawAdditiveBits(spriteBatch, anchor, domain, a, rain, waterUv, time);
            DrawSeal(spriteBatch, anchor, a, rain, time);
            DrawTextBits(spriteBatch, anchor, domain, a, rain, waterUv);
        }

        //====== 鏡体 ======

        private void DrawMirror(SpriteBatch sb, Vector2 anchor, KikasaDomainPlayer domain,
            float a, float rain, float waterUv, float effStir) {
            Rectangle rect = new(
                (int)(anchor.X - KikasaHudTheme.MirrorW * 0.5f),
                (int)(anchor.Y - KikasaHudTheme.MirrorH * 0.5f),
                KikasaHudTheme.MirrorW, KikasaHudTheme.MirrorH);
            float tear = domain.AnyActive ? domain.SpreadProgress : 0f;

            Effect effect = EffectLoader.KikasaHud?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                DrawMirrorCPU(sb, rect, a, rain, tear, waterUv);
                return;
            }
            //引导卡与鏡体共用一份 .fx，技法名逐帧显式指定，防上一位使用者的残留
            Effect mirrorTech = effect;
            if (mirrorTech.Techniques["TechMirror"] != null) {
                mirrorTech.CurrentTechnique = mirrorTech.Techniques["TechMirror"];
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(a);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uWaterY"]?.SetValue(waterUv);
            effect.Parameters["uTear"]?.SetValue(tear);
            effect.Parameters["uRain"]?.SetValue(rain);
            effect.Parameters["uStir"]?.SetValue(effStir);
            effect.Parameters["uBoil"]?.SetValue(domain.FlipBoil);
            //倒转角经 sin 折成一次倾荡来回，落定无跳变
            effect.Parameters["uTilt"]?.SetValue(MathF.Sin(domain.FlipRollAngle) * 0.30f);
            effect.Parameters["uFlash"]?.SetValue(domain.FlipFlash);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(VaultAsset.placeholder2.Value, rect, Color.White);
            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        //CPU 回退：平底窗 + 水线一划，不做同心放大的假羽化

        private void DrawMirrorCPU(SpriteBatch sb, Rectangle rect, float a,
            float rain, float tear, float waterUv) {
            if (tear <= 0.002f && a < 0.05f) {
                return;
            }
            Rectangle vis = rect;
            vis.Inflate(-6, -6);
            Rectangle shadow = vis;
            shadow.Offset(2, 3);
            sb.Draw(VaultAsset.placeholder2.Value, shadow, Color.Black * (0.4f * a));
            sb.Draw(VaultAsset.placeholder2.Value, vis, KikasaHudTheme.Void(rain) * (0.92f * a));
            int waterPix = rect.Y + (int)(rect.Height * MathHelper.Clamp(waterUv, 0f, 1f));
            if (waterPix < vis.Bottom - 2 && tear > 0.1f) {
                int wy = Math.Max(waterPix, vis.Y);
                Rectangle water = new(vis.X, wy, vis.Width, vis.Bottom - wy);
                sb.Draw(VaultAsset.placeholder2.Value, water, KikasaHudTheme.Deep(rain) * (0.75f * a * tear));
                KikasaVaultRenderer.DrawLine(sb, new Vector2(vis.Left + 3, wy), new Vector2(vis.Right - 3, wy),
                    1.4f, KikasaHudTheme.Glow(rain) * (0.5f * a * tear));
            }
            Color edge = KikasaHudTheme.Accent(rain) * (0.35f * a);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Right, vis.Top), 1.2f, edge);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(vis.Left, vis.Bottom), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.7f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Left, vis.Bottom), 1.2f, edge * 0.85f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(vis.Right, vis.Top), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.85f);
        }

        //====== 湖底记忆 ======

        private void DrawMemory(SpriteBatch sb, Vector2 anchor, KikasaDomainPlayer domain,
            float a, float rain, float waterUv) {
            int memoryType = Servant.LastDrownedType;
            if (memoryType <= 0 || servantOutLerp > 0.98f) {
                return;
            }
            //水没漫过湖床它就还没显形；退水时同样先失去它
            float memUvY = 0.5f + KikasaHudTheme.MemoryOffset.Y / KikasaHudTheme.MirrorH;
            float reveal = MathHelper.Clamp((memUvY - waterUv) * KikasaHudTheme.MirrorH / 14f, 0f, 1f);
            float alpha = reveal * a * (1f - servantOutLerp);
            if (alpha <= 0.02f) {
                return;
            }

            Main.instance.LoadNPC(memoryType);
            Texture2D tex = TextureAssets.Npc[memoryType]?.Value;
            if (tex == null) {
                return;
            }
            int frameCount = Math.Max(Main.npcFrameCount[memoryType], 1);
            Rectangle frameRect = new(0, 0, tex.Width, tex.Height / frameCount);
            float fit = KikasaHudTheme.MemoryFit;
            float scale = MathF.Min(1f, fit / MathF.Max(frameRect.Width, frameRect.Height));
            Vector2 pos = MemoryCenter(anchor);
            bool tamed = KikasaServantIndex.TryGet(memoryType, out _);
            float hover = hoverMirror ? 0.35f : 0f;
            //可驱使的沉得浅些醒些；没学会驱使的沉死在血水里
            float form = tamed
                ? MathHelper.Clamp(0.80f - hover - memoryPulse * 0.35f, 0.05f, 1f)
                : 0.92f;

            Effect effect = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(memoryType * 0.173f);
                effect.Parameters["uForm"]?.SetValue(form);
                effect.Parameters["uDissolve"]?.SetValue(0f);
                effect.Parameters["uScanMode"]?.SetValue(0f);
                effect.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frameRect.X / (float)tex.Width, frameRect.Y / (float)tex.Height,
                    frameRect.Width / (float)tex.Width, frameRect.Height / (float)tex.Height));
                effect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                effect.Parameters["uAspect"]?.SetValue(frameRect.Width / (float)frameRect.Height);
                effect.CurrentTechnique.Passes[0].Apply();
                Color color = new(255, 255, 255, (byte)(alpha * (tamed ? 235f : 190f)));
                sb.Draw(tex, pos, frameRect, color, 0f,
                    frameRect.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
            else {
                //着色器缺编：血色平染剪影
                Color fallback = Color.Lerp(Color.White, KikasaHudTheme.Accent(rain), form) * (alpha * 0.9f);
                sb.Draw(tex, pos, frameRect, fallback, 0f,
                    frameRect.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        //====== 加色小件 ======

        private void DrawAdditiveBits(SpriteBatch sb, Vector2 anchor, KikasaDomainPlayer domain,
            float a, float rain, float waterUv, float time) {
            Color glow = KikasaHudTheme.Glow(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            float waterPixY = anchor.Y + (waterUv - 0.5f) * KikasaHudTheme.MirrorH;
            bool hasWater = waterUv < 0.98f;

            KikasaVaultRenderer.BeginAdditive(sb);

            //水线上两点游光，鏡水在呼吸
            if (hasWater && domain.AnyActive) {
                float halfW = KikasaHudTheme.MirrorW * 0.5f - 16f;
                for (int k = 0; k < 2; k++) {
                    float drift = (time * (0.06f + k * 0.025f) + k * 0.5f) % 1f;
                    float gx = MathHelper.Lerp(anchor.X - halfW, anchor.X + halfW,
                        k == 0 ? drift : 1f - drift);
                    float ga = KikasaHudTheme.Breath(time, k * 3.7f, 2.4f);
                    KikasaVaultRenderer.DrawGlowDot(sb, new Vector2(gx, waterPixY),
                        5f, glow * (0.14f * ga * a));
                }
            }

            //记忆的呼吸微光：可驱使才有生气
            int memoryType = Servant.LastDrownedType;
            if (memoryType > 0 && servantOutLerp < 0.9f && hasWater
                && KikasaServantIndex.TryGet(memoryType, out _)) {
                float breath = KikasaHudTheme.Breath(time, 1.3f, 1.7f);
                float memA = (0.10f + 0.08f * breath + memoryPulse * 0.30f)
                    * a * (1f - servantOutLerp);
                KikasaVaultRenderer.DrawGlowDot(sb, MemoryCenter(anchor),
                    KikasaHudTheme.MemoryFit * 0.58f, accent * memA);
            }

            //鬼奴外出：湖床上只剩一个慢旋涡
            if (servantOutLerp > 0.02f && hasWater) {
                Vector2 vc = anchor + KikasaHudTheme.MemoryOffset;
                float va = servantOutLerp * a;
                for (int ring = 0; ring < 2; ring++) {
                    float rp = (vortexSpin * 0.16f + ring * 0.5f) % 1f;
                    float r = MathHelper.Lerp(15f, 4f, rp);
                    KikasaVaultRenderer.DrawRing(sb, vc, r, r * 0.42f,
                        glow * (0.22f * (1f - rp) * va));
                }
                //四点游涡，绕着缩进去
                for (int k = 0; k < 4; k++) {
                    float ang = vortexSpin + k * MathHelper.PiOver2;
                    float rp = (vortexSpin * 0.13f + k * 0.25f) % 1f;
                    float r = MathHelper.Lerp(14f, 2.5f, rp);
                    Vector2 dotPos = vc + ang.ToRotationVector2() * r;
                    KikasaVaultRenderer.DrawGlowDot(sb, dotPos, 2.4f, accent * (0.30f * (1f - rp) * va));
                }
            }

            //气泡
            foreach (Mote m in motes) {
                float la = MathHelper.Clamp(1f - m.Life / (float)m.MaxLife, 0f, 1f);
                KikasaVaultRenderer.DrawGlowDot(sb, m.Pos, m.Size, glow * (0.30f * la * a));
            }

            //倒影恶犬醒着：水下一对余烬目，静静看回来
            if (domain.HoundReflection && hasWater) {
                float breath = KikasaHudTheme.Breath(time, 0.7f, 2.1f);
                Vector2 eyeC = new(anchor.X, waterPixY + 17f);
                Color ember = new(230, 96, 40);
                float ea = (0.28f + 0.24f * breath + reflectPulse * 0.4f) * a;
                KikasaVaultRenderer.DrawGlowDot(sb, eyeC - new Vector2(4.6f, 0f), 2.1f, ember * ea);
                KikasaVaultRenderer.DrawGlowDot(sb, eyeC + new Vector2(4.6f, 0f), 2.1f, ember * ea);
            }

            //沉溺冷却弧：伞拱缘的一道水痕，随冷却退去
            float cd = KikasaDrown.LocalCooldown01;
            Vector2 domeCenter = anchor + KikasaHudTheme.DomeCenterOffset;
            if (cd > 0.005f && domain.AnyActive) {
                DrawArc(sb, domeCenter, KikasaHudTheme.CooldownArcR,
                    -MathHelper.PiOver2 - 0.9f, 1.8f * cd, 1.6f, glow * (0.55f * a));
            }
            //梦中唤犬冷却：同一段拱缘靠里一圈，烬红退去
            if (domain.Phase == KikasaDomainPhase.Dreaming) {
                float hcd = Dream.HoundCooldown01;
                if (hcd > 0.005f) {
                    DrawArc(sb, domeCenter, KikasaHudTheme.CooldownArcR - 5f,
                        -MathHelper.PiOver2 - 0.9f, 1.8f * hcd, 1.4f,
                        new Color(230, 96, 40) * (0.5f * a));
                }
            }
            //冷却结束：拱缘泛光一涨即退
            if (readyFlash > 0.02f) {
                DrawArc(sb, domeCenter, KikasaHudTheme.CooldownArcR + (1f - readyFlash) * 5f,
                    -MathHelper.PiOver2 - 0.9f, 1.8f, 1.3f, glow * (readyFlash * 0.5f * a));
            }

            //沉积线：湖底记着藏了几件，变动时一亮
            int vaultCount = Vault.Stored.Count;
            if (vaultCount > 0 && hasWater) {
                float fillW = (KikasaHudTheme.MirrorW - 44f) * (vaultCount / (float)KikasaVaultPlayer.Capacity);
                float sy = anchor.Y + 45f;
                Color sedCol = accent * ((0.30f + sedimentPulse * 0.45f) * a);
                KikasaVaultRenderer.DrawLine(sb,
                    new Vector2(anchor.X - fillW * 0.5f, sy),
                    new Vector2(anchor.X + fillW * 0.5f, sy), 1.6f, sedCol);
            }

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        /// <summary>分段折线画弧，加色批内用</summary>
        private static void DrawArc(SpriteBatch sb, Vector2 center, float radius,
            float startAngle, float span, float width, Color color) {
            int segs = Math.Max(6, (int)(span * 10f));
            Vector2 prev = center + startAngle.ToRotationVector2() * radius;
            for (int i = 1; i <= segs; i++) {
                float ang = startAngle + span * i / segs;
                Vector2 next = center + ang.ToRotationVector2() * radius;
                KikasaVaultRenderer.DrawLine(sb, prev, next, width, color);
                prev = next;
            }
        }

        //====== 伞章 ======

        //伞骨淡线垫底，伞盖粗笔带亮芯；湖就绪时一段掠光缓巡

        private void DrawSeal(SpriteBatch sb, Vector2 anchor, float a, float rain, float time) {
            Vector2 center = anchor + KikasaHudTheme.SealOffset;
            float scale = KikasaHudTheme.SealR;
            SvgPath canopy = SvgPathPen.Path(KikasaVaultUI.SealCanopy);
            SvgPath frame = SvgPathPen.Path(KikasaVaultUI.SealFrame);
            Color dim = KikasaHudTheme.TextDim(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            Color glow = KikasaHudTheme.Glow(rain);
            SvgPathPen.Stroke(sb, frame, center, scale, 0f, dim, 1.1f, a * 0.8f, 0f, 1f);
            SvgPathPen.Stroke(sb, canopy, center, scale, 0f, accent, 2.0f, a, 0f, 1f, core: glow);
            if (Vault.LakeReady) {
                SvgPathPen.StrokeRunner(sb, canopy, center, scale, 0f,
                    glow, 2.2f, a * 0.5f, time * 0.07f, 0.10f);
            }
        }

        //====== 文字层 ======

        private void DrawTextBits(SpriteBatch sb, Vector2 anchor, KikasaDomainPlayer domain,
            float a, float rain, float waterUv) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Color text = KikasaHudTheme.Text(rain);
            Color dim = KikasaHudTheme.TextDim(rain);
            float chromeA = MathHelper.Clamp((a - 0.4f) / 0.5f, 0f, 1f);
            if (chromeA <= 0.02f) {
                return;
            }

            //沉物计数：贴着鏡底
            int vaultCount = Vault.Stored.Count;
            if (domain.AnyActive && vaultCount > 0) {
                string count = string.Format(KikasaVaultUI.CountFormat.Value,
                    vaultCount, KikasaVaultPlayer.Capacity);
                Vector2 size = font.MeasureString(count) * 0.72f;
                Utils.DrawBorderString(sb, count,
                    new Vector2(anchor.X - size.X * 0.5f, anchor.Y + KikasaHudTheme.MirrorH * 0.5f + 6f),
                    dim * chromeA, 0.72f);
            }

            //持伞未开域：一行水语提示怎么把湖撑开；教学卡在讲同一句时让位
            if (!domain.AnyActive && !KikasaHudLead.CardVisible) {
                string hint = string.Format(DomainHintFormat.Value,
                    CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
                Vector2 size = font.MeasureString(hint) * 0.74f;
                float breathe = 0.55f + 0.25f * KikasaHudTheme.Breath(Main.GlobalTimeWrappedHourly, 0.9f, 1.5f);
                Utils.DrawBorderString(sb, hint,
                    new Vector2(anchor.X - size.X * 0.5f, anchor.Y + KikasaHudTheme.MirrorH * 0.5f + 6f),
                    dim * (breathe * chromeA), 0.74f);
            }

            //悬停名牌：湖底记忆的名字浮上来
            if (hoverMirror && waterUv < 0.9f) {
                int memoryType = Servant.LastDrownedType;
                string name;
                string state = null;
                if (memoryType <= 0) {
                    name = MemoryEmpty.Value;
                }
                else {
                    name = Lang.GetNPCNameValue(memoryType);
                    if (lastServantOut) {
                        state = ServantOutTag.Value;
                    }
                    else if (!KikasaServantIndex.TryGet(memoryType, out _)) {
                        state = KikasaServantPlayer.ServantUnknown.Value;
                    }
                }
                Vector2 nameSize = font.MeasureString(name) * 0.78f;
                Vector2 namePos = new(anchor.X - nameSize.X * 0.5f,
                    anchor.Y + KikasaHudTheme.MemoryOffset.Y - KikasaHudTheme.MemoryFit * 0.5f - 22f);
                Utils.DrawBorderString(sb, name, namePos, text * chromeA, 0.78f);
                if (state != null) {
                    Vector2 stateSize = font.MeasureString(state) * 0.68f;
                    Utils.DrawBorderString(sb, state,
                        new Vector2(anchor.X - stateSize.X * 0.5f, namePos.Y + 20f),
                        dim * chromeA, 0.68f);
                }
            }
        }
    }
}
