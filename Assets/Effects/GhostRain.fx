//鬼雨湿墨水体，预乘 Alpha
//TechSky:    全屏冷灰青天幕，上重下轻+双层湿斑噪声缓漂+尸青渗色+
//            下缘一线非地平线的缝；直线算术+平 tex2D，无分支
//TechThroat: 夺身「雨喉」。上宽下窄的水体，三处各有物理答案：
//            喉口=旋涡吸入环，外沿被噪声啃开，不是平切；
//            下端=收成三股绞索缠住猎物，不是矩形收尾；
//            散场=自喉口向下抽干、股束颈缩断开，不是原地淡出。
//            宽度有生命周期：张开→维持→吞咽颈缩自下而上跑过→排空。
//PerlinNoise 实测值域 0.227~0.776，一切绑定阈值先过 nrm()

float uTime;        //秒
float uSeed;        //风暴种子相位
float uIntensity;   //0-1 阴幕在场强度

float uOpen;        //0-1 喉体张开，自喉口向下长出
float uSwallow;     //0-1 吞咽颈缩位置，0=未吞 1=已收到喉口
float uDrain;       //0-1 自喉口向下抽干
float uGrip;        //0-1 下端绞索收束强度
float uAspect;      //quad 宽/高，供噪声等比取样

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float4 PSSky(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;

    //双层湿斑：大团慢漂+细纹反向，浸透宣纸上未干的水痕
    float n0 = tex2D(noiseSamp, uv * float2(2.1, 1.3)
        + float2(uSeed * 0.31, uTime * 0.007)).r;
    float n1 = tex2D(noiseSamp, uv * float2(5.4, 3.6)
        + float2(-uTime * 0.005, uSeed * 0.77)).r;
    float wet = n0 * 0.62 + n1 * 0.38;

    //冷灰青底：顶最沉，向下稍透
    float3 deep = float3(0.055, 0.070, 0.092);
    float3 pale = float3(0.125, 0.157, 0.188);
    float3 col = lerp(deep, pale, uv.y * 0.85);
    col *= 0.70 + 0.52 * wet;

    //尸青渗色，量极小
    col += float3(-0.006, 0.014, 0.012) * (n1 - 0.5);

    //缝：下方一条不是地平线的微光，被湿斑轻微顶起
    float seamY = 0.78 + (n0 - 0.5) * 0.05;
    float seam = exp2(-abs(uv.y - seamY) * 130.0);
    col += float3(0.10, 0.125, 0.13) * seam * (0.30 + 0.36 * n1);

    //上重下轻的覆盖，底部留出地形剪影
    float cover = saturate(0.60 - 0.26 * uv.y + 0.10 * wet);
    float a = saturate(uIntensity) * cover * vertexColor.a;
    return float4(col * a, a);
}

float nrm(float n)
{
    return saturate((n - 0.227) * 1.821);
}

float4 PSThroat(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float y = coords.y;                 //0=喉口 1=猎物端
    float x = coords.x * 2.0 - 1.0;
    float ax = abs(x);

    //上抽的水：噪声整体向上滚，越靠喉口越快，读作被吸进去
    float speed = uTime * (0.55 + 0.85 * (1.0 - y));
    float n0 = nrm(tex2D(noiseSamp, float2(coords.x * 1.7 * uAspect + uSeed * 0.41,
        y * 1.15 - speed * 0.42)).r);
    float n1 = nrm(tex2D(noiseSamp, float2(coords.x * 4.3 * uAspect + uSeed * 0.83,
        y * 2.9 - speed * 0.78)).r);
    float flow = n0 * 0.62 + n1 * 0.38;

    //张开自喉口向下长出；排空自喉口向下退走
    float grow = saturate((uOpen * 1.45 - y * 0.45) * 2.2);
    float drain = saturate((y + 1.0 - uDrain * 2.0) * 3.0);

    //剖面三段：顶端自己收成钟口（不许被画布上缘切成横杠）→ 最宽处 y≈0.13 →
    //一路收细 → 末端颈缩，股束在到达下缘之前就断掉
    float bell = smoothstep(0.0, 0.13, y - (n0 - 0.5) * 0.035);
    float taper = lerp(0.84, 0.20, pow(saturate((y - 0.10) / 0.90), 0.70));
    float tail = 1.0 - smoothstep(0.88, 1.0, y) * 0.62;
    float radius = taper * bell * tail;

    //吞咽：一道颈缩自猎物端向喉口跑过去
    float pinchPos = 1.02 - uSwallow * 1.04;
    float pinch = exp2(-pow((y - pinchPos) * 7.0, 2.0)) * uSwallow * 0.62;
    radius *= (1.0 - pinch) * grow * drain;
    //噪声啃边，不做羽化
    radius -= (flow - 0.5) * 0.13;

    float edge = 0.045 + 0.09 * y;
    float body = saturate((radius - ax) / max(edge, 0.001));

    //绞股：整条都拧着，越靠猎物端分得越开
    float twist = sin(y * 7.5 + uTime * 2.3 + uSeed * 5.0) * 0.9;
    float lobe = 0.30 + 0.70 * abs(cos(x / max(radius, 0.001) * 4.712 + twist));
    float strandMix = saturate((y - 0.22) / 0.78) * uGrip;
    body *= lerp(1.0, lobe, strandMix * 0.85);

    //末端断成滴：噪声阈值咬掉最后一截，收场读作断流而不是原地淡出
    float breakUp = saturate((y - 0.90) / 0.10);
    body *= 1.0 - breakUp * saturate((0.62 - flow) * 3.2);

    //喉口：最宽处一圈吸入水膜，环内压暗成真正的开口
    float mouthBand = exp2(-pow((y - 0.125) * 17.0, 2.0)) * grow * drain;
    float ring = mouthBand * saturate(1.15 - ax * 1.05) * (0.5 + 0.8 * flow);
    float maw = mouthBand * saturate(1.0 - ax / 0.62);

    //冷灰青，密度走色深不走透明度；中轴压暗，它是个洞
    float3 deep = float3(0.030, 0.046, 0.054);
    float3 pale = float3(0.180, 0.235, 0.242);
    float3 corpse = float3(0.120, 0.205, 0.185);
    float core = saturate(1.0 - ax / max(radius, 0.001));
    float3 col = lerp(pale, deep, saturate(core * 0.85 + maw * 0.6));
    col = lerp(col, corpse, saturate(n1 - 0.55) * 0.5);
    col += float3(0.16, 0.20, 0.21) * ring;

    float a = saturate(body * (0.72 + 0.34 * flow) + ring * 0.55) * vertexColor.a;
    return float4(col * a, a);
}

technique TechSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSky();
    }
}

technique TechThroat
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSThroat();
    }
}
