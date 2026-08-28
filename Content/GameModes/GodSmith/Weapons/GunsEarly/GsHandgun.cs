using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 手枪「点穴连发」：黑钢制式手枪·白蜡握把。<br/>
    /// ①点穴：同一目标连中 3 发后，第 4 发自动化「点穴弹」（+80% 且穿甲 15，白金曳光）；
    /// ②精准速装：匣式整匣拔插，战术装填只要 16t（本族最快手），完美窗最宽；
    /// ③末发「清膛」：滑套后锁一响，弹重 +35%、击退 +50%。<br/>
    /// 后坐 1px + 滑套角度踢。<br/>
    /// 账目：射速原版，点穴均摊 +20%（4 发一循环 ×1.8/4）、清膛 +3%，
    /// 弹匣占空比 0.90（速装身份）→ 伤害行 ×0.95 合计约 112%（待游戏内标定）
    /// </summary>
    internal class GsHandgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Handgun;

        protected override string GsDescFallback =>
            "Reforged: land 3 shots on the same target and the 4th becomes a pressure-point round;\n" +
            "it hits 80% harder and ignores 15 armor. The last round racks the slide with extra punch.\n" +
            "Box-magazine drills: tactical reload is nearly instant, and the sweet spot is generous";

        public override int MagSize => 12;
        public override int ReloadTicks => 40;
        /// <summary>精准速装：战术装填 16t，本族最快手</summary>
        public override int TacticalReloadTicks => 16;
        public override GsReloadStyle Style => GsReloadStyle.Box;
        public override int PerfectWindow => 12;
        protected override int ReloadCueCount => 2;
        protected override float GetRecoil(bool lastRound) => 1f;

        /// <summary>点穴漂字</summary>
        internal static LocalizedText PointText;

        public override void GsSetStaticDefaults() {
            PointText = this.GetLocalization("PointShot", () => "Pressure point!");
        }

        /// <summary>伤害行 ×0.95：点穴均摊回缩，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 0.95f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (mp.comboReady) {
                damage = (int)(damage * 1.8f);  //点穴弹
                velocity *= 1.25f;
            }
            if (lastRound) {
                damage = (int)(damage * 1.35f); //清膛
                knockback *= 1.5f;
            }
        }

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ConsumePointShot(mp, position, velocity);
            return null;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (pendingMark < 2f) {
                pendingMark = 1f;   //清膛弹曳光
            }
            ConsumePointShot(mp, position, velocity);
            if (!VaultUtils.isServer) {
                //滑套后锁脆响
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.45f }, position);
            }
            return null;
        }

        /// <summary>点穴弹出膛：打 2 档标 + 出膛白闪</summary>
        private void ConsumePointShot(GsGunsEarlyPlayer mp, Vector2 position, Vector2 velocity) {
            if (!mp.comboReady) {
                return;
            }
            mp.comboReady = false;
            mp.comboHits = 0;
            pendingMark = 2f;
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.7f, Pitch = 0.5f }, position);
            PRTLoader.NewParticle<PRT_Light>(position + aim * 8f, Vector2.Zero,
                new Color(210, 235, 255), 0.4f)?.Configure(7, 0.85f);
        }

        /// <summary>穿甲只对点穴弹（owner 端命中裁决）</summary>
        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (router.MarkData >= 2f) {
                modifiers.ArmorPenetration += 15f;
            }
        }

        //==================== 点穴计数（owner 端） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsGunsEarlyPlayer mp = State(player);

            if (router.MarkData >= 2f) {
                //点穴命中：震穴环 + 白金迸溅
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                        new Color(210, 235, 255), 0f)?.Configure(0.04f, 0.4f, 12);
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                            Main.rand.NextVector2Circular(4f, 4f), Color.White,
                            Main.rand.NextFloat(0.4f, 0.6f))
                            ?.Configure(new Color(160, 210, 255), Main.rand.Next(12, 20), 0.15f, 0.8f);
                    }
                }
                return;
            }

            //连击计数：换目标重记
            if (mp.comboTarget != target.whoAmI) {
                mp.comboTarget = target.whoAmI;
                mp.comboHits = 0;
            }
            mp.comboHits++;
            if (mp.comboHits >= 3 && !mp.comboReady) {
                mp.comboReady = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = 0.5f }, player.Center);
                    CombatText.NewText(player.getRect(), new Color(190, 225, 255), PointText.Value);
                }
            }
            else if (!VaultUtils.isServer) {
                //连击爬音提示（第 1、2 响）
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f, Pitch = 0.1f * mp.comboHits }, target.Center);
            }
        }

        //==================== 匣式两拍装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //退匣：整匣坠落
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.25f }, player.Center);
            PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 5f, 3f),
                new Vector2(-player.direction * 0.5f, -1.2f),
                new Color(70, 74, 82), 0.9f)
                ?.Configure(new Color(150, 160, 175), 28, 0.7f);
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //上匣拍、放套拍
                SoundEngine.PlaySound(index == 1
                    ? SoundID.Grab with { Volume = 0.6f, Pitch = 0.1f }
                    : SoundID.Unlock with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
            }
        }

        //==================== 后坐姿态：滑套踢 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (1.2f * progress);
            player.itemRotation -= player.direction * 0.06f * progress;
        }

        //==================== 曳光表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            bool point = router.MarkData >= 2f;
            if (point) {
                Lighting.AddLight(proj.Center, 0.2f, 0.28f, 0.38f);
            }
            int interval = point ? 2 : 4;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.35f,
                    -proj.velocity * 0.04f,
                    point ? Color.White : GameModeTheme.GodSmithEmber,
                    Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(point ? new Color(150, 205, 255) : GameModeTheme.GodSmithEmber,
                        Main.rand.Next(8, 14), 0.12f, 0.7f);
            }
        }
    }
}
