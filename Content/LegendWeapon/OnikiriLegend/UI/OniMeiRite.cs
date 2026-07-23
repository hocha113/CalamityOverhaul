using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>鏨仪式三语义</summary>
    internal enum OniMeiRiteKind : byte
    {
        /// <summary>凿铭,空位落鏨</summary>
        Engrave,
        /// <summary>改铭,先锉旧再凿新</summary>
        Rename,
        /// <summary>除铭,只锉不凿</summary>
        Erase,
    }

    /// <summary>
    /// 鏨仪式演出态,<see cref="OniMeiUI"/> 内嵌(演出中吞输入);
    /// 数据在点击时已落 <see cref="OniMeiStore"/>,这里纯演出;
    /// 纯函数于 <see cref="timer"/>,可 <see cref="Skip"/>
    /// </summary>
    internal sealed class OniMeiRite
    {
        //====时间轴(帧)====
        private const float TFocus = 30f;       //定鏨:压暗,鏨具就位,一拍屏息
        private const float StrokePeriod = 12f; //每笔落鏨到拖凿完的时长
        private const float TFile = 46f;        //锉去旧铭(改/除铭前置)
        private const float TFinish = 42f;      //收尾:打粉一扑+油布一抹
        private const float TGold = 34f;        //金阶:熔金入缝
        private const float TSettle = 20f;      //除铭:锉净后的一口气

        public bool Active { get; private set; }
        public OniMeiRiteKind Kind { get; private set; }
        public OniMeiSlotKind Slot { get; private set; }
        /// <summary>被锉去的旧铭 Key(改/除铭),无则 null</summary>
        public string OldKey { get; private set; }
        /// <summary>新凿的铭 Key(凿/改铭),除铭 null</summary>
        public string NewKey { get; private set; }
        /// <summary>新铭金阶(点亮/填缝色走金)</summary>
        public bool GoldTier { get; private set; }

        private float timer;
        private int newStrokes;
        private int lastStrikeIndex;
        private int fileTickTimer;
        private bool powderBurst;
        private bool goldCued;
        private bool doneCued;
        private float shake;
        private float shakeSeed;

        //====派生节点(Start 时按笔数定死)====
        private float carveStart;    //落鏨起点(锉拍之后)
        private float carveEnd;
        private float finishEnd;
        private float total;

        /// <summary>演出总长(帧)</summary>
        public float Total => total;

        /// <summary>开演;oldKey=被锉的旧铭(可 null),newKey=要凿的新铭(除铭 null)</summary>
        public void Start(OniMeiRiteKind kind, OniMeiSlotKind slot, string oldKey, string newKey) {
            Kind = kind;
            Slot = slot;
            OldKey = oldKey;
            NewKey = newKey;
            GoldTier = newKey != null && OniMeiRegistry.TryGet(newKey, out OniMeiDefinition def) && def.IsGoldTier;
            newStrokes = newKey != null ? OniMeiGlyph.StrokeCount(newKey) : 0;

            float filePhase = kind == OniMeiRiteKind.Engrave ? 0f : TFile;
            carveStart = filePhase + (kind == OniMeiRiteKind.Erase ? 0f : TFocus);
            carveEnd = carveStart + newStrokes * StrokePeriod;
            if (kind == OniMeiRiteKind.Erase) {
                carveEnd = filePhase;
                finishEnd = carveEnd + TSettle;
            }
            else {
                finishEnd = carveEnd + TFinish + (GoldTier ? TGold : 0f);
            }
            total = finishEnd;

            timer = 0f;
            lastStrikeIndex = -1;
            fileTickTimer = 0;
            powderBurst = false;
            goldCued = false;
            doneCued = false;
            shake = 0f;
            shakeSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            Active = true;
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.7f, Volume = 0.3f });
        }

        /// <summary>跳到定格:一切读数落到完成态</summary>
        public void Skip() {
            if (!Active) {
                return;
            }
            timer = total;
            shake = 0f;
        }

        /// <summary>演出推进;chiselAnchor=铭位屏幕位,glyphSize=铭位字形尺寸</summary>
        public void Update(Vector2 chiselAnchor, float glyphSize, float rotation, OniUIParticlePool particles) {
            if (!Active) {
                return;
            }
            timer += 1f;
            shake *= 0.82f;

            //锉拍:铁屑簌簌,锉声一下一下
            if (OldKey != null && timer <= TFile) {
                fileTickTimer++;
                if (fileTickTimer >= 13) {
                    fileTickTimer = 0;
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.6f, Volume = 0.28f });
                    shake = MathF.Max(shake, 0.7f);
                }
                if (timer % 4f < 1f) {
                    Vector2 at = chiselAnchor + Main.rand.NextVector2Circular(glyphSize * 0.4f, glyphSize * 0.3f);
                    particles.SpawnFiling(at);
                }
            }

            //落鏨拍:每笔起点一记鏨响+火星迸溅+微震
            if (NewKey != null && timer >= carveStart && timer <= carveEnd + 1f) {
                int strike = Math.Min((int)((timer - carveStart) / StrokePeriod), newStrokes - 1);
                if (strike != lastStrikeIndex) {
                    lastStrikeIndex = strike;
                    SoundEngine.PlaySound(SoundID.Tink with {
                        Pitch = 0.15f + Main.rand.NextFloat(0.25f),
                        Volume = 0.5f,
                    });
                    shake = 1f;
                    Vector2 tip = OniMeiGlyph.GetChiselPoint(NewKey, chiselAnchor, glyphSize, rotation, NewReveal);
                    int burst = Main.rand.Next(4, 8);
                    for (int i = 0; i < burst; i++) {
                        particles.SpawnSpark(tip);
                    }
                }
                //拖凿途中偶发一两粒碎星
                else if (Main.rand.NextBool(5)) {
                    particles.SpawnSpark(OniMeiGlyph.GetChiselPoint(NewKey, chiselAnchor, glyphSize, rotation, NewReveal));
                }
            }

            //打粉:收尾起点一扑白雾
            if (NewKey != null && !powderBurst && timer >= carveEnd + 4f) {
                powderBurst = true;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f, Volume = 0.3f });
                for (int i = 0; i < 14; i++) {
                    particles.SpawnPowder(chiselAnchor + Main.rand.NextVector2Circular(glyphSize * 0.5f, glyphSize * 0.35f));
                }
            }

            //熔金入缝
            if (GoldTier && !goldCued && timer >= carveEnd + TFinish) {
                goldCued = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.5f, Volume = 0.4f });
            }

            //完成:纳刀般的一声轻锵
            if (!doneCued && timer >= total - 1f) {
                doneCued = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.35f, Volume = 0.4f });
            }

            if (timer >= total + 8f) {
                Active = false;
            }
        }

        //====纯函数读数(Draw 用)====

        /// <summary>压暗强度 0~0.5,两端缓入缓出</summary>
        public float Dim {
            get {
                if (!Active) {
                    return 0f;
                }
                float head = MathHelper.Clamp(timer / 14f, 0f, 1f);
                float tail = MathHelper.Clamp((total - timer) / 14f, 0f, 1f);
                return 0.45f * MathF.Min(head, tail);
            }
        }

        /// <summary>本帧震offset(px),落鏨的顿挫</summary>
        public Vector2 Shake => shake < 0.02f ? Vector2.Zero
            : new Vector2(MathF.Sin(timer * 5.1f + shakeSeed), MathF.Cos(timer * 6.3f + shakeSeed)) * (shake * 1.8f);

        /// <summary>旧铭残余 1→0(锉拍),无旧铭恒 0</summary>
        public float OldReveal => OldKey == null ? 0f
            : 1f - MathHelper.Clamp(timer / TFile, 0f, 1f);

        /// <summary>新铭凿现 0~1;未起笔 &lt;0;完成 1</summary>
        public float NewReveal {
            get {
                if (NewKey == null) {
                    return -1f;
                }
                if (timer < carveStart) {
                    return -1f;
                }
                return newStrokes <= 0 ? 1f
                    : MathHelper.Clamp((timer - carveStart) / (newStrokes * StrokePeriod), 0f, 1f);
            }
        }

        /// <summary>金填缝 0~1(仅金阶收尾)</summary>
        public float InlayFill => !GoldTier ? 0f
            : MathHelper.Clamp((timer - (carveEnd + TFinish)) / TGold, 0f, 1f);

        /// <summary>油布抹过 0~1(收尾),扫出成品光</summary>
        public float OilWipe => NewKey == null ? 0f
            : MathHelper.Clamp((timer - (carveEnd + 10f)) / 26f, 0f, 1f);

        /// <summary>定鏨相位 0~1(凿/改铭),鏨具就位的那一拍</summary>
        public float FocusPose {
            get {
                if (NewKey == null) {
                    return 0f;
                }
                float focusStart = carveStart - TFocus;
                if (timer < focusStart) {
                    return 0f;
                }
                return timer >= carveStart ? 1f : MathHelper.Clamp((timer - focusStart) / TFocus, 0f, 1f);
            }
        }

        /// <summary>茎铭位仪式中,右缘大字旧名可见度</summary>
        public float NameOldVis => Slot != OniMeiSlotKind.Nakago ? 1f : OldReveal;

        /// <summary>茎铭位仪式中,右缘大字新名书写进度 0~1</summary>
        public float NameNewVis {
            get {
                if (Slot != OniMeiSlotKind.Nakago) {
                    return 1f;
                }
                float reveal = NewReveal;
                return reveal < 0f ? 0f : reveal;
            }
        }
    }
}
