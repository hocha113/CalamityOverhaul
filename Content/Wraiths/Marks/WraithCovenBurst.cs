using CalamityOverhaul.Content.Wraiths.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Marks
{
    /// <summary>
    /// 三印崩：一个猎物身上同时挂着同一人的三种印时，三只鬼在它身上撞到一起。<br/>
    /// 印记一并引爆算一次伤害，参与的鬼各自付一次复苏
    /// 这是结印三角中心那一格的兑现，也是全系统最贵的一手
    /// </summary>
    internal static class WraithCovenBurst
    {
        /// <summary>凑齐几种印才崩</summary>
        internal const int MarkThreshold = 3;
        /// <summary>每种印折算的武器伤害倍率，按印记强度插值</summary>
        private const float PerMarkMin = 0.35f;
        private const float PerMarkMax = 0.75f;

        /// <summary>权威端检查并引爆；印记被吃掉，要再崩得重新攒。</summary>
        internal static void TryBurst(NPC npc, WraithMarkNPC marks, int owner) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            WraithMark active = marks.Active(owner);
            Player player = Main.player[owner];
            if (player?.active != true || player.dead) {
                return;
            }

            //盘点强度与付费名单：付费对象是槽上记的施加鬼 Key，不查身份表；
            //同一只鬼若发了多种状态也只付一次
            float total = 0f;
            int count = 0;
            string[] payKeys = new string[WraithMarkExtensions.Count];
            int payCount = 0;
            for (int i = 0; i < WraithMarkExtensions.Count; i++) {
                WraithMark mark = WraithMarkExtensions.FromIndex(i);
                if ((active & mark) == 0) {
                    continue;
                }
                count++;
                total += MathHelper.Lerp(PerMarkMin, PerMarkMax,
                    MathHelper.Clamp(marks.PowerOf(mark, owner), 0f, 1f));
                string key = marks.KeyOf(mark, owner);
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }
                bool seen = false;
                for (int k = 0; k < payCount; k++) {
                    if (payKeys[k] == key) {
                        seen = true;
                        break;
                    }
                }
                if (!seen) {
                    payKeys[payCount++] = key;
                }
            }
            if (count < MarkThreshold) {
                return;
            }

            //印先吃掉再结算：夺身若在结算中接管，也不会留下一个能反复崩的目标
            marks.Clear();

            int weaponDamage = Math.Max(player.GetWeaponDamage(player.HeldItem), 1);
            int damage = Math.Max(1, (int)(weaponDamage * total));
            int direction = npc.Center.X >= player.Center.X ? 1 : -1;
            player.ApplyDamageToNPC(npc, damage, 0f, direction, false,
                CWRRef.GetTrueMeleeDamageClass());

            //参与的鬼各付一次；互相催醒会让这一下把整盘都往上顶
            for (int k = 0; k < payCount; k++) {
                WraithAbilityService.TryCommitUse(player, payKeys[k]);
            }

            BroadcastFx(npc);
        }

        private static void BroadcastFx(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath52 with {
                Pitch = -0.85f,
                Volume = 0.7f,
                MaxInstances = 2,
            }, npc.Center);
        }
    }
}
