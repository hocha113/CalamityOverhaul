using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 铭刻仪式弹窗,<see cref="WraithRiteKind"/> 三语义编舞;
    /// 纯函数于 <see cref="timer"/>,可 <see cref="SkipToStill"/>
    /// </summary>
    internal sealed class OniEngraveRiteUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniEngraveRiteUI Instance => UIHandleLoader.GetUIHandleOfType<OniEngraveRiteUI>();

        public static LocalizedText RiteTitle { get; private set; }
        public static LocalizedText RiteTitleRenew { get; private set; }
        public static LocalizedText RiteTitleResubdue { get; private set; }
        public static LocalizedText RiteHint { get; private set; }
        public static LocalizedText RiteHintResubdue { get; private set; }

        public override void SetStaticDefaults() {
            RiteTitle = this.GetLocalization(nameof(RiteTitle), () => "铭 刻");
            RiteTitleRenew = this.GetLocalization(nameof(RiteTitleRenew), () => "续 契");
            RiteTitleResubdue = this.GetLocalization(nameof(RiteTitleResubdue), () => "收 伏");
            RiteHint = this.GetLocalization(nameof(RiteHint), () => "落笔 · 归卷");
            RiteHintResubdue = this.GetLocalization(nameof(RiteHintResubdue), () => "押 回 · 缄 卷");
        }

        public override bool CloseOnEscape => true;
        //仪式是最上层的一次性演出,压过点鬼簿(2)与 HUD(1)
        public override float RenderPriority => 3f;
        public override SoundStyle? OpenSound => SoundID.Item29 with { Pitch = -0.85f, Volume = 0.35f };
        public override SoundStyle? CloseSound => SoundID.Item35 with { Pitch = 0.2f, Volume = 0.3f };

        //====铭刻时间轴(帧),Condense 之后的节点依名讳长度计算====
        private const float TFreeze = 50f;      //凝滞预备拍:鬼影入定,背光收息,给刀痕让出一口屏息
        private const float TSlash = 66f;
        private const float TBreak = 74f;
        private const float TCondense = 90f;
        private const float CharInterval = 6f;
        private const float StampDelay = 16f;
        private const float StampDrop = 13f;
        //====续契时间轴:名讳已在簿上,没有"从无到有"的刀痕与凝字====
        private const float TBow = 16f;         //鬼影俯首起点
        private const float TRewet = 58f;       //湿墨重润起点,步长见 RewetStep
        private const float PressLead = 20f;    //重润完到覆押起
        private const float PressDur = 18f;     //覆押渐压时长,缓压不砸
        //====收伏时间轴:定长,名讳自始在簿(被拖拽着),不依字数====
        private const float TPress1 = 52f;
        private const float TPress2 = 84f;
        private const float NailDelay = 22f;    //第二拍到钉印起落
        private const float NailDrop = 7f;      //钉死下落,比铭刻的砸章更急

        private OniGhostEntry entry;
        private WraithRiteKind riteKind;
        private float timer;
        private bool motesSpawned;
        private bool slashPlayed;
        private bool stampPlayed;
        private bool rewetCued;
        private bool press1Played;
        private bool press2Played;
        private int lastTypeChars = -1;
        private float inkAge = 60f;
        private readonly OniUIParticlePool particles = new(160);
        private int stillFxTimer;

        /// <summary>播放落簿仪式,数据已由 <c>WraithRites</c> 写入;无名不受理</summary>
        public static void Play(OniGhostEntry ghost, WraithRiteKind kind = WraithRiteKind.FirstBind) {
            OniEngraveRiteUI inst = Instance;
            if (inst == null || ghost == null || !ghost.HasName || Main.dedServ) {
                return;
            }
            inst.entry = ghost;
            inst.riteKind = kind;
            inst.ResetPlayback();
            inst.Open();
        }

        //====语义化基调====

        private LocalizedText KindTitle => riteKind switch {
            WraithRiteKind.RenewPact => RiteTitleRenew,
            WraithRiteKind.Resubdue => RiteTitleResubdue,
            _ => RiteTitle,
        };

        /// <summary>背光,铭深红/续鬼火青/收血色</summary>
        private Color KindBacklight => riteKind switch {
            WraithRiteKind.RenewPact => OnikiriUITheme.GhostDim,
            WraithRiteKind.Resubdue => OnikiriUITheme.Bright,
            _ => OnikiriUITheme.Deep,
        };

        private void ResetPlayback() {
            timer = 0f;
            motesSpawned = false;
            slashPlayed = false;
            stampPlayed = false;
            rewetCued = false;
            press1Played = false;
            press2Played = false;
            lastTypeChars = -1;
            inkAge = 60f;
            stillFxTimer = 0;
            particles.Clear();
        }

        //====布局(全部 UI 空间)====
        private static Vector2 Center => new(OnikiriUITheme.UIScreenW * 0.5f, OnikiriUITheme.UIScreenH * 0.5f - 16f);
        private Vector2 SilhouetteCenter => Center + new Vector2(-112f, 6f);
        private bool NameIsVertical => OniBrush.ContainsCJK(entry?.Name?.Invoke() ?? string.Empty);

        private string NameText => entry?.Name?.Invoke() ?? string.Empty;
        private string PowerText => entry?.Power?.Invoke() ?? string.Empty;

        //====时间轴派生量(纯函数)====
        private float NameDoneAt => TCondense + NameText.Length * CharInterval + 8f;
        private float StampHitAt => NameDoneAt + StampDelay + StampDrop;

        /// <summary>续契重润步长,长名加速保总时长</summary>
        private float RewetStep => MathHelper.Clamp(60f / Math.Max(1, NameText.Length), 4f, 10f);
        private float RewetDoneAt => TRewet + NameText.Length * RewetStep + 6f;
        private float PressStartAt => RewetDoneAt + PressLead;
        private float PressDoneAt => PressStartAt + PressDur;

        private float NailStartAt => TPress2 + NailDelay;
        private float NailHitAt => NailStartAt + NailDrop;

        /// <summary>静场时刻:三套编舞各自的终拍余韵后收束到同一交互态</summary>
        private float StillAt => riteKind switch {
            WraithRiteKind.RenewPact => PressDoneAt + 14f,
            WraithRiteKind.Resubdue => NailHitAt + 20f,
            _ => StampHitAt + 16f,
        };

        /// <summary>赋力小字浮现时刻:铭刻随凝字完,续契随重润完,收伏要等钉死才肯认</summary>
        private float PowerRevealAt => riteKind switch {
            WraithRiteKind.RenewPact => RewetDoneAt,
            WraithRiteKind.Resubdue => NailHitAt,
            _ => NameDoneAt,
        };

        /// <summary>已显字符数:仅铭刻走打字机,续契与收伏的名讳自始整行在簿</summary>
        private int TypeChars => riteKind != WraithRiteKind.FirstBind
            ? NameText.Length
            : timer < TCondense ? 0 : Math.Min(NameText.Length, (int)((timer - TCondense) / CharInterval) + 1);

        private float SlashSweep => MathHelper.Clamp((timer - TSlash) / 9f, 0f, 1f);

        /// <summary>鬼影碎裂进度:铭刻被刀痕斩碎,续契押印后缓缓归烟,收伏钉死一瞬崩散</summary>
        private float SilhouetteBreak => riteKind switch {
            WraithRiteKind.RenewPact => MathHelper.Clamp((timer - PressDoneAt) / 46f, 0f, 1f),
            WraithRiteKind.Resubdue => MathHelper.Clamp((timer - NailHitAt) / 10f, 0f, 1f),
            _ => MathHelper.Clamp((timer - TBreak) / 15f, 0f, 1f),
        };

        private float StampProgress => MathHelper.Clamp((timer - NameDoneAt - StampDelay) / StampDrop, 0f, 1f);

        /// <summary>at 帧起 dur 帧线性衰减的冲击闪,印章命中与按压拍共用</summary>
        private float Flash(float at, float dur = 18f) => timer < at ? 0f : Math.Max(0f, 1f - (timer - at) / dur);

        /// <summary>铭刻凝滞预备拍:刀痕前鬼影骤然入定(1→0.1)并屏息,斩落即碎不回魂</summary>
        private float FreezeFactor => riteKind == WraithRiteKind.FirstBind
            ? 1f - 0.9f * MathHelper.Clamp((timer - TFreeze) / 6f, 0f, 1f)
            : 1f;

        /// <summary>续契俯首进度(0~1)</summary>
        private float BowEase {
            get {
                if (riteKind != WraithRiteKind.RenewPact) {
                    return 0f;
                }
                float e = MathHelper.Clamp((timer - TBow) / 46f, 0f, 1f);
                return e * e * (3f - 2f * e);
            }
        }

        /// <summary>续契俯首位移:鬼影向名讳一侧欠身挨近</summary>
        private Vector2 BowOffset => new Vector2(26f, 10f) * BowEase;

        /// <summary>收伏挣扎烈度:开场狂乱 1,每记按压压下一档,钉死归零(每拍数帧缓出)</summary>
        private float Struggle {
            get {
                if (riteKind != WraithRiteKind.Resubdue) {
                    return 0f;
                }
                float s = MathHelper.Lerp(1f, 0.60f, MathHelper.Clamp((timer - TPress1) / 8f, 0f, 1f));
                s = MathHelper.Lerp(s, 0.32f, MathHelper.Clamp((timer - TPress2) / 8f, 0f, 1f));
                s = MathHelper.Lerp(s, 0f, MathHelper.Clamp((timer - NailHitAt) / 6f, 0f, 1f));
                return s;
            }
        }

        /// <summary>收伏按压下沉量(px):每拍瞬间全压,18 帧弹回四成残留,压痕逐拍累积</summary>
        private float ResubduePush => riteKind == WraithRiteKind.Resubdue
            ? PressStroke(TPress1) * 10f + PressStroke(TPress2) * 14f
            : 0f;

        private float PressStroke(float at) {
            if (timer < at) {
                return 0f;
            }
            float k = MathHelper.Clamp((timer - at) / 18f, 0f, 1f);
            float e = k * k * (3f - 2f * k);
            return 1f - 0.6f * e;
        }

        /// <summary>鬼影扭动瞬时值:铭刻吃凝滞拍,续契随俯首入定,收伏随按压逐拍熄火</summary>
        private float CurrentWrithe => riteKind switch {
            WraithRiteKind.RenewPact => MathHelper.Lerp(0.5f, 0.18f, BowEase),
            WraithRiteKind.Resubdue => 0.5f + 1.1f * Struggle,
            _ => 0.85f * FreezeFactor,
        };

        /// <summary>鬼火之眼睁量:铭刻斩前即熄,续契押印时阖眼,收伏睁得早熄于钉印起落</summary>
        private float EyeOpenAmount {
            get {
                if (entry is not { HasEyes: true }) {
                    return 0f;
                }
                return riteKind switch {
                    WraithRiteKind.RenewPact => MathHelper.Clamp((timer - 26f) / 18f, 0f, 1f)
                        * (1f - MathHelper.Clamp((timer - PressStartAt) / 14f, 0f, 1f)),
                    WraithRiteKind.Resubdue => MathHelper.Clamp((timer - 6f) / 8f, 0f, 1f)
                        * (1f - MathHelper.Clamp((timer - NailStartAt) / 8f, 0f, 1f)),
                    _ => MathHelper.Clamp((timer - 38f) / 14f, 0f, 1f)
                        * (1f - MathHelper.Clamp((timer - TSlash) / 5f, 0f, 1f)),
                };
            }
        }

        public override void Update() {
            if (IsOpen) {
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            if (IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }

            timer += 1f;
            particles.Update();
            inkAge = Math.Min(inkAge + 1f, 60f);

            if (riteKind == WraithRiteKind.FirstBind) {
                int chars = TypeChars;
                if (chars != lastTypeChars) {
                    lastTypeChars = chars;
                    inkAge = 0f;
                }
            }

            switch (riteKind) {
                case WraithRiteKind.RenewPact:
                    UpdateRenewBeats();
                    break;
                case WraithRiteKind.Resubdue:
                    UpdateResubdueBeats();
                    break;
                default:
                    UpdateFirstBindBeats();
                    break;
            }

            UpdateStillAmbience();

            //点击:静场前跳拍,静场后收卷
            if (IsOpen && a > 0.6f && keyLeftPressState == KeyPressState.Pressed) {
                if (timer < StillAt) {
                    SkipToStill();
                }
                else {
                    Close();
                }
            }
        }

        /// <summary>铭刻一次性事件:刀痕落下 / 烟碎凝墨 / 朱印砸章</summary>
        private void UpdateFirstBindBeats() {
            //刀痕落下的一拍:音效+屏震+烟碎(凝滞预备拍本身是无声的,静默即蓄力)
            if (!slashPlayed && timer >= TSlash) {
                slashPlayed = true;
                SoundEngine.PlaySound(CWRSound.SwiftSlice with { Volume = 0.6f, Pitch = -0.05f, MaxInstances = 1 });
                player.CWR().GetScreenShake(4f);
            }

            //烟凝成字:碎裂时刻一次性放出墨粒,目标为各字符落位
            if (!motesSpawned && timer >= TBreak) {
                motesSpawned = true;
                SpawnCondenseMotes();
            }

            //朱印砸章
            if (!stampPlayed && timer >= StampHitAt) {
                stampPlayed = true;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f, Pitch = -0.6f, MaxInstances = 1 });
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 1 });
                player.CWR().GetScreenShake(6f);
                //章底灰飞
                Vector2 sealPos = SealCenter();
                for (int i = 0; i < 10; i++) {
                    particles.SpawnAsh(sealPos + Main.rand.NextVector2Circular(9f, 5f));
                }
            }
        }

        /// <summary>续契一次性事件:湿墨起润(水音) / 朱印覆押压实(轻震,不砸)</summary>
        private void UpdateRenewBeats() {
            //墨露提前一口气起飞(飞行约 30 帧),抵达恰逢各字重润
            if (!rewetCued && timer >= TRewet - 26f) {
                rewetCued = true;
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.32f, Pitch = 0.45f, MaxInstances = 1 });
                SpawnRewetMotes();
            }

            if (!stampPlayed && timer >= PressDoneAt) {
                stampPlayed = true;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.42f, Pitch = -0.2f, MaxInstances = 1 });
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.3f, Pitch = 0.05f, MaxInstances = 1 });
                player.CWR().GetScreenShake(2.5f);
                Vector2 sealPos = SealCenter();
                for (int i = 0; i < 5; i++) {
                    particles.SpawnAsh(sealPos + Main.rand.NextVector2Circular(7f, 4f));
                }
            }
        }

        /// <summary>收伏一次性事件:按压两拍渐强 / 钉死重冲击+墨爆;挣扎期墨字甩滴</summary>
        private void UpdateResubdueBeats() {
            if (!press1Played && timer >= TPress1) {
                press1Played = true;
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 1 });
                player.CWR().GetScreenShake(3f);
                SpawnPressAsh(6);
            }

            if (!press2Played && timer >= TPress2) {
                press2Played = true;
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Volume = 0.68f, Pitch = -0.62f, MaxInstances = 1 });
                player.CWR().GetScreenShake(4.5f);
                SpawnPressAsh(9);
            }

            if (!stampPlayed && timer >= NailHitAt) {
                stampPlayed = true;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.75f, MaxInstances = 1 });
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 1 });
                player.CWR().GetScreenShake(8f);
                SpawnNailBurst();
            }

            //挣扎期:被拖拽的字一滴一滴往下甩墨
            if (timer > 10f && timer < NailStartAt && (int)timer % 9 == 0 && NameText.Length > 0) {
                Vector2 charPos = CharAnchor(FontAssets.MouseText.Value, Main.rand.Next(NameText.Length));
                particles.SpawnAsh(charPos + Main.rand.NextVector2Circular(8f, 8f));
            }
        }

        /// <summary>静场余韵:铭刻与续契落瓣两翼,收伏无落瓣的安宁,只余残烟低回</summary>
        private void UpdateStillAmbience() {
            if (timer < StillAt) {
                return;
            }
            stillFxTimer++;
            if (riteKind == WraithRiteKind.Resubdue) {
                if (stillFxTimer >= 34) {
                    stillFxTimer = 0;
                    particles.SpawnAsh(SilhouetteCenter + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(10f, 70f)));
                    if (Main.rand.NextBool()) {
                        particles.SpawnAsh(SealCenter() + Main.rand.NextVector2Circular(12f, 6f));
                    }
                }
                return;
            }
            if (stillFxTimer >= 46) {
                stillFxTimer = 0;
                bool left = Main.rand.NextBool();
                float x = Center.X + (left ? -1f : 1f) * Main.rand.NextFloat(180f, 250f);
                particles.SpawnPetal(new Vector2(x, Center.Y - 210f), left ? -1f : 1f);
            }
        }

        /// <summary>跳拍:时间轴直接推进到静场,补发未发生的一次性事件(不再重复演出音效)</summary>
        private void SkipToStill() {
            motesSpawned = true;
            slashPlayed = true;
            stampPlayed = true;
            rewetCued = true;
            press1Played = true;
            press2Played = true;
            timer = StillAt;
            lastTypeChars = NameText.Length;
            inkAge = 60f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.4f });
        }

        /// <summary>烟碎为墨:自鬼影范围放出墨粒,弧线收束到每个字符的落位</summary>
        private void SpawnCondenseMotes() {
            string name = NameText;
            if (name.Length == 0) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 silhouette = SilhouetteCenter;
            int motesPerChar = Math.Max(3, 26 / name.Length);
            for (int c = 0; c < name.Length; c++) {
                Vector2 charPos = CharAnchor(font, c);
                for (int j = 0; j < motesPerChar; j++) {
                    Vector2 from = silhouette + Main.rand.NextVector2Circular(66f, 74f);
                    Color col = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Deep, Main.rand.NextFloat(0.6f));
                    //按字符次序排延迟,墨是一笔一笔归位的
                    particles.SpawnInkMote(from, charPos + Main.rand.NextVector2Circular(4f, 4f), col, c * CharInterval + j * 1.3f);
                }
            }
        }

        /// <summary>重润墨露:少量湿墨自俯首的鬼影处依字序缓缓落向各字根部</summary>
        private void SpawnRewetMotes() {
            string name = NameText;
            if (name.Length == 0) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 from0 = SilhouetteCenter + BowOffset;
            for (int c = 0; c < name.Length; c++) {
                Vector2 charPos = CharAnchor(font, c);
                for (int j = 0; j < 3; j++) {
                    Vector2 from = from0 + Main.rand.NextVector2Circular(30f, 44f);
                    Color col = Color.Lerp(OnikiriUITheme.Seal, OnikiriUITheme.Deep, Main.rand.NextFloat());
                    particles.SpawnInkMote(from, charPos + Main.rand.NextVector2Circular(3f, 3f), col, c * RewetStep + j * 2.1f);
                }
            }
        }

        /// <summary>按压拍灰迸:自鬼影脚下迸出一圈香灰</summary>
        private void SpawnPressAsh(int count) {
            Vector2 basePos = SilhouetteCenter + new Vector2(0f, 58f + ResubduePush);
            for (int i = 0; i < count; i++) {
                particles.SpawnAsh(basePos + Main.rand.NextVector2Circular(30f, 8f));
            }
        }

        /// <summary>钉死墨爆:墨粒自印心炸开四散 + 章底灰飞</summary>
        private void SpawnNailBurst() {
            Vector2 sealPos = SealCenter();
            for (int i = 0; i < 14; i++) {
                float ang = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(0.3f);
                Vector2 to = sealPos + ang.ToRotationVector2() * Main.rand.NextFloat(26f, 54f);
                Color col = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Deep, Main.rand.NextFloat(0.7f));
                particles.SpawnInkMote(sealPos, to, col, 0f);
            }
            for (int i = 0; i < 10; i++) {
                particles.SpawnAsh(sealPos + Main.rand.NextVector2Circular(10f, 6f));
            }
        }

        /// <summary>第 index 个字符的屏幕落位(竖排沿列向下,拉丁横排沿行向右)</summary>
        private Vector2 CharAnchor(DynamicSpriteFont font, int index) {
            string name = NameText;
            const float NameScale = 1.28f;
            if (NameIsVertical) {
                float charH = font.MeasureString("字").Y * NameScale + 5f;
                Vector2 top = Center + new Vector2(96f, -MathF.Min(name.Length, 6f) * charH * 0.5f);
                return top + new Vector2(0f, index * charH + charH * 0.5f);
            }
            Vector2 size = font.MeasureString(name) * NameScale;
            Vector2 left = Center + new Vector2(40f - size.X * 0.5f, -66f);
            float w = font.MeasureString(name[..index]).X * NameScale;
            return left + new Vector2(w + font.MeasureString(name[index].ToString()).X * NameScale * 0.5f, size.Y * 0.5f);
        }

        private Vector2 SealCenter() {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string name = NameText;
            const float NameScale = 1.28f;
            if (NameIsVertical) {
                float charH = font.MeasureString("字").Y * NameScale + 5f;
                Vector2 top = Center + new Vector2(96f, -MathF.Min(name.Length, 6f) * charH * 0.5f);
                return top + new Vector2(0f, name.Length * charH + 26f);
            }
            Vector2 size = font.MeasureString(name) * NameScale;
            return Center + new Vector2(40f + size.X * 0.5f + 30f, -66f + size.Y * 0.5f);
        }

        /// <summary>背光强度包络:铭刻凝滞收息斩落放回,续契温息呼吸,收伏挣扎搏动叠按压闪</summary>
        private float BacklightAlpha {
            get {
                switch (riteKind) {
                    case WraithRiteKind.RenewPact:
                        return 0.46f + 0.12f * OnikiriUITheme.Breath(GlobalTimer, 2.6f, 1.6f);
                    case WraithRiteKind.Resubdue: {
                        float throb = (0.10f + 0.18f * Struggle) * Math.Abs((float)Math.Sin(GlobalTimer * 5.1f));
                        float flash = 0.22f * (Flash(TPress1, 14f) + Flash(TPress2, 14f)) + 0.30f * Flash(NailHitAt);
                        return Math.Min(0.9f, 0.42f + throb + flash);
                    }
                    default: {
                        float dim = (1f - FreezeFactor) * (1f - MathHelper.Clamp((timer - TSlash) / 5f, 0f, 1f));
                        return 0.55f - 0.18f * dim;
                    }
                }
            }
        }

        private float BacklightRadius => riteKind switch {
            WraithRiteKind.RenewPact => 300f,
            WraithRiteKind.Resubdue => 345f,
            _ => 320f,
        };

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f || entry == null) {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 center = Center;

            //====压暗背景:全屏墨罩 + 构图后方一团语义化背光(背景层随光标轻微反向视差)====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.74f));
            Vector2 parallax = (OnikiriUITheme.UIMouse - center) * -0.012f;
            OniBrush.DrawBacklight(spriteBatch, center + parallax, BacklightRadius, KindBacklight, a * BacklightAlpha);

            //====小题（铭 刻 / 续 契 / 收 伏）+ 两笔角签====
            float titleA = MathHelper.Clamp((timer - 8f) / 22f, 0f, 1f) * a;
            if (titleA > 0.01f) {
                string title = KindTitle.Value;
                Vector2 tSize = font.MeasureString(title) * 0.92f;
                Vector2 tPos = center + new Vector2(-tSize.X * 0.5f, -206f);
                Utils.DrawBorderString(spriteBatch, title, tPos, OnikiriUITheme.HotWhite * titleA, 0.92f);
                OniBrush.DrawTaperedSlash(spriteBatch, tPos + new Vector2(-26f, tSize.Y * 0.55f), tPos + new Vector2(-6f, tSize.Y * 0.5f), 1.6f, 0.5f, titleA * 0.8f);
                OniBrush.DrawTaperedSlash(spriteBatch, tPos + new Vector2(tSize.X + 6f, tSize.Y * 0.5f), tPos + new Vector2(tSize.X + 26f, tSize.Y * 0.45f), 1.6f, 0.5f, titleA * 0.8f);
            }

            //====鬼影(语义化运动),吃同一份背景视差====
            DrawSilhouette(spriteBatch, a, parallax);

            //====白热刀痕:仅铭刻,扫过鬼影,收势后余温残留====
            if (riteKind == WraithRiteKind.FirstBind) {
                float sweep = SlashSweep;
                if (sweep > 0.02f) {
                    float afterglow = timer <= TSlash + 9f ? 1f : Math.Max(0.25f, 1f - (timer - TSlash - 9f) / 40f);
                    Vector2 s = SilhouetteCenter + new Vector2(-128f, 92f);
                    Vector2 e = SilhouetteCenter + new Vector2(120f, -104f);
                    OniBrush.DrawTaperedSlash(spriteBatch, s, e, 7f, 9f, a * afterglow, sweep);
                }
            }

            //====按压拍冲击环:仅收伏,自鬼影处扩散,第二拍更重====
            if (riteKind == WraithRiteKind.Resubdue) {
                DrawPressRing(spriteBatch, a, TPress1, 0.6f);
                DrawPressRing(spriteBatch, a, TPress2, 0.85f);
            }

            //====墨粒/香灰/落瓣====
            particles.Draw(spriteBatch, a);

            //====名讳(三语义各一套写法)====
            DrawName(spriteBatch, font, a);

            //====赋力小字====
            float powerA = MathHelper.Clamp((timer - PowerRevealAt) / 24f, 0f, 1f) * a;
            if (powerA > 0.01f && PowerText.Length > 0) {
                string power = PowerText;
                Vector2 pSize = font.MeasureString(power) * 0.78f;
                Vector2 pPos = center + new Vector2(-pSize.X * 0.5f, 158f);
                Utils.DrawBorderString(spriteBatch, power, pPos, OnikiriUITheme.TextDim * powerA, 0.78f);
                OniBrush.DrawTaperedSlash(spriteBatch, pPos + new Vector2(-4f, -8f), pPos + new Vector2(pSize.X + 4f, -9f), 1.4f, 1.2f, powerA * 0.5f);
            }

            //====朱印(砸章/覆押/钉死)====
            DrawStamp(spriteBatch, a);

            //====静场提示(收伏的静场没有"归卷"的安宁,只有押回)====
            float hintA = MathHelper.Clamp((timer - StillAt - 12f) / 30f, 0f, 1f) * a;
            if (hintA > 0.01f) {
                string hint = (riteKind == WraithRiteKind.Resubdue ? RiteHintResubdue : RiteHint).Value;
                float pulse = OnikiriUITheme.Breath(GlobalTimer, 1.3f, 2.2f);
                Vector2 hSize = font.MeasureString(hint) * 0.72f;
                Utils.DrawBorderString(spriteBatch, hint,
                    new Vector2(center.X - hSize.X * 0.5f, OnikiriUITheme.UIScreenH - 92f),
                    OnikiriUITheme.TextDim * (hintA * (0.55f + pulse * 0.35f)), 0.72f);
            }
        }

        /// <summary>按压冲击环:落在鬼影身上而非印上,一拍比一拍把它按矮</summary>
        private void DrawPressRing(SpriteBatch sb, float a, float at, float strength) {
            float flash = Flash(at, 16f);
            if (flash <= 0.01f) {
                return;
            }
            Texture2D ring = CWRAsset.Ring01.Value;
            Vector2 pos = SilhouetteCenter + new Vector2(0f, 26f + ResubduePush);
            float diameter = 46f + (1f - flash) * 74f;
            sb.Draw(ring, pos, null, OnikiriUITheme.Deep * (a * flash * strength), 0f,
                ring.Size() * 0.5f, diameter / ring.Width, SpriteEffects.None, 0f);
        }

        /// <summary>鬼影,shader 或 CPU 烟团,同吃运动/色/构图三轴</summary>
        private void DrawSilhouette(SpriteBatch sb, float a, Vector2 parallax) {
            //收伏的挣脱体破门而入,不给它慢慢显形
            float grow = riteKind == WraithRiteKind.Resubdue
                ? MathHelper.Clamp(timer / 18f, 0f, 1f)
                : MathHelper.Clamp(timer / 42f, 0f, 1f);
            grow = grow * (2f - grow);
            float break_ = SilhouetteBreak;
            float writhe = entry.State == OniGhostState.Sealed ? 0.12f : CurrentWrithe;
            float sgl = Struggle;
            float push = ResubduePush;

            //语义位移:续契俯首,收伏被逐拍按矮并狂乱抛掷
            Vector2 kindOffset = BowOffset + new Vector2(0f, push);
            if (sgl > 0.01f) {
                kindOffset += new Vector2((float)Math.Sin(GlobalTimer * 3.1f) * 9f, (float)Math.Sin(GlobalTimer * 2.45f + 1.7f) * 5f) * sgl;
            }
            Vector2 basePos = SilhouetteCenter + parallax + kindOffset;

            if (OniGhostShadowDraw.Available) {
                float bodyA = a * grow;
                if (bodyA <= 0.01f || break_ >= 0.999f) {
                    return;
                }
                //按压自顶向下压缩 quad 并整体下沉:整只鬼被按矮压进纸面
                int squash = (int)(push * 2f);
                Rectangle quad = new((int)(basePos.X - 108f), (int)(basePos.Y - 148f) + squash, 216, 286 - squash);
                OniGhostShadowDraw.Draw(sb, quad, new OniGhostShadowParams {
                    Writhe = writhe,
                    Break = break_,
                    EyeOpen = EyeOpenAmount,
                    Glance = Vector2.Zero,
                    Seed = OniGhostShadowDraw.SeedFromKey(entry.Key),
                    Alpha = bodyA,
                    Time = GlobalTimer,
                });
                return;
            }

            float alpha = a * grow * (1f - break_);
            if (alpha <= 0.01f) {
                return;
            }

            Texture2D smoke = CWRAsset.SmokeSheet01.Value;
            int frameSize = smoke.Width / 2;
            Vector2 origin = new(frameSize * 0.5f);
            //运动轴:摆幅走扭动瞬时值;构图轴:续契收拢挨近名讳,收伏挣扎外抛散架
            float sway = 0.35f + writhe * 0.8f;
            float spread = riteKind switch {
                WraithRiteKind.RenewPact => 0.78f,
                WraithRiteKind.Resubdue => 1f + 0.35f * sgl,
                _ => 1f,
            };

            for (int i = 0; i < 3; i++) {
                int frame = (int)(GlobalTimer * 5f + i * 1.7f) % 4;
                Rectangle srcRect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
                float phase = i * 2.1f;
                Vector2 offset = new((float)Math.Sin(GlobalTimer * (0.9f + i * 0.28f) + phase) * (7f + i * 4f) * sway * spread,
                    -20f + i * 22f + (float)Math.Cos(GlobalTimer * 0.7f + phase) * 4f * sway);
                offset *= 1f + break_ * 2.2f;
                float scale = (0.34f + i * 0.075f) * (1f + break_ * 0.5f) * (0.8f + grow * 0.2f);
                float rot = (float)Math.Sin(GlobalTimer * 0.4f + phase) * 0.22f * (0.5f + sway * 0.5f) + break_ * (i - 1) * 0.5f;

                //色轴:铭刻墨黑压深,续契沁青纱(驯),收伏渗血墨(狂)
                Color body = riteKind switch {
                    WraithRiteKind.RenewPact => Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.GhostDim, 0.16f + i * 0.10f),
                    WraithRiteKind.Resubdue => Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Deep, 0.22f + i * 0.14f),
                    _ => Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, i * 0.3f),
                } * (alpha * (0.88f - i * 0.14f));
                sb.Draw(smoke, basePos + offset, srcRect, body, rot, origin, scale, SpriteEffects.None, 0f);
            }
            //一缕纱压在轮廓上:铭/续为鬼火青纱,收伏改血纱(被按的鬼不是安分的烟)
            {
                int frame = (int)(GlobalTimer * 4f + 2f) % 4;
                Rectangle srcRect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
                Vector2 offset = new((float)Math.Sin(GlobalTimer * 1.1f) * 6f * sway, -34f);
                Color veil = riteKind == WraithRiteKind.Resubdue
                    ? OnikiriUITheme.Bright * (alpha * 0.16f)
                    : OnikiriUITheme.GhostDim * (alpha * 0.22f);
                sb.Draw(smoke, basePos + offset * (1f + break_ * 2f), srcRect, veil, 0.1f, origin, 0.30f, SpriteEffects.None, 0f);
            }

            //鬼火之眼(CPU 像素版):睁闭窗口与 shader 分支同源,收伏闪得急,续契闪得缓
            float eyeA = EyeOpenAmount;
            if (eyeA > 0.01f) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                float flickSpeed = riteKind switch {
                    WraithRiteKind.Resubdue => 11.5f,
                    WraithRiteKind.RenewPact => 4.6f,
                    _ => 7.3f,
                };
                float flick = 0.75f + 0.25f * (float)Math.Sin(GlobalTimer * flickSpeed);
                Vector2 eyeSway = new((float)Math.Sin(GlobalTimer * 0.9f) * 7f * sway, (float)Math.Cos(GlobalTimer * 0.7f) * 4f * sway);
                //左右眼步进循环,逐帧零分配(镜像 WraithActor.DrawBody 的 side 惯例)
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 eye = basePos + eyeSway + new Vector2(9f * side, side < 0 ? -46f : -44f);
                    sb.Draw(pixel, eye, src, OnikiriUITheme.GhostDim * (a * eyeA * 0.5f * flick), 0f, new Vector2(0.5f), new Vector2(5.6f, 4.4f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, eye, src, OnikiriUITheme.GhostFire * (a * eyeA * 0.95f * flick), 0f, new Vector2(0.5f), new Vector2(2.6f, 2.0f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawName(SpriteBatch sb, DynamicSpriteFont font, float a) {
            switch (riteKind) {
                case WraithRiteKind.RenewPact:
                    DrawNameRenew(sb, font, a);
                    break;
                case WraithRiteKind.Resubdue:
                    DrawNameResubdue(sb, font, a);
                    break;
                default:
                    DrawNameFirstBind(sb, font, a);
                    break;
            }
        }

        /// <summary>铭刻名讳打字机:CJK 竖排/拉丁横排,最新字符叠湿墨绯罩</summary>
        private void DrawNameFirstBind(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string name = NameText;
            int chars = TypeChars;
            if (chars <= 0 || name.Length == 0) {
                return;
            }
            const float NameScale = 1.28f;
            float ink = 1f - MathHelper.Clamp(inkAge / 16f, 0f, 1f);

            for (int i = 0; i < chars && i < name.Length; i++) {
                Vector2 cPos = CharAnchor(font, i);
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * NameScale;
                Vector2 drawPos = cPos - size * 0.5f;
                Utils.DrawBorderString(sb, s, drawPos, OnikiriUITheme.Paper * a, NameScale);
                //湿墨:最新一字覆一层随时间褪去的绯红
                if (i == chars - 1 && ink > 0.02f) {
                    Utils.DrawBorderString(sb, s, drawPos, OnikiriUITheme.Bright * (a * 0.8f * ink), NameScale);
                }
            }
        }

        /// <summary>续契名讳:旧字以干墨残迹先现,湿墨自 TRewet 起一字一拍重润提亮回正墨</summary>
        private void DrawNameRenew(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string name = NameText;
            if (name.Length == 0) {
                return;
            }
            const float NameScale = 1.28f;
            float dryIn = MathHelper.Clamp((timer - 6f) / 20f, 0f, 1f);
            if (dryIn <= 0.01f) {
                return;
            }
            for (int i = 0; i < name.Length; i++) {
                Vector2 cPos = CharAnchor(font, i);
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * NameScale;
                Vector2 drawPos = cPos - size * 0.5f;
                float wet = MathHelper.Clamp((timer - TRewet - i * RewetStep) / RewetStep, 0f, 1f);
                //干墨近灰,润过归位纸白正墨
                Color body = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, wet) * (a * dryIn * (0.42f + 0.58f * wet));
                Utils.DrawBorderString(sb, s, drawPos, body, NameScale);
                //重润行笔:润到一半的字覆一层湿绯,润完褪去
                if (wet > 0.01f && wet < 0.999f) {
                    float flush = (float)Math.Sin(wet * Math.PI);
                    Utils.DrawBorderString(sb, s, drawPos, OnikiriUITheme.Bright * (a * 0.7f * flush), NameScale);
                }
            }
        }

        /// <summary>收伏名讳,拖拽错位,按压归位</summary>
        private void DrawNameResubdue(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string name = NameText;
            if (name.Length == 0) {
                return;
            }
            const float NameScale = 1.28f;
            float bornIn = MathHelper.Clamp(timer / 14f, 0f, 1f);
            float sgl = Struggle;
            float nailFlash = Flash(NailHitAt);

            for (int i = 0; i < name.Length; i++) {
                Vector2 cPos = CharAnchor(font, i);
                string ch = name[i].ToString();
                Vector2 size = font.MeasureString(ch) * NameScale;
                //拖拽:双谐波错位 + 每字固定撕扯偏置,烈度随按压塌落
                Vector2 drag = new Vector2(
                    (float)Math.Sin(GlobalTimer * 2.3f + i * 2.7f) * 4.6f + (OniBrush.Hash01(i * 131) - 0.5f) * 7f,
                    (float)Math.Cos(GlobalTimer * 1.9f + i * 1.3f) * 3.2f) * sgl;
                Vector2 drawPos = cPos - size * 0.5f + drag;
                //血墨残影:沿被拖拽的方向渗开一层
                if (sgl > 0.03f) {
                    Utils.DrawBorderString(sb, ch, drawPos + drag * 1.6f, OnikiriUITheme.Bright * (a * bornIn * 0.30f * sgl), NameScale);
                }
                Utils.DrawBorderString(sb, ch, drawPos, OnikiriUITheme.Paper * (a * bornIn), NameScale);
                //钉死一帧:全名闪定一层湿绯,随冲击退去
                if (nailFlash > 0.02f) {
                    Utils.DrawBorderString(sb, ch, drawPos, OnikiriUITheme.Bright * (a * 0.55f * nailFlash), NameScale);
                }
            }
        }

        private void DrawStamp(SpriteBatch sb, float a) {
            switch (riteKind) {
                case WraithRiteKind.RenewPact:
                    DrawStampRenew(sb, a);
                    break;
                case WraithRiteKind.Resubdue:
                    DrawStampResubdue(sb, a);
                    break;
                default:
                    DrawStampFirstBind(sb, a);
                    break;
            }
        }

        /// <summary>铭刻朱印:自高处带旋压落,命中一帧起冲击环</summary>
        private void DrawStampFirstBind(SpriteBatch sb, float a) {
            float p = StampProgress;
            if (p <= 0.001f) {
                return;
            }
            float ease = p * p * (3f - 2f * p);
            Vector2 sealPos = SealCenter();
            float scale = MathHelper.Lerp(2.0f, 1f, ease);
            float rot = MathHelper.Lerp(0.34f, 0.05f, ease);
            float integrity = entry.State == OniGhostState.Restless ? entry.Mastery + 0.25f : 1f;
            OniBrush.DrawSealGlyph(sb, sealPos, 15f * scale, a * (0.35f + ease * 0.65f), rot, MathHelper.Clamp(integrity, 0f, 1f));

            float flash = Flash(StampHitAt);
            if (flash > 0.01f) {
                Texture2D ring = CWRAsset.Ring01.Value;
                //冲击环:直径自 30px 扩至 100px,随扩散淡出
                float diameter = 30f + (1f - flash) * 70f;
                sb.Draw(ring, sealPos, null, OnikiriUITheme.Seal * (a * flash * 0.8f), 0f,
                    ring.Size() * 0.5f, diameter / ring.Width, SpriteEffects.None, 0f);
            }
        }

        /// <summary>续契朱印:旧印半旧带裂先在,新印半透明对位覆押缓缓压实,非砸下</summary>
        private void DrawStampRenew(SpriteBatch sb, float a) {
            Vector2 sealPos = SealCenter();
            float oldIn = MathHelper.Clamp((timer - 10f) / 18f, 0f, 1f);
            float p = MathHelper.Clamp((timer - PressStartAt) / PressDur, 0f, 1f);
            float ease = p * p * (3f - 2f * p);

            //旧印:褪色开裂微歪,新印压实后隐去
            if (oldIn > 0.01f) {
                OniBrush.DrawSealGlyph(sb, sealPos + new Vector2(1.5f, 1f), 14f, a * oldIn * 0.42f * (1f - ease * 0.8f), 0.17f, 0.55f);
            }
            //新印:自旧印的歪角对位转正,透明度随压实攀升
            if (p > 0.001f) {
                float scale = MathHelper.Lerp(1.18f, 1f, ease);
                float rot = MathHelper.Lerp(0.17f, 0.03f, ease);
                OniBrush.DrawSealGlyph(sb, sealPos, 15f * scale, a * (0.15f + ease * 0.85f), rot, 1f);
            }

            float flash = Flash(PressDoneAt);
            if (flash > 0.01f) {
                Texture2D ring = CWRAsset.Ring01.Value;
                float diameter = 24f + (1f - flash) * 44f;
                sb.Draw(ring, sealPos, null, OnikiriUITheme.Seal * (a * flash * 0.55f), 0f,
                    ring.Size() * 0.5f, diameter / ring.Width, SpriteEffects.None, 0f);
            }
        }

        /// <summary>收伏朱印:只加速不减速的钉死坠落,双层冲击环,印面带勉强压住的裂痕</summary>
        private void DrawStampResubdue(SpriteBatch sb, float a) {
            float p = MathHelper.Clamp((timer - NailStartAt) / NailDrop, 0f, 1f);
            if (p <= 0.001f) {
                return;
            }
            float ease = p * p;
            Vector2 sealPos = SealCenter();
            float scale = MathHelper.Lerp(2.8f, 1f, ease);
            float rot = MathHelper.Lerp(0.55f, 0.04f, ease);
            float integrity = MathHelper.Clamp(entry.Mastery + 0.35f, 0f, 1f);
            OniBrush.DrawSealGlyph(sb, sealPos, 16f * scale, a * (0.25f + ease * 0.75f), rot, integrity);

            float flash = Flash(NailHitAt, 22f);
            if (flash > 0.01f) {
                Texture2D ring = CWRAsset.Ring01.Value;
                float diameter = 34f + (1f - flash) * 106f;
                sb.Draw(ring, sealPos, null, OnikiriUITheme.Seal * (a * flash * 0.9f), 0f,
                    ring.Size() * 0.5f, diameter / ring.Width, SpriteEffects.None, 0f);
                //里圈血环:重冲击的第二层波
                float inner = 20f + (1f - flash) * 60f;
                sb.Draw(ring, sealPos, null, OnikiriUITheme.Bright * (a * flash * 0.5f), 0f,
                    ring.Size() * 0.5f, inner / ring.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
