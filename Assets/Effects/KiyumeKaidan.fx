// ============================================================================
//KiyumeKaidan.fx 鬼梦怪谈材质（两 pass 一次带齐，P4-B 立，P4-F 只消费不改）：
//TechPaperGhost 纸衣化：原版帧 alpha 为轮廓，体色去饱和提灰成纸白丧服，
//  竖向布纹随夜风微动 + 下摆碎边（uDissolve 常值即碎裾，拉满整身化雾）+ 帽下余烬双目。
//TechFacelessSkin 面区涂抹：uFaceRect（未翻转帧内 uv）内五官抹平成蛋壳肤，
//  区外保持原贴图色（乘顶点色），共用蚀散/缘光/双目语法。
//帧区域由 uUvRect 归一，邻域采样钳帧内防串帧（KikasaHound 同款纪律）。
//门控全走 step/lerp/smoothstep；只用绑定噪声 s1=PerlinNoise，无 fbm 栈
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸
float uAspect;      //帧宽/帧高，噪声采样防拉伸
float uFlipH;       //1=水平翻转（画面朝右）；在采样里做，不靠 SpriteEffects
float uFlipV;       //1=垂直翻转（本组常 0，保链）
float uEyeGlow;     //双目余烬 0~1
float2 uEyeAnchor;  //眼睛帧内 uv（未翻转、面向左的原生坐标）
float uDissolve;    //0=完好 1=化雾散尽（纸衣 pass 下摆先蚀）
float3 uEdgeTint;   //轮廓缘光色
float3 uPaperTint;  //纸衣主调；面妖 pass 借作肤色基调
float4 uFaceRect;   //面区（帧内 uv：xy=偏移 zw=尺寸，未翻转原生坐标）

//余烬双目调色（KikasaHound 同源）
static const float3 EMBER_CORE = float3(0.950, 0.340, 0.140);
static const float3 EMBER_HALO = float3(0.620, 0.100, 0.060);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//采样贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

//公共底座：帧内坐标、原色采样、蚀散保留量与缘光（hemOn=1 下摆先蚀）
void kaidanBase(float2 uv, float hemOn, out float2 luv, out float qx, out float qy,
    out float4 src, out float keep, out float3 rim) {
    luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    qx = lerp(luv.x, 1.0 - luv.x, uFlipH);
    qy = lerp(luv.y, 1.0 - luv.y, uFlipV);
    float2 srcUv = uUvRect.xy + float2(qx, luv.y) * uUvRect.zw;
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    src = tex2D(uImage0, clamp(srcUv, lo, hi));

    //轮廓缘光：邻域 alpha 落差一线
    float aL = frameAlpha(srcUv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(srcUv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(srcUv - float2(0.0, uTexel.y));
    float aD = frameAlpha(srcUv + float2(0.0, uTexel.y));
    float rimShape = saturate((src.a - min(min(aL, aR), min(aU, aD))) * 2.2);

    //蚀散：噪声阈值 + 下摆偏置（常值 uDissolve 只碎裙裾，拉满头脚皆空）
    float2 nuv = float2(qx * uAspect, qy);
    float dn = noiseTex(nuv * 1.6 + float2(uSeed * 1.9, uSeed * 0.6));
    //绑定噪声实测值域 r∈[0.22,0.78]、p1..p99≈[0.32,0.67]：按分布归一，阈值扫程才吃得满
    float dn01 = saturate((dn - 0.32) / 0.35);
    float hemBias = saturate(qy * 1.35 - 0.45) * hemOn;
    //0.96：uDissolve=0.85 尚余最后一撮纸屑；=1 只剩个别近透明斑点，
    //消费方在 uDissolve 走满的同帧退场，残点不上屏
    float thr = uDissolve * 0.96 + hemBias * (0.10 + 0.30 * uDissolve);
    float dnv = dn01 * 0.82 + 0.18;
    keep = smoothstep(thr, thr + 0.11, dnv);
    //蚀口一线余烬边
    float eatRim = exp(-abs(dnv - thr - 0.055) * 22.0) * saturate(thr * 5.0);
    rim = uEdgeTint * (rimShape * 0.45 + eatRim * 0.5);
}

//余烬双目：芯 + 晕 + 贴后一点的第二目（KikasaHound 同款语法）
float3 kaidanEyes(float2 luv) {
    float2 eyeA = float2(lerp(uEyeAnchor.x, 1.0 - uEyeAnchor.x, uFlipH), uEyeAnchor.y);
    float2 ed = (luv - eyeA) * float2(uAspect, 1.0);
    float eyeCore = exp(-dot(ed, ed) * 5200.0);
    float eyeHalo = exp(-dot(ed, ed) * 620.0);
    float eye2x = lerp(0.055, -0.055, uFlipH);
    float2 ed2 = (luv - eyeA - float2(eye2x, 0.012)) * float2(uAspect, 1.0);
    float eye2 = exp(-dot(ed2, ed2) * 5200.0) * 0.45;
    float breath = 0.86 + 0.14 * sin(uTime * 2.1 + uSeed * 5.0);
    return (EMBER_CORE * (eyeCore + eye2) * 1.25 + EMBER_HALO * eyeHalo * 0.5)
        * uEyeGlow * breath;
}

//====== 纸衣化：去饱和提灰 + 竖向布纹 + 下摆碎边 ======
float4 PSPaperGhost(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float2 luv; float qx; float qy; float4 src; float keep; float3 rim;
    kaidanBase(uv, 1.0, luv, qx, qy, src, keep, rim);

    //纸白丧服：明度只留体积暗示，色相全部让给纸调
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float weave = noiseTex(float2(qx * uAspect * 3.4 + uSeed,
        qy * 0.30 + uSeed * 1.7 + uTime * 0.03));
    float3 paper = uPaperTint * (0.58 + lum * 0.40) * (0.84 + 0.26 * weave);

    float3 eyes = kaidanEyes(luv);

    //合成（预乘输出，KikasaHound 同约定；目光允许少量溢出轮廓）
    float aOut = src.a * keep * vc.a;
    float3 col = paper * vc.rgb * aOut + rim * aOut
        + eyes * (0.30 + 0.70 * src.a) * keep * vc.a;
    return float4(col, aOut);
}

//====== 面区涂抹：uFaceRect 内五官抹平成蛋壳肤，区外原色 ======
float4 PSFacelessSkin(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float2 luv; float qx; float qy; float4 src; float keep; float3 rim;
    kaidanBase(uv, 0.0, luv, qx, qy, src, keep, rim);

    //面区软窗：原生帧内坐标（x 用 qx，翻转时窗自动跟脸）
    float2 q = float2(qx, luv.y);
    float2 fLo = uFaceRect.xy;
    float2 fHi = uFaceRect.xy + uFaceRect.zw;
    float2 win = smoothstep(fLo - 0.02, fLo + 0.02, q)
        * (1.0 - smoothstep(fHi - 0.02, fHi + 0.02, q));
    float faceMask = win.x * win.y;

    //蛋壳肤：基调 uPaperTint（消费方传肤色），微幅噪声防死平
    float n = noiseTex(float2(qx * uAspect * 2.1 + uSeed * 3.1,
        qy * 2.1 + uSeed + uTime * 0.02));
    float3 skin = uPaperTint * (0.90 + 0.10 * n);
    float3 body = lerp(src.rgb, skin, faceMask);

    float3 eyes = kaidanEyes(luv);

    float aOut = src.a * keep * vc.a;
    float3 col = body * vc.rgb * aOut + rim * aOut
        + eyes * (0.30 + 0.70 * src.a) * keep * vc.a;
    return float4(col, aOut);
}

technique TechPaperGhost {
    pass PassPaperGhost {
        PixelShader = compile ps_3_0 PSPaperGhost();
    }
}

technique TechFacelessSkin {
    pass PassFacelessSkin {
        PixelShader = compile ps_3_0 PSFacelessSkin();
    }
}
