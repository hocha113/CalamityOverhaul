using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms
{
    /// <summary>
    /// 调试快捷沉湖：背包里凑足 need 件已注册武器就全部消耗，
    /// 以玩家为中心向上扇形散开、错帧坠入湖中，普通逐件沉湖的快捷版。
    /// 触发方式由调试代码自行接线（直接调 <see cref="TryQuickSink"/>）；
    /// 仅所有者本机受理，入账语义与 KikasaVaultPlayer.TrySink 逐件一致（湖藏数据本机私有），
    /// 演出不发网络包，调试工具不惊动别的端
    /// </summary>
    internal static class KikasaArmsQuickSink
    {
        /// <summary>
        /// 凑足即沉：背包该武器不足 need 件或湖没准备好则拒绝（一声轻点），
        /// 成功返回 true 并把 need 件全部转入湖藏 + 写入鬼奴记忆
        /// </summary>
        internal static bool TryQuickSink(Player player, int itemType = ItemID.Minishark, int need = 5) {
            if (player == null || player.whoAmI != Main.myPlayer || need <= 0) {
                return false;
            }
            KikasaVaultPlayer vault = player.GetModPlayer<KikasaVaultPlayer>();
            if (!vault.LakeReady
                || !KikasaArmsIndex.TryGet(itemType, out _)
                || vault.Stored.Count + need > KikasaVaultPlayer.Capacity) {
                Refuse(player);
                return false;
            }

            //清点：不足不动手
            int have = 0;
            foreach (Item item in player.inventory) {
                if (item?.IsAir == false && item.type == itemType) {
                    have += Math.Max(item.stack, 1);
                }
            }
            if (have < need) {
                Refuse(player);
                return false;
            }

            //消耗并逐件入账（堆叠按件拆入，湖藏一件一格）
            int taken = 0;
            for (int slot = 0; slot < player.inventory.Length && taken < need; slot++) {
                Item item = player.inventory[slot];
                if (item?.IsAir != false || item.type != itemType) {
                    continue;
                }
                while (item.stack > 0 && taken < need) {
                    Item one = item.Clone();
                    one.stack = 1;
                    vault.Stored.Add(one);
                    item.stack--;
                    taken++;
                }
                if (item.stack <= 0) {
                    item.TurnToAir();
                }
            }
            player.GetModPlayer<KikasaServantPlayer>().RecordDrownedItem(itemType);

            //散开抛沉演出：自玩家上方扇形铺开，中央最高、错帧起跳依次坠湖
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = domain.LakeWorldY;
            bool underwater = player.MountedCenter.Y > lakeY + 8f;
            for (int i = 0; i < taken; i++) {
                float lane = i - (taken - 1) * 0.5f;
                Vector2 from = player.MountedCenter + new Vector2(lane * 6f, -8f);
                float anchorX;
                float apexY;
                if (underwater) {
                    //水下出手：轻抬原地闷沉，摊子铺小一点
                    anchorX = player.Center.X + lane * 30f;
                    apexY = from.Y - 26f - MathF.Abs(lane) * 4f;
                }
                else {
                    anchorX = player.Center.X + lane * 64f;
                    //扇形悬点：中央最高、两翼渐低，且至少悬在湖面上方一段
                    apexY = MathF.Min(
                        player.Center.Y - 118f + MathF.Abs(lane) * 14f,
                        lakeY - 92f);
                }
                KikasaLakeFX.SpawnSinkScattered(player, itemType, from, anchorX, apexY, i * 6);
            }

            //上抛挥响 + 湖的低应
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = -0.15f, MaxInstances = 2 }, player.Center);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 2 },
                new Vector2(player.Center.X, lakeY));
            return true;
        }

        private static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
        }
    }
}
