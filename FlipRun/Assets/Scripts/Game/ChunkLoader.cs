using System.Collections.Generic;
using UnityEngine;

namespace FlipRun.Game
{
    /// <summary>Assets/Resources/Chunks/chunk_*.json を読み込む。</summary>
    public static class ChunkLoader
    {
        public const string ResourceFolder = "Chunks";

        public static List<ChunkData> LoadAll()
        {
            var list = new List<ChunkData>();
            foreach (var asset in Resources.LoadAll<TextAsset>(ResourceFolder))
            {
                var c = JsonUtility.FromJson<ChunkData>(asset.text);
                if (c != null && c.rows != null && c.rows.Length == c.height) list.Add(c);
                else Debug.LogWarning($"[FlipRun] チャンクを読めませんでした: {asset.name}");
            }
            return list;
        }
    }

    /// <summary>ハイスコアを PlayerPrefs に保存する。</summary>
    public sealed class HighScoreStore
    {
        public const string DefaultKey = "FlipRun.HighScore";
        readonly string _key;

        public HighScoreStore(string key = DefaultKey) { _key = key; }

        public int Best => PlayerPrefs.GetInt(_key, 0);

        /// <summary>スコアを記録し、ハイスコアを更新したら true。</summary>
        public bool Submit(int score)
        {
            if (score <= Best) return false;
            PlayerPrefs.SetInt(_key, score);
            PlayerPrefs.Save();
            return true;
        }

        public void Clear() => PlayerPrefs.DeleteKey(_key);
    }
}
