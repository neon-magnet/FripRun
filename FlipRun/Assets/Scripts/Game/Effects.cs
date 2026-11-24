using System.Collections.Generic;
using UnityEngine;

namespace FlipRun.Game
{
    /// <summary>
    /// 小さな四角の粒を出す軽い仕組み（足元の残像・死亡時の破片）。
    /// ParticleSystem を使わず、SpriteRenderer を使い回す。
    /// </summary>
    public sealed class SpriteParticles
    {
        struct P
        {
            public Vector2 Pos, Vel;
            public float Life, MaxLife, Size, Gravity, Spin, Angle;
            public Color Color;
        }

        readonly SpriteRenderer[] _renderers;
        readonly P[] _ps;
        int _next;

        public SpriteParticles(Transform parent, string name, Sprite sprite, int capacity, int sortingOrder)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            _renderers = new SpriteRenderer[capacity];
            _ps = new P[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                _renderers[i] = sr;
            }
        }

        public void Emit(Vector2 pos, Vector2 vel, float life, float size, Color color, float gravity = 0f, float spin = 0f)
        {
            int i = _next;
            _next = (_next + 1) % _ps.Length;
            _ps[i] = new P { Pos = pos, Vel = vel, Life = life, MaxLife = life, Size = size, Color = color, Gravity = gravity, Spin = spin };
            _renderers[i].enabled = true;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _ps.Length; i++)
            {
                if (_ps[i].Life <= 0f) continue;
                ref var p = ref _ps[i];
                p.Life -= dt;
                var sr = _renderers[i];
                if (p.Life <= 0f) { sr.enabled = false; continue; }
                p.Vel.y += p.Gravity * dt;
                p.Pos += p.Vel * dt;
                p.Angle += p.Spin * dt;
                float k = p.Life / p.MaxLife;
                var t = sr.transform;
                t.position = new Vector3(p.Pos.x, p.Pos.y, 0f);
                t.localScale = Vector3.one * (p.Size * (0.4f + 0.6f * k));
                t.localRotation = Quaternion.Euler(0f, 0f, p.Angle);
                var c = p.Color;
                c.a *= k;
                sr.color = c;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _ps.Length; i++)
            {
                _ps[i].Life = 0f;
                _renderers[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// 色反転：重力が下ならパレットA（背景＝濃紺、地形＝明るい色）、上ならパレットB（その逆）。
    /// 切り替えは 0.08秒かけて行う。
    /// </summary>
    public sealed class PaletteFx
    {
        public const float Duration = 0.08f;

        public static readonly Color BackgroundA = new Color32(0x14, 0x1B, 0x3A, 0xFF);
        public static readonly Color TerrainA = new Color32(0xE6, 0xEA, 0xF5, 0xFF);
        public static Color BackgroundB => TerrainA;
        public static Color TerrainB => BackgroundA;

        float _t;      // 0 = A、1 = B
        float _target;

        public float Blend => _t;

        public void SetTarget(bool paletteB) => _target = paletteB ? 1f : 0f;

        public void Snap(bool paletteB) => _t = _target = paletteB ? 1f : 0f;

        public void Tick(float dt)
        {
            _t = Mathf.MoveTowards(_t, _target, dt / Duration);
        }

        public Color Background => Color.Lerp(BackgroundA, BackgroundB, _t);
        public Color Terrain => Color.Lerp(TerrainA, TerrainB, _t);
    }

    /// <summary>画面揺れ（減衰する）。</summary>
    public sealed class ScreenShake
    {
        float _time, _duration, _amplitude;

        public void Start(float duration, float amplitude)
        {
            _time = _duration = duration;
            _amplitude = amplitude;
        }

        public Vector2 Tick(float dt)
        {
            if (_time <= 0f) return Vector2.zero;
            _time -= dt;
            float k = Mathf.Clamp01(_time / _duration);
            return Random.insideUnitCircle * (_amplitude * k);
        }
    }

    /// <summary>
    /// プレイヤーの見た目：
    /// ・走り4フレーム（進んだ距離に合わせて切り替える）、空中は走りの2枚目、着地の直後は着地フレーム
    /// ・着地のスクワッシュ（横1.3倍・縦0.7倍 → 0.1秒で戻る）
    /// ・重力が上向きなら絵を上下反転
    /// 絵の大きさが当たり判定（0.8）と違っても、足元が床（天井）に接するように位置をずらす。
    /// </summary>
    public sealed class PlayerVisual
    {
        public const float SquashTime = 0.1f;
        public const float LandFrameTime = 0.12f;
        public const float RunStride = 0.55f; // この距離を進むごとに走りのフレームを1つ進める
        static readonly Vector2 Squash = new Vector2(1.3f, 0.7f);

        readonly Transform _t;
        readonly SpriteRenderer _sr;
        readonly GameArt _art;
        float _squash;    // 1 → 0
        float _landTimer;

        public PlayerVisual(Transform t, SpriteRenderer sr, GameArt art)
        {
            _t = t;
            _sr = sr;
            _art = art;
            _sr.sprite = art.Run[0];
            _sr.color = art.PlayerTint;
        }

        public void Land()
        {
            _squash = 1f;
            _landTimer = LandFrameTime;
        }

        public void Reset()
        {
            _squash = 0f;
            _landTimer = 0f;
        }

        public void Apply(in PlayerState s, float dt)
        {
            _squash = Mathf.MoveTowards(_squash, 0f, dt / SquashTime);
            _landTimer -= dt;

            var run = _art.Run;
            if (_landTimer > 0f) _sr.sprite = _art.Land;
            else if (!s.Grounded) _sr.sprite = run[Mathf.Min(1, run.Length - 1)];
            else _sr.sprite = run[(int)(s.X / RunStride) % run.Length];

            float sx = Mathf.Lerp(1f, Squash.x, _squash);
            float sy = Mathf.Lerp(1f, Squash.y, _squash);
            float size = _art.PlayerSize;
            _t.localScale = new Vector3(size * sx, size * sy, 1f);
            // 絵の足元（重力の向きの端）を、当たり判定の足元に合わせる
            float half = size * sy * 0.5f;
            float y = s.Y + s.GravityDir * (GameConfig.PlayerHalfSize - half);
            _t.position = new Vector3(s.X, y, 0f);
            _sr.flipY = s.GravityUp;
        }
    }
}
