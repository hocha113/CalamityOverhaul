// ============================================================================
//WofBloodSea.fx 墙后尸山血海背景层
//单quad单pass：①远景凝血海面(横流血浪+波峰粘稠亮线+破碎天光镜带)
//②中景尸山剪影(噪声阈值成形双层视差山脊+肋骨拱隙+脊线血缘光)
//③近景升腾(血雾柱/余烬火星/近海面热浪抖动)
//坐标全笛卡尔；预乘输出 AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //PerlinNoise 512

float4 uWorldRect;   //quad世界矩形 x,y,w,h
float uEdgeX;        //血海前缘世界X(墙体背后)
float uDir;          //墙推进方向±1(海域在 -uDir 侧)
float uSurfaceY;     //海平面基准世界Y
float uScreenX;      //屏幕左缘世界X(山体视差用)
float uTime;
float uIntensity;    //0~1 整体强度

float snoise(float2 p)
{
    return tex2D(uImage1, p).r;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 worldPos = uWorldRect.xy + coords * uWorldRect.zw;

    //前缘羽化：海域只存在于墙背后，边界被噪声撕开(墙体拖尾覆膜会叠在此过渡带上)
    float frontDist = (uEdgeX - worldPos.x) * uDir
        + (snoise(float2(worldPos.y * 0.0042, uTime * 0.04)) - 0.5) * 130.0;
    if (frontDist < -60.0)
    {
        return float4(0, 0, 0, 0);
    }
    float frontFade = smoothstep(-40.0, 300.0, frontDist);

    //海面起伏：低频涌浪+高频碎浪
    float wave = (snoise(float2(worldPos.x * 0.0021 - uTime * 0.045, 0.31)) - 0.5) * 46.0
               + (snoise(float2(worldPos.x * 0.0072 + uTime * 0.11, 0.63)) - 0.5) * 15.0;
    float sd = worldPos.y - (uSurfaceY + wave);   //>0 海面下
    float below = smoothstep(-4.0, 5.0, sd);

    //=== ① 凝血海面 ===
    float depthT = saturate(sd / 520.0);
    float flow1 = snoise(float2(worldPos.x * 0.0038 - uTime * 0.06, worldPos.y * 0.0095));
    float flow2 = snoise(float2(worldPos.x * 0.0090 + uTime * 0.035, worldPos.y * 0.017 + 0.41));
    float streak = flow1 * 0.62 + flow2 * 0.38;
    float3 seaCol = lerp(float3(0.30, 0.032, 0.045), float3(0.05, 0.007, 0.013), depthT);
    seaCol *= 0.72 + streak * 0.55;
    //波峰粘稠亮线：贴海面陡衰减，噪声门撕成断续
    float crest = exp(-sd * sd * 0.0055) * smoothstep(0.52, 0.80, flow2);
    seaCol += float3(0.85, 0.15, 0.10) * crest * 0.6;
    //破碎天光镜带：海面下方一段倒影带，被横流撕断
    float mirrorBand = exp(-pow((sd - 58.0) * 0.019, 2.0)) * smoothstep(0.50, 0.76, flow1);
    seaCol += float3(0.48, 0.085, 0.055) * mirrorBand * 0.38;
    float seaAlpha = below * (0.80 + depthT * 0.13);

    //=== ② 尸山剪影(海面上) ===
    float h = -sd;
    //热浪抖动：近海面处轮廓微晃
    h += (snoise(float2(worldPos.x * 0.011 + uTime * 0.27, 0.77)) - 0.5) * 9.0 * exp(-max(h, 0.0) * 0.004);

    //远脊：慢视差、深暗红、更高更缓
    float mx2 = worldPos.x - uScreenX * 0.62;
    float ridge2 = snoise(float2(mx2 * 0.00085, 0.52)) * 0.78 + snoise(float2(mx2 * 0.0041, 0.29)) * 0.22;
    float rh2 = ridge2 * 460.0 + 130.0;
    float m2 = smoothstep(rh2 + 22.0, rh2 - 22.0, h);
    //近脊：快视差、近黑、碎而低
    float mx1 = worldPos.x - uScreenX * 0.45;
    float ridge1 = snoise(float2(mx1 * 0.0016, 0.14)) * 0.7 + snoise(float2(mx1 * 0.0062, 0.86)) * 0.3;
    float rh1 = ridge1 * 290.0 + 40.0;
    float m1 = smoothstep(rh1 + 14.0, rh1 - 14.0, h);
    //肋骨拱隙：近脊线下方周期拱形微透光(骨山暗示，不用贴图)
    float rib = pow(abs(sin(mx1 * 0.020 + ridge1 * 7.0)), 8.0)
        * exp(-(rh1 - h) * (rh1 - h) * 0.00045) * m1;
    //脊线血缘光：山轮廓上沿被海光镶边
    float rim1 = exp(-(h - rh1) * (h - rh1) * 0.002) * (1.0 - m1);

    //=== 天光雾 + ③ 升腾层 ===
    float hz = max(h, 0.0);
    float horizonGlow = exp(-hz * 0.006);
    float haze = exp(-hz * 0.0015);
    //血雾柱缓升
    float mist = snoise(float2(worldPos.x * 0.0034, worldPos.y * 0.0028 - uTime * 0.05)) * exp(-hz * 0.0028);
    //余烬火星：噪声阈值取尾部成点，集中在近海面。
    //PerlinNoise 实测灰度域 0.22~0.78(top1%≈0.667)，阈值必须落在域内，0.90 档永不可达
    float ember = smoothstep(0.67, 0.71,
        tex2D(uImage1, float2(worldPos.x * 0.0026 + 3.13, worldPos.y * 0.0035 + uTime * 0.14)).g)
        * smoothstep(430.0, 50.0, hz);

    float3 aboveCol = float3(0.16, 0.020, 0.028) * horizonGlow
                    + float3(0.075, 0.010, 0.016) * haze * 0.7
                    + float3(0.30, 0.045, 0.04) * mist * 0.5;
    float aboveAlpha = saturate(horizonGlow * 0.42 + haze * 0.30 + mist * 0.22);
    //远脊压上
    aboveCol = lerp(aboveCol, float3(0.095, 0.014, 0.021) * (0.85 + mist * 0.4), m2);
    aboveAlpha = max(aboveAlpha, m2 * 0.80);
    //近脊压上(带肋骨透光)
    float3 nearCol = float3(0.042, 0.006, 0.011) + float3(0.30, 0.05, 0.04) * rib * 0.7;
    aboveCol = lerp(aboveCol, nearCol, m1);
    aboveAlpha = max(aboveAlpha, m1 * 0.90);
    //脊线镶边+余烬
    aboveCol += float3(0.55, 0.10, 0.07) * rim1 * 0.35;
    aboveCol += float3(1.0, 0.40, 0.13) * ember * 0.8;
    aboveAlpha = saturate(aboveAlpha + ember * 0.5);

    //=== 合成 ===
    float3 color = lerp(aboveCol, seaCol, below);
    float alpha = lerp(aboveAlpha, seaAlpha, below);

    alpha *= frontFade * uIntensity;
    //预乘输出
    return float4(color * alpha, alpha) * vertexColor.a;
}

technique Technique1
{
    pass WofBloodSeaPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
