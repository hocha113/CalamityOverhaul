using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 手枪「点穴连发」：黑钢制式手枪·白蜡握把。<br/>
    /// ①点穴：同一目标连中 3 发后，第 4 发自动化「点穴弹」（+80% 且穿甲 15）；
    /// ②匣式整匣拔插两拍；
    /// ③末发「清膛」：滑套后锁一响，弹重 +35%、击退 +50%。<br/>
    /// 后坐 1px + 滑套角度踢。<br/>
    /// 账目：射速原版，点穴均摊 +20%（4 发一循环 ×1.8/4）、清膛 +3%，
    /// 弹匣占空比 0.90（速装身份）→ 伤害行 ×0.95 合计约 112%（待游戏内标定）
    /// </summary>
    internal class GsHandgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Handgun;

        protected override string GsDescFallback =>
            "Reforged: land 3 shots on the same target and the 4th becomes a pressure-point round;\nit hits 80% harder and ignores 15 armor. The last round racks the slide with extra punch.\nBox-magazine drills: tactical reload is nearly instant, and the sweet spot is generous";
        public override int MagSize => 12;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Box;
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
            ConsumePointShot(mp, position);
            return null;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (pendingMark < 2f) {
                pendingMark = 1f;   //清膛弹标
            }
            ConsumePointShot(mp, position);
            if (!VaultUtils.isServer) {
                //滑套后锁脆响
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.45f }, position);
            }
            return null;
        }

        /// <summary>点穴弹出膛：打 2 档标 + 出膛音</summary>
        private void ConsumePointShot(GsGunsEarlyPlayer mp, Vector2 position) {
            if (!mp.comboReady) {
                return;
            }
            mp.comboReady = false;
            mp.comboHits = 0;
            pendingMark = 2f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.7f, Pitch = 0.5f }, position);
            }
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
                //点穴命中：震穴重响
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
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
            if (!VaultUtils.isServer) {
                //退匣
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.25f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //上匣拍、放套拍
                SoundEngine.PlaySound(index == 1
                    ? SoundID.Grab with { Volume = 0.6f, Pitch = 0.1f }
                    : SoundID.Unlock with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
            }
        }

        //==================== 后坐姿态：滑套踢（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 1.2f, 0.06f);
    }
}
