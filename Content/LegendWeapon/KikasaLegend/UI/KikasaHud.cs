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
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
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
    /// 鏡面可点：悬停占鼠标，点击开阖湖窗（比目鱼之眼点开图鉴的同款语义），
    /// 湖没涨起来时点击给"湖还没涨到脚边"的拒绝答话。
    /// </summary>
    internal class KikasaHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaHud Instance => UIHandleLoader.GetUIHandleOfType<KikasaHud>();

        public static LocalizedText MemoryEmpty { get; private set; }
        public static LocalizedText ServantOutTag { get; private set; }
        public static LocalizedText DomainHintFormat { get; private set; }
        public static LocalizedText OpenHintFormat { get; private set; }

        public override void SetStaticDefaults() {
            MemoryEmpty = this.GetLocalization(nameof(MemoryEmpty), () => "湖底还空着");
            ServantOutTag = this.GetLocalization(nameof(ServantOutTag), () => "它替你出手去了");
            DomainHintFormat = this.GetLocalization(nameof(DomainHintFormat), () => "按 {0} 撑开血湖");
            OpenHintFormat = this.GetLocalization(nameof(OpenHintFormat), () => "点击或按 {0} 开阖湖窗");
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
        float IBottomLeftHud.HudStackTopExtent => 76f;
        float IBottomLeftHud.HudStackBottomExtent => 78f;
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
        //悬停的平滑值，喂伞章提亮与鏡水活性
        private float hoverLerp;
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

        //湿纸滴水：拱缘偶尔一滴顺壁滑落，触水起微圈
        private struct Drip
        {
            public Vector2 Pos;
            public float Vy;
            public int Life;
        }

        private readonly List<Drip> drips = [];
        private int nextDripIn = 120;
        //触水微圈（位置 + 余龄）
        private readonly List<(Vector2 pos, int timer)> dripRings = [];

        //==================== 墨骨勾线 ====================
        //伞拱轮廓，归一空间（scale = RimHalfW，原点鏡心）：
        //圆拱两段三次曲线 + 直裙边 + 四瓣荷缘 Q 弧，与 KikasaHud.fx 的 SDF 同形
        private static readonly string RimOutline = BuildRimOutline();

        private static string BuildRimOutline() {
            float hh = KikasaHudTheme.RimHalfH / KikasaHudTheme.RimHalfW;  //≈0.918
            float cy = 1f - hh;                                            //拱心 y ≈0.082
            const float k = 0.5523f;                                       //四分圆三次近似
            float skirt = hh - 5f / KikasaHudTheme.RimHalfW;               //荷缘尖端 y
            float dip = 2f * hh - skirt;                                   //荷缘 Q 控制点 y
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", inv);
            return $"M -1 {F(cy)} "
                + $"C -1 {F(cy - k)} -{F(k)} -{F(hh)} 0 -{F(hh)} "
                + $"C {F(k)} -{F(hh)} 1 {F(cy - k)} 1 {F(cy)} "
                + $"L 1 {F(skirt)} "
                + $"Q 0.75 {F(dip)} 0.5 {F(skirt)} "
                + $"Q 0.25 {F(dip)} 0 {F(skirt)} "
                + $"Q -0.25 {F(dip)} -0.5 {F(skirt)} "
                + $"Q -0.75 {F(dip)} -1 {F(skirt)} "
                + $"L -1 {F(cy)}";
        }

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
            if (hoverMirror) {
                //水知道你在看它
                restStir = MathF.Max(restStir, 0.30f);
            }
            stir = MathHelper.Lerp(stir, restStir, 0.06f);
            memoryPulse *= 0.94f;
            sedimentPulse *= 0.93f;
            readyFlash = MathF.Max(readyFlash - 0.03f, 0f);
            reflectPulse *= 0.94f;

            //悬停鏡面占鼠标，点击开阖湖窗（比目鱼之眼点开图鉴的同款语义）
            Vector2 mouse = KikasaHudTheme.UIMouse;
            Rectangle mirrorRect = new(
                (int)(anchor.X - KikasaHudTheme.MirrorW * 0.5f),
                (int)(anchor.Y - KikasaHudTheme.MirrorH * 0.5f),
                KikasaHudTheme.MirrorW, KikasaHudTheme.MirrorH);
            hoverMirror = appear > 0.6f && mirrorRect.Contains(mouse.ToPoint());
            hoverLerp = MathHelper.Lerp(hoverLerp, hoverMirror ? 1f : 0f, 0.15f);
            if (hoverMirror) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    ToggleVaultWindow();
                }
            }

            //悬停记忆冒泡：湖底的它知道你在看
            if (hoverMirror && memoryType > 0 && !servantOut
                && frame % 9 == 0 && motes.Count < MoteCap) {
                BurstBubbles(MemoryCenter(anchor), 1);
            }

            UpdateMotes(anchor);
            UpdateDrips(anchor, domain);
        }

        /// <summary>湿纸滴水：窗撕开后拱缘偶尔渗一滴，顺壁滑落触水即圈</summary>
        private void UpdateDrips(Vector2 anchor, KikasaDomainPlayer domain) {
            bool wet = domain.AnyActive && domain.SpreadProgress > 0.6f;
            if (wet && --nextDripIn <= 0 && drips.Count < 2) {
                nextDripIn = Main.rand.Next(80, 200);
                //拱顶上半圈取一点，滴沿内壁起步
                float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.0f, 1.0f);
                Vector2 rim = anchor + KikasaHudTheme.DomeCenterOffset
                    + ang.ToRotationVector2() * (KikasaHudTheme.RimHalfW - 3f);
                drips.Add(new Drip { Pos = rim, Vy = 0.15f });
            }

            float waterPixY = anchor.Y + (WaterUv(domain) - 0.5f) * KikasaHudTheme.MirrorH;
            float floorY = anchor.Y + KikasaHudTheme.RimHalfH - 4f;
            for (int i = drips.Count - 1; i >= 0; i--) {
                Drip dp = drips[i];
                dp.Life++;
                dp.Vy = MathF.Min(dp.Vy + 0.04f, 1.6f);
                dp.Pos.Y += dp.Vy;
                float endY = MathF.Min(waterPixY, floorY);
                if (dp.Pos.Y >= endY || dp.Life > 240) {
                    if (dp.Pos.Y >= waterPixY - 1f && waterPixY < floorY) {
                        //触水：一记微圈，水面轻应
                        dripRings.Add((new Vector2(dp.Pos.X, waterPixY), 0));
                        stir = MathF.Max(stir, 0.22f);
                    }
                    drips.RemoveAt(i);
                    continue;
                }
                drips[i] = dp;
            }
            for (int i = dripRings.Count - 1; i >= 0; i--) {
                (Vector2 pos, int timer) = dripRings[i];
                if (++timer >= 22) {
                    dripRings.RemoveAt(i);
                }
                else {
                    dripRings[i] = (pos, timer);
                }
            }
        }

        /// <summary>
        /// 鏡面点击：开着就合上，合尽了才开——湖窗自己的"点窗外合上"可能在同一击里
        /// 先把窗合了，这里若无视合拢余韵会当帧把它重新掀开
        /// </summary>
        private void ToggleVaultWindow() {
            KikasaVaultUI ui = KikasaVaultUI.Instance;
            if (ui == null) {
                return;
            }
            if (ui.IsOpen) {
                ui.Close();
                return;
            }
            if (ui.OpenProgress > 0.01f) {
                //正在合拢，这一击不受理
                return;
            }
            if (Vault.LakeReady) {
                ui.Open();
                return;
            }
            //拒绝也答话：湖没涨起来，窗开不了
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
            CombatText.NewText(player.Hitbox, new Color(190, 84, 80), KikasaVaultPlayer.LakeNotReady.Value);
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
            DrawInkBones(spriteBatch, anchor, domain, a, rain, time);
            DrawMemory(spriteBatch, anchor, domain, a, rain, waterUv);
            DrawAdditiveBits(spriteBatch, anchor, domain, a, rain, waterUv, time);
            DrawSeal(spriteBatch, anchor, a, rain, time);
            DrawTextBits(spriteBatch, anchor, domain, a, rain, waterUv);
        }

        //====== 墨骨层：拱缘勾线与干纸伞骨 ======

        /// <summary>
        /// 锐利前景：伞拱轮廓一笔勾线（湖就绪时拱缘走巡光）；
        /// 干纸态透出四根伞骨，撕开后随 tear 淡出——纸伞变窗
        /// </summary>
        private void DrawInkBones(SpriteBatch sb, Vector2 anchor, KikasaDomainPlayer domain,
            float a, float rain, float time) {
            SvgPath rim = SvgPathPen.Path(RimOutline);
            Color accent = KikasaHudTheme.Accent(rain);
            Color dim = KikasaHudTheme.TextDim(rain);
            //外柔内锐两笔：暗托底，亮描形
            SvgPathPen.Stroke(sb, rim, anchor, KikasaHudTheme.RimHalfW, 0f,
                KikasaHudTheme.Void(rain), 2.6f, a * 0.5f);
            SvgPathPen.Stroke(sb, rim, anchor, KikasaHudTheme.RimHalfW, 0f,
                accent, 1.1f, a * (0.4f + hoverLerp * 0.2f));
            //湖就绪：拱缘一段巡光缓走，"这扇窗现在开得了"
            if (Vault.LakeReady) {
                SvgPathPen.StrokeRunner(sb, rim, anchor, KikasaHudTheme.RimHalfW, 0f,
                    KikasaHudTheme.Glow(rain), 1.5f, a * 0.45f, time * 0.05f, 0.08f);
            }

            //干纸伞骨：自拱心放射四根，避开正上方的伞章弯钩
            float tear = domain.AnyActive ? domain.SpreadProgress : 0f;
            float dry = 1f - tear;
            if (dry > 0.03f) {
                Vector2 hub = anchor + KikasaHudTheme.DomeCenterOffset;
                Span<float> ribs = [-2.62f, -1.92f, -1.22f, -0.52f];
                foreach (float ang in ribs) {
                    Vector2 dir = ang.ToRotationVector2();
                    KikasaVaultRenderer.DrawLine(sb, hub + dir * 6f,
                        hub + dir * (KikasaHudTheme.RimHalfW - 3f), 1f,
                        dim * (0.28f * dry * a));
                }
                //骨间纸面一点鼓起的受光（伞收拢时的折面感）
                KikasaVaultRenderer.DrawLine(sb, hub + new Vector2(0f, 2f),
                    hub + new Vector2(0f, KikasaHudTheme.RimHalfH - 8f), 1f,
                    dim * (0.18f * dry * a));
            }
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

            //湖床落影：真阿尔法的压扁暗环，剪影不再悬空（加色画不出暗，这层必须留在普通批）
            Vector2 shadowAt = anchor + KikasaHudTheme.MemoryOffset
                + new Vector2(0f, fit * 0.46f);
            KikasaVaultRenderer.DrawRing(sb, shadowAt, fit * 0.40f, fit * 0.11f,
                KikasaHudTheme.Void(rain) * (0.5f * alpha));
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

            //水面本身给一条锐利泡沫线（宽度贴着拱形收窄），游光骑在线上
            if (hasWater && domain.AnyActive) {
                float lineHalf = WaterLineHalfWidth(anchor, waterPixY);
                KikasaVaultRenderer.DrawLine(sb,
                    new Vector2(anchor.X - lineHalf, waterPixY),
                    new Vector2(anchor.X + lineHalf, waterPixY), 1f,
                    glow * ((0.16f + stir * 0.18f) * a));
                for (int k = 0; k < 2; k++) {
                    float drift = (time * (0.06f + k * 0.025f) + k * 0.5f) % 1f;
                    float gx = MathHelper.Lerp(anchor.X - lineHalf + 4f, anchor.X + lineHalf - 4f,
                        k == 0 ? drift : 1f - drift);
                    float ga = KikasaHudTheme.Breath(time, k * 3.7f, 2.4f);
                    KikasaVaultRenderer.DrawGlowDot(sb, new Vector2(gx, waterPixY),
                        4.5f, glow * (0.14f * ga * a));
                }
            }

            //湿纸滴水与触水微圈
            foreach (Drip dp in drips) {
                KikasaVaultRenderer.DrawLine(sb, dp.Pos - new Vector2(0f, 2.6f + dp.Vy * 1.6f),
                    dp.Pos, 1f, glow * (0.30f * a));
                KikasaVaultRenderer.DrawGlowDot(sb, dp.Pos, 1.6f, glow * (0.28f * a));
            }
            foreach ((Vector2 pos, int timer) in dripRings) {
                float t = timer / 22f;
                KikasaVaultRenderer.DrawRing(sb, pos, 2f + t * 7f, (2f + t * 7f) * 0.35f,
                    glow * (0.30f * (1f - t) * a));
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
                    float r = MathHelper.Lerp(13f, 3.5f, rp);
                    KikasaVaultRenderer.DrawRing(sb, vc, r, r * 0.42f,
                        glow * (0.22f * (1f - rp) * va));
                }
                //四点游涡，绕着缩进去
                for (int k = 0; k < 4; k++) {
                    float ang = vortexSpin + k * MathHelper.PiOver2;
                    float rp = (vortexSpin * 0.13f + k * 0.25f) % 1f;
                    float r = MathHelper.Lerp(12f, 2.2f, rp);
                    Vector2 dotPos = vc + ang.ToRotationVector2() * r;
                    KikasaVaultRenderer.DrawGlowDot(sb, dotPos, 2.2f, accent * (0.30f * (1f - rp) * va));
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

            //沉积层：满刻度暗槽 + 存量亮层 + 每五件一粒刻点，变动时一亮
            int vaultCount = Vault.Stored.Count;
            if (vaultCount > 0 && hasWater) {
                const float fullW = KikasaHudTheme.MirrorW - 40f;
                float sy = anchor.Y + KikasaHudTheme.RimHalfH - 8f;
                float left = anchor.X - fullW * 0.5f;
                Color dimCol = KikasaHudTheme.TextDim(0f) * (0.10f * a);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(left, sy),
                    new Vector2(left + fullW, sy), 1f, dimCol);
                float fillW = fullW * MathHelper.Clamp(
                    vaultCount / (float)KikasaVaultPlayer.Capacity, 0f, 1f);
                Color sedCol = accent * ((0.32f + sedimentPulse * 0.45f) * a);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(left, sy),
                    new Vector2(left + fillW, sy), 1.4f, sedCol);
                //刻点：容量每 1/8（5 件）一粒，存到亮起
                for (int k = 1; k <= 8; k++) {
                    float tx = left + fullW * k / 8f;
                    bool lit = vaultCount >= k * KikasaVaultPlayer.Capacity / 8;
                    KikasaVaultRenderer.DrawGlowDot(sb, new Vector2(tx, sy), 1.5f,
                        lit ? glow * ((0.38f + sedimentPulse * 0.3f) * a)
                            : KikasaHudTheme.TextDim(0f) * (0.12f * a));
                }
            }

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        /// <summary>水面线在拱窗内的半宽：拱区按弦长收窄，裙区吃满</summary>
        private static float WaterLineHalfWidth(Vector2 anchor, float waterPixY) {
            float dy = waterPixY - (anchor.Y + KikasaHudTheme.DomeCenterOffset.Y);
            if (dy >= 0f) {
                return KikasaHudTheme.RimHalfW - 5f;
            }
            float r = KikasaHudTheme.RimHalfW;
            float chord = MathF.Sqrt(MathF.Max(r * r - dy * dy, 0f));
            return MathF.Max(chord - 5f, 6f);
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

        //伞骨淡线垫底，伞盖粗笔带亮芯；湖就绪时一段掠光缓巡；悬停鏡面时伞章应声提亮

        private void DrawSeal(SpriteBatch sb, Vector2 anchor, float a, float rain, float time) {
            Vector2 center = anchor + KikasaHudTheme.SealOffset;
            float scale = KikasaHudTheme.SealR;
            SvgPath canopy = SvgPathPen.Path(KikasaVaultUI.SealCanopy);
            SvgPath frame = SvgPathPen.Path(KikasaVaultUI.SealFrame);
            Color dim = KikasaHudTheme.TextDim(rain);
            Color accent = Color.Lerp(KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain), hoverLerp * 0.4f);
            Color glow = KikasaHudTheme.Glow(rain);
            float sealA = a * (1f + hoverLerp * 0.2f);
            SvgPathPen.Stroke(sb, frame, center, scale, 0f, dim, 1.1f, sealA * 0.8f, 0f, 1f);
            SvgPathPen.Stroke(sb, canopy, center, scale, 0f, accent, 2.0f, sealA, 0f, 1f, core: glow);
            //就绪巡光移交给了拱缘勾线；伞章的掠光只在悬停时应一下
            if (hoverLerp > 0.03f) {
                SvgPathPen.StrokeRunner(sb, canopy, center, scale, 0f,
                    glow, 2.2f, a * 0.6f * hoverLerp, time * 0.07f, 0.10f);
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

            //鏡底一行：悬停时讲"点击开阖湖窗"，平时报沉物计数
            int vaultCount = Vault.Stored.Count;
            if (domain.AnyActive) {
                string footer = null;
                Color footerCol = dim;
                float footerScale = 0.72f;
                if (hoverMirror) {
                    footer = string.Format(OpenHintFormat.Value,
                        CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value));
                    footerCol = Color.Lerp(dim, text, 0.5f);
                    footerScale = 0.74f;
                }
                else if (vaultCount > 0) {
                    footer = string.Format(KikasaVaultUI.CountFormat.Value,
                        vaultCount, KikasaVaultPlayer.Capacity);
                }
                if (footer != null) {
                    Vector2 size = font.MeasureString(footer) * footerScale;
                    Utils.DrawBorderString(sb, footer,
                        new Vector2(anchor.X - size.X * 0.5f, anchor.Y + KikasaHudTheme.MirrorH * 0.5f + 6f),
                        footerCol * chromeA, footerScale);
                }
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
