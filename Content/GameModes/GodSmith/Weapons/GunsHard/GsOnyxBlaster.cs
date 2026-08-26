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
    /// 玛瑙爆破枪重铸：暗渍与黑洞。<br/>
    /// [玛瑙齐射]：原版爆球加霰粒原样；爆球爆点留 0.5 秒暗渍，滞留区多跳向心轻拽。<br/>
    /// [黑洞蓄压]：装填放慢到 2.2 倍换一颗巨玛瑙球（约 5.2 倍基伤），
    /// 爆点拉开 130px 黑洞把敌人拽向爆心，霰粒改为爆时环状 8 粒放射。<br/>
    /// 拉扯全部走命中击退向心（不写 NPC 速度），弹药身份由爆时真子弹结算保住
    /// </summary>
    internal class GsOnyxBlaster : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.OnyxBlaster;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch charge\n" +
            "Onyx Volley leaves a lingering dark stain at the blast that drags foes inward\n" +
            "Singularity Charge fires slower but hurls one huge onyx orb: its blast pulls everything toward the core and sprays your bullets in a ring";

        /// <summary>黑洞蓄压弹私有 flag</summary>
        private const int FlagHeavy = 1;

        /// <summary>玛瑙暗紫</summary>
        private static readonly Color OnyxPurple = new(150, 84, 226);

        /// <summary>本次射击的弹药子弹 type（蓄压档爆时环射用，打标窗口消费）</summary>
        private int pendingAmmoType;

        /// <summary>本次射击为蓄压弹的世界帧</summary>
        private uint heavyShotTick = uint.MaxValue;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeVolley", EnName = "Onyx Volley",
            },
            new GsFireMode {
                Key = "ModeSingularity", EnName = "Singularity Charge",
                UseSpeed = 1f / 2.2f,
            },
        ];

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex != 1) {
                return null;
            }
            //蓄压：只射一颗巨玛瑙球。伤害账：原版爆球为 2 倍基伤，蓄压 ×2.6 → 基伤 ×5.2；
            //霰粒不出膛，其预算转入爆时环状 8 粒（OnKill 补生成）
            heavyShotTick = Main.GameUpdateCount;
            pendingAmmoType = type;
            Projectile.NewProjectile(source, position, velocity * 0.85f, ProjectileID.BlackBolt,
                (int)(damage * 5.2f), knockback * 1.5f, player.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = -0.5f }, position);
                Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                //出手相：向后的暗紫烟锥
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(position - unit * Main.rand.NextFloat(4f, 14f),
                        -unit * Main.rand.NextFloat(1f, 2.4f), OnyxPurple * 0.6f,
                        Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(18, 28), 0.5f, 0.02f);
                }
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            bool heavy = proj.type == ProjectileID.BlackBolt && heavyShotTick == Main.GameUpdateCount;
            router.MarkData = PackMark(mp.ModeIndex, heavy ? FlagHeavy : 0);
            if (heavy) {
                //第二槽携带弹药子弹 type，爆时环射结算真子弹（弹药身份不灭）
                router.MarkData2 = pendingAmmoType;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //只演出爆球本体（环射子弹承签了标记，按 type 分流防误入）
            if (proj.type != ProjectileID.BlackBolt || VaultUtils.isServer) {
                return;
            }
            bool heavy = MarkFlagOf(router.MarkData) == FlagHeavy;
            Lighting.AddLight(proj.Center, OnyxPurple.ToVector3() * (heavy ? 0.6f : 0.3f));
            if (heavy && proj.timeLeft % 2 == 0) {
                //蓄压球飞行相：暗紫吞噬尾
                PRTLoader.NewParticle<PRT_Smoke>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    OnyxPurple * 0.7f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 20), 0.5f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            //蓄压球：加色重影读出巨球体量（identity 定相脉动，禁随机）
            if (proj.type != ProjectileID.BlackBolt || MarkFlagOf(router.MarkData) != FlagHeavy) {
                return null;
            }
            Main.instance.LoadProjectile(proj.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[proj.type].Value;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + proj.identity * 0.63f);
            Color glow = OnyxPurple * (0.55f * pulse);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null, glow,
                proj.rotation, tex.Size() / 2f, 1.6f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return null;
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //爆球爆点：齐射档留暗渍、蓄压档开黑洞 + 环状 8 粒真子弹。
            //owner 端补生成（Kill 各端都跑，生成守 owner 防翻倍）
            if (proj.type != ProjectileID.BlackBolt) {
                return;
            }
            bool heavy = MarkFlagOf(router.MarkData) == FlagHeavy;
            if (proj.owner == Main.myPlayer) {
                int zoneType = ModContent.ProjectileType<GsGunsHardZoneProj>();
                if (heavy) {
                    Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                        zoneType, Math.Max(1, proj.damage / 6), 1.2f, proj.owner, 130f, 1f);
                    int ammoType = (int)router.MarkData2;
                    if (ammoType > 0) {
                        //环状 8 粒真子弹：霰粒预算的爆点返还
                        for (int i = 0; i < 8; i++) {
                            Vector2 dir = (MathHelper.TwoPi * i / 8f).ToRotationVector2();
                            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, dir * 9f,
                                ammoType, (int)(proj.damage * 0.115f), 1f, proj.owner);
                        }
                    }
                }
                else if (MarkModeOf(router.MarkData) == 0) {
                    Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                        zoneType, Math.Max(1, proj.damage / 5), 0.6f, proj.owner, 90f, 0f);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = heavy ? 0.9f : 0.5f, Pitch = -0.4f }, proj.Center);
                int sparkCount = heavy ? 8 : 4;
                for (int i = 0; i < sparkCount; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                        Main.rand.NextVector2Circular(4f, 4f), OnyxPurple,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
                }
            }
        }
    }
}
