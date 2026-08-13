// ============================================================================
//EmpressScreenPrism.fx 光之女皇·全屏棱彩后效
//脉冲=径向RGB分光+扩张白环+短暂白闪；环境档=屏缘轻色散描边
//采样uImage0屏幕；直线算术+plain tex2D，无分支无tex2Dlod（FNA约束）
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //脉冲进度 0起爆→1结束
float uIntensity;  //脉冲强度 0~1
float uAmbient;    //环境棱彩档 0~1
float2 uCenter;    //脉冲中心UV
float uAspect;     //宽高比

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //几何：以脉冲中心为原点的径向量（修正宽高比）
    float2 rel = coords - uCenter;
    rel.x *= uAspect;
    float dist = length(rel) + 1e-5;
    float2 dir = rel / dist;
    dir.x /= uAspect;

    //--------------------------------------------------------------------
    //脉冲：色散强度随进度退潮；分光位移沿径向
    //--------------------------------------------------------------------
    float pulseEnv = uIntensity * pow(saturate(1.0 - uProgress), 1.35);
    //扩张白环：progress推着半径走
    float ringR = lerp(0.05, 1.35, pow(saturate(uProgress), 0.62));
    float ring = exp(-pow((dist - ringR) * 9.0, 2.0)) * pulseEnv;

    //色散取样位移：中心近处强，随距离缓减；环带上再加一脚
    float split = (0.012 + 0.02 * exp(-dist * 1.8) + 0.015 * ring) * pulseEnv;

    //--------------------------------------------------------------------
    //环境档：屏缘轻色散（离屏心越远越明显），常驻低强度
    //--------------------------------------------------------------------
    float2 cent = coords - float2(0.5, 0.5);
    cent.x *= uAspect;
    float rimDist = length(cent);
    float2 rimDir = cent / (rimDist + 1e-5);
    rimDir.x /= uAspect;
    float rimSplit = uAmbient * 0.0045 * smoothstep(0.25, 0.85, rimDist);

    //合并位移：脉冲径向+环境屏缘
    float2 offR = dir * split + rimDir * rimSplit;
    float2 offB = -dir * split - rimDir * rimSplit;

    float4 src = tex2D(uImage0, coords);
    float rCh = tex2D(uImage0, coords + offR).r;
    float bCh = tex2D(uImage0, coords + offB).b;
    float3 prismatic = float3(rCh, src.g, bCh);

    //白闪：起爆头几帧的全屏提亮，快速退潮
    float flash = uIntensity * pow(saturate(1.0 - uProgress), 3.0) * 0.34;
    //环带上叠加珠光
    float3 ringGlow = float3(1.0, 0.97, 0.92) * ring * 0.5;

    //环境档轻提饱和（乘性，弱到只作氛围）
    float lum = dot(prismatic, float3(0.299, 0.587, 0.114));
    float3 ambientBoost = lerp(prismatic, prismatic + (prismatic - float3(lum, lum, lum)) * 0.22, uAmbient);

    float3 color = ambientBoost + ringGlow + float3(flash, flash, flash);
    return float4(color, src.a) * vertexColor;
}

technique Technique1
{
    pass EmpressScreenPrismPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
