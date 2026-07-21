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
    /// 反噬。躁动 Bound 鬼按概率挣脱显形；判定 owner，生成权威。<br/>
    /// 重收伏=逼死机+Resubdue
    /// </summary>
    internal static class WraithBacklash
    {
        /// <summary>同键挣脱最短间隔帧</summary>
        public const int KeyCooldownTicks = 60 * 90;

        /// <summary>owner 1Hz 掷签；遭遇中不掷；一次至多一只</summary>
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
                //上线闸关则正典不掷签
                if (!WraithDirector.ContentActiveFor(definition)) {
                    continue;
                }
                if (wraithPlayer.BacklashOnCooldown(key, now) || wraithPlayer.IsEscapePending(key)
                    || AnyEscapedAlive(key, player.whoAmI)) {
                    continue;
                }

                //躁动深度 0~1，叠侵蚀
                float restless = 1f - record.Mastery / WraithDefinition.RestlessThreshold;
                float chance = 0.010f + restless * 0.020f + wraithPlayer.Erosion * 0.045f;
                if (Main.rand.NextFloat() >= chance) {
                    continue;
                }

                //确认制，冷却不预烧
                Trigger(player, definition, wraithPlayer);
                break;
            }
        }

        /// <summary>触发挣脱，确认制；多人挂起观测后再播报落冷却</summary>
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

        /// <summary>挣脱播报，仅实体真实存在后</summary>
        internal static void AnnounceEscape(Player owner, WraithDefinition definition) {
            VaultUtils.Text(WraithSystemText.BacklashEscape.Format(definition.DisplayName.Value), new Color(190, 60, 70));
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.6f, Volume = 0.6f }, owner.Center);
            owner.CWR()?.GetScreenShake(5f);
        }

        /// <summary>权威端生成挣脱体；互斥/资格挡下返回 false</summary>
        internal static bool SpawnEscaped(int playerWhoAmI, WraithDefinition definition) {
            if (VaultUtils.isClient || definition?.ActorType == null
                || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[playerWhoAmI];
            if (owner == null || !owner.active || owner.dead) {
                return false;
            }
            //同键在场不叠加
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

        /// <summary>该键对该玩家是否已有挣脱体；真则不可借力，仪式只受理 Resubdue</summary>
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
