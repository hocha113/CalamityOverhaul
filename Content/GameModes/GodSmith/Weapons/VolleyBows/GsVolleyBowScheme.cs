using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 弓连弩·困难族共享基类：齐射编队 / 猎标追击 / 处决 rider 三件套。<br/>
    /// 充能：每次射击积攒 <see cref="ChargePerShot"/>，满 100 下一发自动成齐射；
    /// 右键充能 ≥<see cref="AltReleaseThreshold"/> 可按比例提前泄放。<br/>
    /// 弹药口径：齐射帧原版链已扣恰好 1 发，编队副箭 / 点射补射 / 追击箭 / 处决弹全部免费生成，
    /// 不覆写 GsCanConsumeAmmo（弹药节约装备照常生效）。<br/>
    /// 联机纪律：齐射与补射全部 owner 侧生成（GsShoot 只在 owner 端执行）；
    /// 编队位形来自 FormationLib 纯函数与 MarkData2 相位，各端确定性一致；
    /// 猎标层数是攻击方端本地量（命中钩子只在攻击方端跑），跨端处决表现全走真弹幕
    /// </summary>
    internal abstract class GsVolleyBowScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "VolleyBows";

        //==================== 家族色板 ====================

        /// <summary>齐射箭拖尾主色（逐武器覆写成族内风味色）</summary>
        protected virtual Color TrailColor => GameModeTheme.GodSmithAccent;

        //==================== 编队 profile（逐武器覆写） ====================

        /// <summary>齐射编队箭总数（含主箭）</summary>
        protected virtual int VolleyCount => 3;

        /// <summary>编队形状</summary>
        protected virtual GsVolleyFormation Formation => GsVolleyFormation.Line;

        /// <summary>编队间距 px（Cone 时解释为总扇角度数）</summary>
        protected virtual float SpreadPx => 14f;

        /// <summary>每次有效射击积攒的充能（满 100 齐射）。快弓取低值，期望增益见各武器注释</summary>
        protected virtual float ChargePerShot => 100f / 6f;

        /// <summary>编队副箭伤害系数（主箭恒 1.0）</summary>
        protected virtual float SideArrowMul => 0.55f;

        /// <summary>齐射箭初速倍率（钴钢连弩 rider 用）</summary>
        protected virtual float VolleyVelMul => 1f;

        /// <summary>齐射编队是否置换弹种（默认用玩家弹药弹种）</summary>
        protected virtual int VolleyProjType(int ammoProjType) => ammoProjType;

        /// <summary>true=齐射帧叠加在原版射击之上（经典不毁弓）；false=齐射帧替换原版弹幕</summary>
        protected virtual bool VolleyAdditive => false;

        //==================== 标记 profile ====================

        /// <summary>齐射命中叠标层数；0=本武器不参与猎标</summary>
        protected virtual int MarksPerVolleyHit => 1;

        /// <summary>标记层上限（驻留满层后下一次标记型命中触发处决）</summary>
        protected virtual int MarkCap => 3;

        /// <summary>标记持续帧（计划口径 4 秒）</summary>
        protected virtual int MarkDuration => 240;

        /// <summary>有标时每第 N 发普通箭放出 1 支追击箭；0=关闭</summary>
        protected virtual int PursuitEvery => 4;

        /// <summary>追击箭伤害系数</summary>
        protected virtual float PursuitDamageMul => 0.4f;

        //==================== 连弩点射 profile ====================

        /// <summary>连弩三连点射开关（组签名：第 N 发后 +4f/+8f 补射两发）</summary>
        protected virtual bool UsePointBlast => false;

        /// <summary>每第 N 发触发三连点射</summary>
        protected virtual int PointBlastEvery => 6;

        /// <summary>补射两发的伤害系数（计划 0.75 会叠爆强度带，压至 0.3，偏差见回报）</summary>
        protected virtual float PointBlastMul => 0.3f;

        //==================== 右键泄放 ====================

        /// <summary>右键提前泄放的充能门槛</summary>
        protected virtual float AltReleaseThreshold => 60f;

        //==================== 瞬时字段（GsShoot 只在 owner 端执行，天然本机契约） ====================

        /// <summary>射击计数（点射/追击节拍）</summary>
        protected int shotCounter;

        private int pendingRole;
        private float pendingParam;
        private bool pendingTag;

        //==================== 射击主流程 ====================

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
            shotCounter++;

            //右键泄放：按充能比例折算编队箭数
            if (player.altFunctionUse == 2 && vp.Charge >= AltReleaseThreshold) {
                int count = Math.Max(2, (int)MathF.Round(VolleyCount * vp.Charge / 100f));
                vp.Charge = 0f;
                FireVolley(item, player, source, position, velocity, type, damage, knockback, count);
                VolleyFX(player, position, velocity);
                AfterVolley(item, player, vp);
                return VolleyAdditive ? null : false;
            }

            //满充：本发自动成齐射
            if (vp.Charge >= 100f) {
                vp.Charge = 0f;
                FireVolley(item, player, source, position, velocity, type, damage, knockback, VolleyCount);
                VolleyFX(player, position, velocity);
                AfterVolley(item, player, vp);
                return VolleyAdditive ? null : false;
            }

            //普通射击：积攒充能
            float before = vp.Charge;
            vp.Charge = MathF.Min(100f, vp.Charge + ChargePerShot);
            if (before < 100f && vp.Charge >= 100f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.4f }, player.Center);
            }

            //连弩三连点射：+4f/+8f 同弹道 ±2° 补射，免弹药
            if (UsePointBlast && PointBlastEvery > 0 && shotCounter % PointBlastEvery == 0) {
                int blastDamage = (int)(damage * PointBlastMul);
                for (int i = 0; i < 2; i++) {
                    vp.Enqueue(new GsPendingShot {
                        Delay = 4 + i * 4,
                        WeaponType = item.type,
                        ProjType = type,
                        Velocity = velocity.RotatedBy(MathHelper.ToRadians(i == 0 ? 2f : -2f)),
                        Damage = blastDamage,
                        Knockback = knockback * 0.5f,
                        Role = GsVolleyRole.PointBlast,
                    });
                }
            }

            //标记追击：有标敌在场时每第 N 发分裂 1 支追击箭（15f 节流）
            if (PursuitEvery > 0 && shotCounter % PursuitEvery == 0 && vp.PursuitCooldown <= 0) {
                NPC marked = GsHuntMarkNPC.FindNearestMarked(player.Center, 1100f);
                if (marked != null) {
                    vp.PursuitCooldown = 15;
                    SpawnPursuit(player, marked, (int)(damage * PursuitDamageMul), knockback * 0.5f);
                }
            }

            return OnNormalShoot(item, player, source, position, velocity, type, damage, knockback);
        }

        /// <summary>普通射击帧的子类扩展（默认放行原版弹幕）</summary>
        protected virtual bool? OnNormalShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) => null;

        /// <summary>齐射发出后的子类扩展（Phantasm 喂射手时长等）</summary>
        protected virtual void AfterVolley(Item item, Player player, GsVolleyPlayer vp) { }

        /// <summary>
        /// 生成齐射编队：默认按 FormationLib 位形一次性铺开。
        /// Rain/Butterfly 类分帧编队由子类覆写（借 GsVolleyPlayer 队列错帧）
        /// </summary>
        protected virtual void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int mainIndex = FormationLib.MainIndex(count);
            for (int i = 0; i < count; i++) {
                FormationLib.Get(Formation, i, count, SpreadPx, out float side, out float back, out float rotOff);
                bool isMain = i == mainIndex;
                int dmg = isMain ? damage : (int)(damage * SideArrowMul);
                Vector2 pos = position + perp * side - dir * back;
                Vector2 vel = velocity.RotatedBy(rotOff) * VolleyVelMul;
                SpawnTagged(player, source, pos, vel, VolleyProjType(type), dmg,
                    isMain ? knockback : knockback * 0.7f,
                    isMain ? GsVolleyRole.VolleyMain : GsVolleyRole.VolleySide, i);
            }
        }

        /// <summary>齐射帧口部三连闪与弦响（owner 端个人反馈，编队箭本体跨端可见）</summary>
        protected void VolleyFX(Player player, Vector2 position, Vector2 velocity) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.9f, Pitch = 0.2f }, position);
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(position + dir * (10f + i * 9f),
                    dir * (1.5f + i * 0.8f), TrailColor, 0.12f - i * 0.02f)?.Configure(10 + i * 3, 0.8f);
            }
        }

        //==================== 打标生成（pending 机制） ====================

        /// <summary>
        /// 生成一支带角色标的弹幕：出生源用 ItemUse 类，router 自动打标，
        /// 打标回调窗口内把角色与参数写进 MarkData/MarkData2 随生成包过线。
        /// 只在 owner 端调用（GsShoot / 队列消费都满足）
        /// </summary>
        internal Projectile SpawnTagged(Player player, IEntitySource source, Vector2 pos, Vector2 vel,
            int projType, int damage, float knockback, int role, float param = 0f) {
            pendingRole = role;
            pendingParam = param;
            pendingTag = true;
            int idx = Projectile.NewProjectile(source, pos, vel, projType, damage, knockback, player.whoAmI);
            pendingTag = false;
            return idx >= 0 && idx < Main.maxProjectiles ? Main.projectile[idx] : null;
        }

        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (pendingTag) {
                router.MarkData = pendingRole;
                router.MarkData2 = pendingParam;
            }
            OnSpawnMarkedHook(proj, router);
        }

        /// <summary>打标出生窗口的子类扩展（识别凤凰、改写角色、穿透 rider 等；此窗口内改 MarkData 安全）</summary>
        protected virtual void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) { }

        //==================== 右键 ====================

        public override bool? GsAltFunctionUse(Item item, Player player)
            => player.GetModPlayer<GsVolleyPlayer>().Charge >= AltReleaseThreshold ? true : null;

        //==================== 连弩后坐姿态 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (!UsePointBlast || player.itemAnimationMax <= 0) {
                return;
            }
            //每发 2px 后坐，随使用进度回弹（确定性输入，各端画同一姿态）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            Vector2 dir = new(player.direction, 0f);
            player.itemLocation -= dir * (2f * progress);
        }

        //==================== 弹幕表现（各端） ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.None || VaultUtils.isServer) {
                return;
            }
            //编队箭飞行拖尾：低频火星点缀（禁恒速裸箭），发射类四相中的飞行相
            int interval = role == GsVolleyRole.PointBlast ? 6 : 4;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.05f,
                    TrailColor, 0.09f)?.Configure(9, 0.75f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.None) {
                return null;
            }
            DrawSpeedGhost(proj, TrailColor, role == GsVolleyRole.PointBlast ? 0.22f : 0.34f);
            return null;
        }

        /// <summary>速度重影拖尾：沿速度反向三层衰减残像，A=0 加色，identity 定相零随机</summary>
        protected static void DrawSpeedGhost(Projectile proj, Color color, float strength) {
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.61f);
            Color glow = color with { A = 0 };
            for (int i = 1; i <= 3; i++) {
                Vector2 at = proj.Center - proj.velocity * (0.55f * i);
                float alpha = strength * pulse / i;
                Main.EntitySpriteDraw(tex, at - Main.screenPosition, null, glow * alpha,
                    proj.rotation, tex.Size() * 0.5f, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            }
        }

        //==================== 命中：叠标与驻留处决 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            Player owner = Main.player[proj.owner];

            if (MarksPerVolleyHit > 0 && IsMarkingHit(proj, role) && GsHuntMarkNPC.CanMark(target)) {
                GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
                mark.Cap = MarkCap;
                if (mark.Stacks >= MarkCap) {
                    //驻留处决：满层敌再吃一次标记型命中，消耗全部层触发 rider
                    mark.Stacks = 0;
                    mark.Timer = 0;
                    ExecuteFX(target);
                    OnExecute(owner, target, proj, damageDone);
                }
                else {
                    mark.Stacks = Math.Min(MarkCap, mark.Stacks + MarksPerVolleyHit);
                    mark.Timer = MarkDuration;
                }
            }

            OnMarkedProjHit(proj, target, hit, damageDone, router);
        }

        /// <summary>哪些角色的命中参与叠标与处决触发（默认齐射主/副箭）</summary>
        protected virtual bool IsMarkingHit(Projectile proj, int role)
            => role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide;

        /// <summary>处决 rider（owner 端）。默认：立即补射两支追击箭咬向目标</summary>
        protected virtual void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            for (int i = 0; i < 2; i++) {
                SpawnPursuitFrom(player, player.Center + new Vector2(0f, -20f + i * 40f),
                    target, (int)(proj.damage * PursuitDamageMul), 1f);
            }
        }

        /// <summary>打标弹幕命中的子类扩展（花瓣爆、蚀影追加等；处决判定之后调用）</summary>
        protected virtual void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) { }

        /// <summary>处决触发的通用点缀（攻击方端个人反馈；跨端表现由 rider 弹幕承担）</summary>
        protected void ExecuteFX(NPC target) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = 0.5f }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, TrailColor, 0.2f)?.Configure(10, 0.85f);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    TrailColor, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        //==================== 通用生成 helpers（owner 端调用） ====================

        /// <summary>从玩家口部放出一支追击箭（Misc 源，不打标，弹幕自治）</summary>
        protected void SpawnPursuit(Player player, NPC target, int damage, float knockback)
            => SpawnPursuitFrom(player, player.Center, target, damage, knockback);

        /// <summary>从指定位置放出一支追击箭</summary>
        protected static void SpawnPursuitFrom(Player player, Vector2 from, NPC target, int damage, float knockback) {
            if (damage <= 0 || target == null || !target.active) {
                return;
            }
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * 12.5f;
            Projectile.NewProjectile(player.GetSource_Misc("GsVolleyPursuit"), from, vel,
                ModContent.ProjectileType<GsPursuitArrow>(), damage, knockback, player.whoAmI, target.whoAmI);
        }

        /// <summary>生成一记参数化 AoE 爆（Misc 源；theme 见 GsVolleyBurstProj 主题表）</summary>
        protected static void SpawnBurst(Player player, Vector2 pos, int damage, float radiusPx, int theme) {
            if (damage <= 0) {
                return;
            }
            Projectile.NewProjectile(player.GetSource_Misc("GsVolleyBurst"), pos, Vector2.Zero,
                ModContent.ProjectileType<GsVolleyBurstProj>(), damage, 2f, player.whoAmI, radiusPx, theme);
        }
    }
}
