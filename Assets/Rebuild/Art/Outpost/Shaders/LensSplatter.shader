Shader "LeftRight/Lens Splatter"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Tint", Color) = (0.28,0.008,0.016,0.45)
        _Softness("Lens defocus", Range(0.1,1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            float4 _Color;
            float4 _Splats[8]; // viewport x/y, birth time, size in screen-height units
            float4 _Seeds[8];  // random seed, lifetime, drift speed, opacity multiplier
            float _Now, _Aspect, _Softness;
            int _SplatCount;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position=UnityObjectToClipPos(input.vertex); output.uv=input.uv; output.color=input.color;
                return output;
            }
            float hash(float n) { return frac(sin(n*127.1)*43758.5453); }
            float blob(float2 coordinate, float radius, float seed)
            {
                float angle=atan2(coordinate.y,coordinate.x);
                float edge=radius*(1+.10*sin(angle*3+seed)+.06*sin(angle*7-seed*2));
                // Broad analytic falloff gives defocused lens blood without blurring the
                // world image or requiring another full-screen color-buffer copy.
                float blur=max(radius*_Softness,.0025*_Softness);
                return 1-smoothstep(max(0,edge-blur),edge+blur*.7,length(coordinate));
            }
            float4 frag(Varyings input):SV_Target
            {
                float coverage=0, gloss=0;
                [loop] for(int i=0;i<_SplatCount;i++)
                {
                    float age=max(0,_Now-_Splats[i].z), life=_Seeds[i].y;
                    if(age>=life) continue;
                    float fade=1-smoothstep(life*.60,life,age);
                    float size=_Splats[i].w;
                    float2 p=(input.uv-_Splats[i].xy)*float2(_Aspect,1);
                    // Cheap bounds reject: most screen pixels never evaluate droplet shapes.
                    if(abs(p.x)>size*1.4 || p.y>size*1.3 || p.y<-size*1.3-age*.05) continue;
                    float seed=_Seeds[i].x;
                    float impact=saturate(age/.09);
                    float mask=blob(p,size*.32*lerp(.25,1,impact),seed);
                    [unroll] for(int d=0;d<9;d++)
                    {
                        float random=hash(seed+d*13.7);
                        float angle=hash(seed+d*7.1)*6.28318;
                        float2 origin=float2(cos(angle),sin(angle))*size*(.30+.65*random);
                        float radius=size*(.055+.12*hash(seed+d*3.7));
                        float delay=.5+hash(seed+d*4.3)*1.8;
                        float fall=max(0,age-delay)*_Seeds[i].z*(.6+random);
                        float2 head=origin-float2(sin(age*.4+seed+d)*fall*.025,fall);
                        float drop=blob((p-head)*float2(1,1/(1+fall*3)),radius,seed+d);
                        // A thin wet trail connects the slowly moving drop to its impact point.
                        float along=saturate((origin.y-p.y)/max(.0001,fall));
                        float trailX=lerp(origin.x,head.x,along);
                        float trail=(1-smoothstep(radius*.08,radius*.4+.004*_Softness,abs(p.x-trailX)))
                            * step(head.y,p.y)*step(p.y,origin.y)*saturate(fall*80)*.55;
                        mask=max(mask,max(drop,trail));
                        gloss=max(gloss,(1-smoothstep(0,radius*(.8+_Softness),length(p-head-float2(-radius*.3,radius*.4))))*.08*fade);
                    }
                    coverage=1-(1-coverage)*(1-mask*fade*.85*_Seeds[i].w);
                }
                float3 color=_Color.rgb*(.6+.4*(1-coverage))+gloss*float3(.35,.12,.10);
                return float4(color,coverage*_Color.a)*input.color;
            }
            ENDHLSL
        }
    }
}
