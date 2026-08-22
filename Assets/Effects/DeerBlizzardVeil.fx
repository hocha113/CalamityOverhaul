// ============================================================================
//DeerBlizzardVeil.fx 独眼巨鹿暴风雪视界(全屏后效)
//三层视差定向雪幕+视界压缩暗晕+凝视渐晕+白澈领域(近boss清明反转)
//直线算术+普通tex2D，无分支(FNA3D全屏效果铁律)
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（全屏拷贝）

// 噪声固定 s1：sampler_state 自动分配在 SpriteBatch 下必被 s0 覆写（曾靠 uImage0 占位侥幸落 s1）；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

float uTime;
float uIntensity;   //0~1 暴风雪浓度
float uWhiteout;    //0~1 白澈领域强度
float uGazeWarn;    //0~1 凝视警告(本地面向它时爬升)
float uPunish;      //0~1 凝视惩罚白闪
float2 uBossUV;     //boss屏幕uv(白澈清明圈圆心)
float uClearRadius; //清明圈半径(屏高归一)
float uAspect;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //风轴坐标系(仿射旋转，无极角)
    float2 windDir = normalize(float2(-1.0, 0.30));
    float2 p = coords * float2(uAspect, 1.0);
    float lon = dot(p, windDir);
    float lat = dot(p, float2(-windDir.y, windDir.x));

    //雪幕对画面的细微扰动折射
    float turb = tex2D(noiseTex, float2(lon * 1.7 - uTime * 0.9, lat * 1.7)).r - 0.5;
    float2 refr = float2(-windDir.y, windDir.x) * turb * 0.005 * uIntensity;
    refr.x /= uAspect;
    float3 col = tex2D(uImage0, coords + refr).rgb;

    //三层视差雪縷：沿风向压缩采样=拉长成风丝，各层频率/速度/权重不同
    float s1 = tex2D(noiseTex, float2(lon * 0.9 - uTime * 2.6, lat * 3.6)).r;
    float s2 = tex2D(noiseTex, float2(lon * 1.6 - uTime * 1.7 + 0.37, lat * 6.2 + 0.19)).r;
    float s3 = tex2D(noiseTex, float2(lon * 2.6 - uTime * 1.1 + 0.71, lat * 9.5 + 0.53)).r;
    float snow = smoothstep(0.60, 0.92, s1) * 0.55
               + smoothstep(0.62, 0.90, s2) * 0.33
               + smoothstep(0.66, 0.90, s3) * 0.20;
    snow *= uIntensity;

    //大尺度流动雾霭
    float fogN = tex2D(noiseTex, coords * 0.8 + float2(-uTime * 0.05, uTime * 0.012)).r;
    float haze = uIntensity * (0.13 + 0.24 * fogN);

    //视界压缩：边缘沉入冷灰蓝(不是纯黑，是雪盲)
    float2 c = (coords - 0.5) * float2(uAspect, 1.0);
    float r = length(c);
    float murk = smoothstep(0.42, 1.0, r) * uIntensity;

    float3 snowColor = float3(0.87, 0.93, 1.0);
    float3 hazeColor = float3(0.66, 0.74, 0.86);
    float3 murkColor = float3(0.42, 0.50, 0.64);

    //冷调渗入→雾霭→边缘压缩→雪縷
    col = lerp(col, col * float3(0.88, 0.95, 1.06), uIntensity * 0.5);
    col = lerp(col, hazeColor, haze * 0.55);
    col = lerp(col, murkColor, murk * 0.62);
    col = lerp(col, snowColor, saturate(snow));

    //白澈领域：唯boss身侧留一圈清明，圈外白噪吞没
    float2 db = (coords - uBossUV) * float2(uAspect, 1.0);
    float distBoss = length(db);
    float clearMask = smoothstep(uClearRadius, uClearRadius * 1.85, distBoss);
    float whiteN = tex2D(noiseTex, float2(lon * 1.3 - uTime * 3.4, lat * 5.0 + 0.83)).r;
    float white = uWhiteout * clearMask * (0.82 + 0.18 * whiteN);
    //圈缘一道薄薄的冷蓝呼吸环
    float rim = smoothstep(0.10, 0.0, abs(distBoss - uClearRadius)) * uWhiteout * (0.5 + 0.2 * whiteN);
    col = lerp(col, float3(0.94, 0.97, 1.0), saturate(white));
    col += float3(0.30, 0.55, 0.95) * rim * 0.4;

    //凝视渐晕：暗影紫自边缘咬入+内缘结霜细闪，"你正看着它"
    float bite = smoothstep(0.62 - uGazeWarn * 0.34, 1.15, r) * uGazeWarn;
    float3 gazeColor = float3(0.14, 0.05, 0.22);
    col = lerp(col, gazeColor, bite * 0.8);
    float frostN = tex2D(noiseTex, coords * 3.4 + float2(uTime * 0.03, frac(uTime * 0.017) * 0.5)).r;
    float frostBand = smoothstep(0.06, 0.0, abs(r - (0.62 - uGazeWarn * 0.30))) * uGazeWarn;
    col += float3(0.55, 0.75, 1.0) * step(0.74, frostN) * frostBand * 0.5;

    //凝视惩罚白闪：冰蓝一瞬
    col = lerp(col, float3(0.82, 0.93, 1.0), uPunish * 0.82);

    return float4(col, 1.0);
}

technique Technique1
{
    pass DeerBlizzardVeilPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
