// ============================================================================
//KikasaItemForm.fx 鬼伞湖藏的物品幽灵材质：以物品贴图 alpha 为轮廓，
//内部填血湖材质（深血雾底 + 流动血红 + 血沫微光 + 轮廓水膜），
//uForm 驱动血水态↔真身：uScanMode=1 自上而下凝实扫描（提取演出），
//uScanMode=0 噪声斑驳交融（湖窗槽位的半沉态）；
//uDissolve 驱动噪声侵蚀溶解（沉入演出），蚀缘泛血沫光、余身渐染血色。
//调色板与 KikasaGrade.fx 血湖常量同源；门控全走 step/lerp，无动态分支。
//s0=物品贴图（帧区域由 uUvRect 归一）s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位，两件沉物不共享同一蚀纹
float uForm;        //0=真身 1=全血水
float uDissolve;    //0=完好 1=蚀尽
float uScanMode;    //1=自上而下凝实扫描 0=噪声斑驳交融
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸，轮廓检测用
float uAspect;      //帧宽/帧高，噪声采样防拉伸

//====== 与 KikasaGrade 同源的血湖调色 ======
static const float3 LAKE_TINT = float3(0.930, 0.300, 0.270);  //血红流层
static const float3 LAKE_FOG  = float3(0.170, 0.024, 0.036);  //深部血雾底
static const float3 FOAM_COL  = float3(0.965, 0.520, 0.440);  //血沫微光

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//采样物品贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSItemForm(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float srcA = src.a;

    //帧内归一坐标；噪声采样用等比坐标防拉伸
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    //====== 血水身体：下淌流层 + 深浅两频 + 高光血沫 ======
    float n0 = noiseTex(nuv * 0.85 + float2(uSeed, uTime * 0.16 + uSeed));
    float n1 = noiseTex(nuv * 1.9 + float2(-uTime * 0.05, uTime * 0.34) + uSeed * 1.7);
    float3 blood = LAKE_FOG * 1.25
        + LAKE_TINT * (0.22 + n0 * 0.40 + n1 * 0.24);
    //稀疏高光：血面偶尔一点湿亮
    float glint = pow(saturate(n1 * 1.15), 6.0);
    blood += FOAM_COL * glint * 0.50;

    //轮廓水膜：剪影边缘一线血沫光，让血水读出物品的形
    float aL = frameAlpha(uv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(uv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(uv - float2(0.0, uTexel.y));
    float aD = frameAlpha(uv + float2(0.0, uTexel.y));
    float minN = min(min(aL, aR), min(aU, aD));
    float rimShape = saturate((srcA - minN) * 2.4);
    blood += FOAM_COL * rimShape * 0.42;

    //====== 凝实遮罩：两种模式整算后按 uScanMode 选边 ======
    float jn = noiseTex(nuv * 1.3 + uSeed * 0.7);
    //扫描式：凝实线自上而下推进，锯口带噪
    float scan = (1.0 - uForm) * 1.34 - 0.17 + (jn - 0.5) * 0.20;
    float maskScan = 1.0 - smoothstep(scan - 0.06, scan + 0.06, luv.y);
    //斑驳式：噪声阈值交融，半沉态血水里透出真身碎片
    float maskBlend = saturate((jn - uForm) * 3.0 + 0.5);
    float trueMask = lerp(maskBlend, maskScan, uScanMode);

    //凝实前沿泛血沫，只在过渡中段发亮
    float formGate = saturate(uForm * (1.0 - uForm) * 12.0);
    float band = exp(-(luv.y - scan) * (luv.y - scan) / 0.0045) * uScanMode;
    float patchEdge = exp(-abs(jn - uForm) * 16.0) * (1.0 - uScanMode);

    //====== 溶解侵蚀：dn 低于阈值的像素被血水吃掉 ======
    float dn = noiseTex(nuv * 1.55 + float2(uSeed * 1.9, uSeed * 0.6));
    float thr = uDissolve * 1.12 - 0.06;
    float keep = smoothstep(thr, thr + 0.09, dn);
    float eatRim = exp(-abs(dn - thr - 0.045) * 20.0) * saturate(uDissolve * 8.0);

    //====== 合成（预乘输出：本体乘 alpha，血沫光走加色项） ======
    float3 body = lerp(blood, src.rgb, trueMask);
    //越溶越沉入血色
    body = lerp(body, blood, saturate(uDissolve * 1.35) * 0.62);
    float aOut = saturate(srcA * keep) * vc.a;
    //凝实前沿 + 蚀缘的血沫光：预乘批里 rgb 直加即加色，不吃 alpha
    float3 glow = FOAM_COL * ((band + patchEdge) * formGate * 0.85 + eatRim * 0.90)
        * srcA * vc.a;
    return float4(body * vc.rgb * aOut + glow, aOut);
}

technique TechItemForm {
    pass P0 {
        PixelShader = compile ps_3_0 PSItemForm();
    }
}
