// ============================================================================
//KikasaHound.fx 鬼梦黑犬材质：以原版狼贴图 alpha 为轮廓，内部填湿墨黑液
//（近黑双频翻涌 + 轮廓一线暗缘光 + 余烬红双目）。
//uMode=0 倒影态：水线裁剪 + 波动缝、深处溶水毛边与横向折射晃动——
//它是湖镜里的一层影，越深越散；uMode=1 实体态：体成而实，靠 uDissolve 化雾来去。
//帧区域由 uUvRect 归一，邻域采样全部钳在帧内防串帧（KikasaItemForm 同款纪律）。
//门控全走 step/lerp/smoothstep，无动态分支。s0=狼贴图 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸
float uAspect;      //帧宽/帧高，噪声采样防拉伸
float uFlipH;       //1=水平翻转（画面朝右）；在采样里做，不靠 SpriteEffects
float uFlipV;       //1=垂直翻转（倒影）
float uMode;        //0=倒影 1=实体
float uSeamGate;    //水线缝可见度：犬背贴着水线才有缝，沉深了自然没有
float uWobble;      //水线晃动与折射幅度，吃湖面泡沫/沸腾
float uEyeGlow;     //双目辉光 0~1
float2 uEyeAnchor;  //眼睛帧内 uv（未翻转、面向左的原生坐标）
float uDissolve;    //0=完好 1=化雾散尽
float3 uEdgeTint;   //轮廓缘光色，CPU 侧过 CoolTint

//====== 湿墨黑犬调色 ======
static const float3 INK_DEEP  = float3(0.030, 0.016, 0.022);  //墨黑底
static const float3 INK_SHEEN = float3(0.085, 0.052, 0.070);  //湿墨光泽
static const float3 SINK_FOG  = float3(0.052, 0.010, 0.016);  //深水沉雾（倒影深处沉入的色）
static const float3 EMBER_CORE = float3(0.950, 0.340, 0.140); //目芯
static const float3 EMBER_HALO = float3(0.620, 0.100, 0.060); //目晕

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//采样狼贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSHound(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //帧内归一坐标与画面朝向坐标（q 系：x 向画面右，y 向画面下）
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float qx = lerp(luv.x, 1.0 - luv.x, uFlipH);
    float qy = lerp(luv.y, 1.0 - luv.y, uFlipV);
    float2 nuv = float2(qx * uAspect, luv.y);
    float refl = 1.0 - uMode;

    //倒影深处的横向折射晃动：越深晃得越碎
    float refr = sin(qy * 21.0 + uTime * 2.6 + uSeed * 7.0)
        * (0.006 + uWobble * 0.8) * qy * refl;
    //水平翻转在采样里做（不靠 SpriteEffects），与 KikasaDream.fx 的 uHoundFlipH 同约定
    float2 srcUv = uUvRect.xy + float2(qx, luv.y) * uUvRect.zw
        + float2(refr * uUvRect.z, 0.0);

    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    float4 src = tex2D(uImage0, clamp(srcUv, lo, hi));
    float srcA = src.a;

    //====== 湿墨身体：近黑双频翻涌，倒影随深度沉入水雾色 ======
    float n0 = noiseTex(nuv * 0.9 + float2(uSeed, uTime * 0.11 + uSeed));
    float n1 = noiseTex(nuv * 2.1 + float2(-uTime * 0.04, uTime * 0.22) + uSeed * 1.7);
    float3 body = INK_DEEP + INK_SHEEN * (0.26 + n0 * 0.42 + n1 * 0.20);
    //贴图明度只留一点体积暗示，黑要读作黑
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    body += INK_SHEEN * lum * 0.30;
    body = lerp(body, SINK_FOG, qy * 0.55 * refl);

    //====== 轮廓缘光：一线暗血/冷雨色的水膜，深处熄灭 ======
    float aL = frameAlpha(srcUv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(srcUv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(srcUv - float2(0.0, uTexel.y));
    float aD = frameAlpha(srcUv + float2(0.0, uTexel.y));
    float minN = min(min(aL, aR), min(aU, aD));
    float rimShape = saturate((srcA - minN) * 2.2);
    float rimFade = saturate(1.0 - qy * 0.85 * refl);
    float3 rim = uEdgeTint * rimShape * rimFade * 0.55;

    //====== 水线缝与裁剪：贴着水线那一段有一条晃动的湿缝 ======
    float wob = sin(qx * 9.0 + uTime * 2.3 + uSeed * 3.0)
        * (0.5 + 0.5 * n0) * uWobble * 2.2;
    float clipLine = (0.012 + wob) * uSeamGate * refl;
    float waterFade = smoothstep(clipLine - 0.012, clipLine + 0.028, qy + (1.0 - refl));
    //缝下一线湿光
    float seamBand = exp(-(qy - clipLine) * (qy - clipLine) / 0.0016)
        * uSeamGate * refl;
    rim += uEdgeTint * seamBand * 0.8;

    //====== 溶水毛边 / 化雾：倒影深处蚀散，实体死亡整体化散 ======
    float dn = noiseTex(nuv * 1.6 + float2(uSeed * 1.9, uSeed * 0.6));
    float depthEat = saturate(qy * 1.2 - 0.34) * 0.5 * refl;
    float thr = depthEat + uDissolve;
    float keep = smoothstep(thr, thr + 0.11, dn * 0.82 + 0.18);
    float eatRim = exp(-abs(dn * 0.82 + 0.18 - thr - 0.055) * 22.0)
        * saturate((depthEat + uDissolve) * 6.0);
    rim += uEdgeTint * eatRim * 0.5;

    //====== 余烬双目：芯 + 晕，辉光微微透出轮廓外 ======
    float2 eyeA = float2(lerp(uEyeAnchor.x, 1.0 - uEyeAnchor.x, uFlipH), uEyeAnchor.y);
    float2 ed = (luv - eyeA) * float2(uAspect, 1.0);
    float eyeCore = exp(-dot(ed, ed) * 5200.0);
    float eyeHalo = exp(-dot(ed, ed) * 620.0);
    //第二只眼贴后一点、弱一半，侧面像里只是一点余光
    float eye2x = lerp(0.055, -0.055, uFlipH);
    float2 ed2 = (luv - eyeA - float2(eye2x, 0.012)) * float2(uAspect, 1.0);
    float eye2 = exp(-dot(ed2, ed2) * 5200.0) * 0.45;
    float breath = 0.86 + 0.14 * sin(uTime * 2.1 + uSeed * 5.0);
    float3 eyes = (EMBER_CORE * (eyeCore + eye2) * 1.25 + EMBER_HALO * eyeHalo * 0.5)
        * uEyeGlow * breath;

    //====== 合成（预乘输出）======
    float aOut = srcA * keep * waterFade * vc.a;
    float3 col = body * vc.rgb * aOut + rim * aOut
        //目光可以隔着一层薄水看见：允许少量溢出轮廓
        + eyes * (0.30 + 0.70 * srcA) * keep * waterFade * vc.a;
    return float4(col, aOut);
}

technique TechHound {
    pass P0 {
        PixelShader = compile ps_3_0 PSHound();
    }
}
