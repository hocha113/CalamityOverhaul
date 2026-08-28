using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠手持接管的绘制桥：held 在场时压掉原版手持物品贴图，消除「重铸动画 + 原版挥舞」叠影
    /// （原版按 player.direction 镜像、held 自算角度，往左攻击时一把朝左一把朝右）。<br/>
    /// 根因：神匠 held 每帧强撑 itemAnimation=2 撑姿态，而原版 DrawPlayer_27_HeldItem 在
    /// itemAnimation&gt;0 且物品 noUseGraphic=false 时照画手中物品（阔剑/弓/枪/法器全中；
    /// 矛/连枷/短剑原版 noUseGraphic=true 天然免疫）。<br/>
    /// 不能整层 Hide()：27 层开头还负责记录 projectileDrawPosition（heldProj 的内联插绘位置），
    /// hide=true 的枪族 held（链枪/迷你鲨/凤凰爆破枪/喷射器）全靠这条通道显示，整层隐藏武器直接消失。
    /// 故改在 ModifyDrawInfo 把 drawInfo.heldItem 换成同类型但 noUseGraphic=true 的绘制替身：
    /// 27 层先记录插绘位置、再按原版自身条件退出；喷射器背罐等按类型判定的视觉不受影响；
    /// 爪套层（DrawPlayer_30_BladedGlove）的第二份叠影也被同一标记一并压掉。<br/>
    /// 各端本地判定：heldProj 由 held 的 AI 在各端逐帧自报，弹幕类型是同步态，无需网络。
    /// </summary>
    internal class GodSmithHeldVisualBridge : ModPlayer
    {
        /// <summary>按弹幕 type 缓存「命名空间是否在神匠树下」；加载集定型后惰性建表，热路径零反射</summary>
        private static bool[] gsProjByType;

        /// <summary>绘制替身，只塞进本帧的 PlayerDrawSet，绝不进背包或世界</summary>
        private static Item drawStub;

        public override void Unload() {
            gsProjByType = null;
            drawStub = null;
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            Player player = drawInfo.drawPlayer;
            int heldIndex = player.heldProj;
            if (heldIndex < 0 || heldIndex >= Main.maxProjectiles) {
                return;
            }
            Projectile proj = Main.projectile[heldIndex];
            if (!proj.active || proj.owner != player.whoAmI || !IsGodSmithProj(proj.type)) {
                return;
            }
            Item held = drawInfo.heldItem;
            if (held == null || held.IsAir || held.noUseGraphic) {
                return;//本就不画手中物品（矛/连枷等），无叠影可压
            }

            drawStub ??= new Item();
            if (drawStub.type != held.type) {
                drawStub.SetDefaults(held.type);//规整全部字段，背罐/隐身着色等按类型的判定不受影响
            }
            drawStub.noUseGraphic = true;//SetDefaults 会复位该值，逐帧兜底
            drawInfo.heldItem = drawStub;
        }

        /// <summary>type 是否为神匠树下的弹幕；表长随加载集自愈，模组重载后自动重建</summary>
        private static bool IsGodSmithProj(int type) {
            bool[] table = gsProjByType;
            if (table == null || table.Length != ProjectileLoader.ProjectileCount) {
                gsProjByType = table = BuildTable();
            }
            return (uint)type < (uint)table.Length && table[type];
        }

        private static bool[] BuildTable() {
            string root = typeof(GameModeSystem).Namespace + ".GodSmith";
            string rootDot = root + ".";
            bool[] table = new bool[ProjectileLoader.ProjectileCount];
            for (int type = ProjectileID.Count; type < table.Length; type++) {
                string ns = ProjectileLoader.GetProjectile(type)?.GetType().Namespace;
                table[type] = ns != null && (ns == root || ns.StartsWith(rootDot, StringComparison.Ordinal));
            }
            return table;
        }
    }
}
