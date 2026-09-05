using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 榴弹发射器重铸：工兵布控。榴弹碰砖即贴附、碰敌即粘身
    /// （贴敌爆炸 +25%，目标死亡立即殉爆），贴附后仍按原版引信倒数起爆；场上限 6 枚，
    /// 第 7 枚落位时最老的一枚自爆腾位。液体/集束/迷你核榴弹的爆炸行为原样保真。<br/>
    /// 状态走 MarkData：0 飞行 / 1 贴砖 / 2 贴敌；MarkData2 = 落位序号（最老判定键）。
    /// 贴附判定各端按同步的位置速度确定性推导，owner 端 netUpdate 收口
    /// </summary>
    internal class GsGrenadeLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.GrenadeLauncher;

        protected override string GsDescFallback =>
            "Reforged: grenades stick to walls and foes (+25% damage when stuck on flesh); up to 6 planted, the oldest pops to make room";
        /// <summary>榴弹主弹全家（普通/集束/液体/迷你核/干性）</summary>
        internal static readonly HashSet<int> GrenadeTypes = [
            ProjectileID.GrenadeI, ProjectileID.GrenadeII, ProjectileID.GrenadeIII, ProjectileID.GrenadeIV,
            ProjectileID.ClusterGrenadeI, ProjectileID.ClusterGrenadeII,
            ProjectileID.WetGrenade, ProjectileID.LavaGrenade, ProjectileID.HoneyGrenade,
            ProjectileID.MiniNukeGrenadeI, ProjectileID.MiniNukeGrenadeII, ProjectileID.DryGrenade,
        ];

        /// <summary>布设上限</summary>
        private const int PlantedCap = 6;

        /// <summary>每弹幕本地包：状态沿检测（贴附咔嗒声只响一次）</summary>
        private class GrenadeState
        {
            public int prevState;
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.2f);
            return null;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (!GrenadeTypes.Contains(proj.type)) {
                return true;
            }
            if (proj.timeLeft <= 3) {
                //爆窗：恢复判伤交回原版（Resize、置液、撒子雷都在这）
                proj.friendly = true;
                return true;
            }
            int state = (int)router.MarkData;
            GrenadeState st = router.GetOrCreateState<GrenadeState>();
            if (state != st.prevState) {
                //贴附沿：各端各响一声咔嗒
                if (state >= 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.45f, Pitch = -0.3f }, proj.Center);
                }
                st.prevState = state;
            }
            if (state == 0) {
                return true;
            }

            //贴附态：接管原版 AI（引信倒数照走），压掉命中判定
            proj.friendly = false;
            proj.velocity = Vector2.Zero;
            if (state == 2) {
                NPC npc = Main.npc[(int)proj.ai[0]];
                if (!npc.active || npc.life <= 0) {
                    //宿主死亡即殉爆
                    if (proj.IsOwnedByLocalPlayer()) {
                        GsDetonate(proj);
                    }
                    return false;
                }
                float ang = IdentityHash01(proj.identity) * MathHelper.TwoPi;
                proj.Center = npc.Center + ang.ToRotationVector2() * (npc.width * 0.3f);
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!GrenadeTypes.Contains(proj.type) || proj.timeLeft <= 3 || (int)router.MarkData != 0) {
                return;
            }
            //飞行段：压掉直击（碰敌不再转爆窗，改为贴附）
            proj.friendly = false;

            //贴敌：owner 端判定后 netUpdate 收口
            if (proj.IsOwnedByLocalPlayer()) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy(proj) || !proj.Hitbox.Intersects(npc.Hitbox)) {
                        continue;
                    }
                    proj.ai[0] = npc.whoAmI;
                    router.MarkData = 2f;
                    OnPlanted(proj, router);
                    return;
                }
            }

            //贴砖：预测下一帧碰撞，各端确定性推导
            Vector2 moved = Collision.TileCollision(proj.position, proj.velocity, proj.width, proj.height);
            if (moved != proj.velocity) {
                proj.position += moved;
                proj.velocity = Vector2.Zero;
                router.MarkData = 1f;
                if (proj.IsOwnedByLocalPlayer()) {
                    OnPlanted(proj, router);
                }
            }
        }

        /// <summary>落位收口（owner 端）：发落位序号、netUpdate、超上限爆最老</summary>
        private void OnPlanted(Projectile proj, GodSmithProjRouter router) {
            Player player = Main.player[proj.owner];
            GsLaunchersPlayer mp = player.GetModPlayer<GsLaunchersPlayer>();
            router.MarkData2 = ++mp.grenadeSeq;
            proj.netUpdate = true;

            int count = 0;
            Projectile oldest = null;
            float oldestSeq = float.MaxValue;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner != proj.owner || !GrenadeTypes.Contains(p.type) || p.timeLeft <= 3
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter r)
                    || r.MarkScheme != this || r.MarkData < 1f) {
                    continue;
                }
                count++;
                if (r.MarkData2 < oldestSeq) {
                    oldestSeq = r.MarkData2;
                    oldest = p;
                }
            }
            if (count > PlantedCap && oldest != null) {
                GsDetonate(oldest);
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //贴敌雷的爆炸吃 +25%（爆窗判伤时 MarkData 仍为 2）
            if (GrenadeTypes.Contains(proj.type) && (int)router.MarkData == 2) {
                modifiers.FinalDamage *= 1.25f;
            }
        }
    }
}
