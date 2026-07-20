using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 反噬事件框架（鬼律第十一条）：躁动之鬼（Bound 且驾驭度低于
    /// <see cref="WraithDefinition.RestlessThreshold"/>）按概率从载体挣脱，
    /// 在主人身边以挣脱态显形——据点制的唯一合法例外。<br/>
    /// 判定在 owner 端（载体数据与侵蚀都归持有人权威），生成在服务器权威
    /// （单人直呼 / 多人经 <see cref="WraithNet.SendBacklashSpawn"/>）。
    /// 重收伏 = 按该鬼规则逼进死机 + 仪式（Resubdue），钩子链：
    /// <c>WraithActor.MarkEscaped → OnBacklashEscape → 子类怪谈表现 → 死机 → WraithRites</c>
    /// </summary>
    internal static class WraithBacklash
    {
        /// <summary>同一只鬼两次挣脱判定命中的最短间隔（帧）</summary>
        public const int KeyCooldownTicks = 60 * 90;

        /// <summary>
        /// owner 端 1Hz 掷签：躁动越深、侵蚀越高，挣脱概率越大。
        /// 一次至多放出一只；纯数据鬼（无实体类）挣不出来；
        /// 遭遇进行中（全局同屏一鬼）不掷签——服务器侧同样会拒绝，这里先省掉必败的请求
        /// </summary>
        public static void Judge(WraithPlayer wraithPlayer) {
            Player player = wraithPlayer.Player;
            if (player.dead || WraithDirector.EncounterInProgress()) {
                return;
            }
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
            if (!vessel.IsValid) {
                return;
            }

            long now = (long)Main.GameUpdateCount;
            foreach ((string key, WraithProgressRecord record) in vessel.Store.Records) {
                if (record.State != WraithBindState.Bound
                    || record.Mastery >= WraithDefinition.RestlessThreshold) {
                    continue;
                }
                if (!WraithRegistry.TryGet(key, out WraithDefinition definition) || definition.ActorType == null) {
                    continue;
                }
                if (wraithPlayer.BacklashOnCooldown(key, now) || wraithPlayer.IsEscapePending(key)
                    || AnyEscapedAlive(key, player.whoAmI)) {
                    continue;
                }

                //躁动深度 0~1(贴线=0,归零=1),叠加侵蚀放大
                float restless = 1f - record.Mastery / WraithDefinition.RestlessThreshold;
                float chance = 0.010f + restless * 0.020f + wraithPlayer.Erosion * 0.045f;
                if (Main.rand.NextFloat() >= chance) {
                    continue;
                }

                //冷却不在此预烧:确认制——单人生成成功即落账,多人待首次真实观测到挣脱体再落账
                Trigger(player, definition, wraithPlayer);
                break;
            }
        }

        /// <summary>
        /// 触发一次挣脱（owner 端，调试器强制路径也走这里），确认制：
        /// 单人=生成成功当场播报+落冷却，失败无声无息；
        /// 多人=发请求并挂起观察，播报/冷却延迟到 <c>WraithPlayer.WatchEscaped</c>
        /// 首次真实观测到挣脱体——绝不先播"挣脱了"再等一个可能不来的鬼
        /// </summary>
        public static void Trigger(Player owner, WraithDefinition definition, WraithPlayer wraithPlayer = null) {
            if (definition?.ActorType == null || owner.whoAmI != Main.myPlayer) {
                return;
            }
            wraithPlayer ??= owner.GetModPlayer<WraithPlayer>();
            if (VaultUtils.isClient) {
                WraithNet.SendBacklashSpawn(definition);
                wraithPlayer.NotePendingEscape(definition.Key);
                return;
            }
            if (SpawnEscaped(owner.whoAmI, definition)) {
                wraithPlayer.NoteEscaped(definition.Key);
                wraithPlayer.SetBacklashCooldown(definition.Key, (long)Main.GameUpdateCount + KeyCooldownTicks);
                AnnounceEscape(owner, definition);
            }
        }

        /// <summary>挣脱播报（文本/音/震屏），确认制下只在挣脱体真实存在后播放</summary>
        internal static void AnnounceEscape(Player owner, WraithDefinition definition) {
            VaultUtils.Text(WraithSystemText.BacklashEscape.Format(definition.DisplayName.Value), new Color(190, 60, 70));
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.6f, Volume = 0.6f }, owner.Center);
            owner.CWR()?.GetScreenShake(5f);
        }

        /// <summary>
        /// 权威端生成挣脱体：主人外围环带落点（鬼身不吃物块阻挡，仅避世界边缘）。
        /// 返回是否真的生成并标记成功——被全局互斥/资格挡下返回 false，调用方据此决定落不落账
        /// </summary>
        internal static bool SpawnEscaped(int playerWhoAmI, WraithDefinition definition) {
            if (VaultUtils.isClient || definition?.ActorType == null
                || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[playerWhoAmI];
            if (owner == null || !owner.active || owner.dead) {
                return false;
            }
            //同键挣脱体在场不叠加(重复请求/竞态防御)
            if (AnyEscapedAlive(definition.Key, playerWhoAmI)) {
                return false;
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(480f, 760f);
            Vector2 center = owner.Center + angle.ToRotationVector2() * dist;
            float margin = 800f;
            center.X = MathHelper.Clamp(center.X, margin, Main.maxTilesX * 16f - margin);
            center.Y = MathHelper.Clamp(center.Y, margin, Main.maxTilesY * 16f - margin);
            Vector2 topLeft = center - new Vector2(definition.HitboxWidth * 0.5f, definition.HitboxHeight * 0.5f);

            int whoAmI = WraithDirector.Materialize(definition, topLeft);
            if (whoAmI >= 0 && ActorLoader.Actors[whoAmI] is WraithActor wraith) {
                wraith.MarkEscaped(playerWhoAmI);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 该键是否已有针对指定玩家的挣脱体在场（两端都可查询，实体经内建同步可见）。
        /// 挣脱契约态（鬼律第十一条）：本谓词为真期间该键不可借力、仪式只受理其 Resubdue
        /// </summary>
        public static bool AnyEscapedAlive(string key, int playerWhoAmI) {
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (!wraith.IsEscaped || wraith.Definition == null || wraith.Definition.Key != key) {
                    continue;
                }
                if (wraith.EscapedOwnerPlayer?.whoAmI == playerWhoAmI) {
                    return true;
                }
            }
            return false;
        }
    }
}
