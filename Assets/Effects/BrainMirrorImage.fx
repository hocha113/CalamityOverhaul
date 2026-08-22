// ============================================================================
// BrainMirrorImage.fx 克脑镜像质感
// uGhost=瞬移溶解量（噪声阈值侵蚀+蚀缘血光）
// uCold=假体冷偏（去饱和+冷紫移+镜面竖纹微晃），恐怖谷的“哪里不对”
// 采样限制在 uFrameUV 帧区域内，防竖排图集串帧
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（SpriteBatch 当前精灵=克脑图集）

// 噪声固定 s1：sampler_state 自动分配在 SpriteBatch 下必被 s0 覆写（曾靠 uImage0 占位侥幸落 s1）；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseTex : register(s1);

float uTime;
float uGhost;        //0 实体 ~ 1 完全消散
float uCold;         //0 真身 ~ 1 假体
float4 uFrameUV;     //xy=帧起点 zw=帧尺寸（贴图uv空间）

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //帧内归一化坐标
    float2 local = (coords - uFrameUV.xy) / uFrameUV.zw;

    //假体镜面竖纹微晃：横向亚像素错位（幅度极小，潜意识级不适）
    float wobble = sin(local.y * 34.0 + uTime * 2.6) * 0.0035 * uCold;
    float2 sampleUV = coords + float2(wobble * uFrameUV.z, 0.0);
    //钳回帧内防串帧
    float2 frameMin = uFrameUV.xy + uFrameUV.zw * 0.002;
    float2 frameMax = uFrameUV.xy + uFrameUV.zw * 0.998;
    sampleUV = clamp(sampleUV, frameMin, frameMax);

    float4 src = tex2D(uImage0, sampleUV);

    //溶解：帧内噪声阈值侵蚀
    float noise = tex2D(noiseTex, local * 1.7 + float2(uTime * 0.06, uTime * 0.023)).r;
    float erode = step(uGhost, noise);
    //蚀缘血光：紧贴阈值的一圈提亮
    float rim = saturate(1.0 - abs(noise - uGhost) * 9.0) * step(0.005, uGhost) * step(uGhost, 0.995);
    float3 rimBlood = float3(0.78, 0.10, 0.14) * rim * src.a * 0.85;

    //冷偏：去饱和+冷紫染
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float3 coldTone = lerp(src.rgb, float3(lum, lum, lum), 0.38 * uCold);
    coldTone = lerp(coldTone, coldTone * float3(0.82, 0.72, 1.02) + float3(0.02, 0.0, 0.05), uCold * 0.8);

    float4 result;
    result.rgb = coldTone * erode + rimBlood;
    result.a = src.a * erode;
    return result * vertexColor;
}

technique Technique1
{
    pass BrainMirrorPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
