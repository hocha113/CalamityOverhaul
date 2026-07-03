// ============================================================================
//OniPaperPeel.fx 表里翻转：旧世界画面作为纸层被刀痕切成两半剥落
//C#侧每半各画一次（SpriteBatch 平移+旋转），本着色器在捕获帧的纹理空间内
//做半面遮罩、切口焦燃蚕食、卷边翘曲；坐标全为笛卡尔，无极坐标
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float uPeelProgress;    //0~1 剥落进度
float2 uScreenSize;     //捕获帧像素尺寸
float2 uSlashPoint;     //切口上一点（纹理像素空间）
float2 uSlashDir;       //切口方向单位向量
float uHalfSign;        //+1/-1 本次绘制保留哪一侧

static const float3 CHAR_BLACK = float3(0.02, 0.015, 0.02);
static const float3 EMBER_RED = float3(0.95, 0.16, 0.06);
static const float3 EMBER_GOLD = float3(1.0, 0.62, 0.18);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSPeel(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = coords * uScreenSize;
    float2 nrm = float2(-uSlashDir.y, uSlashDir.x);
    float s = dot(p - uSlashPoint, nrm) * uHalfSign;   //保留侧为正
    float t = dot(p - uSlashPoint, uSlashDir);          //沿切口坐标

    if (s < -2.0) {
        return float4(0, 0, 0, 0);
    }

    float diag = length(uScreenSize);
    float prog = saturate(uPeelProgress);

    //切口蚕食：烧掉的宽度随进度增长，前沿被噪声撕出参差
    float jag = noiseTex(float2(t / diag * 6.0, 0.37)) * 0.7
              + noiseTex(float2(t / diag * 17.0, 0.81)) * 0.3;
    float eat = (0.004 + pow(prog, 1.35) * 0.055) * diag * (0.65 + jag * 0.7);

    if (s < eat) {
        return float4(0, 0, 0, 0);
    }

    //卷边：近切口处纸面向外拱起，采样向切口方向回卷
    float curlW = diag * 0.045 * (0.3 + prog);
    float curl = exp(-(s - eat) / max(curlW, 1.0));
    float2 uvWarp = coords - nrm * uHalfSign * (curl * curlW * 0.55) / uScreenSize;
    float3 paper = tex2D(uImage0, saturate(uvWarp)).rgb;

    //卷边明暗：拱起面受光突变，先亮一线再落入阴影
    float rim = exp(-pow((s - eat - curlW * 0.35) / (curlW * 0.28), 2.0));
    paper *= 1.0 - curl * 0.38;
    paper += rim * 0.10;

    //焦炭带 + 余烬线
    float charW = diag * (0.006 + prog * 0.012);
    float charZone = exp(-(s - eat) / max(charW, 1.0));
    float flick = 0.6 + 0.4 * noiseTex(float2(t / diag * 11.0 - uTime * 0.9, 0.13));
    paper = lerp(paper, CHAR_BLACK, charZone * 0.92);

    float emberLine = exp(-pow((s - eat) / (charW * 0.45), 2.0));
    float3 ember = lerp(EMBER_RED, EMBER_GOLD, flick * flick);
    paper += ember * emberLine * flick * 1.6;

    //整半渐隐离场
    float fade = 1.0 - smoothstep(0.72, 1.0, prog);
    float alpha = fade * vertexColor.a;
    return float4(paper * alpha, alpha);
}

technique TechPeel {
    pass P0 {
        PixelShader = compile ps_3_0 PSPeel();
    }
}
