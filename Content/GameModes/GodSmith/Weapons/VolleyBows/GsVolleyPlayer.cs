using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>延迟补射请求：三连点射、天雨、镜像凤凰等错帧生成全走这一条队列</summary>
    internal struct GsPendingShot
    {
        /// <summary>剩余延迟帧</summary>
        public int Delay;
        /// <summary>入队时的手持武器 ID；消费时已切武器则作废</summary>
        public int WeaponType;
        /// <summary>弹幕类型</summary>
        public int ProjType;
        /// <summary>发射速度（已含 ±角偏移）</summary>
        public Vector2 Velocity;
        /// <summary>伤害（已折算）</summary>
        public int Damage;
        /// <summary>击退</summary>
        public float Knockback;
        /// <summary>打标角色（GsVolleyRole）</summary>
        public int Role;
        /// <summary>角色参数（写进 MarkData2）</summary>
        public float Param;
        /// <summary>true 用 <see cref="Pos"/> 绝对坐标；false 从玩家口部发射</summary>
        public bool AbsolutePos;
        /// <summary>绝对生成坐标（天雨箭等）</summary>
        public Vector2 Pos;
    }

    /// <summary>
    /// 齐射家族的每玩家状态：充能条、延迟补射队列、追击箭节流。
    /// 全部是本机玩家消费的本地量（生成只发生在 owner 端），不入存档不联机同步
    /// </summary>
    internal class GsVolleyPlayer : ModPlayer
    {
        /// <summary>齐射充能 0~100，满后下一次射击自动成齐射；切武器归零</summary>
        internal float Charge;

        /// <summary>追击箭节流：>0 时不再生成（计划口径：每 15 帧至多 1 支）</summary>
        internal int PursuitCooldown;

        /// <summary>延迟补射队列，只在本机玩家路径消费</summary>
        internal readonly List<GsPendingShot> Pending = [];

        private int lastHeldType;

        public override void PostUpdate() {
            if (PursuitCooldown > 0) {
                PursuitCooldown--;
            }

            //切武器：充能与队列一并作废
            if (Player.HeldItem.type != lastHeldType) {
                lastHeldType = Player.HeldItem.type;
                Charge = 0f;
                Pending.Clear();
            }

            if (Pending.Count == 0) {
                return;
            }
            //补射生成是 owner 侧行为；死亡或模式关闭时清空残留
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (Player.dead || !GameModeSystem.GodSmithActive) {
                Pending.Clear();
                return;
            }

            for (int i = Pending.Count - 1; i >= 0; i--) {
                GsPendingShot shot = Pending[i];
                if (--shot.Delay > 0) {
                    Pending[i] = shot;
                    continue;
                }
                Pending.RemoveAt(i);
                if (Player.HeldItem.type != shot.WeaponType) {
                    continue;
                }
                if (!GodSmithScheme.TryGetScheme(shot.WeaponType, out GodSmithScheme scheme)
                    || scheme is not GsVolleyBowScheme volleyScheme) {
                    continue;
                }
                Vector2 pos = shot.AbsolutePos
                    ? shot.Pos
                    : Player.Center + shot.Velocity.SafeNormalize(Vector2.UnitX) * 24f;
                volleyScheme.SpawnTagged(Player, Player.GetSource_ItemUse(Player.HeldItem),
                    pos, shot.Velocity, shot.ProjType, shot.Damage, shot.Knockback, shot.Role, shot.Param);
            }
        }

        /// <summary>入队一发延迟补射</summary>
        internal void Enqueue(GsPendingShot shot) => Pending.Add(shot);
    }
}
