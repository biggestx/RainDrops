Shader "Custom/RenderFeature/KawaseBlur"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "Noise"{}
        _Color("Color", Color) = (0,0,0,0)
        
    }

        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            sampler2D _NoiseTex;

            // sampler2D _CameraOpaqueTexture;
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;

            float _offset;

            float4 _Color;

            float2 _ShadeDir;

            // custom

            float shading(float2 dir)
            {
                _ShadeDir = float2(-1.0f, 1.0f);

                float s = dot(normalize(dir), normalize(_ShadeDir));
                s = max(s, 0.0);
                return s;
            }

            float4 layerEliemichel(float2 uv, float2 p, float4 curr, float time)
            {
                // 이 부분 추가 확인
                float2 x = float2(30,30); // controls the size vs density
                
                
                float2 xuv = x * p;
                float4 n = tex2D(_NoiseTex, round(xuv - 0.3) / x);

                float2 offset = (tex2D(_NoiseTex, p * 0.1f).rg - 0.5f) * 2.; // expands to [-1, 1]
                float2 z = xuv * 6.3f + offset; // 6.3 is a magic number
                
                x = sin(z) - frac(time * (n.b + 0.1f) + n.g) * 0.5f;
                //x = frac(time * (n.b + 0.1f) + n.g) * 0.5f; // 크기로인해 sin 제거

                if (x.x + x.y - n.r * 3.0f > 0.5f)
                {
                    float2 dir = float2(cos(z.x),cos(z.y)); // float2 x,y 에 넣는게 맞는지
                    curr = tex2Dlod(_MainTex, float4((uv + dir * 0.2f).xy , 0 , 0.0f)); // 뒷부분 float4가 맞는지 확인

                    curr += shading(dir) * curr.xyzw;
                }
                return curr;
            }



            float3 n31(float p)
            {
                //  3 out, 1 in... DAVE HOSKINS
                float3 p3 = frac(float3(p,p,p) * float3(.1031, .11369, .13787));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac(float3((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y, (p3.y + p3.z) * p3.x));
            }

            float sawTooth(float t) {
                return cos(t + cos(t)) + sin(2.0 * t) * 0.2 + sin(4.0 * t) * .02;
            }
            float deltaSawTooth(float t) {
                return 0.4 * cos(2.0 * t) + 0.08 * cos(4.0 * t) - (1.0 - sin(t)) * sin(t + cos(t));
            }


            float3 getDrops(float2 uv, float seed, float t)
            {

                float2 o = float2(0,0);

                uv.y += t * 0.05;

                uv *= float2(20.0, 2.5) * 2.0;

                float2 id = floor(uv);
                float3 n = n31(id.x + (id.y + seed) * 546.3524);
                float2 bd = frac(uv);

                float2 uv2 = bd;

                bd -= .5;

                bd.y *= 4.;

                bd.x += (n.x - .5) * .6;

                t += n.z * 6.28;
                float slide = sawTooth(t);

                float ts = 1.5;
                float2 trailPos = float2(bd.x * ts, (frac(bd.y * ts * 2. - t * 2.) - .5) * .5);

                bd.y += slide * 2.;								// make drops slide down

                //#ifdef HIGH_QUALITY
                float dropShape = bd.x * bd.x;
                dropShape *= deltaSawTooth(t);
                bd.y += dropShape;								// change shape of drop when it is falling
                //#endif

                float d = length(bd);							// distance to main drop

                float trailMask = smoothstep(-0.2, 0.2, bd.y);				// mask out drops that are below the main
                trailMask *= bd.y;								// fade dropsize
                float td = length(trailPos * max(0.5, trailMask));	// distance to trail drops

                float mainDrop = smoothstep(0.2, 0.1, d);
                float dropTrail = 0.0;//smoothstep(.1, .002, td);

                dropTrail *= trailMask;
                o = lerp(bd * mainDrop, trailPos, dropTrail);		// mix main drop and drop trail
                // mix -> lerp

                return float3(o, d);
            }


            float4 layerBigWIngs(float2 uv, float2 p, float4 curr, float time)
            {
                float3 drop = getDrops(p, 1., time);

                if (length(drop.xy) > 0.0)
                {
                    float2 offset = -drop.xy * (1.0 - drop.z);
                    curr = tex2D(_MainTex, uv + offset);
                    curr += shading(offset) * curr * 0.5;
                }

                return curr;
            }


            //



            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            //fixed4 frag(v2f input) : SV_Target
            //{
            //    float2 res = _MainTex_TexelSize.xy;
            //    float i = _offset;

            //    fixed4 col;
            //    col.rgb = tex2D(_MainTex, input.uv).rgba;
            //    col.rgb += tex2D(_MainTex, input.uv + float2(i, i) * res).rgb;
            //    col.rgb += tex2D(_MainTex, input.uv + float2(i, -i) * res).rgb;
            //    col.rgb += tex2D(_MainTex, input.uv + float2(-i, i) * res).rgb;
            //    col.rgb += tex2D(_MainTex, input.uv + float2(-i, -i) * res).rgb;
            //    col.rgb /= 5.0f;

            //    return col;
            //    //return col;
            //}

            fixed4 frag(v2f input) : SV_Target
            {
                float2 g = float2(1,1);
				float2 uv = input.uv.xy / g;

				// background
				//fixed4 ret = tex2Dlod(_MainTex, float4(input.uv.x,input.uv.y,0,0));
                fixed4 ret = tex2D(_MainTex, uv);

                float fTime = _Time.x * 5.0f;

				// use BigWIngs layer as base drops
				ret = layerBigWIngs(uv, uv * 2.2, ret, fTime * 0.5);

				// add Eliemichel layers with fbm (see https://www.shadertoy.com/view/lsl3RH) as detailed drops.
				float4 m = float4(0.80, 0.60, -0.60, 0.80);
				float2 p = uv * 0.7;

				ret = layerEliemichel(input.uv, p + float2(0, fTime * 0.01), ret, fTime * 0.25);
				p = m * p * 2.02;

				ret = layerEliemichel(input.uv, p, ret, fTime * 0.125);
				p = m * p * 1.253;

				ret = layerEliemichel(input.uv, p, ret, fTime * 0.125);
                return ret;
			}

            ENDCG
        }
    }
}
