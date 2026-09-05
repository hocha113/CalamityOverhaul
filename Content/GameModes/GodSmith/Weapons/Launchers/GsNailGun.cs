using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 钉枪重铸：木匠的嵌钉节奏。钉命中不再消耗而是嵌进目标
    /// （每目标至多 5 枚），嵌钉期间该目标受到的一切伤害 +3%/钉（封顶 15%）。<br/>
    /// 钉 = 弹幕状态载体，天然同步：MarkData2 0 普通 / 1 嵌入
    /// </summary>
    internal class GsNailGun : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.NailGun;

        protected override string GsDescFallback =>
            "Reforged: nails embed into flesh (up to 5 per victim), each making the victim take +3% damage from all sources";
        /// <summary>每目标嵌钉上限</summary>
        internal const int EmbedCap = 5;

        /// <summary>数一个目标身上（该玩家的）嵌钉数</summary>
        internal static int CountEmbedded(int owner, int npcIndex) {
            int n = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != ProjectileID.NailFriendly || p.owner != owner
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter r)
                    || r.MarkData2 != 1f || (int)p.ai[0] != npcIndex) {
                    continue;
                }
                if (++n >= EmbedCap) {
                    break;
                }
            }
            return n;
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 0.6f);
            return null;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.NailFriendly || router.MarkData2 != 1f) {
                return true;
            }
            //嵌入态：钉是插在宿主身上的状态载体
            NPC host = Main.npc[(int)proj.ai[0]];
            if (!host.active || host.life <= 0) {
                proj.Kill();
                return false;
            }
            float ang = IdentityHash01(proj.identity) * MathHelper.TwoPi;
            Vector2 offset = ang.ToRotationVector2() * (host.width * 0.28f);
            proj.Center = host.Center + offset;
            proj.rotation = ang - MathHelper.PiOver2;
            proj.velocity = Vector2.Zero;
            proj.friendly = false;
            proj.tileCollide = false;
            if (proj.alpha > 0) {
                proj.alpha = Math.Max(0, proj.alpha - 25);
            }
            return false;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只有未嵌入的主射钉转嵌入
            if (proj.type != ProjectileID.NailFriendly || router.MarkData2 != 0f
                || !target.active || target.life <= 0 || target.type == NPCID.TargetDummy
                || CountEmbedded(proj.owner, target.whoAmI) >= EmbedCap) {
                return;
            }
            //抵掉本次命中的穿透消耗让钉存活（>0 守卫防无限穿被写坏）
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
            proj.ai[0] = target.whoAmI;
            router.MarkData2 = 1f;
            proj.timeLeft = 600;
            proj.friendly = false;
            proj.tileCollide = false;
            proj.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 5 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 嵌钉承伤层：目标身上每枚嵌钉让它受到的一切来源伤害 +3%（封顶 15%）。
    /// 层数从场上弹幕即时清点（弹幕表各端同步，命中裁决端读数一致），
    /// NPC 身上不落任何字段。自建钩子自查模式旗
    /// </summary>
    internal class GsNailGunGlobalNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            int nails = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != ProjectileID.NailFriendly
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter r)
                    || r.MarkData2 != 1f || (int)p.ai[0] != npc.whoAmI) {
                    continue;
                }
                if (++nails >= GsNailGun.EmbedCap) {
                    break;
                }
            }
            if (nails > 0) {
                modifiers.FinalDamage *= 1f + 0.03f * nails;
            }
        }
    }
}
