using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 门徒debuff的敌怪侧结算：启示印易伤、真言揭示剥甲显形、财富祝福的奉献金雨。
    /// 状态全部走原版NPC buff同步，钩子只查询不自持
    /// </summary>
    internal class ElysiumGlobalNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            //启示印：受到的一切伤害提高
            if (npc.HasBuff<RevelationMarkDebuff>()) {
                modifiers.FinalDamage *= 1.08f;
            }
            //真言揭示：护甲被剥离
            if (npc.HasBuff<TruthRevealDebuff>()) {
                modifiers.ArmorPenetration += 25f;
            }
        }

        /// <summary>瘟疫印：圣瘟持续侵蚀(约80/秒)</summary>
        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!npc.HasBuff<PlagueMarkDebuff>()) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            npc.lifeRegen -= 160;
            if (damage < 20) {
                damage = 20;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //真言揭示：显形(整体提亮并透出绿金光)
            if (npc.HasBuff<TruthRevealDebuff>()) {
                drawColor = Color.Lerp(drawColor, Color.White, 0.45f);
                Lighting.AddLight(npc.Center, 0.25f, 0.4f, 0.28f);
                if (Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_Light>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f)
                        , Vector2.Zero, new Color(190, 245, 200), 0.2f)?.Configure(12, 0.7f);
                }
            }
            //瘟疫印：病绿浸染与滴落
            if (npc.HasBuff<PlagueMarkDebuff>()) {
                drawColor = Color.Lerp(drawColor, new Color(150, 200, 110), 0.3f);
                if (Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_Light>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.4f)
                        , new Vector2(0f, Main.rand.NextFloat(1f, 2.4f)), new Color(170, 220, 110), 0.2f)
                        ?.Configure(Main.rand.Next(12, 20), 0.75f);
                }
            }
            //财富祝福：金辉闪烁
            if (npc.HasBuff<WealthBlessingDebuff>() && Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f)
                    , new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), new Color(255, 226, 130), 0.4f)
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        /// <summary>财富祝福的兑现：死亡时掉落奉献银币并迸发金雨伤害(服务器/单人语境)</summary>
        public override void OnKill(NPC npc) {
            if (!npc.HasBuff<WealthBlessingDebuff>() || npc.friendly) {
                return;
            }

            //奉献银币
            int coins = Main.rand.Next(2, 6);
            Item.NewItem(npc.GetSource_Death(), npc.Hitbox, ItemID.SilverCoin, coins);

            //金雨伤害归属：就近寻找马太在职的主人(登记表在服务器同样有效)
            int burstOwner = -1;
            float closest = 1300f;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead
                    || !player.TryGetModPlayer(out ElysiumPlayer ep) || !ep.IsSeatAlive(7)) {
                    continue;
                }
                float dist = Vector2.Distance(player.Center, npc.Center);
                if (dist < closest) {
                    closest = dist;
                    burstOwner = i;
                }
            }
            if (burstOwner < 0) {
                return;
            }

            int damage = (int)(ElysiumPlayer.GetElysiumDamage(Main.player[burstOwner]) * 0.6f);
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MatthewCoinBurst>(), damage, 4f, burstOwner);
        }
    }
}
