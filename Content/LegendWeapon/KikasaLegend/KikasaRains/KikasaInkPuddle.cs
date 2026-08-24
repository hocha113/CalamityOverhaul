using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨洼:湖倾档(S≥<see cref="KikasaOverride.TierLakeTilt"/>)或潦符解锁的墨滴落地后积成的一汪滞墨,
    /// 踩进来的持续受召唤伤害。出生吸附地表并压一枚渍斑贴花(贴花寿命长于本体,余韵留在地上);
    /// 宽度包络成洼铺开→末段收干,判定与可见同源。同主近洼的合并在墨滴谢幕侧完成。
    /// ai[0]=半径倍率、ai[1]=寿命倍率(唤雨符潦,0 视作 1,随生成包各端一致)。
    /// 唤雨符洼系挂钩(汐潮/渍层/霜镜/沆瘴)按所有者当前持伞逐帧活解,不打洼身标签——
    /// 洼面材质/运动/蒸腾/挂层是并存通道,一枚标签装不下
    /// </summary>
    internal class KikasaInkPuddle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int LifeFrames = 150;
        private const int SpreadFrames = 10;
        private const int DryFrames = 26;
        //满铺宽度/洼深(px)：符系（汐白沫落点/霜寻镜等）同源取此，禁再内联抄值
        internal const float WidthPx = 92f;
        internal const float DepthPx = 16f;

        /// <summary>接触扫描节流(帧),所有者端 OnPuddleContact 的派发周期</summary>
        private const int ContactInterval = 10;

        private bool anchored;
        private float life;
        private int contactCadence;

        //唤雨符:每帧一解的派发器快照,绘制线程复用上一帧
        private KikasaTalismanHookRunner puddleHooks;

        /// <summary>
        /// 洼宽旋钮(汐潮涨落等):OnPuddleUpdate 逐帧写,每帧派发前回 1;
        /// 判定与可见同源,一个旋钮同时喂 Colliding 与 PreDraw
        /// </summary>
        internal float TalismanWidthMul = 1f;

        /// <summary>判定关断旋钮(霜镜无 DoT 等):OnPuddleUpdate 逐帧写,每帧派发前复位</summary>
        internal bool TalismanDamageOff;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>半径倍率(潦符),生成包 ai[0],0 视作 1</summary>
        private float RadiusMul => Projectile.ai[0] > 0.01f ? Projectile.ai[0] : 1f;

        /// <summary>寿命倍率(潦符),生成包 ai[1],0 视作 1</summary>
        private float LifeMul => Projectile.ai[1] > 0.01f ? Projectile.ai[1] : 1f;

        /// <summary>出生寿命帧数(寿命倍率与首帧钳制同式折算);符系潮钟/镜寿同源取此</summary>
        internal static int SpawnLifeFrames(float lifeMul)
            => Math.Max((int)(LifeFrames * lifeMul), DryFrames + 4);

        /// <summary>洼龄(帧):出生寿命减当前 timeLeft;墨滴合并续命把 timeLeft 顶回出生值即归零重计</summary>
        internal int Age => SpawnLifeFrames(LifeMul) - Projectile.timeLeft;

        /// <summary>洼的满铺宽度(px),符倍率已折入</summary>
        private float FullWidthPx => WidthPx * RadiusMul;

        /// <summary>铺开(EaseOut)→收干(EaseIn)的宽度包络</summary>
        private float WidthT {
            get {
                float grow = MathHelper.Clamp(life / SpreadFrames, 0f, 1f);
                grow = 1f - (1f - grow) * (1f - grow);
                float dry = 1f - MathHelper.Clamp(Projectile.timeLeft / (float)DryFrames, 0f, 1f);
                return grow * (1f - dry * dry);
            }
        }

        public override void SetDefaults() {
            Projectile.width = (int)WidthPx;
            Projectile.height = (int)DepthPx;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            life++;
            //唤雨符洼系挂钩:旋钮先复位再派发(符卸下即自动回落),空绳零开销
            puddleHooks = KikasaTalismanHooks.ForOwner(Projectile.owner);
            TalismanWidthMul = 1f;
            TalismanDamageOff = false;
            puddleHooks.OnPuddleUpdate(Projectile);
            //接触挂钩:所有者端节流扫描(渍层/霜镜减速等状态类写入走这里)
            if (!puddleHooks.IsEmpty && Main.myPlayer == Projectile.owner
                && ++contactCadence >= ContactInterval) {
                contactCadence = 0;
                ScanContacts();
            }
            if (!anchored) {
                anchored = true;
                //寿命倍率首帧一次性落定:各端从同步的 ai[1] 推得同一个值
                if (LifeMul != 1f) {
                    Projectile.timeLeft = SpawnLifeFrames(LifeMul);
                }
                //吸附地表:自出生点向下找实心;找不到就原地作数(空中命中的浮墨)
                if (TryFindGroundBelow(Projectile.Center, 96f, out float surfaceY)) {
                    Projectile.Center = new Vector2(Projectile.Center.X, surfaceY - DepthPx * 0.5f + 3f);
                }
                if (!Main.dedServ) {
                    //贴花比本体长命:洼干了渍还在
                    KikasaInkFX.AddGroundSplat(Projectile.Center + Vector2.UnitY * 6f,
                        Vector2.UnitY * 10f, FullWidthPx * 0.6f);
                    KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.3f, -0.5f, 4);
                }
            }

            //洼面冒泡:偶发一粒墨珠鼓起又塌回
            if (!Main.dedServ && WidthT > 0.5f && Main.rand.NextBool(9)) {
                float xOff = Main.rand.NextFloat(-0.42f, 0.42f) * FullWidthPx * WidthT;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + new Vector2(xOff, -2f),
                    new Vector2(xOff * 0.01f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, 0.08f, 0.015f, 0.02f);
        }

        private static bool TryFindGroundBelow(Vector2 from, float maxDown, out float surfaceY) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int endY = (int)((from.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surfaceY = y * 16f;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }

        /// <summary>洼面判定盒,判定/接触扫描共用同一几何</summary>
        private Rectangle ContactBox(float w)
            => new((int)(Projectile.Center.X - w * 0.5f),
                (int)(Projectile.Center.Y - DepthPx * 0.5f - 6f), (int)w, (int)DepthPx + 10);

        /// <summary>所有者端接触扫描:浸洼的敌人逐个派发 OnPuddleContact</summary>
        private void ScanContacts() {
            float w = FullWidthPx * WidthT * TalismanWidthMul;
            if (w < 14f) {
                return;
            }
            Rectangle box = ContactBox(w);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                if (box.Intersects(npc.Hitbox)) {
                    puddleHooks.OnPuddleContact(Projectile, npc);
                }
            }
        }

        /// <summary>判定随包络收窄,干透即失能;霜镜类经旋钮整体关断,宽度旋钮判定可见同源</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (TalismanDamageOff) {
                return false;
            }
            float w = FullWidthPx * WidthT * TalismanWidthMul;
            if (w < 14f) {
                return false;
            }
            return ContactBox(w).Intersects(targetHitbox);
        }

        //==================== 命中挂钩(引擎保证只在归属端跑) ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => KikasaTalismanHooks.ForOwner(Projectile.owner)
                .ModifyRainHitNPC(Projectile, KikasaRainSourceKind.Puddle, target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => KikasaTalismanHooks.ForOwner(Projectile.owner)
                .OnRainHitNPC(Projectile, KikasaRainSourceKind.Puddle, target, in hit, damageDone);

        /// <summary>扁平三层:暗缘垫底→墨体→血芯细线,加一线 A=0 湿反光;符配色挂钩可整套换调(霜银/瘴绿/霞纹)</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            float wT = WidthT;
            if (tex == null || wT <= 0.03f) {
                return false;
            }
            //配色挂钩:复用本帧 AI 解析的派发器,宽度旋钮与判定同源
            KikasaPuddleDrawParams draw = new() {
                Deep = KikasaInk.InkDeep,
                Body = KikasaInk.InkBody,
                Core = KikasaInk.BloodCore,
                Sheen = KikasaInk.WetSheen,
            };
            puddleHooks.ModifyPuddleDraw(Projectile, ref draw);

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float w = FullWidthPx * wT * TalismanWidthMul;
            float wob = 1f + MathF.Sin(life * 0.11f + Seed * 4f) * 0.04f;

            Main.EntitySpriteDraw(tex, pos, null, draw.Deep * 0.7f, 0f, origin,
                new Vector2(w * 1.16f / tex.Width, DepthPx * 1.5f / tex.Height * wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, draw.Body * 0.95f, 0f, origin,
                new Vector2(w / tex.Width, DepthPx / tex.Height * wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(0f, -1f), null, draw.Core * 0.4f, 0f, origin,
                new Vector2(w * 0.5f / tex.Width, 4f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(w * 0.12f, -3f), null,
                (draw.Sheen with { A = 0 }) * (0.26f * wob), 0f, origin,
                new Vector2(w * 0.22f / tex.Width, 2.6f / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
