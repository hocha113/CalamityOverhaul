using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    /// <summary>
    /// 发现引导播报（发现引导批，2026-08-27）：三条 Boss 线的入层氛围句与接近预告句。<br/>
    /// 入层：本地玩家在 L2/L4/L6 带内驻留 1.5s，首次给一条「层主题+指路」；<br/>
    /// 接近：本地玩家与该线蛰伏种子/Boss 距离进入 <see cref="ApproachDistPx"/>，首次给一条预告。
    /// 判定与显示全在本端（服务器早退，零网络包）：种子由看守在 2000px 布防并随 SyncNPC
    /// 到达每个客户端，接近半径取同值，文字恰在「房间已布防」的时点出现，且每端各自判距，
    /// 旁观者不吃触发者的包、迟入场玩家走自己的闩不重播。<br/>
    /// 闩为本端会话态（static=每客户端一份，纯本地表现合法），ClearWorld 复位；
    /// 世界为回放制（ShouldSave=false），重进世界房间重蛰伏，播报同步重放属预期。
    /// </summary>
    internal class DungeonworldGuideNotes : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public override bool IsLoadingEnabled(Mod mod) => DungeonworldBossRecords.AnyGateEnabled;

        /// <summary>接近预告半径（px）。与看守布防半径同值：文字与「种子就位」同拍出现</summary>
        private const float ApproachDistPx = 2000f;
        /// <summary>入层驻留门槛（tick），楼梯井边缘反复横跳不触发</summary>
        private const int DwellTicks = 90;
        /// <summary>接近扫场间隔（tick）</summary>
        private const int ScanInterval = 30;

        //三条线的顺序约定：0=禁室(L2) 1=泄洪堂(L4) 2=验收堂(L6)
        private static readonly int[] LineBandIndex = [1, 3, 5];
        private static readonly Color[] LineColor = [
            new(236, 116, 156), new(88, 154, 148), new(222, 138, 58),
        ];

        private static LocalizedText[] layerNotes;
        private static LocalizedText[] approachNotes;

        private static readonly bool[] layerShown = new bool[3];
        private static readonly bool[] approachShown = new bool[3];
        private static readonly int[] dwell = new int[3];
        private static int scanTimer;

        public override void SetStaticDefaults() {
            layerNotes = [
                this.GetLocalization("LayerNoteGaol", () => "牢狱层。铐链和锈往哪边密，深牢禁室就在哪边。"),
                this.GetLocalization("LayerNoteFlood", () => "水牢。水都往最底一层的泄洪堂去，你也一样。"),
                this.GetLocalization("LayerNoteProof", () => "铸造机关层。齿轮没停过，最底下那条天轨通向验收堂。"),
            ];
            approachNotes = [
                this.GetLocalization("ApproachGaol", () => "铐链密得挂不下了。前面那间牢房，门是从外面锁的。"),
                this.GetLocalization("ApproachFlood", () => "水线爬到了顶。前面的泄洪堂里，有东西还在等水。"),
                this.GetLocalization("ApproachProof", () => "天轨到了头。炉火还热着，验收堂在等今天的工件。"),
            ];
        }

        public override void ClearWorld() => Reset();

        public override void Unload() {
            Reset();
            layerNotes = null;
            approachNotes = null;
        }

        private static void Reset() {
            for (int i = 0; i < 3; i++) {
                layerShown[i] = false;
                approachShown[i] = false;
                dwell[i] = 0;
            }
            scanTimer = 0;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu || !Dungeonworld.Active) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return;
            }

            UpdateLayerNotes(player);

            if (++scanTimer >= ScanInterval) {
                scanTimer = 0;
                UpdateApproachNotes(player);
            }
        }

        //==================== 入层播报（带内驻留首触）====================

        private static void UpdateLayerNotes(Player player) {
            float row = player.Center.Y / 16f;
            for (int k = 0; k < 3; k++) {
                if (!LineEnabled(k)) {
                    continue;
                }
                LayerBand band = DungeonworldMetrics.Bands[LineBandIndex[k]];
                if (row < band.Top || row >= band.Bottom) {
                    dwell[k] = 0;
                    continue;
                }
                if (layerShown[k] || ++dwell[k] < DwellTicks) {
                    continue;
                }
                layerShown[k] = true;
                Main.NewText(layerNotes[k].Value, LineColor[k]);
            }
        }

        //==================== 接近预告（种子/Boss 距离首触）====================

        private static void UpdateApproachNotes(Player player) {
            for (int k = 0; k < 3; k++) {
                if (approachShown[k] || !LineEnabled(k)) {
                    continue;
                }
                if (!AnyLineNpcNear(k, player.Center)) {
                    continue;
                }
                approachShown[k] = true;
                Main.NewText(approachNotes[k].Value, LineColor[k]);
            }
        }

        private static bool LineEnabled(int line) => line switch {
            0 => DeepGaolWraithGate.Enabled,
            1 => UndrownedGate.Enabled,
            _ => FoundryOverseerGate.Enabled,
        };

        /// <summary>该线的蛰伏种子或 Boss 是否进入接近半径。种子由看守 2000px 布防且
        /// 随 SyncNPC 全端同步，先于本判定成立；Boss 兜底盖住迟入场/战斗中的情况</summary>
        private static bool AnyLineNpcNear(int line, Vector2 from) {
            int seedType = line switch {
                0 => ModContent.NPCType<GaolDormantSkull>(),
                1 => ModContent.NPCType<UndrownedThrone>(),
                _ => ModContent.NPCType<OverseerDormantRig>(),
            };
            int bossType = line switch {
                0 => ModContent.NPCType<DeepGaolWraith>(),
                1 => ModContent.NPCType<Undrowned>(),
                _ => ModContent.NPCType<FoundryOverseer>(),
            };
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != seedType && npc.type != bossType)) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, from) < ApproachDistPx) {
                    return true;
                }
            }
            return false;
        }
    }
}
