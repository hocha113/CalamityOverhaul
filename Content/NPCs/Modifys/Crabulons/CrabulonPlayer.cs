using CalamityMod.NPCs.Crabulon;
using CalamityMod.Systems;
using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    internal class CrabulonPlayer : PlayerOverride
    {
        /// <summary>
        /// 存在的菌生蟹索引，如果为-1则表示没有
        /// </summary>
        public int CrabulonIndex;
        /// <summary>
        /// 骑乘的菌生蟹实例，如果没有骑乘，则为null
        /// </summary>
        public ModifyCrabulon MountCrabulon;
        private bool oldIsMount;
        public bool IsMount;
        public List<ModifyCrabulon> ModifyCrabulons = [];
        public override void ResetEffects() => CrabulonIndex = -1;
        public static void CloseDuringDash(Player player) {
            CWRPlayer modPlayer = player.CWR();
            player.fullRotation = 0;
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.RotationDirection = player.direction;
            modPlayer.DashCooldownCounter = 95;
            modPlayer.CustomCooldownCounter = 90;
        }
        public override void PostUpdate() {
            if (!IsMount) {
                ModifyCrabulon.mountPlayerHeldProj = -1;
                MountCrabulon = null;
                if (oldIsMount) {
                    CloseDuringDash(Player);
                }
            }
            oldIsMount = IsMount;

            ModifyCrabulons.Clear();
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.boss || npc.type != ModContent.NPCType<Crabulon>()) {
                    continue;
                }
                ModifyCrabulons.Add(npc.GetOverride<ModifyCrabulon>());
            }
        }
        private static bool PlayerIsMount(Player player) {
            if (!VaultLoad.LoadenContent) {
                return false;//没加载好内容，直接返回
            }
            if (!player.Alives()) {
                return false;//玩家无效，直接返回
            }
            if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer) || crabulonPlayer == null) {
                return false;//找不到实例，直接返回
            }
            return crabulonPlayer.IsMount;
        }
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            players = players.Where(p => !PlayerIsMount(p));//删掉关于骑乘玩家的绘制
            return true;
        }
        public override IEnumerable<string> GetActiveSceneEffectFullNames() {
            yield return typeof(CrabulonMusicScene).FullName;
        }
        public override bool? PreIsSceneEffectActive(ModSceneEffect modSceneEffect) {
            if (CrabulonIndex == -1) {
                return false;//直接返回，这里算作一次性能优化
            }
            int crabulon = ModContent.NPCType<Crabulon>();
            foreach (var npc in Main.ActiveNPCs) {
                if (!npc.boss) {//这里可以排除掉被驯服的菌生蟹，因为被驯服后不会被算作Boss
                    continue;
                }
                if (npc.type == crabulon) {
                    return true;
                }
            }
            return false;
        }
    }
}
