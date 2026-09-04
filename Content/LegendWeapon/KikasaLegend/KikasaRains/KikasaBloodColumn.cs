using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 血柱:血湖形态下血珠首次撞湖面,湖在落点应一声——自水面拔起的上升射流,
    /// 根粗头细、一侧先撕、头部冠状绽开断成血滴落回湖面;塌回=根部断供整柱坠回水里砸第二记水花,不淡出。
    /// 额外伤害,一柱对同一目标只结算一次;判定线自水线下 <see cref="RootDepthPx"/> 的根扎到柱头,半沉的怪也在里面。
    /// ai[0]=柱高 px、ai[1]=符标签位段(自珠子抄来)、ai[2]=柱宽 px,全部随生成包同步;
    /// 高度分档(手动/自卫/散射)由珠子的归属端本地字段在生成时折进 ai[0],柱本身不再分辨。
    /// 只在归属端生成(<see cref="SpawnFromDrop"/>),集中绘制在 <see cref="KikasaRainRender"/>
    /// </summary>
    internal class KikasaBloodColumn : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int RiseFrames = 6;
        public const int HoldFrames = 12;
        public const int CollapseFrames = 10;
        public const int TotalFrames = RiseFrames + HoldFrames + CollapseFrames;

        /// <summary>判定根扎进水线下的深度</summary>
        public const float RootDepthPx = 50f;

        private ref float HeightAi => ref Projectile.ai[0];
        private ref float WidthAi => ref Projectile.ai[2];

        /// <summary>符标签(0=无符),供符的绘制/命中分支</summary>
        internal int TalismanTag => KikasaTalismanHooks.ReadTagId(Projectile.ai[1]);

        /// <summary>符标签载荷</summary>
        internal int TalismanTagPayload => KikasaTalismanHooks.ReadTagPayload(Projectile.ai[1]);

        /// <summary>满柱高(px)</summary>
        internal float HeightPx => MathF.Max(HeightAi, 16f);

        /// <summary>柱宽(px)</summary>
        internal float WidthPx => MathF.Max(WidthAi, 8f);

        /// <summary>入水动能读数 0~1(自柱高反推,喂冠量)</summary>
        private float Ke => MathHelper.Clamp(
            (HeightPx - KikasaBloodForm.ColumnHeightMin * 0.4f)
            / (KikasaBloodForm.ColumnHeightMax - KikasaBloodForm.ColumnHeightMin * 0.4f), 0f, 1f);

        private float life;
        private bool erupted;
        private bool collapseBeatDone;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>塌回进度 0~1</summary>
        internal float CollapseT
            => MathHelper.Clamp((life - RiseFrames - HoldFrames) / (float)CollapseFrames, 0f, 1f);

        /// <summary>起柱包络:EaseOutBack 过冲窜起,随后微晃</summary>
        private float RiseT {
            get {
                float t = MathHelper.Clamp(life / RiseFrames, 0f, 1f);
                const float c1 = 1.3f;
                const float c3 = c1 + 1f;
                float rise = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * (t - 1f) * (t - 1f);
                float wobble = life > RiseFrames
                    ? 1f + MathF.Sin((life - RiseFrames) * 0.5f + Seed) * 0.02f : 1f;
                return MathHelper.Clamp(rise, 0f, KikasaBloodColumnDraw.RiseOvershoot) * wobble;
            }
        }

        /// <summary>当前柱高(px,含过冲;塌回的下坠由绘制侧按 CollapseT 自算)</summary>
        internal float CurrentHeightPx => HeightPx * RiseT;

        /// <summary>根部溅丘强度:起柱四帧涨满,塌回随进度退</summary>
        private float Mound => MathHelper.Clamp(life / 4f, 0f, 1f) * (1f - CollapseT);

        /// <summary>回落帘强度:起柱期无,持续段两翼渐起(液体到顶开始往回落),塌回拉满</summary>
        private float Fallback {
            get {
                float collapse = CollapseT;
                if (collapse > 0f) {
                    return MathHelper.Lerp(0.55f, 1f, collapse);
                }
                return MathHelper.Clamp((life - RiseFrames) / (float)HoldFrames, 0f, 1f) * 0.55f;
            }
        }

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>飞沫落回的水线:观看域在场取活水线,否则用柱根(没有湖就当普通坠落)</summary>
        private float SprayLakeY {
            get {
                Player owner = Owner;
                if (owner?.active == true && owner.TryGetModPlayer(out KikasaDomainPlayer kdp)
                    && kdp.AnyActive) {
                    return kdp.LakeWorldY;
                }
                return Projectile.Center.Y;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //一柱对同一目标只结算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.netImportant = true;
        }

        /// <summary>
        /// 归属端生成:高度=入水动能插值×分档倍率,宽随动能与珠体;伤害为珠伤害的额外一份;
        /// 符标签自珠子抄进 ai[1];在场柱超限不生成
        /// </summary>
        internal static void SpawnFromDrop(Projectile drop, KikasaInkDrop inkDrop, Vector2 surface, float ke) {
            if (CountAlive() >= KikasaBloodForm.MaxColumnsAlive) {
                return;
            }
            float k = MathHelper.Clamp((ke - 0.25f) / 0.75f, 0f, 1f);
            float height = MathHelper.Lerp(KikasaBloodForm.ColumnHeightMin, KikasaBloodForm.ColumnHeightMax, k)
                * MathHelper.Clamp(inkDrop.ColumnHeightMul, 0.1f, 1.5f);
            float width = MathHelper.Lerp(KikasaBloodForm.ColumnWidthMin, KikasaBloodForm.ColumnWidthMax, k)
                * MathHelper.Clamp(drop.scale, 0.8f, 1.6f);
            float dmgMul = KikasaBloodForm.ColumnDamageMul;
            if (inkDrop.ColumnScatter) {
                width *= KikasaBloodForm.ScatterColumnWidthMul;
                dmgMul *= KikasaBloodForm.ScatterColumnDamageMul;
            }
            float tag = KikasaTalismanHooks.PackTag(inkDrop.TalismanTagId, inkDrop.TalismanTagPayload);
            Projectile.NewProjectile(drop.GetSource_FromThis(), surface, Vector2.Zero,
                ModContent.ProjectileType<KikasaBloodColumn>(),
                Math.Max((int)(drop.damage * dmgMul), 1), drop.knockBack * 1.3f, drop.owner,
                MathF.Round(height), tag, MathF.Round(width));
        }

        private static int CountAlive() {
            int type = ModContent.ProjectileType<KikasaBloodColumn>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type) {
                    count++;
                }
            }
            return count;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            if (!erupted) {
                erupted = true;
                EruptBeat();
            }

            life++;
            if (life >= TotalFrames) {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ) {
                float collapse = CollapseT;
                float lakeY = SprayLakeY;
                if (collapse <= 0f) {
                    //柱头甩滴:顶上的液团不断撕出血团,越过头顶再抛物落回湖里
                    if (life > RiseFrames - 2) {
                        Vector2 head = Projectile.Center - new Vector2(0f, CurrentHeightPx);
                        KikasaBloodColumnFX.ShedHead(head, WidthPx, HeightPx, Ke, lakeY);
                    }
                    //两翼回落血丝:液体到顶后开始沿柱身往回落
                    if (life > RiseFrames + 2) {
                        KikasaBloodColumnFX.Curtain(Projectile.Center, WidthPx, CurrentHeightPx, lakeY);
                    }
                }
                //塌回起拍:根部断供的一声闷响,柱身碎成落血坠回水里
                if (!collapseBeatDone && collapse > 0f) {
                    collapseBeatDone = true;
                    KikasaBloodColumnFX.Collapse(Projectile.Center, WidthPx, CurrentHeightPx, lakeY);
                    if (KikasaBloodForm.TakeSoundBudget()) {
                        KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.36f, -0.55f, 3);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0f, CurrentHeightPx * 0.5f),
                0.14f, 0.03f, 0.04f);
        }

        /// <summary>
        /// 起柱拍:符挂钩(各端)、重水花+闷鼓(帧内限量)、湖面涟漪与破水血滴(观看端)、根部一蓬上掷血珠
        /// </summary>
        private void EruptBeat() {
            KikasaTalismanHooks.OnColumnErupt(Projectile);
            if (Main.dedServ) {
                return;
            }
            float ke = Ke;
            if (KikasaBloodForm.TakeSoundBudget()) {
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.5f + 0.2f * ke, -0.45f - 0.2f * ke, 3);
                KikasaInk.Play(SoundID.DD2_MonkStaffGroundImpact, Projectile.Center, 0.22f + 0.16f * ke, -0.7f, 2);
            }
            Player owner = Owner;
            if (owner?.active == true && owner.TryGetModPlayer(out KikasaDomainPlayer kdp)
                && ReferenceEquals(KikasaDomain.Viewed, kdp)) {
                Vector2 surface = new(Projectile.Center.X, kdp.LakeWorldY);
                KikasaDomainDeco.RippleAt(surface, 0.9f + 0.7f * ke);
                KikasaDomainDeco.SplashAt(surface, 6 + (int)(8f * ke));
                if (ke > 0.8f) {
                    Main.LocalPlayer?.CWR()?.GetScreenShake(1.5f + 1.0f * (ke - 0.8f) * 5f);
                }
            }
            //溅裙 + 随头冲天的血团:液体被顶上天的第一证据
            KikasaBloodColumnFX.Erupt(Projectile.Center, WidthPx, HeightPx, ke, SprayLakeY);
        }

        /// <summary>塌回落定:柱体砸回水面的第二记较小水花与微圈</summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Player owner = Owner;
            if (owner?.active == true && owner.TryGetModPlayer(out KikasaDomainPlayer kdp)
                && ReferenceEquals(KikasaDomain.Viewed, kdp)) {
                Vector2 surface = new(Projectile.Center.X, kdp.LakeWorldY);
                KikasaDomainDeco.RippleAt(surface, 0.45f + 0.3f * Ke);
                KikasaDomainDeco.SplashAt(surface, 4 + (int)(3f * Ke));
            }
            if (KikasaBloodForm.TakeSoundBudget()) {
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.3f, -0.2f, 3);
            }
        }

        /// <summary>竖直线判定:水下根到柱头,宽藏在可见体内;塌回过半失能,下坠按绘制同一口径缩短</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collapse = CollapseT;
            if (!erupted || collapse > 0.5f) {
                return false;
            }
            float drop = collapse * collapse;
            float h = CurrentHeightPx * (1f - drop * 0.9f);
            float _ = 0f;
            Vector2 root = Projectile.Center + new Vector2(0f, RootDepthPx);
            Vector2 top = Projectile.Center - new Vector2(0f, h);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                root, top, WidthPx * 0.7f, ref _);
        }

        //==================== 命中挂钩(引擎保证只在归属端跑) ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退按"玩家→敌人"定,柱子立在敌人脚下时原生方向会乱指
            Player owner = Owner;
            if (owner?.active == true) {
                modifiers.HitDirectionOverride = target.Center.X >= owner.Center.X ? 1 : -1;
            }
            KikasaTalismanHooks.ForOwner(Projectile.owner)
                .ModifyRainHitNPC(Projectile, KikasaRainSourceKind.Column, target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => KikasaTalismanHooks.ForOwner(Projectile.owner)
                .OnRainHitNPC(Projectile, KikasaRainSourceKind.Column, target, in hit, damageDone);

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        internal void DrawColumnQuad(SpriteBatch sb, Effect fx, Texture2D canvas) {
            if (!erupted) {
                return;
            }
            KikasaBloodColumnDraw.DrawQuad(sb, fx, canvas, Projectile.Center, WidthPx, HeightPx,
                CurrentHeightPx, CollapseT, Seed, MathHelper.Clamp(life / 2f, 0f, 1f), Ke, Mound, Fallback);
        }

        internal void DrawColumnFallback(SpriteBatch sb) {
            if (!erupted) {
                return;
            }
            KikasaBloodColumnDraw.DrawFallback(sb, Projectile.Center, WidthPx, CurrentHeightPx,
                CollapseT, Seed, life, MathHelper.Clamp(life / 2f, 0f, 1f));
        }
    }
}
