using System.Collections.Generic;
using UnityEngine;

namespace FlipRun.Game
{
    public enum SfxId { Flip, Land, Coin, NearMiss, Death, HighScore }

    /// <summary>
    /// 効果音6種類（Resources/Audio/sfx_*.wav）とミュート切替。
    /// 音は Tools/generate_sfx.py で作ったもの（jsfxr と同じ考え方の簡単な合成音）。
    /// </summary>
    public sealed class Sfx
    {
        public const string MuteKey = "FlipRun.Muted";

        static readonly Dictionary<SfxId, string> Files = new Dictionary<SfxId, string>
        {
            { SfxId.Flip, "Audio/sfx_flip" },
            { SfxId.Land, "Audio/sfx_land" },
            { SfxId.Coin, "Audio/sfx_coin" },
            { SfxId.NearMiss, "Audio/sfx_nearmiss" },
            { SfxId.Death, "Audio/sfx_death" },
            { SfxId.HighScore, "Audio/sfx_highscore" },
        };

        static readonly Dictionary<SfxId, float> Volumes = new Dictionary<SfxId, float>
        {
            { SfxId.Flip, 0.6f }, { SfxId.Land, 0.35f }, { SfxId.Coin, 0.5f },
            { SfxId.NearMiss, 0.7f }, { SfxId.Death, 0.8f }, { SfxId.HighScore, 0.8f },
        };

        readonly AudioSource _source;
        readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();

        /// <summary>テスト用：鳴らした音の記録。</summary>
        public readonly List<SfxId> History = new List<SfxId>();

        public bool Muted { get; private set; }

        public Sfx(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            foreach (var kv in Files)
            {
                var clip = Resources.Load<AudioClip>(kv.Value);
                if (clip) _clips[kv.Key] = clip;
                else Debug.LogWarning("[FlipRun] 効果音が見つかりません: Resources/" + kv.Value);
            }
            SetMuted(PlayerPrefs.GetInt(MuteKey, 0) == 1, save: false);
        }

        public void Play(SfxId id)
        {
            History.Add(id);
            if (History.Count > 256) History.RemoveAt(0);
            if (_clips.TryGetValue(id, out var clip)) _source.PlayOneShot(clip, Volumes[id]);
        }

        public void ToggleMute() => SetMuted(!Muted, save: true);

        public void SetMuted(bool muted, bool save)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            if (save)
            {
                PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }

    /// <summary>分析イベントを Debug.Log に出すだけ（MVP）。</summary>
    public sealed class DebugAnalytics : IAnalytics
    {
        /// <summary>テスト用：送ったイベントの記録。</summary>
        public readonly List<(string name, Dictionary<string, object> p)> Sent = new List<(string, Dictionary<string, object>)>();

        public void Log(string eventName, Dictionary<string, object> p = null)
        {
            Sent.Add((eventName, p));
            if (Sent.Count > 512) Sent.RemoveAt(0);
            var sb = new System.Text.StringBuilder("[Analytics] ").Append(eventName);
            if (p != null)
                foreach (var kv in p) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }
    }
}
