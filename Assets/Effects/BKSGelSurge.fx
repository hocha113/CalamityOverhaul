// ============================================================================
//BKSGelSurge.fx 残酷史莱姆王 凝胶潮体条带
//潮汐冲刷/海啸波/立塔塔身/地下隆起线 共用，参数驱动无模式分支
//vs+ps 世界坐标 TriangleStrip；预乘 alpha 输出 + AlphaBlend
//材质三律：底部贴地暗带(meniscus)、高光只走窄反射带、波峰噪声撕裂成泡沫
//极角审计：全程笛卡尔噪声，无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFlow;        //内部流速(沿条带)
float uFoam;        //波峰泡沫带宽 0~0.5
float uAlpha;       //整体透明度
float uEdgeGlow;    //前缘高光强度 0~1.5
float uChurn;       //内部翻涌幅度 0~1
float uSeed;        //实例扰动
float3 uColorDeep;  //深层凝胶(暗蓝紫)
float3 uColorMid;   //中层皇家蓝
float3 uColorFoam;  //泡沫淡蓝白

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    AddressU = wrap;
    AddressV = wrap;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
};

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0), transformMatrix);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}

//顶点色约定：R=波峰能量(前缘0~1) G=局部高度占比 A=不透明包络
float4 PSMain(PSInput input) : COLOR0
{
    float2 uv = input.UV;          //x 沿条带 0尾→1头，y 0顶面→1贴地
    float crest = input.Color.r;   //前缘能量
    float envelope = input.Color.a;

    //---------------- 内部双频流动 ----------------
    //低频大团涌动 + 高频细流，均沿条带反向卷动(凝胶被推着走，内部向后翻)
    float2 flowUV1 = float2(uv.x * 2.6 + uSeed - uTime * uFlow, uv.y * 1.4 + uSeed * 3.0);
    float2 flowUV2 = float2(uv.x * 6.5 + uSeed * 7.0 - uTime * uFlow * 1.7, uv.y * 3.2 - uTime * 0.22);
    float flow1 = tex2D(noiseSamp, flowUV1).r;
    float flow2 = tex2D(noiseSamp, flowUV2).g;
    float body = flow1 * 0.62 + flow2 * 0.38;

    //翻涌：竖向扰动让顶面呼吸(凝胶不是静水)
    float churnWave = tex2D(noiseSamp, float2(uv.x * 3.4 + uTime * 0.55 + uSeed, 0.35)).r;
    float topLine = 0.06 + (churnWave - 0.5) * 0.16 * uChurn;

    //---------------- 顶面撕裂泡沫 ----------------
    //泡沫带以顶面为中心上下延伸；波峰(crest 高)处更宽更碎；噪声阈值咬边而非平滑渐隐
    float foamBand = uFoam * (0.45 + crest * 0.9);
    float foamDist = abs(uv.y - topLine);
    float foamZone = saturate(1.0 - foamDist / max(foamBand, 0.001));
    float foamNoise = tex2D(noiseSamp, float2(uv.x * 9.0 - uTime * (uFlow * 2.2 + 0.4), uv.y * 5.0 + uSeed * 11.0)).r;
    //撕裂：泡沫区内噪声高于阈值才存在，越远离顶面阈值越高
    float foamCut = step(1.0 - foamZone * 0.85, foamNoise);
    float foam = foamZone * foamCut * (0.5 + crest * 0.7);

    //顶面以上完全透明(被泡沫撕裂出的飞沫除外)
    float aboveTop = saturate((topLine - uv.y) / 0.03);
    float bodyMask = 1.0 - aboveTop * (1.0 - foamCut * foamZone);

    //---------------- 分层配色 ----------------
    //贴地暗带：底部 14% 更暗更饱和(表面张力挂地)
    float groundBand = saturate((uv.y - 0.86) / 0.14);
    //深度渐变：越深越暗
    float depth = saturate((uv.y - topLine) / max(1.0 - topLine, 0.001));
    float3 gel = lerp(uColorMid, uColorDeep, depth * 0.72 + groundBand * 0.28);
    //内部流动团块提亮(凝胶内含物)
    gel += uColorMid * (body - 0.5) * 0.55;

    //悬浮气泡：高频点状，向上漂(y 减 time)
    float bubbleNoise = tex2D(noiseSamp, float2(uv.x * 14.0 + uSeed * 5.0, uv.y * 8.0 - uTime * 0.9)).b;
    float bubbles = step(0.82, bubbleNoise) * (1.0 - depth * 0.5);
    gel += uColorFoam * bubbles * 0.5;

    //---------------- 前缘窄反射带 ----------------
    //顶面下方一条各向异性高光(圆形高光=塑料，这里是沿面延展的窄带)
    float hlDist = uv.y - (topLine + 0.045);
    float highlight = exp2(-hlDist * hlDist * 900.0) * uEdgeGlow;
    //高光被流动噪声打碎，避免死直线
    highlight *= 0.55 + 0.45 * flow2;
    gel += uColorFoam * highlight * (0.55 + crest * 0.6);

    //泡沫覆色
    gel = lerp(gel, uColorFoam, foam);

    //---------------- 合成 ----------------
    //半透明厚体：深处更实，顶面更透
    float alpha = (0.42 + depth * 0.36 + groundBand * 0.12) * bodyMask;
    alpha += foam * 0.5 + highlight * 0.3;
    alpha = saturate(alpha) * uAlpha * envelope;

    //波头鼻形收口：越接近头端，越只保留贴地部分(圆润浪面而非竖切)
    float headRound = saturate((1.0 - uv.x) * 5.0 + (uv.y - topLine) * 1.6 + crest * 0.25);
    //条带两端收敛保险
    float endGuard = saturate(uv.x * 14.0) * headRound;
    alpha *= saturate(endGuard);

    return float4(gel * alpha, alpha);
}

technique Technique1
{
    pass GelSurgePass
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMain();
    }
}
