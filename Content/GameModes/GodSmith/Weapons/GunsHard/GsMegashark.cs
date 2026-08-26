using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 巨兽鲨重铸：进食节奏。原版 50% 省弹词条原样不动。<br/>
    /// [进食狂潮]：持续开火每 20 发提一档射速（+8%/档至 +24%），停火即逐渐回落。<br/>
    /// [鲨口收束]：散布收束 70%、+15% 伤的咬合精度形态。<br/>
    /// 两档共享「血腥味」：累计 50 次命中后 3 秒内命中挂流血，鲨闻到血了
    /// </summary>
    internal class GsMegashark : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Megashark;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch jaw\n" +
            "Feeding Frenzy ramps fire rate the longer you hold the trigger; Shark Bite tightens the spread and bites 15% harder\n" +
            "Land 50 hits to draw blood: for 3 seconds every hit leaves a bleeding wound";

        /// <summary>狂潮已连续射击数；只在 owner 路径读写</summary>
        private int frenzyShots;

        /// <summary>累计命中数（血腥味计数）；命中回调只在攻击方端跑，天然本地</summary>
        private int hitCount;

        /// <summary>血腥味有效期（世界帧）</summary>
        private uint bloodUntil;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFrenzy", EnName = "Feeding Frenzy",
            },
            new GsFireMode {
                Key = "ModeSharkBite", EnName = "Shark Bite",
                DamageMul = 1.15f, Converge = 0.7f,
            },
        ];

        protected override float GsGunUseSpeed(Item item, Player player) {
            //狂潮档射速梯度：20 发一档、三档封顶（远端读默认 0 层，仅动画速率差异）
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            if (mp.ModeIndex != 0) {
                return 1f;
            }
            return 1f + 0.08f * Math.Min(3, frenzyShots / 20);
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex == 0) {
                int oldStage = Math.Min(3, frenzyShots / 20);
                frenzyShots = Math.Min(60, frenzyShots + 1);
                int newStage = Math.Min(3, frenzyShots / 20);
                if (newStage > oldStage && !VaultUtils.isServer) {
                    //升档提示：鲨颚咬合声渐急
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.3f + 0.25f * newStage }, position);
                }
            }
            return null;
        }

        protected override void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) {
            //停火 1 秒后狂潮逐 tick 回落（约每 20 tick 掉一档）
            if (frenzyShots > 0 && Main.GameUpdateCount - mp.LastShotTick > 60) {
                frenzyShots--;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：血腥味是本地计数（训练假人不算，防挂机攒层）
            if (target.type == NPCID.TargetDummy || target.friendly) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (now < bloodUntil) {
                //血腥味窗口内：命中挂流血（客户端 AddBuff 自动请求服务器权威落地）
                target.AddBuff(BuffID.Bleeding, 100);
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                        Main.rand.NextVector2Circular(2f, 1.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f),
                        new Color(190, 30, 40), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(18, 28));
                }
                return;
            }
            hitCount++;
            if (hitCount >= 50) {
                hitCount = 0;
                bloodUntil = now + 180;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie9 with { Volume = 0.5f, Pitch = 0.35f }, target.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                            Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                            new Color(190, 30, 40), Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(20, 32));
                    }
                }
            }
        }

        internal override void GsGunHeldReset(Player player) {
            frenzyShots = 0;
            hitCount = 0;
            bloodUntil = 0;
        }
    }
}
