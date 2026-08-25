using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>光色分级关键帧：某世界行处的目标色/推力/亮度系数</summary>
    internal readonly struct GradeKey
    {
        internal readonly float Row;
        internal readonly Color Tile;
        internal readonly float TileF;
        internal readonly Color Bg;
        internal readonly float BgF;
        internal readonly float Bright;

        internal GradeKey(float row, Color tile, float tileF, Color bg, float bgF, float bright) {
            Row = row;
            Tile = tile;
            TileF = tileF;
            Bg = bg;
            BgF = bgF;
            Bright = bright;
        }
    }

    /// <summary>一次性点缀条目：带内周期播放的远景声</summary>
    internal readonly struct AccentCue
    {
        internal readonly int Band;        //0..6 层带，7=深渊
        internal readonly SoundStyle Style;
        internal readonly float Volume;
        internal readonly float Pitch;
        internal readonly int Period;      //平均周期（tick）
        internal readonly float Jitter;    //周期抖动 ±比例
        internal readonly int Hits;        //连响次数（骨响/纸声双连）
        internal readonly int HitGap;      //连响间隔（tick）

        internal AccentCue(int band, SoundStyle style, float volume, float pitch,
            int period, float jitter, int hits = 1, int hitGap = 0) {
            Band = band;
            Style = style;
            Volume = volume;
            Pitch = pitch;
            Period = period;
            Jitter = jitter;
            Hits = hits;
            HitGap = hitGap;
        }
    }

    /// <summary>
    /// 深度音景与光色分级的配置表（WAVE2-ATMOSPHERE E-2）：调音只改此文件。
    /// 色值全部由 DungeonworldLoadTheme.BandAccents 去饱和压暗推导（雾色同源哲学），
    /// 力度刻意压低——分级是"空气层"不是"重涂"，不与 LayerTint 漆面打架
    /// </summary>
    internal static class AmbienceScore
    {
        //==== 硬顶（写死为常量：原版 ZoneDungeon 音乐仍是主角，声床只是房间底噪）====
        internal const float LoopVolCap = 0.16f;
        internal const float AccentVolCap = 0.5f;
        /// <summary>点缀全局冷却 ≥2s</summary>
        internal const int AccentGlobalCooldown = 120;

        //==== 层界仪式常量 ====
        internal const float BellVolume = 0.4f;
        internal const float BellBasePitch = 0.1f;
        /// <summary>每深一层钟声降调一档（越深越沉）</summary>
        internal const float BellPitchStep = 0.15f;
        internal const float CeremonyFogRadius = 260f;
        internal const int CeremonyFogTtl = 26;
        internal const float CeremonyFogFeather = 420f;
        /// <summary>仪式色温抖动：分级力度 ×(1+0.35) 再回落</summary>
        internal const float CeremonyBoost = 0.35f;
        internal const int CeremonyBoostUp = 30;
        internal const int CeremonyBoostDown = 60;
        /// <summary>仪式冷却 30s（电梯反复横跳不连刷）</summary>
        internal const int CeremonyCooldown = 1800;
        /// <summary>跨带滞回（行）：离开中线这么远才重新武装</summary>
        internal const float CeremonyHysteresis = 16f;

        //==== 光色分级关键帧 ====
        //10 帧 = 7 带中点 + L6 末保持帧(5168) + 深渊 2 帧；
        //5168 保持帧把 L6→L7 的暖色坍塌压进最后 200 行 + 隔离带（"色温坠落"关键帧）
        internal static readonly GradeKey[] GradeKeys = [
            new(135f,  new Color(58, 60, 74),  0.10f, new Color(18, 22, 34), 0.30f, 1.00f),   //L1 教堂
            new(297f,  new Color(66, 56, 62),  0.12f, new Color(24, 18, 22), 0.32f, 1.00f),   //L2 牢狱
            new(1058f, new Color(78, 68, 50),  0.12f, new Color(30, 25, 16), 0.32f, 1.00f),   //L3 档案馆
            new(2244f, new Color(52, 72, 62),  0.14f, new Color(14, 26, 20), 0.36f, 1.00f),   //L4 水牢
            new(3456f, new Color(86, 84, 74),  0.10f, new Color(30, 29, 24), 0.30f, 1.00f),   //L5 万骨窖
            new(4768f, new Color(92, 64, 44),  0.14f, new Color(34, 20, 12), 0.36f, 0.98f),   //L6 铸造
            new(5168f, new Color(92, 64, 44),  0.14f, new Color(34, 20, 12), 0.36f, 0.98f),   //L6 末保持
            new(5490f, new Color(58, 52, 88),  0.20f, new Color(16, 14, 34), 0.44f, 0.92f),   //L7 倒吊教堂
            new(5650f, new Color(30, 28, 52),  0.30f, new Color(8, 7, 18),  0.55f, 0.85f),    //深渊上部
            new(6000f, new Color(30, 28, 52),  0.30f, new Color(8, 7, 18),  0.55f, 0.85f),    //深渊底（平尾）
        ];

        /// <summary>行 → 分级插值采样（O(10) 线性走查，每 tick 一次）</summary>
        internal static void SampleGrade(float row, out Color tileT, out float tileF,
            out Color bgT, out float bgF, out float bright) {
            var keys = GradeKeys;
            if (row <= keys[0].Row) {
                tileT = keys[0].Tile; tileF = keys[0].TileF;
                bgT = keys[0].Bg; bgF = keys[0].BgF; bright = keys[0].Bright;
                return;
            }
            for (int i = 0; i < keys.Length - 1; i++) {
                if (row > keys[i + 1].Row) {
                    continue;
                }
                float t = (row - keys[i].Row) / (keys[i + 1].Row - keys[i].Row);
                tileT = Color.Lerp(keys[i].Tile, keys[i + 1].Tile, t);
                tileF = MathHelper.Lerp(keys[i].TileF, keys[i + 1].TileF, t);
                bgT = Color.Lerp(keys[i].Bg, keys[i + 1].Bg, t);
                bgF = MathHelper.Lerp(keys[i].BgF, keys[i + 1].BgF, t);
                bright = MathHelper.Lerp(keys[i].Bright, keys[i + 1].Bright, t);
                return;
            }
            var last = keys[^1];
            tileT = last.Tile; tileF = last.TileF;
            bgT = last.Bg; bgF = last.BgF; bright = last.Bright;
        }

        //==== 声床曲线 ====

        /// <summary>石殿风音量：随深度 0.05→0.12（进硬顶前再乘 presence/duck）</summary>
        internal static float WindVolume(float row)
            => MathHelper.Lerp(0.05f, 0.12f, MathHelper.Clamp(row / 6000f, 0f, 1f));

        /// <summary>石殿风音调：基准 −0.6，L4 水腔内压到 −0.75（边缘 60 行平滑）</summary>
        internal static float WindPitch(float row) {
            var l4 = Gen.DungeonworldMetrics.Bands[3];
            float inL4 = Smooth01((row - (l4.Top - 60f)) / 60f) * Smooth01(((l4.Bottom + 60f) - row) / 60f);
            return MathHelper.Lerp(-0.6f, -0.75f, inL4);
        }

        /// <summary>炉鸣/渊鸣音量：L6 顶起淡入 0→0.10，L7/深渊 →0.16</summary>
        internal static float FurnaceVolume(float row) {
            var l6 = Gen.DungeonworldMetrics.Bands[5];
            if (row <= l6.Top) {
                return 0f;
            }
            if (row <= l6.Bottom) {
                return MathHelper.Lerp(0f, 0.10f, (row - l6.Top) / (l6.Bottom - l6.Top));
            }
            return MathHelper.Lerp(0.10f, 0.16f, MathHelper.Clamp((row - l6.Bottom) / (5650f - l6.Bottom), 0f, 1f));
        }

        /// <summary>炉鸣音调：−0.4 →（深渊）−0.8</summary>
        internal static float FurnacePitch(float row) {
            var l6 = Gen.DungeonworldMetrics.Bands[5];
            return MathHelper.Lerp(-0.4f, -0.8f,
                MathHelper.Clamp((row - l6.Top) / (5650f - l6.Top), 0f, 1f));
        }

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==== 一次性点缀表 ====
        //音效全部来自库内已验证可用集；L3 纸声与 L7 逆唱诗为"实现期试听备选"项（见计划）
        internal static readonly AccentCue[] Accents = [
            new(0, SoundID.Item35,     0.22f, -0.10f, 18 * 60, 0.40f),          //L1 远钟
            new(1, SoundID.Unlock,     0.25f, -0.50f, 16 * 60, 0.30f),          //L2 铁链/锁
            new(2, SoundID.Drip,       0.10f, +0.90f, 14 * 60, 0.30f, 2, 6),    //L3 纸页窸窣（双连）
            new(3, SoundID.Drip,       0.30f, -0.20f, 5 * 60,  0.35f),          //L4 滴水加密
            new(3, SoundID.SplashWeak, 0.20f, -0.50f, 25 * 60, 0.30f),          //L4 远水花
            new(4, SoundID.Tink,       0.18f, -0.85f, 15 * 60, 0.30f, 2, 7),    //L5 骨响（双连）
            new(5, SoundID.Dig,        0.25f, -0.90f, 12 * 60, 0.30f),          //L6 远锤
            new(6, SoundID.Roar,       0.12f, +0.50f, 26 * 60, 0.30f),          //L7 逆唱诗（拉高变薄）
            new(7, SoundID.Thunder,    0.14f, -0.90f, 30 * 60, 0.30f),          //深渊闷雷
        ];

        //==== 带归属 ====

        /// <summary>
        /// 行 → 归属带 0..7（7=深渊）。隔离带按中线切开归上/下带（点缀调度用，
        /// 与 DungeonworldMetrics.OwnerBand 同一口径）
        /// </summary>
        internal static int BandAt(float row) {
            if (row >= 5600f) {
                return 7;
            }
            var bands = Gen.DungeonworldMetrics.Bands;
            if (row < bands[0].Top) {
                return 0;
            }
            for (int i = 0; i < bands.Length; i++) {
                if (row < bands[i].Bottom) {
                    return i;
                }
                if (i < bands.Length - 1 && row < bands[i + 1].Top) {
                    float mid = bands[i].Bottom + (bands[i + 1].Top - bands[i].Bottom) * 0.5f;
                    return row < mid ? i : i + 1;
                }
            }
            return 6;
        }

        /// <summary>第 i 道隔离带中线行（i=0..5，介于带 i 与带 i+1 之间）</summary>
        internal static float SeparatorMid(int i) {
            var bands = Gen.DungeonworldMetrics.Bands;
            return bands[i].Bottom + (bands[i + 1].Top - bands[i].Bottom) * 0.5f;
        }
    }
}
