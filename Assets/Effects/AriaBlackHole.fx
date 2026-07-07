// ============================================================================
//AriaBlackHole.fx 寰宇咏叹调·黑洞本体
//Backdrop=AlphaBlend 暗空间背板+不透明事件视界；Glow=Additive 恒星/光子环/吸积盘/透镜弧/喷流
//恒星坍缩→视界诞生→稳态蓄力→蒸发终曲全程由 C# 侧参数驱动，shader 只负责当帧形态
//极坐标接缝纪律：噪声一律刚性旋转笛卡尔或 tex2D 整数倍角度(采样器wrap)，正弦项系数全整数
// ============================================================================

sampler uImage0 : register(s0);
texture noiseTexture;
sampler noiseTex = sampler_state
{
    texture = <noiseTexture>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

matrix transformMatrix;
float uTime;        //秒
float uSpinPhase;   //盘旋转累计相位(C#积分，转速变化不跳帧)
float uSeed;        //个体随机种
float uFade;        //整体不透明度
float uStretch;     //运动拉伸(>=1)
float uMotAngle;    //运动方向
//恒星阶段
float uStarR;       //纹理空间半径 0=无恒星
float uStarBright;
float uCollapse;    //0~1 坍缩龟裂/白热化
//视界
float uHorizonR;    //纹理空间半径 0=无视界
float uRingBright;  //光子环强度
//吸积盘
float uDiskIn;
float uDiskOut;
float uDiskFlat;    //y 压扁比(0~1)
float uDiskBright;
float uArc;         //上下透镜弧强度
float uDoppler;     //多普勒不对称 0~0.6
float uInflow;      //视界坠入流强度
float uBlueshift;   //满蓄蓝移热斑
//演出
float uFlash;       //白闪(坍缩内爆/蒸发终曲)
float uJet;         //两极喷流(蒸发终曲)
float uJetAsym;     //0=双极对称 1=仅前向(-y)单喷流(炮台形态)
float uPalShift;    //0=金橙物质态(左键) 1=蓝紫高能态(右键领域)

#define TAU 6.28318530

//统一色板：白热→金橙→洋红→紫外；暗部深空紫黑
static const float3 ColHot   = float3(1.00, 0.973, 0.910);
static const float3 ColGold  = float3(1.00, 0.702, 0.278);
static const float3 ColRose  = float3(1.00, 0.369, 0.478);
static const float3 ColUV    = float3(0.420, 0.184, 0.659);
static const float3 ColSpace = float3(0.078, 0.031, 0.122);
static const float3 ColIce   = float3(0.72, 0.86, 1.00);

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, transformMatrix);
    return output;
}

float2 Rot(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//蓝移高能态色板
static const float3 ColBCore = float3(0.949, 0.922, 1.0);
static const float3 ColBViolet = float3(0.608, 0.420, 1.0);
static const float3 ColBCheren = float3(0.220, 0.714, 1.0);
static const float3 ColBDeep = float3(0.16, 0.10, 0.38);

//温度梯度：t=0 内缘白热 → 1 外缘紫外；uPalShift 蓝移
float3 DiskPalette(float t)
{
    float3 warm = lerp(ColHot, ColGold, saturate(t / 0.35));
    warm = lerp(warm, ColRose, saturate((t - 0.35) / 0.35));
    warm = lerp(warm, ColUV, saturate((t - 0.70) / 0.30));

    float3 cold = lerp(ColBCore, ColBViolet, saturate(t / 0.35));
    cold = lerp(cold, ColBCheren, saturate((t - 0.35) / 0.35));
    cold = lerp(cold, ColBDeep, saturate((t - 0.70) / 0.30));

    return lerp(warm, cold, uPalShift);
}

//中心坐标：运动方向压缩→视觉沿速度拉长
float2 Centered(float2 uv)
{
    float2 c = uv - 0.5;
    c = Rot(c, -uMotAngle);
    c.x /= max(uStretch, 1.0);
    c = Rot(c, uMotAngle);
    return c;
}

//=== 暗背板 + 事件视界（AlphaBlend 预乘输出）===
float4 BackdropPS(VertexShaderOutput input) : COLOR0
{
    float2 c = Centered(input.TexCoords);
    float dist = length(c);

    float circleFade = 1.0 - smoothstep(0.44, 0.5, dist);

    //深空暗晕：给白天背景压出对比度
    float halo = pow(smoothstep(0.5, 0.05, dist), 1.7);
    float haloK = saturate(uStarBright * 0.45 + uDiskBright * 0.85 + uRingBright * 0.4);
    float haloA = halo * 0.62 * haloK;

    //绝对黑的事件视界：不透明核心，仅保留相对2px级的软边
    float hasHole = saturate(uHorizonR * 800.0);
    float core = (1.0 - smoothstep(uHorizonR * 0.955, uHorizonR * 1.005, dist)) * hasHole;

    //视界边缘一丝极暗蓝紫菲涅尔（在黑与亮环之间垫一层过渡）
    float rim = exp(-pow((dist - uHorizonR) / 0.011, 2.0)) * hasHole;

    //XNA AlphaBlend 为预乘：rgb 必须已乘各自权重(核心黑=0贡献，halo=ColSpace*haloA)
    float3 rgb = ColSpace * haloA * (1.0 - core) + float3(0.055, 0.024, 0.10) * rim * 0.5;
    float a = saturate(core + haloA);

    float k = uFade * circleFade;
    return float4(rgb * k, a * k) * input.Color.a;
}

//=== 发光层（Additive）===
float4 GlowPS(VertexShaderOutput input) : COLOR0
{
    float2 c = Centered(input.TexCoords);
    float dist = length(c);
    float ang = atan2(c.y, c.x + 1e-6);

    float circleFade = 1.0 - smoothstep(0.43, 0.49, dist);
    float3 col = float3(0.0, 0.0, 0.0);

    //---------- 恒星（蓄力前段，坍缩时白热化+龟裂） ----------
    float starOn = saturate(uStarR * 800.0) * uStarBright;
    if (starOn > 0.001)
    {
        float nsD = dist / max(uStarR, 1e-4);
        float starMask = 1.0 - smoothstep(0.92, 1.06, nsD);
        float sphereZ = sqrt(saturate(1.0 - nsD * nsD));
        float featScale = 1.0 / max(uStarR * 2.0, 0.05);

        //对流米粒：双层刚性旋转笛卡尔噪声
        float2 sUV = Rot(c, uTime * 0.12) * featScale;
        float conv = tex2D(noiseTex, sUV * 0.55 + uSeed).r * 0.6
                   + tex2D(noiseTex, sUV * 1.35 - uSeed).g * 0.4;
        conv = conv * 0.5 + 0.62;

        float limb = 0.55 + sphereZ * 0.45;
        float coreGlow = pow(saturate(1.0 - nsD), 2.0);

        //坍缩龟裂：亮纹随 uCollapse 浮现
        float crackN = tex2D(noiseTex, Rot(c, -uTime * 0.07) * featScale * 2.2 + uSeed * 2.0).r;
        float cracks = smoothstep(0.62, 0.8, crackN) * uCollapse;

        float3 starCol = lerp(ColGold, ColHot, saturate(0.35 + uCollapse * 0.65 + (1.0 - nsD) * 0.35));
        float star = starMask * (conv * limb + coreGlow * 1.2 + cracks * 1.9) * uStarBright;

        //坍缩期外缘吸入辉光
        float infallGlow = exp(-pow((nsD - 1.25) / 0.35, 2.0)) * uCollapse * 0.5;

        col += starCol * star + ColGold * infallGlow * uStarBright;
    }

    //---------- 吸积盘（压扁椭圆 + 温度梯度 + 差速湍流 + 3臂 + 多普勒） ----------
    if (uDiskBright > 0.001)
    {
        float2 q = c;
        q.y /= max(uDiskFlat, 0.05);
        float dq = length(q);
        float aq = atan2(q.y, q.x + 1e-6);

        float band = smoothstep(uDiskIn - 0.012, uDiskIn + 0.028, dq)
                   * (1.0 - smoothstep(uDiskOut - 0.07, uDiskOut + 0.015, dq));

        if (band > 0.001)
        {
            float tRad = saturate((dq - uDiskIn) / max(uDiskOut - uDiskIn, 1e-3));

            //差速湍流：内层转速高，双层刚性旋转采样按半径混合（笛卡尔→免接缝）
            float2 uvFast = Rot(q, -uSpinPhase * 1.25 - 2.1) * 4.6 - uSeed;
            float2 uvSlow = Rot(q, -uSpinPhase * 0.55) * 2.4 + uSeed;
            float turb = lerp(tex2D(noiseTex, uvFast).g, tex2D(noiseTex, uvSlow).r, saturate(tRad * 1.15));
            turb = turb * 0.55 + 0.5;

            //径向流动：logR 连续量；v 通道整数倍角度→采样器wrap无缝
            float logR = log(dq * 9.0 + 1.0);
            float flow = tex2D(noiseTex, float2(logR * 1.6 - uTime * 0.45, aq / TAU * 2.0 + uSeed)).b;
            flow = flow * 0.4 + 0.72;

            //3臂螺旋（整数臂免接缝）
            float arms = 0.68 + 0.32 * sin(3.0 * aq - logR * 6.0 + uSpinPhase * 1.6);

            //多普勒：接近侧(右)增亮偏蓝，远离侧压暗偏红
            float dopCos = cos(aq);
            float dop = 1.0 + uDoppler * dopCos;
            float3 dopTint = lerp(float3(1.06, 0.95, 0.88), float3(0.92, 1.0, 1.14), saturate(dopCos * 0.5 + 0.5));

            //内缘白热边界层
            float rimHeat = exp(-pow((dq - uDiskIn) / 0.014, 2.0)) * 1.45;

            //近远侧遮挡：远侧(上半)被视界吞掉，近侧(下半)从黑核前掠过——立体感的关键
            float occFar = smoothstep(uHorizonR * 1.01, uHorizonR * 1.14, dist);
            float occ = lerp(occFar, 1.0, smoothstep(-0.015, 0.02, c.y));

            float diskI = band * turb * flow * arms * dop * (1.0 + (1.0 - tRad) * 0.7);
            float3 diskCol = DiskPalette(tRad) * dopTint;

            col += (diskCol * diskI + ColHot * rimHeat * band * dop) * uDiskBright * occ;
        }
    }

    //---------- 光子环（细过曝主环 + 宽柔光，带多普勒不对称） ----------
    float hasHole = saturate(uHorizonR * 800.0);
    if (hasHole > 0.001 && uRingBright > 0.001)
    {
        float ringR = uHorizonR * 1.22;
        float dr = dist - ringR;
        float ring = exp(-pow(dr / 0.0045, 2.0)) * 1.7 + exp(-pow(dr / 0.018, 2.0)) * 0.4;

        float rDop = 1.0 + uDoppler * 1.3 * cos(ang);
        float flick = 0.92 + 0.08 * sin(uTime * 9.0 + uSeed * 40.0);
        float3 ringCol = lerp(ColHot, ColIce, saturate(cos(ang) * 0.5 + 0.5) * 0.55);

        //环不进入视界内侧，保证黑核纯净
        float insideGuard = smoothstep(uHorizonR * 0.99, uHorizonR * 1.03, dist);
        col += ringCol * ring * rDop * flick * insideGuard * uRingBright * hasHole;
    }

    //---------- 透镜弧（盘的远侧光被引力弯到视界上/下方） ----------
    if (hasHole > 0.001 && uArc > 0.001 && uDiskBright > 0.001)
    {
        float arcR = uHorizonR * 1.5;
        float sigma = max(uHorizonR * 0.34, 1e-3);
        float g = exp(-pow((dist - arcR) / sigma, 2.0));

        float sinA = c.y / max(dist, 1e-4);
        float top = pow(saturate(-sinA), 1.7);
        float bot = pow(saturate(sinA), 1.7) * 0.42;

        //沿弧湍流：u=整数4倍角→wrap 无缝
        float arcTurb = tex2D(noiseTex, float2(ang / TAU * 4.0 + uSpinPhase * 0.16, (dist - arcR) * 7.0 + uSeed)).r;
        arcTurb = arcTurb * 0.5 + 0.62;

        float arcDop = 1.0 + uDoppler * 0.7 * cos(ang);
        col += DiskPalette(0.12) * g * (top + bot) * arcTurb * arcDop * uArc * uDiskBright * 1.15;
    }

    //---------- 视界坠入流（螺旋流光被拉进黑洞，蓝移变白） ----------
    if (hasHole > 0.001 && uInflow > 0.001)
    {
        float swirl = ang + dist * 15.0 - uSpinPhase * 2.6 - uTime * 1.1;
        //u=整数2倍(swirl含ang一次项,跨缝跳2)→wrap 无缝
        float sN = tex2D(noiseTex, float2(swirl / TAU * 2.0, dist * 3.2 - uTime * 0.8 + uSeed)).r;
        float streaks = smoothstep(0.52, 0.86, sN);

        float inMask = smoothstep(uHorizonR * 1.02, uHorizonR * 1.45, dist)
                     * (1.0 - smoothstep(uDiskIn * 1.05, uDiskIn * 1.55, dist));

        float3 inCol = lerp(ColHot, ColIce, 0.55);
        col += inCol * streaks * inMask * uInflow * (0.75 + 0.25 * sin(uTime * 5.0));
    }

    //---------- 满蓄蓝移热斑（接近侧内缘） ----------
    if (uBlueshift > 0.001)
    {
        float2 q2 = c;
        q2.y /= max(uDiskFlat, 0.05);
        float2 hs = float2(uDiskIn * 1.12, 0.0);
        float hot = exp(-dot(q2 - hs, q2 - hs) / max(pow(uDiskIn * 0.5, 2.0), 1e-5));
        col += ColIce * hot * uBlueshift * 1.6;
    }

    //---------- 两极喷流（蒸发终曲双极 / 炮台形态单向） ----------
    if (uJet > 0.001)
    {
        float jw = 0.016 + abs(c.y) * 0.06;
        float jetCore = exp(-pow(abs(c.x) / jw, 2.0));
        float jetLen = 1.0 - smoothstep(0.06, 0.46, abs(c.y));
        float diamonds = 0.75 + 0.25 * sin(c.y * 90.0 - uTime * 22.0);
        //单向模式：只保留 -y 前向瓣
        float lobe = lerp(1.0, smoothstep(0.02, -0.06, c.y), uJetAsym);
        float jet = jetCore * jetLen * diamonds * lobe * smoothstep(uHorizonR * 0.8, uHorizonR * 1.35, dist);
        col += float3(0.82, 0.75, 1.0) * jet * uJet * 2.2;
    }

    //---------- 白闪（坍缩内爆/终曲） ----------
    if (uFlash > 0.001)
    {
        col += float3(1.0, 0.98, 0.95) * uFlash * exp(-pow(dist / 0.4, 2.0)) * 2.4;
    }

    col *= uFade * circleFade;
    float a = saturate(max(col.r, max(col.g, col.b)));
    return float4(col, a) * input.Color;
}

technique Backdrop
{
    pass BackdropPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BackdropPS();
    }
}

technique Glow
{
    pass GlowPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 GlowPS();
    }
}
