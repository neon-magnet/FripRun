using System;
using System.Collections.Generic;

namespace FlipRun
{
    /// <summary>
    /// 広告の窓口（ブリーフ第7章）。unityroom版では広告を出さないので、MVPではスタブを使う。
    /// 将来スマホ版を出すときに、実際の広告SDKを使う実装に差し替える。
    /// </summary>
    public interface IAdService
    {
        void ShowRewarded(string placement, Action onReward);
        void ShowInterstitial(string placement);
    }

    /// <summary>分析イベントの窓口（ブリーフ第8章）。MVPではコンソールに出すだけ。</summary>
    public interface IAnalytics
    {
        void Log(string eventName, Dictionary<string, object> p = null);
    }

    /// <summary>分析イベントの名前とパラメータ。</summary>
    public static class AnalyticsEvents
    {
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
        public const string LevelStart = "level_start";
        public const string LevelFail = "level_fail";
        public const string LevelComplete = "level_complete"; // エンドレスなのでハイスコア更新時に送る
        public const string AdRewardedShown = "ad_rewarded_shown";
        public const string AdInterstitialShown = "ad_interstitial_shown";

        public const string PlacementRevive = "revive";
        public const string PlacementDeath3 = "death_3";

        public static string DeathCauseName(DeathCause c)
        {
            switch (c)
            {
                case DeathCause.Spike: return "spike";
                case DeathCause.OutOfBounds: return "out_of_bounds";
                case DeathCause.SideHit: return "side_hit";
                default: return "none";
            }
        }

        public static Dictionary<string, object> LevelFailParams(RunSession s)
        {
            return new Dictionary<string, object>
            {
                { "distance", s.DistanceMeters },
                { "coins", s.Coins },
                { "time_sec", (float)Math.Round(s.State.Frame / (double)GameConfig.StepsPerSecond, 2) },
                { "chunk_id", s.CurrentChunkId },
                { "death_cause", DeathCauseName(s.State.Death) },
            };
        }
    }

    /// <summary>
    /// 広告スタブ：実際には何も表示しない。リワードはすぐに onReward を呼ぶ。
    /// 呼ばれたことは分析イベント（ad_*_shown）として記録する。
    /// </summary>
    public sealed class StubAdService : IAdService
    {
        readonly IAnalytics _analytics;
        readonly Action<string> _log;

        public StubAdService(IAnalytics analytics, Action<string> log = null)
        {
            _analytics = analytics;
            _log = log;
        }

        public void ShowRewarded(string placement, Action onReward)
        {
            _log?.Invoke($"[Ad stub] rewarded: {placement}");
            _analytics?.Log(AnalyticsEvents.AdRewardedShown, new Dictionary<string, object> { { "placement", placement } });
            onReward?.Invoke();
        }

        public void ShowInterstitial(string placement)
        {
            _log?.Invoke($"[Ad stub] interstitial: {placement}");
            _analytics?.Log(AnalyticsEvents.AdInterstitialShown, new Dictionary<string, object> { { "placement", placement } });
        }
    }

    /// <summary>
    /// ニアミスの判定：トゲとの距離が 0.3マス以内まで近づいてから、死なずに通り過ぎたら1回だけ成立。
    /// 距離はプレイヤーの当たり判定の箱とトゲの当たり判定の箱の、いちばん近い点どうしの距離。
    /// </summary>
    public sealed class NearMissTracker
    {
        public const float Threshold = 0.3f;
        public const int Bonus = 5;

        readonly Dictionary<long, float> _closest = new Dictionary<long, float>();
        readonly List<long> _passed = new List<long>();

        /// <summary>1フレームごとに呼ぶ。このフレームで成立したニアミスの数を返す。</summary>
        public int Update(ITileWorld world, in PlayerState s)
        {
            if (s.IsDead) return 0;
            float h = GameConfig.PlayerHalfSize;
            float pxMin = s.X - h, pxMax = s.X + h, pyMin = s.Y - h, pyMax = s.Y + h;
            int c0 = (int)Math.Floor(pxMin) - 1, c1 = (int)Math.Floor(pxMax) + 1;

            for (int c = c0; c <= c1; c++)
                for (int r = GameConfig.FloorRow; r <= GameConfig.CeilingRow; r++)
                {
                    var t = world.GetTile(c, r);
                    if (!PlayerSim.SpikeBox(t, c, r, out float ax, out float bx, out float ay, out float by)) continue;
                    if (pxMin > bx) continue; // もう通り過ぎたトゲ（判定済み）
                    float dx = Math.Max(0f, Math.Max(ax - pxMax, pxMin - bx));
                    float dy = Math.Max(0f, Math.Max(ay - pyMax, pyMin - by));
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    long key = ((long)c << 8) | (uint)(r + 128);
                    if (!_closest.TryGetValue(key, out float prev) || d < prev) _closest[key] = d;
                }

            int count = 0;
            _passed.Clear();
            foreach (var kv in _closest)
            {
                int col = (int)(kv.Key >> 8);
                float spikeRight = col + 0.5f + PlayerSim.SpikeHalfWidth;
                if (pxMin <= spikeRight) continue; // まだ通り過ぎていない
                _passed.Add(kv.Key);
                if (kv.Value <= Threshold) count++;
            }
            foreach (var k in _passed) _closest.Remove(k);
            return count;
        }
    }
}
