using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【蓝冰月刃】材质：寒潭魔冰锻成的月牙镰。签名：①每拍掷出全额自旋冰镰（原版每挥必发的保真，
    /// 初速 12、减速自旋、无 autoReuse 手感不动）②凝停冰晶：旋镰转到尽头不消散，凝成冰晶停在原地，
    /// 用挥砍打碎则向前炸出 5 枚冰棱扇 ③越慢转得越急的晶化变脆观感
    /// </summary>
    internal class GsIceSickle : GsOdditiesComboScheme
    {
        public override int TargetItemID => ItemID.IceSickle;

        protected override int HeldProjID => ModContent.ProjectileType<GsIceSickleHeld>();

        protected override int ComboBeats => 3;

        protected override string GsDescFallback =>
            "Reforged: the thrown sickle freezes into an ice prism where it stops;\nshatter the prism with a slash to burst a fan of icicles forward";
        internal static readonly Color FrostWhite = new(210, 242, 255);  //霜白刃缘
        internal static readonly Color GlacialBlue = new(118, 178, 232); //蓝冰体色
        internal static readonly Color CoreBlue = new(160, 224, 255);    //冰芯亮蓝

        //×1.05：每拍全额旋镰是原版保真不算增益；净增收益=凝停冰晶的碎晶扇
        //（5×0.4 伤 + 霜火 60 帧，要专门用挥砍点碎的条件收益），计入包络后底伤只小幅让利
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0 而本器 noMelee=false，
        /// ItemCheck 近战尾巴（挥舞碰撞箱直击+切割）在 owner 端仍会逐帧执行，不压则与 held 双份直击
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;
    }

    /// <summary>
    /// 蓝冰月刃手持：三拍。0/1 交替快斩，2 凝晶重斩（前压）。每拍斩切爆发掷全额旋镰。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsIceSickleHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.IceSickle;
        protected override Color EdgeBright => GsIceSickle.FrostWhite;
        protected override Color BodyMain => GsIceSickle.GlacialBlue;
        protected override Color HotAccent => GsIceSickle.CoreBlue;

        /// <summary>原版 scale 1.15 的大刃，触及略放</summary>
        protected override float BaseReach => 124f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //凝晶重斩：重弧前压
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.08f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.18f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = stage == 0 ? 6 : 5;
            b.Recover = 10;
            b.DamageMult = 0.95f;
            b.SwingPitch = stage == 0 ? 0.08f : 0.16f; //冰刃音色偏亮
            return b;
        }

        /// <summary>斩切爆发：掷全额旋镰（原版保真）+ 扫描点碎前方晶化旋镰</summary>
        protected override void OnSlashBegin() {
            Vector2 aim = baseAngle.ToRotationVector2();
            int spinType = ModContent.ProjectileType<GsIceSickleSpinProj>();
            //原版每挥必发的保真：全额伤害、初速 12
            SpawnOwnedProj(spinType, Hand + aim * 24f, aim * 12f, Projectile.damage, Projectile.knockBack);

            //碎晶触发：owner 记账，斩击落点（手前 90px）170px 内的晶化旋镰标 ai[1]=1 过线，下一帧自炸冰棱扇
            if (Projectile.owner == Main.myPlayer) {
                Vector2 focus = Hand + aim * 90f;
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.owner == Projectile.owner && p.type == spinType
                        && p.ai[0] == 1f && p.ai[1] == 0f && p.Distance(focus) <= 170f) {
                        p.ai[1] = 1f;
                        p.netUpdate = true;
                    }
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.45f, Pitch = 0.25f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 自旋冰镰：原版 263 保真（34px、穿透 4、撞墙亡、冰伤、timeLeft 180、idStatic 8、×0.95 减速），
    /// 自旋越慢转得越急（晶化变脆观感）。签名「凝停冰晶」：速度低于 0.4 时定格为冰晶
    /// （ai[0]=1，各端同式判定 + netUpdate 对齐；ai[2] 存晶化朝向），晶化态不判伤、90 帧自然碎裂；
    /// 被挥砍标记 ai[1]=1 则 owner 端向最近敌或存向炸 5 枚冰棱扇。贴图借原版冰镰弹幕（263）默认绘制
    /// </summary>
    internal class GsIceSickleSpinProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceSickle;
        public override LocalizedText DisplayName => Language.GetText("ItemName.IceSickle");

        /// <summary>自旋方向（首帧按横速符号定，各端同式）</summary>
        private int spinDir;

        private bool IsCrystal => Projectile.ai[0] == 1f;

        public override void SetDefaults() {
            //镜像原版 263：Projectile.cs SetDefaults
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 180;
            Projectile.coldDamage = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 8;
        }

        /// <summary>原版 aiStyle 106（含 263）不切物块，照封</summary>
        public override bool? CanCutTiles() => false;

        /// <summary>晶化态不判伤</summary>
        public override bool? CanDamage() => IsCrystal ? false : null;

        public override void AI() {
            if (spinDir == 0) {
                spinDir = Projectile.velocity.X >= 0f ? 1 : -1;
            }
            if (IsCrystal) {
                CrystalAI();
                return;
            }

            //飞行：×0.95 减速（原版保真）；自旋越慢转得越急=晶化变脆观感
            Projectile.rotation += spinDir * (0.15f + 0.55f * (1f - Projectile.timeLeft / 180f));
            Projectile.velocity *= 0.95f;
            //镜像原版 GetAlpha(263)：随 timeLeft 渐隐
            Projectile.alpha = 255 - Math.Clamp(Projectile.timeLeft, 0, 255);

            //凝停：慢到临界即定格成冰晶。速度衰减各端确定同步，同式就地翻转，netUpdate 对齐掉队者
            if (Projectile.velocity.Length() < 0.4f) {
                Projectile.ai[0] = 1f;
                Projectile.ai[2] = Projectile.velocity.ToRotation(); //存晶化朝向，碎晶无敌可寻时用
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = 90;
                Projectile.alpha = 0; //冰晶定格实体化，不再渐隐
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.55f }, Projectile.Center);
                }
            }
        }

        private void CrystalAI() {
            //被斩击点碎：owner 端炸冰棱扇，各端随后走 Kill
            if (Projectile.ai[1] == 1f) {
                if (Projectile.owner == Main.myPlayer) {
                    ShatterIntoShards();
                }
                Projectile.Kill();
                return;
            }
            Projectile.velocity = Vector2.Zero;
        }

        /// <summary>碎晶扇：朝最近敌（600px 内）否则存下的晶化朝向，扇形 5 枚各 0.4 伤（只在 owner 端被调）</summary>
        private void ShatterIntoShards() {
            float aim = Projectile.ai[2];
            NPC target = null;
            float best = 600f;
            foreach (NPC n in Main.ActiveNPCs) {
                if (!n.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Distance(n.Center);
                if (dist < best) {
                    best = dist;
                    target = n;
                }
            }
            if (target != null) {
                aim = (target.Center - Projectile.Center).ToRotation();
            }
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.4f));
            int type = ModContent.ProjectileType<GsIceSickleCrystalProj>();
            for (int i = -2; i <= 2; i++) {
                Vector2 vel = (aim + i * 0.21f).ToRotationVector2() * 9f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    type, dmg, 1f, Projectile.owner);
            }
        }

        /// <summary>碎冰响：打碎与 90 帧自然碎裂共用</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.1f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 碎晶冰棱：晶化旋镰被斩碎时的扇形弹（0.4 伤、初速 9、轻重力、穿透 1、命中挂霜火 60）。
    /// 贴图借原版冰刃冰弹（118）默认绘制
    /// </summary>
    internal class GsIceSickleCrystalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;
        public override LocalizedText DisplayName => Language.GetText("ItemName.IceSickle");

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            //轻重力，尖头顺速度
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.12f, 10f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 60);
    }
}
