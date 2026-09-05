using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 啾鸣号角「七音阶」：黄铜号角枪·音栓键片。<br/>
    /// ①七音匣：一匣七发按 do 到 si 上行，每发一音；
    /// ②「和弦」：同一目标连中三音，以第三音上色炸出调色爆；
    /// ③音匣装填是一段上行音阶（七个键位响）。<br/>
    /// 后坐 1px + 号口上扬。<br/>
    /// 账目：射速原版；和弦 3 中一循环均摊 +14%，伤害行 ×0.98 → 约 110%（待游戏内标定）
    /// </summary>
    internal class GsPewMaticHorn : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.PewMaticHorn;

        protected override string GsDescFallback =>
            "Reforged: a seven-note magazine, do to ti, each pew tinted by its note.\nLand three notes in a row on the same target to strike a chord: a paint burst in the third note's color.\nReloading plays the scale back up; catch the sweet spot on the high note for bigger chords";
        public override int MagSize => 7;
        public override int ReloadTicks => 42;
        public override GsReloadStyle Style => GsReloadStyle.Music;
        protected override int ReloadCueCount => 7;
        protected override float GetRecoil(bool lastRound) => 1f;

        /// <summary>七音色板：do 到 si（和弦漂字着色）</summary>
        internal static readonly Color[] NotePalette = [
            new Color(226, 72, 72), new Color(232, 143, 58), new Color(226, 208, 74),
            new Color(96, 200, 96), new Color(72, 148, 226), new Color(140, 92, 208),
            new Color(226, 108, 178)];

        /// <summary>七音音高：do 到 si 的大调音阶（半音比）</summary>
        private static readonly float[] NotePitch = [0f, 2f, 4f, 5f, 7f, 9f, 11f];

        /// <summary>和弦漂字</summary>
        internal static LocalizedText ChordText;

        public override void GsSetStaticDefaults() {
            ChordText = this.GetLocalization("Chord", () => "Chord!");
        }

        /// <summary>伤害行 ×0.98：和弦均摊回缩，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 0.98f;

        /// <summary>本发音序（0=do ... 6=si）。Fire* 时余弹已被共享层扣 1，故减一还原</summary>
        private int NoteIndex(GsGunsEarlyPlayer mp) => Math.Clamp(MagSize - mp.magLeft - 1, 0, 6);

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => PlayNote(mp, position);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => PlayNote(mp, position);

        /// <summary>出膛即奏：打标本音（MarkData=音序+1）</summary>
        private bool? PlayNote(GsGunsEarlyPlayer mp, Vector2 position) {
            int note = NoteIndex(mp);
            pendingMark = note + 1f;
            if (!VaultUtils.isServer) {
                //大调上行：半音换算音高偏移
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = 0.55f,
                    Pitch = -0.3f + NotePitch[note] / 12f * 0.8f
                }, position);
            }
            return null;
        }

        //==================== 和弦（owner 端权威） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer || router.MarkData < 1f) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsGunsEarlyPlayer mp = State(player);
            int note = (int)router.MarkData - 1;

            if (mp.paintTarget != target.whoAmI) {
                mp.paintTarget = target.whoAmI;
                mp.paintStreak = 0;
            }
            mp.paintStreak++;
            mp.paintColor = note;

            if (mp.paintStreak >= 3) {
                mp.paintStreak = 0;
                //和弦：以第三音上色的调色爆
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                    Math.Max(1, (int)(proj.damage * 0.8f)), 3f, proj.owner,
                    70f, 2f, Math.Clamp(note, 0, 6));
                if (!VaultUtils.isServer) {
                    //三连音收束和弦
                    for (int i = 0; i < 3; i++) {
                        SoundEngine.PlaySound(SoundID.Item26 with {
                            Volume = 0.6f - i * 0.12f,
                            Pitch = -0.3f + NotePitch[Math.Clamp(note - 2 + i * 2, 0, 6)] / 12f * 0.8f
                        }, target.Center);
                    }
                    CombatText.NewText(player.getRect(), NotePalette[note], ChordText.Value);
                }
            }
        }

        //==================== 音匣装填：上行音阶 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.45f, Pitch = 0.2f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //七键位逐个响：do re mi fa sol la si
                int note = Math.Clamp(index - 1, 0, 6);
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = 0.5f,
                    Pitch = -0.3f + NotePitch[note] / 12f * 0.8f
                }, player.Center);
            }
        }

        //==================== 后坐姿态：号口上扬（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 1f, 0.08f);
    }
}
