using InnoVault.Cinematics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>
    /// 标记：Boss 死亡演出运镜（观众态——镜头被接管、操作被锁，玩家不该为看戏付出血量）。
    /// 死亡 clip 实现它即自动获得 <see cref="BossDeathCutsceneGuardPlayer"/> 的全程保护；
    /// 抓取类运镜里的伤害是设计内的，不要实现本接口
    /// </summary>
    internal interface IBossDeathCutscene
    {
    }

    /// <summary>
    /// 死亡演出守卫：本机玩家正在看 <see cref="IBossDeathCutscene"/> 时——
    /// <see cref="ImmuneTo"/> 在 Hurt 最前段拦掉一切伤害（含经 PlayerHurtV2 抵达本机的 PvP）、
    /// 减益 DoT 钳零、<see cref="PreKill"/> 兜底、补满水下气；演出结束那一帧再给一段缓冲无敌帧，
    /// 防镜头刚交还就被围殴。伤害判定在受害者本机完成，全部本地生效，无需同步
    /// </summary>
    internal sealed class BossDeathCutsceneGuardPlayer : ModPlayer
    {
        /// <summary>演出结束后的缓冲无敌帧</summary>
        internal const int ExitImmuneTicks = 60;

        private bool guardedLastTick;

        internal bool Active => !Main.dedServ && Player.whoAmI == Main.myPlayer
            && CutsceneDirector.CurrentClip is IBossDeathCutscene;

        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => Active;

        /// <summary>免伤语义覆盖减益 DoT：immune 类判定只挡碰撞与弹幕，中毒/灼烧走 lifeRegen</summary>
        public override void UpdateBadLifeRegen() {
            if (Active && Player.lifeRegen < 0) {
                Player.lifeRegen = 0;
            }
        }

        /// <summary>兜底：任何绕过 Hurt 的直扣血路径也不许在看戏时把人杀掉</summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (!Active) {
                return true;
            }
            playSound = false;
            genGore = false;
            if (Player.statLife < 1) {
                Player.statLife = 1;
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
                }
            }
            return false;
        }

        public override void PostUpdate() {
            bool active = Active;
            if (active) {
                //锁操作期间人可能泡在水里：补满气，别让观众淹死
                Player.breath = Player.breathMax;
                Player.breathCD = 0;
            }
            else if (guardedLastTick) {
                Player.SetImmuneTimeForAllTypes(ExitImmuneTicks);
            }
            guardedLastTick = active;
        }
    }
}
