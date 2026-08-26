using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神赋路由 ModPlayer：每帧比对三件套决定激活方案，向 <see cref="Player.setBonus"/>
    /// 追加「神赋：…」行（原版套装奖励保留），并把命中/受击/击杀分发给方案。<br/>
    /// 同时提供三个每玩家暂存寄存器给激活方案自由使用（复杂状态请族内自建 ModPlayer）；
    /// 方案切走或模式关闭时寄存器自动清零，保证零残留
    /// </summary>
    internal class GodSmithArmorPlayer : ModPlayer
    {
        /// <summary>当前命中的神赋方案；null = 未命中或模式关闭</summary>
        internal GodSmithArmorScheme ActiveScheme { get; private set; }

        //——每玩家暂存寄存器：归当前激活方案所有，方案切换即清——

        /// <summary>整型暂存（层数/计数）</summary>
        internal int EndowCharge;

        /// <summary>计时暂存（对比 Main.GameUpdateCount）</summary>
        internal uint EndowTimer;

        /// <summary>布尔暂存（姿态就绪等）</summary>
        internal bool EndowFlag;

        internal void ClearScratch() {
            EndowCharge = 0;
            EndowTimer = 0;
            EndowFlag = false;
        }

        public override void PostUpdateEquips() {
            GodSmithArmorScheme next = ResolveScheme();
            if (next != ActiveScheme) {
                //切走先给旧方案清理机会（默认清空寄存器）
                ActiveScheme?.OnEndowLost(Player, this);
                ActiveScheme = next;
            }
            if (ActiveScheme == null) {
                return;
            }

            ActiveScheme.UpdateEndowment(Player, this);

            //神赋行叠加在原版套装奖励之后
            string endow = GameModeText.GodSmithEndowPrefix.Value + ActiveScheme.EndowLine.Value;
            Player.setBonus = string.IsNullOrEmpty(Player.setBonus)
                ? endow
                : Player.setBonus + "\n" + endow;
        }

        private GodSmithArmorScheme ResolveScheme() {
            if (!GameModeSystem.GodSmithActive) {
                return null;
            }
            if (!GodSmithArmorScheme.SchemesByBody.TryGetValue(Player.armor[1].type, out var candidates)) {
                return null;
            }
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i].Matches(Player)) {
                    return candidates[i];
                }
            }
            return null;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => DispatchHit(target, hit, damageDone, sourceProj: null);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => DispatchHit(target, hit, damageDone, sourceProj: proj);

        private void DispatchHit(NPC target, in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (ActiveScheme == null || !GameModeSystem.GodSmithActive) {
                return;
            }
            ActiveScheme.OnEndowHitNPC(Player, this, target, hit, damageDone, sourceProj);
            //击杀判定：命中后目标生命归零即视为击杀
            if (target.life <= 0) {
                ActiveScheme.OnEndowKillNPC(Player, this, target);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (ActiveScheme == null || !GameModeSystem.GodSmithActive) {
                return;
            }
            ActiveScheme.OnEndowHurt(Player, this, info);
        }
    }
}
